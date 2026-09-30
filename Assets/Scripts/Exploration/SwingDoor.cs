using System.Collections;
using UnityEngine;

// 경첩을 축으로 열리고 닫히는 문. (탐색 구간용)
//  - 이 컴포넌트는 "경첩" 오브젝트에 붙이고, 문짝 모델은 그 자식으로 둔다.
//    경첩의 Y축을 기준으로 돌아간다.
//  - interactive를 켜면 가까이 가서 바라보고 [E]로 직접 여닫을 수 있다.
//  - InteractSpot(잠긴 문 등)이나 점프 스퀘어가 Open()/Close()/SlamOpen()을 불러 쓸 수도 있다.
public class SwingDoor : MonoBehaviour
{
    [Header("움직임")]
    [Tooltip("열렸을 때 돌아가는 각도")]
    [SerializeField] private float openAngle = 95f;
    [Tooltip("플레이어 반대쪽으로 열리게 할지 (끄면 openAngle 방향 그대로)")]
    [SerializeField] private bool openAwayFromPlayer = true;
    [SerializeField] private float openDuration = 0.9f;
    [SerializeField] private float closeDuration = 0.6f;
    [SerializeField] private bool startsOpen = false;

    [Header("직접 여닫기 ([E])")]
    [SerializeField] private bool interactive = false;
    [SerializeField] private string openPrompt = "[E] 문 열기";
    [SerializeField] private string closePrompt = "[E] 문 닫기";
    [SerializeField] private float radius = 2.2f;
    [SerializeField] private float lookAngle = 55f;
    [Tooltip("이 단서가 기억 노트에 있어야 열 수 있다. (비우면 항상 열림)")]
    [SerializeField] private string requiredClueId = "";
    [SerializeField] private string lockedMessage = "잠겨 있다.";

    [Header("소리 (비우면 코드로 만든 소리)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 0.8f;

    public bool IsOpen { get; private set; }
    public bool IsMoving => moveRoutine != null;

    private static SwingDoor focused;
    private Quaternion closedRotation;
    private bool initialized;
    private float openSign = 1f;
    private Coroutine moveRoutine;
    private AudioSource source;

    public void SetLocked(string clueId, string message = null)
    {
        requiredClueId = clueId ?? "";
        if (message != null) lockedMessage = message;
    }

    public void SetInteractive(bool on) => interactive = on;

    private void Awake() => Init();

    private void Init()
    {
        if (initialized) return;
        initialized = true;
        closedRotation = transform.localRotation;
        if (startsOpen) SnapOpen();
    }

    /// <summary>처음 상태(닫힘 또는 startsOpen)로 즉시 되돌린다.</summary>
    public void ResetState()
    {
        Init();
        StopMove();
        if (startsOpen) SnapOpen();
        else { transform.localRotation = closedRotation; IsOpen = false; }
    }

    public void Open() => Move(true, openDuration, true);
    public void Close() => Move(false, closeDuration, true);
    public void Toggle() { if (IsOpen) Close(); else Open(); }

    /// <summary>점프 스퀘어용: 문을 확 열어젖히고 끝에 쾅 소리.</summary>
    public void SlamOpen(float duration = 0.18f)
    {
        Init();
        StopMove();
        ChooseSide();
        IsOpen = true;
        moveRoutine = StartCoroutine(MoveRoutine(OpenRotation(), duration, null, closeSound != null ? closeSound : ProceduralSfx.DoorThud(), 1.2f));
    }

    private void Move(bool open, float duration, bool withSound)
    {
        Init();
        if (open == IsOpen && moveRoutine == null) return;
        StopMove();
        if (open) ChooseSide();
        IsOpen = open;

        AudioClip startClip = null, endClip = null;
        if (withSound)
        {
            if (open) startClip = openSound != null ? openSound : ProceduralSfx.DoorCreak();
            else endClip = closeSound != null ? closeSound : ProceduralSfx.DoorThud();
        }
        moveRoutine = StartCoroutine(MoveRoutine(open ? OpenRotation() : closedRotation, duration, startClip, endClip, 1f));
    }

    private void SnapOpen()
    {
        IsOpen = true;
        transform.localRotation = OpenRotation();
    }

    private Quaternion OpenRotation() => closedRotation * Quaternion.Euler(0f, openAngle * openSign, 0f);

    // 플레이어가 있는 쪽의 반대로 열리도록 방향을 고른다.
    private void ChooseSide()
    {
        openSign = 1f;
        if (!openAwayFromPlayer) return;

        FreeRoamMovement player = FindFirstObjectByType<FreeRoamMovement>();
        if (player == null || !player.IsControlling) return;

        // 문짝이 경첩에서 뻗은 방향 (모델마다 축이 달라서 실제 모양으로 잰다)
        Vector3 leaf = transform.right;
        var r = GetComponentInChildren<Renderer>();
        if (r != null)
        {
            leaf = r.bounds.center - transform.position;
            leaf.y = 0f;
            if (leaf.sqrMagnitude < 0.0001f) leaf = transform.right;
        }
        leaf.Normalize();
        Vector3 normal = Vector3.Cross(Vector3.up, leaf);
        Vector3 toPlayer = player.transform.position - transform.position;

        // +각도로 돌 때 문짝 끝이 움직이는 방향
        Vector3 axis = transform.parent != null ? transform.parent.rotation * (closedRotation * Vector3.up) : closedRotation * Vector3.up;
        Vector3 swingDir = Quaternion.AngleAxis(1f, axis) * leaf - leaf;
        float playerSide = Vector3.Dot(toPlayer, normal);
        float swingSide = Vector3.Dot(swingDir, normal);
        openSign = (playerSide * swingSide > 0f) ? -1f : 1f;
    }

    private void StopMove()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        moveRoutine = null;
    }

    private IEnumerator MoveRoutine(Quaternion target, float duration, AudioClip startClip, AudioClip endClip, float endVolume)
    {
        Play(startClip, 1f);
        Quaternion from = transform.localRotation;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // 처음엔 무겁게, 끝에서 살짝 감속
            k = k * k * (3f - 2f * k);
            transform.localRotation = Quaternion.Slerp(from, target, k);
            yield return null;
        }
        transform.localRotation = target;
        Play(endClip, endVolume);
        moveRoutine = null;
    }

    private void Play(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.85f;
            source.minDistance = 2f;
            source.maxDistance = 25f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }
        source.PlayOneShot(clip, Mathf.Clamp01(soundVolume * volumeScale));
    }

    // ── [E]로 직접 여닫기 ──
    private void Update()
    {
        if (!interactive) { if (focused == this) Unfocus(); return; }

        FreeRoamMovement player = FindFirstObjectByType<FreeRoamMovement>();
        bool usable = player != null && player.IsControlling && !player.InputPaused && moveRoutine == null;
        bool inFocus = usable && !InteractSpot.AnyFocused && InFocus(player.Head);

        if (inFocus && (focused == null || focused == this || !focused.InFocus(player.Head)))
        {
            string prompt = IsOpen ? closePrompt : openPrompt;
            if (focused != this) focused = this;
            ObjectiveHUD.Instance.SetPrompt(prompt);

            if (Input.GetKeyDown(KeyCode.E))
            {
                if (!IsOpen && !string.IsNullOrEmpty(requiredClueId) && !MemoryNotebook.Has(requiredClueId))
                    ObjectiveHUD.Instance.ShowMessage(lockedMessage, 2f);
                else
                    Toggle();
            }
        }
        else if (focused == this)
        {
            Unfocus();
        }
    }

    private void OnDisable()
    {
        if (focused == this) Unfocus();
    }

    private void Unfocus()
    {
        focused = null;
        ObjectiveHUD.Instance.SetPrompt(null);
    }

    private bool InFocus(Transform head)
    {
        Vector3 target = transform.position;
        var r = GetComponentInChildren<Renderer>();
        if (r != null) target = r.bounds.center;

        Vector3 flat = target - head.position; flat.y = 0f;
        if (flat.magnitude > radius) return false;
        return Vector3.Angle(head.forward, (target - head.position).normalized) <= lookAngle;
    }
}
