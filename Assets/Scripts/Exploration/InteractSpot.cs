using System.Collections;
using UnityEngine;

// 탐색 구간에서 [E]로 상호작용하는 지점. (전화 받기, 문 열기, 서랍 뒤지기 등)
//  - 가까이 가서 바라보면 화면 아래에 안내("[E] 전화 받기")가 뜬다.
//  - 누르면: (필요한 단서가 있는지 확인) → 대사 재생 → 단서 기록 → 지정한 오브젝트 끄기/켜기
//  - 반복 소리(전화벨 등)를 넣으면 사용하기 전까지 그 자리에서 울린다.
// 탐색 구간(FlashbackFreeRoamSegment)이 "꼭 해야 하는 상호작용"으로 기다릴 수도 있다.
public class InteractSpot : MonoBehaviour
{
    [Header("안내")]
    [SerializeField] private string prompt = "[E] 살펴보기";
    [SerializeField] private float radius = 2.2f;
    [SerializeField] private float lookAngle = 50f;
    [Tooltip("한 번만 쓸 수 있는지")]
    [SerializeField] private bool oneShot = true;

    [Header("잠김 (선택)")]
    [Tooltip("이 단서가 기억 노트에 있어야 쓸 수 있다. (예: 열쇠)")]
    [SerializeField] private string requiredClueId = "";
    [SerializeField] private string lockedMessage = "잠겨 있다.";
    [Tooltip("이 문이 열려 있어야 보인다/쓸 수 있다. (칸막이 안의 물건 등)")]
    [SerializeField] private SwingDoor requireOpenDoor;

    [Header("쓰면 일어나는 일")]
    [Tooltip("재생할 대사 그룹 (TypeWriter). -1이면 없음")]
    [SerializeField] private int dialogueGroupIndex = -1;
    [Tooltip("대사 대신/대사 뒤에 화면 아래 잠깐 띄울 문구")]
    [TextArea] [SerializeField] private string message = "";
    [SerializeField] private GameObject[] disableOnUse;
    [SerializeField] private GameObject[] enableOnUse;
    [SerializeField] private AudioClip useSound;
    [Tooltip("쓰면 열리는 문 (여닫는 애니메이션)")]
    [SerializeField] private SwingDoor[] openOnUse;
    [Tooltip("쓰면 펼쳐지는 문서 (중요한 곳에 줄 긋기)")]
    [SerializeField] private UnderlineDocument underlineDocument;

    [Header("기억 노트에 적을 단서 (선택)")]
    [SerializeField] private string clueId = "";
    [SerializeField] private string clueTitle = "";
    [TextArea(3, 8)] [SerializeField] private string clueText = "";
    [SerializeField] private int memoryIndex = 0;

    [Header("반복 소리 (전화벨 등, 쓰기 전까지)")]
    [SerializeField] private AudioClip loopSound;
    [Tooltip("소리 파일이 없으면 코드로 만든 전화벨을 쓴다")]
    [SerializeField] private bool usePhoneRing = false;
    [Range(0f, 1f)] [SerializeField] private float loopVolume = 1f;
    [SerializeField] private float loopMaxDistance = 30f;

    public bool Used { get; private set; }
    public bool IsBusy { get; private set; }

    private static InteractSpot focused;

    /// <summary>지금 어떤 상호작용 지점이 안내를 띄우고 있는지 (문 안내와 겹치지 않게)</summary>
    public static bool AnyFocused => focused != null;

    public UnderlineDocument UnderlineDocument => underlineDocument;
    private AudioSource loopSource;
    private AudioSource oneShotSource;

    public void SetDialogueGroup(int index) => dialogueGroupIndex = index;

    public void SetTexts(string newPrompt = null, string newMessage = null, string title = null, string text = null)
    {
        if (newPrompt != null) prompt = newPrompt;
        if (newMessage != null) message = newMessage;
        if (title != null) clueTitle = title;
        if (text != null) clueText = text;
    }

    /// <summary>탐색 구간이 다시 시작될 때 처음 상태로 되돌린다.</summary>
    public void ResetState()
    {
        Used = false;
        IsBusy = false;
        foreach (var go in disableOnUse) if (go != null) go.SetActive(true);
        foreach (var go in enableOnUse) if (go != null) go.SetActive(false);
        if (openOnUse != null) foreach (var d in openOnUse) if (d != null) d.ResetState();
    }

    private void OnDisable()
    {
        if (focused == this) { focused = null; ObjectiveHUD.Instance.SetPrompt(null); }
        if (loopSource != null) loopSource.Stop();
    }

    private void Update()
    {
        FreeRoamMovement player = ActivePlayer();
        UpdateLoop(player);

        if (player == null || IsBusy || (oneShot && Used))
        {
            if (focused == this) Unfocus();
            return;
        }

        bool inFocus = !player.InputPaused && InFocus(player.Head);
        if (inFocus && (focused == null || focused == this || !focused.InFocus(player.Head)))
        {
            if (focused != this) { focused = this; ObjectiveHUD.Instance.SetPrompt(prompt); }
            if (Input.GetKeyDown(KeyCode.E))
                StartCoroutine(UseRoutine(player));
        }
        else if (focused == this && !inFocus)
        {
            Unfocus();
        }
    }

    private void Unfocus()
    {
        focused = null;
        ObjectiveHUD.Instance.SetPrompt(null);
    }

    private bool InFocus(Transform head)
    {
        if (requireOpenDoor != null && !requireOpenDoor.IsOpen) return false;

        Vector3 target = transform.position;
        var r = GetComponentInChildren<Renderer>();
        if (r != null) target = r.bounds.center;

        Vector3 flat = target - head.position; flat.y = 0f;
        if (flat.magnitude > radius) return false;
        if (flat.magnitude < 0.8f) return true;
        return Vector3.Angle(head.forward, (target - head.position).normalized) <= lookAngle;
    }

    private IEnumerator UseRoutine(FreeRoamMovement player)
    {
        Unfocus();

        // 잠겨 있으면 문구만
        if (!string.IsNullOrEmpty(requiredClueId) && !MemoryNotebook.Has(requiredClueId))
        {
            ObjectiveHUD.Instance.ShowMessage(lockedMessage, 2f);
            yield break;
        }

        IsBusy = true;
        player.InputPaused = true;

        if (loopSource != null) loopSource.Stop();
        PlayOneShot(useSound);

        if (dialogueGroupIndex >= 0)
        {
            TimelineManager tm = FindFirstObjectByType<TimelineManager>(FindObjectsInactive.Include);
            if (tm != null)
            {
                bool done = false;
                tm.PlayImmediateDialogue(dialogueGroupIndex, () => done = true);
                yield return new WaitUntil(() => done);
                // 대사가 끝나면 TimelineManager가 테이블 카메라 잠금을 다시 걸 수 있으니 탐색용으로 되돌린다.
                CamMove.blockLook = true;
                CamMove.blockInteraction = true;
            }
        }

        // 문서를 펼쳐 중요한 곳에 줄을 긋는다. 다 그으면 그은 문장들이 기억 노트에 적힌다.
        if (underlineDocument != null)
        {
            yield return underlineDocument.Run(player);
            if (!string.IsNullOrEmpty(clueTitle))
                MemoryNotebook.Add(clueId, clueTitle, underlineDocument.BuildNoteText(clueText), memoryIndex);
        }
        else if (!string.IsNullOrEmpty(clueTitle))
            MemoryNotebook.Add(clueId, clueTitle, clueText, memoryIndex);

        if (openOnUse != null) foreach (var d in openOnUse) if (d != null) d.Open();

        foreach (var go in disableOnUse) if (go != null) go.SetActive(false);
        foreach (var go in enableOnUse) if (go != null) go.SetActive(true);

        if (!string.IsNullOrEmpty(message))
            ObjectiveHUD.Instance.ShowMessage(message, 2.5f);

        Used = true;
        IsBusy = false;
        player.InputPaused = false;
    }

    private void UpdateLoop(FreeRoamMovement player)
    {
        if (loopSound == null && usePhoneRing) loopSound = ProceduralSfx.PhoneRing();
        if (loopSound == null) return;

        bool shouldPlay = player != null && !Used &&
                          Vector3.Distance(player.transform.position, transform.position) <= loopMaxDistance;

        if (shouldPlay && loopSource == null)
        {
            loopSource = gameObject.AddComponent<AudioSource>();
            loopSource.clip = loopSound;
            loopSource.loop = true;
            loopSource.spatialBlend = 1f;
            loopSource.rolloffMode = AudioRolloffMode.Linear;
            loopSource.minDistance = 1f;
            loopSource.maxDistance = loopMaxDistance;
            loopSource.volume = loopVolume;
        }

        if (loopSource == null) return;
        if (shouldPlay && !loopSource.isPlaying) loopSource.Play();
        else if (!shouldPlay && loopSource.isPlaying) loopSource.Stop();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null) return;
        if (oneShotSource == null)
        {
            oneShotSource = gameObject.AddComponent<AudioSource>();
            oneShotSource.spatialBlend = 0f;
            oneShotSource.playOnAwake = false;
        }
        oneShotSource.PlayOneShot(clip);
    }

    private static FreeRoamMovement ActivePlayer()
    {
        var p = FindFirstObjectByType<FreeRoamMovement>();
        return p != null && p.IsControlling ? p : null;
    }
}
