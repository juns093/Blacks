using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TypeWriter : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Text dialogueText;
    [SerializeField] private Text promptText; // F키 안내 텍스트

    [Header("대사가 표시될 패널")]
    [Tooltip("대사 출력이 이 패널에 종속됩니다. 시퀀스 시작 시 자동으로 켜지고, " +
             "hidePanelOnFinish가 체크되어 있으면 시퀀스가 끝날 때 자동으로 꺼집니다.")]
    [SerializeField] private GameObject dialoguePanel;

    [Tooltip("대사 시퀀스가 모두 끝났을 때 dialoguePanel을 자동으로 비활성화할지 여부")]
    [SerializeField] private bool hidePanelOnFinish = true;

    [Tooltip("dialoguePanel(또는 자식)의 배경 Image. StoryScene처럼 타임라인 Animation Track이 " +
             "이 배경의 알파를 고정된 시간 동안만 반짝이도록 구워둔 경우, 실제 대사 출력 시간(타이핑 속도에 따라 " +
             "가변적)과 어긋나서 대사 도중 배경이 사라지는 문제가 생깁니다. " +
             "이 필드를 연결하면 대사가 재생 중인 동안 매 프레임 알파를 1로 강제 고정해 타임라인 커브를 덮어씁니다. " +
             "비워두면 dialoguePanel에서 자동으로 Image를 찾아 사용합니다.")]
    [SerializeField] private Image dialoguePanelBackground;

    [Tooltip("체크하면 dialoguePanelBackground(배경 이미지)를 아예 표시하지 않습니다. StoryScene처럼 대사 배경 판넬 없이 텍스트만 보여주고 싶을 때 사용합니다.")]
    [SerializeField] private bool hidePanelBackground = false;

    // ── 대사 "그룹" 정의: 이름(참고용) + 그 그룹에 속한 대사 줄들 ──
    [System.Serializable]
    public class DialogueGroup
    {
        [Tooltip("인스펙터에서 구분하기 쉽도록 붙이는 이름 (로직에는 영향 없음)")]
        public string groupName;

        [TextArea(2, 5)]
        [Tooltip("이 그룹에서 순서대로 재생될 대사 줄들")]
        public string[] lines;
    }

    [Header("대사 목록 (그룹별로 관리)")]
    [Tooltip("대사를 그룹 단위로 나눠서 등록합니다. 예: [0]=최초 기상 대사, [1]=재장전 대사 ... " +
             "PlayDialogueGroup(인덱스)를 호출하면 해당 그룹의 대사가 처음부터 재생됩니다.")]
    [SerializeField] private DialogueGroup[] dialogueGroups;

    [Header("설정")]
    [SerializeField] private float typingSpeed = 0.05f;       // 한 글자당 대기 시간(초)
    [SerializeField] private float delayBetweenDialogues = 2f; // 대사 간 자동 넘김 딜레이(초)

    [Header("타이핑 효과음")]
    [SerializeField] private AudioClip typingSound;           // 타이핑 효과음
    [SerializeField] private float soundVolume = 0.5f;        // 효과음 볼륨 (0~1)

    [Header("스킵 허용 여부")]
    [Tooltip("체크 해제하면 좌클릭으로 스킵/다음 대사 넘기기가 되지 않음 (엔딩 컷씬 등에서 사용)")]
    [SerializeField] private bool allowSkip = true;

    [Header("시작 시 자동 재생 여부")]
    [Tooltip("체크하면 씬 시작(Start)과 동시에 dialogueGroups[0]이 자동으로 재생됨. " +
             "TimelineManager가 타이밍을 제어하는 경우(예: Wakeup 타임라인이 끝난 뒤 재생) " +
             "반드시 체크 해제하고, 대신 StartDialogueSequence() 또는 PlayDialogueGroup(0)을 외부에서 호출할 것.")]
    [SerializeField] private bool playOnStart = false;

    // 스토리 기본 대사 자동 주입은 GameSceneStoryDialogues 컴포넌트로 분리되었습니다.
    // (StoryScene과 GameScene의 대사 데이터가 서로 충돌하지 않도록 하기 위함)

    // 현재 재생 중인(활성화된) 대사 줄 배열 - dialogueGroups 중 하나가 선택되어 여기에 복사됨
    private string[] currentDialogues;

    private int currentIndex = 0;
    private bool isTyping = false;
    private bool isWaitingDelay = false;
    private bool skipRequested = false;
    private Coroutine typingCoroutine;
    public static bool allFinished = false;
    private AudioSource audioSource;  // 효과음 재생용

    /// <summary>
    /// 타이핑이 진행 중인지 확인합니다.
    /// </summary>
    public bool IsTyping => isTyping;

    /// <summary>
    /// 현재 재생 중인 대사 그룹(시퀀스)이 모두 끝났을 때 호출되는 이벤트.
    /// TimelineManager 등 외부에서 구독해서 "대사가 끝나면 다음 타임라인 재생" 같은 처리를 할 수 있습니다.
    /// </summary>
    public event System.Action OnSequenceFinished;

    void Awake()
    {
        // AudioSource 캐싱 (없으면 생성)
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (dialogueText != null)
            dialogueText.text = "";

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        // 패널은 재생이 시작될 때만 켜져야 하므로, 씬 시작 시점(Awake)에 항상 꺼둔다.
        // (인스펙터에서 켜진 채로 배치돼 있어도 강제로 끈다.)
        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (dialoguePanelBackground == null && dialoguePanel != null)
            dialoguePanelBackground = dialoguePanel.GetComponent<Image>();

        if (hidePanelBackground && dialoguePanelBackground != null)
            dialoguePanelBackground.enabled = false;

        // 스토리 기본 대사 자동 주입은 GameSceneStoryDialogues가 담당(분리됨)
    }

    // StoryScene 등에서 타임라인 Animation Track이 dialoguePanel 배경의 알파를 고정된 시간 동안만
    // 반짝이도록 구워둔 경우, 실제 대사 출력 시간(가변적)과 어긋나 배경이 대사 도중 사라지는 문제가 있다.
    // 대사창이 켜져 있는 동안에는 매 프레임 알파를 1로 강제 고정해 그 커브를 덮어쓴다.
    void LateUpdate()
    {
        if (hidePanelBackground) return;
        if (dialoguePanelBackground == null || dialoguePanel == null) return;
        if (!dialoguePanel.activeInHierarchy) return;

        Color c = dialoguePanelBackground.color;
        if (c.a != 1f)
        {
            c.a = 1f;
            dialoguePanelBackground.color = c;
        }
    }



    public void EnsureDialogueGroupSize(int requiredSize)
    {
        if (dialogueGroups == null)
            dialogueGroups = new DialogueGroup[requiredSize];

        if (dialogueGroups.Length >= requiredSize)
            return;

        DialogueGroup[] resized = new DialogueGroup[requiredSize];
        for (int i = 0; i < dialogueGroups.Length; i++)
            resized[i] = dialogueGroups[i];

        dialogueGroups = resized;
    }

    public void SetDialogueGroup(int index, string name, string[] lines)
    {
        if (dialogueGroups[index] == null)
            dialogueGroups[index] = new DialogueGroup();

        dialogueGroups[index].groupName = name;
        dialogueGroups[index].lines = lines;
    }

    void Start()
    {

        // playOnStart가 체크되어 있을 때만 씬 시작과 동시에 0번 그룹을 자동 재생.
        // 체크 안 되어 있으면 아무것도 하지 않고 외부(PlayDialogueGroup 등)를 기다린다.
        // TimelineManager가 씬에 있으면 대사 타이밍은 그쪽이 정한다.
        // 여기서 0번 그룹(인스펙터에 남은 옛 대사)을 자동 재생하면 인트로 대신 엉뚱한 대사가 먼저 나온다.
        bool timelineControlled = playOnStart && FindFirstObjectByType<TimelineManager>() != null;
        if (timelineControlled)
            Debug.LogWarning("[TypeWriter] playOnStart가 켜져 있지만 TimelineManager가 대사 타이밍을 제어하므로 자동 재생을 건너뜁니다.");

        if (playOnStart && !timelineControlled && dialogueGroups != null && dialogueGroups.Length > 0)
        {
            PlayDialogueGroup(0);
        }
        else if (dialogueGroups == null || dialogueGroups.Length == 0)
        {
            allFinished = true;
        }
    }

    void Update()
    {
        if (!allowSkip) return; // 스킵이 꺼져있으면 아래 로직 전부 무시

        // 좌클릭으로 스킵
        if (Input.GetMouseButtonDown(0))
        {
            if (isTyping)
            {
                // 타이핑 중이면 즉시 전체 표시
                skipRequested = true;
            }
            else if (isWaitingDelay)
            {
                // 딜레이 대기 중이면 즉시 다음 대사로
                skipRequested = true;
            }
        }
    }

    /// <summary>
    /// 외부에서 좌클릭 스킵 기능을 켜고 끌 수 있게 해줌
    /// </summary>
    public void SetSkipEnabled(bool enabled)
    {
        allowSkip = enabled;
    }

    /// <summary>
    /// 인스펙터에 등록해둔 dialogueGroups 중 원하는 인덱스의 대사 그룹을 처음부터 재생합니다.
    /// 예: PlayDialogueGroup(0) = 최초 기상 대사, PlayDialogueGroup(1) = 재장전 대사 ...
    /// 그룹의 대사가 모두 끝나면 OnSequenceFinished 이벤트가 발생합니다.
    /// </summary>
    public void PlayDialogueGroup(int groupIndex)
    {
        // 재생할 게 없을 때도 반드시 "끝났다"를 알린다.
        // 그냥 return하면 대사가 끝나기를 기다리던 쪽(사망 연출, 아이템 연출 등)이 영원히 멈춘다.
        if (dialogueGroups == null || groupIndex < 0 || groupIndex >= dialogueGroups.Length)
        {
            Debug.LogWarning($"[TypeWriter] dialogueGroups[{groupIndex}]가 존재하지 않습니다. " +
                              $"등록된 그룹 개수: {(dialogueGroups == null ? 0 : dialogueGroups.Length)}. 바로 완료 처리합니다.");
            OnAllDialoguesFinished();
            return;
        }

        var group = dialogueGroups[groupIndex];
        if (group == null)
        {
            Debug.LogWarning($"[TypeWriter] dialogueGroups[{groupIndex}]가 null입니다. 바로 완료 처리합니다.");
            OnAllDialoguesFinished();
            return;
        }

        if (group.lines == null || group.lines.Length == 0)
        {
            Debug.LogWarning($"[TypeWriter] dialogueGroups[{groupIndex}]의 lines가 비어 있습니다. 바로 완료 처리합니다.");
            OnAllDialoguesFinished();
            return;
        }

        // 실제로 이 groupIndex에 어떤 내용이 들어있는지 그대로 출력 (인스펙터 데이터 진단용)
        string linesPreview = group.lines == null
            ? "(null)"
            : string.Join(" | ", group.lines);
        Debug.Log($"[TypeWriter] PlayDialogueGroup({groupIndex}) 호출됨. " +
                   $"groupName=\"{group.groupName}\", lines=[{linesPreview}]");

        // 배열 참조 공유 여부 확인용: 이 그룹의 lines 배열이 다른 그룹과 같은 참조인지 체크
        for (int i = 0; i < dialogueGroups.Length; i++)
        {
            if (i == groupIndex) continue;

            var otherGroup = dialogueGroups[i];
            if (otherGroup == null) continue;

            if (otherGroup.lines != null && ReferenceEquals(otherGroup.lines, group.lines))
            {
                Debug.LogError($"[TypeWriter] 경고! dialogueGroups[{groupIndex}]와 dialogueGroups[{i}]가 " +
                                $"똑같은 lines 배열 인스턴스를 공유하고 있습니다! (인스펙터에서 복제(Duplicate) 시 발생하는 문제) " +
                                $"인스펙터에서 그룹 [{groupIndex}] 또는 [{i}]의 Lines를 완전히 삭제 후 새로 입력해서 배열을 분리하세요.");
            }
        }

        currentDialogues = group.lines;
        StartDialogueSequenceInternal();
    }

    /// <summary>
    /// 현재 설정된 대사(currentDialogues)를 처음(0번)부터 재생 시작합니다.
    /// 아직 한 번도 PlayDialogueGroup()이 호출된 적이 없다면 dialogueGroups[0]을 사용합니다.
    /// TimelineManager처럼 "타이밍만 지시"하고 싶을 때 이 함수를 호출하세요.
    /// </summary>
    public void StartDialogueSequence()
    {
        if (currentDialogues == null && dialogueGroups != null && dialogueGroups.Length > 0)
        {
            var firstGroup = dialogueGroups[0];
            currentDialogues = firstGroup != null ? firstGroup.lines : null;
        }

        StartDialogueSequenceInternal();
    }

    private void StartDialogueSequenceInternal()
    {
        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        // 대사가 나오는 동안엔 BGM을 줄여서 대사가 잘 들리게 한다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.RequestDuck();

        // 대사 진행 중에는 카메라를 움직이지 못하게 막는다
        CamMove.blockLook = true;

        currentIndex = 0;
        allFinished = false;

        if (dialogueText != null)
            dialogueText.text = "";

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (currentDialogues != null && currentDialogues.Length > 0)
        {
            Debug.Log(currentDialogues.Length);
            ShowDialogue(0);
        }
        else
        {
            Debug.LogWarning("[TypeWriter] 재생할 대사가 없습니다. 바로 완료 처리합니다.");
            OnAllDialoguesFinished();
        }
    }

    /// <summary>
    /// 지정 인덱스의 대사를 한 글자씩 출력합니다. (currentDialogues 배열 기준, 끝나면 자동으로 다음 대사로 넘어감)
    /// </summary>
    public void ShowDialogue(int index)
    {
        if (currentDialogues == null || index < 0 || index >= currentDialogues.Length)
            return;

        currentIndex = index;

        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        typingCoroutine = StartCoroutine(TypeAndAdvance(currentDialogues[index]));
    }

    /// <summary>
    /// 대사 한 줄만 타이핑하고, 끝나면 다음 대사로 자동으로 넘어가지 않고 바로 완료 처리함.
    /// 엔딩 컷씬처럼 "이 한 줄만 보여주고 끝"인 경우에 사용.
    /// </summary>
    public void PlaySingleLine(string line)
    {
        if (typingCoroutine != null)
            StopCoroutine(typingCoroutine);

        // 대사 진행 중에는 카메라를 움직이지 못하게 막는다
        CamMove.blockLook = true;

        // 대사가 나오는 동안엔 BGM을 줄여서 대사가 잘 들리게 한다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.RequestDuck();

        allFinished = false;
        typingCoroutine = StartCoroutine(TypeSingleLineRoutine(line));
    }

    /// <summary>
    /// 외부에서 대사 배열을 세팅하고 처음부터 재생합니다.
    /// (그룹 방식 대신 즉석에서 대사 내용을 직접 넘기고 싶을 때 사용)
    /// </summary>
    public void SetDialogues(string[] newDialogues)
    {
        currentDialogues = newDialogues;
        currentIndex = 0;
        allFinished = false;

        if (dialogueText != null)
            dialogueText.text = "";

        if (promptText != null)
            promptText.gameObject.SetActive(false);

        if (currentDialogues != null && currentDialogues.Length > 0)
            ShowDialogue(0);
        else
            allFinished = true;
    }

    /// <summary>
    /// 현재 대사가 마지막인지 확인합니다.
    /// </summary>
    public bool IsLastDialogue => currentDialogues == null || currentIndex >= currentDialogues.Length - 1;

    // ── 코루틴: 한 글자씩 출력 → 딜레이 → 자동 다음 대사 (배열 기반 재생용) ──
    private IEnumerator TypeAndAdvance(string fullText)
    {
        isTyping = true;
        isWaitingDelay = false;
        skipRequested = false;
        dialogueText.text = "";

        // 한 글자씩 출력
        foreach (char c in fullText)
        {
            if (skipRequested)
            {
                // 좌클릭 스킵: 전체 텍스트 즉시 표시
                dialogueText.text = fullText;
                break;
            }

            dialogueText.text += c;

            // 타이핑 효과음 재생 (공백 제외)
            if (!char.IsWhiteSpace(c))
            {
                PlayTypingSound();
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        dialogueText.text = fullText;
        isTyping = false;

        // 딜레이 대기 (좌클릭으로 스킵 가능)
        isWaitingDelay = true;
        skipRequested = false;

        float elapsed = 0f;
        while (elapsed < delayBetweenDialogues)
        {
            if (skipRequested)
                break;

            elapsed += Time.deltaTime;
            yield return null;
        }

        isWaitingDelay = false;

        // 다음 대사로
        currentIndex++;
        if (currentDialogues != null && currentIndex < currentDialogues.Length)
        {
            ShowDialogue(currentIndex);
        }
        else
        {
            OnAllDialoguesFinished();
        }
    }

    // ── 코루틴: 한 글자씩 출력만 하고, 끝나면 바로 완료 처리 (자동 다음 넘김 없음) ──
    private IEnumerator TypeSingleLineRoutine(string fullText)
    {
        isTyping = true;
        isWaitingDelay = false;
        skipRequested = false;
        dialogueText.text = "";

        foreach (char c in fullText)
        {
            if (skipRequested)
            {
                dialogueText.text = fullText;
                break;
            }

            dialogueText.text += c;

            if (!char.IsWhiteSpace(c))
            {
                PlayTypingSound();
            }

            yield return new WaitForSeconds(typingSpeed);
        }

        dialogueText.text = fullText;
        isTyping = false;

        // 다음 대사로 넘어가지 않고 여기서 바로 완료 처리
        allFinished = true;

        // 대사가 끝났다고 무조건 카메라를 풀면, 아이템 사용 타임라인처럼
        // TurnManager가 잠가둔 카메라까지 풀려버리는 버그가 생긴다.
        // TurnManager가 있으면 현재 턴/잠금 상태에 맞춰서만 다시 계산한다.
        if (TurnManager.Instance != null)
            TurnManager.ApplyCameraBlockForCurrentTurn();
        else
            CamMove.blockLook = false;

        // 대사가 끝났으니 줄여뒀던 BGM 볼륨을 되돌린다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.ReleaseDuck();

        if (promptText != null)
            promptText.gameObject.SetActive(true);
    }

    // ── 타이핑 효과음 재생 ──
    private void PlayTypingSound()
    {
        if (audioSource != null && typingSound != null)
        {
            audioSource.PlayOneShot(typingSound, soundVolume);
        }
    }

    // ── 모든 대사 완료 시 호출 (배열 기반 재생 전용) ──
    private void OnAllDialoguesFinished()
    {
        allFinished = true;

        if (hidePanelOnFinish && dialoguePanel != null)
            dialoguePanel.SetActive(false);

        // 대사가 끝났다고 무조건 카메라를 풀면, 아이템 사용 타임라인처럼
        // TurnManager가 잠가둔 카메라까지 풀려버리는 버그가 생긴다.
        // TurnManager가 있으면 현재 턴/잠금 상태에 맞춰서만 다시 계산한다.
        if (TurnManager.Instance != null)
            TurnManager.ApplyCameraBlockForCurrentTurn();
        else
            CamMove.blockLook = false;

        // 대사가 끝났으니 줄여뒀던 BGM 볼륨을 되돌린다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.ReleaseDuck();

        // TimelineManager 등 외부 구독자에게 "대사 시퀀스 끝났다"고 알림
        OnSequenceFinished?.Invoke();
    }
}