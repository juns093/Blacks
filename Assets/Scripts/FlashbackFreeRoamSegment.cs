using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 회상(기억 파편) 도중 직접 걸어 다니며 찾는 탐색 구간.
//
// ── 시작 방식 (startMode) ──
//  PauseTimeline  : 회상 타임라인을 pauseTimelineAt 초에 멈추고 탐색 → 끝나면 resumeTimelineAt부터 이어서 재생
//                   (4번째 아이템: 거리에서 구급상자를 줍고 차를 찾는다)
//  BeforeTimeline : 아이템을 쓰면 먼저 회상 장소로 들어가 탐색 → 마지막 장소에 닿으면 그때 타임라인 재생
//                   (2번째 아이템: 열쇠 → 단서들 → "여기구나")
//
// ── 목표 (전부 선택. 채운 것만 이 순서대로 진행) ──
//  1. pickupItem  : 제일 먼저 주울 것 (열쇠 등)
//  2. clueItems   : 모두 주워야 하는 단서들 (순서 무관)
//  3. evidenceItems : 찾아서 [E] 살펴보고 [F] 조작해야 하는 증거들 (순서 무관)
//  4. carFinder   : E키 경적으로 차 찾기 → 찾으면 차에 타고 떠나는 연출
//  5. finalSpot   : 마지막 장소. 닿으면 "여기구나" 후 암전
//  아무 목표도 없으면 외부에서 CompleteSegment()를 부를 때까지 계속됩니다.
//
// DeathItemSpawner의 "자유 이동 구간" 칸에 연결하면 해당 순서의 아이템에서 동작합니다.
public class FlashbackFreeRoamSegment : MonoBehaviour
{
    public enum StartMode
    {
        PauseTimeline,
        BeforeTimeline
    }

    [Header("시작 방식")]
    [Tooltip("PauseTimeline: 타임라인 도중에 멈추고 탐색 / BeforeTimeline: 탐색을 먼저 하고 마지막에 타임라인 재생")]
    [SerializeField] private StartMode startMode = StartMode.PauseTimeline;

    [Header("PauseTimeline 방식 (타임라인 기준, 초)")]
    [Tooltip("이 시점에서 타임라인을 멈추고 탐색을 시작합니다. 음수면 타임라인이 완전히 끝난 뒤에 시작합니다.")]
    [SerializeField] private float pauseTimelineAt = 38.5f;

    [Tooltip("탐색이 끝나면 이 시점부터 타임라인을 이어서 틉니다. 음수면 멈췄던 자리에서 그대로 이어갑니다.")]
    [SerializeField] private float resumeTimelineAt = 42.6f;

    [Header("BeforeTimeline 방식")]
    [Tooltip("탐색이 끝난 뒤 타임라인을 이 시점부터 재생합니다. " +
             "타임라인 앞부분이 테이블 장면이면, 회상 장면이 시작되는 시점으로 잡으세요.")]
    [SerializeField] private float timelineStartAt = 0f;

    public float PauseTimelineAt => pauseTimelineAt;
    public float ResumeTimelineAt => resumeTimelineAt;
    public float TimelineStartAt => timelineStartAt;
    public bool RunsBeforeTimeline => startMode == StartMode.BeforeTimeline;

    [Header("플레이어")]
    [Tooltip("WASD 이동 + 시점 컨트롤러. 평소엔 꺼진 오브젝트에 붙어 있습니다. 여러 구간이 같이 써도 됩니다.")]
    [SerializeField] private FreeRoamMovement player;

    [Tooltip("움직일 카메라. 비워두면 Camera.main")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("탐색 중에 꺼둘 CinemachineBrain. 비워두면 targetCamera에서 자동으로 찾습니다.")]
    [SerializeField] private Behaviour cinemachineBrain;

    [Tooltip("탐색을 시작할 위치(발 위치)와 바라볼 방향. 비워두면 지금 카메라가 보는 자리에서 시작합니다. " +
             "지정하면 암전 후 이곳으로 옮겨 시작합니다.")]
    [SerializeField] private Transform playerStartPoint;

    [Tooltip("시작 위치로 옮길 때 어두워졌다 밝아지는 시간(초)")]
    [SerializeField] private float enterFadeDuration = 0.8f;

    [Tooltip("이 구간에서 손전등(F키)을 쓸지 여부")]
    [SerializeField] private bool useFlashlight = false;

    [Tooltip("켜면 Shift로 뛸 수 있다. (뛰면 행인이 발소리를 듣는다)")]
    [SerializeField] private bool allowSprint = false;

    [Header("탐색 중 화면 밝기")]
    [Tooltip("테이블 장면용 화면 보정(노출 -2.67, 대비 75)은 걸어 다니기엔 너무 어둡다. " +
             "켜면 탐색하는 동안에만 아래 값으로 바꿨다가 끝나면 되돌립니다.")]
    [SerializeField] private bool overrideGrading = false;
    [SerializeField] private float explorePostExposure = -1.4f;
    [SerializeField] private float exploreContrast = 30f;

    [Header("걷다가 걸려 오는 전화 (주머니 속 전화)")]
    [Tooltip("켜면: 걷다가 callTrigger 근처에 오면 전화벨이 울리고, [E]로 받으면 이 구간이 끝나고 영상이 이어진다.")]
    [SerializeField] private bool pocketCall = false;
    [SerializeField] private Transform callTrigger;
    [SerializeField] private float callTriggerRadius = 4f;
    [Tooltip("비워두면 코드로 만든 전화벨")]
    [SerializeField] private AudioClip ringClip;
    [Range(0f, 1f)] [SerializeField] private float ringVolume = 0.8f;
    [TextArea] [SerializeField] private string exploreHint = "주변을 둘러보자";
    [Tooltip("켜면: 주변 단서(선택 단서)를 전부 모아야 전화가 울린다. (callTrigger 대신)")]
    [SerializeField] private bool callAfterClues = false;
    [TextArea] [SerializeField] private string collectHint = "역을 둘러보며 단서를 모으세요  ({0}/{1})";
    [TextArea] [SerializeField] private string callHint = "전화가 울린다";
    [SerializeField] private string callPrompt = "[E] 전화 받기";

    [Header("전화를 받은 뒤 (통화 영상을 멈추고 이어서 탐색)")]
    [Tooltip("켜면: 전화를 받고 통화 영상이 나온 뒤 영상을 멈추고 다시 걸어 다닌다. (화장실 자료 → 노크 → 복귀)")]
    [SerializeField] private bool continueAfterCall = false;
    [Tooltip("통화 영상을 멈출 시점(초). 마커 대사는 멈춘 채로 끝까지 읽는다.")]
    [SerializeField] private float afterCallPauseAt = 6f;
    [Tooltip("탐색이 끝나면 영상을 이어 틀 시점(초). (테이블로 돌아가는 전환 구간)")]
    [SerializeField] private float afterCallResumeAt = 45.57f;
    [Tooltip("통화 뒤 먼저 역 밖으로 나가는 곳 (계단 위 등). 비워두면 바로 자료 찾기.")]
    [SerializeField] private Transform afterCallExitSpot;
    [SerializeField] private float afterCallExitRadius = 3f;
    [TextArea] [SerializeField] private string afterCallExitHint = "역 밖으로 나가세요";
    [Tooltip("역 밖으로 나가야 할 때만 켜지는 표시 (출구 쪽 불빛 등)")]
    [SerializeField] private GameObject afterCallExitMarker;
    [Tooltip("역 밖으로 나가면 암전 후 이 자리(발밑, 방향)에서 다시 걷는다.")]
    [SerializeField] private Transform afterCallOutsideStart;

    [Tooltip("찾아야 할 것 (화장실에 둔 사건 자료 등, [E])")]
    [SerializeField] private InteractSpot afterCallInteract;
    [TextArea] [SerializeField] private string afterCallHint = "화장실에 두고 갔다는 자료를 찾으세요";
    [Tooltip("자료를 본 뒤 돌아가는 길에 나오는 점프 스퀘어")]
    [SerializeField] private KnockJumpScare afterCallScare;
    [SerializeField] private Transform afterCallReturnSpot;
    [SerializeField] private float afterCallReturnRadius = 3f;
    [TextArea] [SerializeField] private string afterCallReturnHint = "승강장으로 돌아가세요";
    [Tooltip("탐색을 시작할 때 닫힌 상태로 되돌릴 문들")]
    [SerializeField] private SwingDoor[] afterCallResetDoors;

    public bool HasAfterCall => pocketCall && continueAfterCall;

    // 전화를 받은 자리. 통화 뒤 탐색은 여기서 다시 시작한다.
    private Vector3 callFeetPosition;
    private float callYaw;
    private bool hasCallPosition;
    public float AfterCallPauseAt => afterCallPauseAt;
    public float AfterCallResumeAt => afterCallResumeAt;
    public InteractSpot AfterCallInteract => afterCallInteract;

    [Header("목표 0: 꼭 해야 하는 상호작용 (전화 받기 등, [E])")]
    [SerializeField] private InteractSpot requiredInteract;
    [TextArea] [SerializeField] private string interactHint = "울리는 전화를 받으세요";

    [Header("선택 단서 (주우면 기억 노트에 적힘, 안 주워도 진행 가능)")]
    [SerializeField] private FreeRoamPickupItem[] optionalClues;
    [SerializeField] private InteractSpot[] resetOnStart;

    [Tooltip("이 구간에서 쓸 눈 높이 (0이면 플레이어 기본값). 병원처럼 맵이 크게 만들어진 곳은 높여야 한다.")]
    [SerializeField] private float eyeHeight = 0f;

    [Header("목격자 (들키면 처음 자리에서 다시)")]
    [SerializeField] private WitnessPatrol[] witnesses;
    [TextArea] [SerializeField] private string caughtHint = "들켰다...!";

    [Header("마지막: 추리 질문 (맞혀야 기억이 완성됨)")]
    [SerializeField] private DeductionQuestion deduction;
    [Tooltip("켜면 탐색 구간 끝이 아니라, 회상 영상이 끝난 뒤(테이블로 돌아온 뒤)에 묻는다.")]
    [SerializeField] private bool deductionAfterTimeline = false;

    public bool AsksDeductionAfterTimeline => deduction != null && deductionAfterTimeline;

    [Header("목표 1: 제일 먼저 주울 것 (열쇠 등)")]
    [SerializeField] private FreeRoamPickupItem pickupItem;

    [Header("목표 2: 모두 찾아야 하는 단서 (순서 무관)")]
    [SerializeField] private FreeRoamPickupItem[] clueItems;

    [Header("목표 3: 증거 조작 (순서 무관)")]
    [Tooltip("가까이 가서 [E]로 살펴보고 [F]로 조작해야 하는 증거들")]
    [SerializeField] private EvidenceItem[] evidenceItems;

    [Tooltip("증거 문서를 보여줄 화면. 비워두면 자동으로 만듭니다.")]
    [SerializeField] private EvidenceViewer evidenceViewer;

    [Header("목표 4: 차 찾기 (E키 경적)")]
    [SerializeField] private CarSoundFinder carFinder;

    [Header("목표 5: 마지막 장소")]
    [Tooltip("여기에 닿으면 '여기구나' 문구 후 암전됩니다.")]
    [SerializeField] private Transform finalSpot;

    [Tooltip("마지막 장소 판정 반경(수평, m)")]
    [SerializeField] private float finalSpotRadius = 2f;

    [Tooltip("마지막 장소에 닿은 뒤 암전되기 전까지 머무는 시간(초)")]
    [SerializeField] private float finalHoldDuration = 1.2f;

    [Header("안내 문구 (선택)")]
    [Tooltip("아래 가운데에 잠깐 뜨는 문구 (주웠을 때 문구, [E] 살펴보기 등). 목표는 왼쪽 위(ObjectiveHUD)에 뜹니다.")]
    [SerializeField] private Text hintText;
    [Tooltip("시작할 때 아래에 뜨는 조작 안내. WASD를 한 번 누르면 사라집니다. 손전등이 있으면 자동으로 덧붙습니다.")]
    [SerializeField] private string controlsHint = "WASD 이동  /  마우스 시점";
    [TextArea] [SerializeField] private string pickupHint = "근처에 떨어진 물건을 주우세요";
    [Tooltip("{0} = 찾은 개수, {1} = 전체 개수")]
    [TextArea] [SerializeField] private string cluesHint = "단서를 찾으세요  ({0}/{1})";
    [Tooltip("{0} = 조작한 개수, {1} = 전체 개수")]
    [TextArea] [SerializeField] private string evidenceHint = "증거를 찾아 없애세요  ({0}/{1})";
    [TextArea] [SerializeField] private string evidenceFocusHint = "[E] 살펴보기";
    [TextArea] [SerializeField] private string evidenceDoneHint = "...이제 아무도 모를 거야.";
    [TextArea] [SerializeField] private string searchHint = "E : 차 키 버튼\n가까울수록 경적이 크게 들립니다";
    [TextArea] [SerializeField] private string finalHint = "마지막 장소를 찾으세요";
    [TextArea] [SerializeField] private string foundHint = "찾았다...";
    [TextArea] [SerializeField] private string finalReachedHint = "여기구나...";

    [Tooltip("물건을 주웠을 때 그 물건의 문구를 보여주는 시간(초)")]
    [SerializeField] private float pickupMessageDuration = 2.5f;

    [Header("차에 타고 떠나기 (차 찾기 목표가 있을 때)")]
    [Tooltip("문 닫는 소리. 비워두면 코드로 만든 소리를 씁니다.")]
    [SerializeField] private AudioClip carDoorSound;

    [Tooltip("시동 + 출발 소리. 비워두면 코드로 만든 소리(약 4초)를 씁니다.")]
    [SerializeField] private AudioClip carEngineSound;

    [Tooltip("시동이 걸린 뒤 이어지는 엔진(주행) 소리. 비워두면 시동 소리만 냅니다.")]
    [SerializeField] private AudioClip carDriveSound;

    [Tooltip("주행 소리를 들려주는 시간(초). 이 동안 서서히 작아집니다.")]
    [SerializeField] private float carDriveDuration = 4.5f;

    [Range(0f, 1f)]
    [SerializeField] private float carSoundVolume = 0.9f;

    [Tooltip("차를 찾은 뒤 차 쪽으로 고개를 돌리는 시간(초)")]
    [SerializeField] private float lookAtCarDuration = 0.7f;

    [Tooltip("문 소리가 난 뒤 시동 소리가 시작되기까지(초)")]
    [SerializeField] private float engineDelayAfterDoor = 0.6f;

    [Tooltip("엔진 소리가 끝난 뒤에도 암전을 유지하는 시간(초)")]
    [SerializeField] private float holdBlackAfterEngine = 0.3f;

    [Header("암전")]
    [Tooltip("구간이 끝날 때 화면이 완전히 까매지는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 1.2f;

    [Tooltip("타임라인으로 넘어간 뒤 화면이 다시 밝아지는 시간(초)")]
    [SerializeField] private float fadeInDuration = 1.0f;

    [Tooltip("암전에 쓸 검은 이미지. 비워두면 맨 위에 그려지는 캔버스를 자동으로 만듭니다.")]
    [SerializeField] private Image fadeImage;

    [Header("구간 종료 후")]
    [Tooltip("구간이 끝났을 때 추가로 호출할 동작 (선택)")]
    public UnityEvent onSegmentComplete;

    public bool IsRunning { get; private set; }

    public FreeRoamPickupItem PickupItem => pickupItem;
    public EvidenceItem[] EvidenceItems => evidenceItems;
    public FreeRoamPickupItem[] ClueItems => clueItems;
    public FreeRoamPickupItem[] OptionalClues => optionalClues;
    public InteractSpot RequiredInteract => requiredInteract;
    public DeductionQuestion Deduction => deduction;

    /// <summary>
    /// 대본 파일(GameSceneStoryDialogues)에서 안내 문구를 채울 때 씁니다. null인 항목은 그대로 둡니다.
    /// </summary>
    public void SetCollectHint(string hint, bool callOnlyAfterClues = true)
    {
        if (hint != null) collectHint = hint;
        callAfterClues = callOnlyAfterClues;
    }

    public void SetAfterCallTexts(string find = null, string back = null, string exit = null)
    {
        if (exit != null) afterCallExitHint = exit;
        if (find != null) afterCallHint = find;
        if (back != null) afterCallReturnHint = back;
    }

    public void SetHintTexts(string pickup = null, string evidence = null, string evidenceFocus = null,
                             string evidenceDone = null, string search = null, string final = null,
                             string found = null, string finalReached = null, string controls = null,
                             string interact = null, string clues = null,
                             string explore = null, string call = null, string callPromptText = null)
    {
        if (explore != null) exploreHint = explore;
        if (call != null) callHint = call;
        if (callPromptText != null) callPrompt = callPromptText;
        if (controls != null) controlsHint = controls;
        if (interact != null) interactHint = interact;
        if (clues != null) cluesHint = clues;
        if (pickup != null) pickupHint = pickup;
        if (evidence != null) evidenceHint = evidence;
        if (evidenceFocus != null) evidenceFocusHint = evidenceFocus;
        if (evidenceDone != null) evidenceDoneHint = evidenceDone;
        if (search != null) searchHint = search;
        if (final != null) finalHint = final;
        if (found != null) foundHint = found;
        if (finalReached != null) finalReachedHint = finalReached;
    }

    private AudioSource sfx;
    private Coroutine fadeRoutine;
    private GameObject fadeCanvasObject;
    private bool completeRequested = false;
    private string currentStepHint;
    private float messageUntil;

    private bool HasClues => clueItems != null && clueItems.Length > 0;
    private bool HasEvidence => evidenceItems != null && evidenceItems.Length > 0;
    private bool HasAnyGoal => pocketCall || requiredInteract != null || pickupItem != null || HasClues || HasEvidence || carFinder != null || finalSpot != null;

    private void Awake()
    {
        if (player != null) player.gameObject.SetActive(false);
        if (hintText != null) hintText.gameObject.SetActive(false);
        if (optionalClues != null) foreach (var c in optionalClues) if (c != null) c.gameObject.SetActive(false);
        if (witnesses != null) foreach (var w in witnesses) if (w != null) w.gameObject.SetActive(false);
        if (HasEvidence) foreach (var e in evidenceItems) if (e != null) e.Disarm();
        // 통화 뒤에 찾을 물건은 그때까지 쓰지 못한다. (먼저 읽어 버리지 않게)
        if (afterCallInteract != null) afterCallInteract.enabled = false;
    }

    /// <summary>
    /// 목표와 상관없이 지금 구간을 끝냅니다. 맵에 직접 만든 트리거나 타임라인 Signal에서 호출하세요.
    /// </summary>
    public void CompleteSegment()
    {
        if (IsRunning) completeRequested = true;
    }

    /// <summary>
    /// 탐색 구간 전체를 진행합니다. 모든 목표를 끝낼 때까지 돌아오지 않습니다.
    /// ItemFovInteract가 yield return으로 기다립니다.
    /// </summary>
    public IEnumerator RunSegment()
    {
        if (IsRunning)
        {
            Debug.LogWarning("[FlashbackFreeRoamSegment] 이미 진행 중입니다. 중복 호출을 무시합니다. " + System.Environment.StackTrace);
            yield break;
        }

        if (player == null)
        {
            Debug.LogWarning($"[FlashbackFreeRoamSegment] '{name}': player가 연결되어 있지 않아 탐색을 건너뜁니다!");
            yield break;
        }

        if (!HasAnyGoal)
            Debug.LogWarning($"[FlashbackFreeRoamSegment] '{name}': 목표가 하나도 없습니다. " +
                             "CompleteSegment()가 호출될 때까지 끝나지 않습니다.");

        IsRunning = true;
        completeRequested = false;
        messageUntil = 0f;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Behaviour brain = cinemachineBrain != null ? cinemachineBrain : FindBrain(cam);

        // 회상 마커 대사가 아직 떠 있으면 다 읽을 때까지 기다린다.
        // 기다리는 동안 멈춘 영상이 카메라를 놓아 버리면 테이블 카메라로 돌아가므로,
        // 지금 카메라 자리를 먼저 고정해 둔다. (탐색은 이 자리에서 시작한다)
        TimelineManager timelineManager = FindFirstObjectByType<TimelineManager>(FindObjectsInactive.Include);
        if (timelineManager != null && timelineManager.IsImmediateDialoguePlaying)
        {
            Vector3 holdPos = cam.transform.position;
            Quaternion holdRot = cam.transform.rotation;
            if (brain != null) brain.enabled = false;

            Debug.Log("[FlashbackFreeRoamSegment] 회상 대사가 끝나기를 기다립니다.");
            while (timelineManager.IsImmediateDialoguePlaying)
            {
                cam.transform.SetPositionAndRotation(holdPos, holdRot);
                yield return null;
            }
        }

        Debug.Log($"[FlashbackFreeRoamSegment] '{name}' 탐색 구간을 시작합니다. (방식: {startMode})");

        // 테이블 쪽 조작은 막고, 카메라는 Brain 대신 플레이어가 직접 움직인다.
        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        // 시작 위치가 따로 있으면 암전한 뒤 옮긴다.
        bool teleport = playerStartPoint != null;
        if (teleport && enterFadeDuration > 0f)
        {
            StartFade(1f, enterFadeDuration, 0);
            yield return new WaitForSeconds(enterFadeDuration);
        }

        if (brain != null) brain.enabled = false;

        if (overrideGrading) SetGradingWeight(1f);

        if (teleport)
            cam.transform.SetPositionAndRotation(playerStartPoint.position + Vector3.up * 1.65f, playerStartPoint.rotation);

        player.gameObject.SetActive(true);
        player.EyeHeightOverride = eyeHeight;
        player.BeginControl(cam);
        player.AllowSprint = allowSprint;

        FreeRoamFlashlight flashlight = player.GetComponent<FreeRoamFlashlight>();
        if (useFlashlight && flashlight == null)
            flashlight = player.gameObject.AddComponent<FreeRoamFlashlight>();
        if (flashlight != null)
            flashlight.SetAvailable(useFlashlight);
        if (carFinder != null) carFinder.SetListener(player.Head);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (teleport)
            StartFade(0f, enterFadeDuration, 0);

        // 조작 안내는 아래에 잠깐 (WASD를 누르면 사라짐), 목표는 왼쪽 위에.
        string controls = controlsHint;
        if (useFlashlight && !string.IsNullOrEmpty(controls) && !controls.Contains("손전등"))
            controls += "  /  F 손전등";
        if (allowSprint && !string.IsNullOrEmpty(controls) && !controls.Contains("Shift"))
            controls += "  /  Shift 달리기";
        ObjectiveHUD.Instance.ShowControls(controls);

        // 선택 단서 / 상호작용 지점 / 목격자 준비
        if (optionalClues != null) foreach (var c in optionalClues) if (c != null) c.Arm(player.transform);
        if (resetOnStart != null) foreach (var s in resetOnStart) if (s != null) s.ResetState();
        if (requiredInteract != null) requiredInteract.ResetState();
        if (witnesses != null) foreach (var w in witnesses) if (w != null) w.Arm();
        caughtPending = false;
        WitnessPatrol.OnCaught += HandleCaught;
        var caughtWatcher = StartCoroutine(CaughtWatcher());

        // ── 걷다가 걸려 오는 전화 ──
        //  둘러보다가 지정한 곳 근처에 오면 전화벨 → [E]로 받으면 구간이 끝나고 영상(통화 장면)으로 넘어간다.
        if (pocketCall && !completeRequested)
        {
            int clueTotal = 0;
            if (callAfterClues && optionalClues != null)
                foreach (var c in optionalClues) if (c != null) clueTotal++;

            if (clueTotal > 0)
            {
                // 단서를 전부 모으면 그때 전화가 울린다.
                SetWaypoints(() => NotPicked(optionalClues));
                int shown = -1;
                while (!completeRequested)
                {
                    int found = 0;
                    foreach (var c in optionalClues) if (c != null && c.IsPicked) found++;
                    if (found != shown)
                    {
                        shown = found;
                        SetStep(collectHint.Replace("{0}", found.ToString()).Replace("{1}", clueTotal.ToString()));
                    }
                    if (found >= clueTotal) break;
                    yield return null;
                }
                // 마지막 단서 문구를 읽을 시간
                if (!completeRequested) yield return new WaitForSeconds(1.5f);
            }
            else
            {
                SetStep(exploreHint);
                if (callTrigger != null)
                    yield return new WaitUntil(() => completeRequested ||
                        FlatDistance(player.transform.position, callTrigger.position) <= callTriggerRadius);
            }

            AudioSource ring = EnsureSfx();
            ring.clip = ringClip != null ? ringClip : ProceduralSfx.PhoneRing();
            ring.loop = true;
            ring.volume = ringVolume;
            ring.Play();
            Debug.Log("[FlashbackFreeRoamSegment] 전화가 울린다.");

            SetWaypoints(null);
            SetStep(callHint);
            ObjectiveHUD.Instance.SetPrompt(callPrompt);
            yield return new WaitUntil(() => completeRequested || (!player.InputPaused && Input.GetKeyDown(KeyCode.E)));

            ring.Stop();
            ring.loop = false;
            ObjectiveHUD.Instance.SetPrompt(null);

            callFeetPosition = player.transform.position;
            player.GetLook(out callYaw, out _);
            hasCallPosition = true;
            Debug.Log("[FlashbackFreeRoamSegment] 전화를 받았다. 영상으로 넘어간다.");
            completeRequested = true; // 나머지 목표 없이 바로 영상으로
        }

        // ── 목표 0: 꼭 해야 하는 상호작용 (전화 받기 등) ──
        if (requiredInteract != null && !completeRequested)
        {
            SetStep(interactHint);
            SetWaypoints(() => requiredInteract.Used ? null : One(requiredInteract.transform));
            yield return new WaitUntil(() => requiredInteract.Used || completeRequested);
        }

        // ── 목표 1: 제일 먼저 주울 것 ──
        if (pickupItem != null && !completeRequested)
        {
            pickupItem.Arm(player.transform);
            SetStep(pickupHint);
            SetWaypoints(() => pickupItem.IsPicked ? null : One(pickupItem.transform));
            yield return new WaitUntil(() => pickupItem.IsPicked || completeRequested);
            if (pickupItem.IsPicked)
            {
                Debug.Log($"[FlashbackFreeRoamSegment] '{pickupItem.name}' 획득.");
                ShowMessage(pickupItem.PickupMessage);
            }
        }

        // ── 목표 2: 단서들 (순서 무관) ──
        if (HasClues && !completeRequested)
        {
            int total = 0;
            foreach (var clue in clueItems)
                if (clue != null) { clue.Arm(player.transform); total++; }

            // 직전 프레임까지 주웠던 단서. 새로 주운 것을 골라 그 문구를 보여주기 위해 기억해 둔다.
            SetWaypoints(() => NotPicked(clueItems));
            var seen = new bool[clueItems.Length];
            int shown = -1;
            while (!completeRequested)
            {
                int found = 0;
                FreeRoamPickupItem newest = null;
                for (int i = 0; i < clueItems.Length; i++)
                {
                    var clue = clueItems[i];
                    if (clue == null || !clue.IsPicked) continue;
                    found++;
                    if (!seen[i]) { seen[i] = true; newest = clue; }
                }

                if (found != shown)
                {
                    shown = found;
                    SetStep(cluesHint.Replace("{0}", found.ToString()).Replace("{1}", total.ToString()));
                    if (newest != null) ShowMessage(newest.PickupMessage);
                    Debug.Log($"[FlashbackFreeRoamSegment] 단서 {found}/{total}");
                }

                if (found >= total) break;
                yield return null;
            }
        }

        // ── 목표 3: 증거 조작 (순서 무관) ──
        if (HasEvidence && !completeRequested)
        {
            SetWaypoints(NotTampered);
            yield return EvidenceStepRoutine();
        }

        // ── 목표 4: 차 찾기 (위치는 매 판 무작위) ──
        bool carFound = false;
        void HandleFound() => carFound = true;

        if (carFinder != null && !completeRequested)
        {
            carFinder.OnCarFound += HandleFound;
            carFinder.BeginSearch();
            SetWaypoints(null);
            SetStep(searchHint);

            yield return new WaitUntil(() => carFound || completeRequested);

            carFinder.OnCarFound -= HandleFound;
            carFinder.StopSearch();
        }

        // ── 목표 5: 마지막 장소 ──
        bool reachedFinal = false;
        if (finalSpot != null && !completeRequested && !carFound)
        {
            SetStep(finalHint);
            SetWaypoints(() => One(finalSpot));
            yield return new WaitUntil(() => completeRequested || FlatDistance(player.transform.position, finalSpot.position) <= finalSpotRadius);
            reachedFinal = !completeRequested;
            if (reachedFinal) Debug.Log("[FlashbackFreeRoamSegment] 마지막 장소에 도착했습니다.");
        }

        EndWaypoints();

        // 목격자는 여기서 멈춘다. (영상으로 넘어가는 동안 들키면 안 된다)
        WitnessPatrol.OnCaught -= HandleCaught;
        StopCoroutine(caughtWatcher);
        if (witnesses != null) foreach (var w in witnesses) if (w != null) w.Disarm();

        // 목표가 없으면 외부 호출을 기다린다.
        if (!HasAnyGoal)
            yield return new WaitUntil(() => completeRequested);

        // ── 마무리 연출 ──
        player.AllowSprint = false;
        player.EndControl();
        if (flashlight != null) flashlight.SetAvailable(false);

        if (carFound)
        {
            // 차를 바라봄 -> 문 소리 + 암전 -> 시동 걸고 출발
            ShowHint(foundHint);
            yield return LookAtCarRoutine(cam);
            HideHint();
            yield return BoardCarRoutine();
        }
        else
        {
            // "여기구나..." 잠깐 보여주고 암전
            if (reachedFinal)
            {
                ShowHint(finalReachedHint);
                if (finalHoldDuration > 0f)
                    yield return new WaitForSeconds(finalHoldDuration);
            }
            HideHint();
            StartFade(1f, fadeOutDuration, 0);
            yield return new WaitForSeconds(fadeOutDuration + 0.1f);
        }

        // 암전된 동안 조작을 거두고 원래 카메라 체계로 돌려준다.
        HideHint();
        ObjectiveHUD.Instance.HideAll();
        SetGradingWeight(0f);
        player.gameObject.SetActive(false);
        if (pickupItem != null) pickupItem.Disarm();
        if (HasClues) foreach (var clue in clueItems) if (clue != null) clue.Disarm();
        if (HasEvidence) foreach (var e in evidenceItems) if (e != null) e.Disarm();
        if (optionalClues != null) foreach (var c in optionalClues) if (c != null) c.Disarm();

        if (brain != null) brain.enabled = true;

        // CamMove의 기본 커서 상태로 되돌린다.
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        Debug.Log($"[FlashbackFreeRoamSegment] '{name}' 탐색 구간을 종료합니다.");
        EndWaypoints();
        IsRunning = false;
        completeRequested = false;

        onSegmentComplete?.Invoke();

        // 이 코루틴이 끝나면 ItemFovInteract가 같은 프레임에 타임라인을 틀거나 이어서 튼다.
        // 한 프레임 뒤에 밝아지기 시작해야 그 화면이 드러난다.
        StartFade(0f, fadeInDuration, 1);
    }

    /// <summary>
    /// 전화를 받은 뒤의 두 번째 탐색. ItemFovInteract가 통화 영상을 멈추고 기다린다.
    /// 영상이 보여 준 그 자리에서 다시 걷기 시작 → 자료 찾기 → (돌아가는 길에 노크 점프 스퀘어) → 복귀 지점
    /// </summary>
    public IEnumerator RunAfterCall()
    {
        if (IsRunning || player == null) yield break;

        IsRunning = true;
        completeRequested = false;
        messageUntil = 0f;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        Behaviour brain = cinemachineBrain != null ? cinemachineBrain : FindBrain(cam);

        // 통화 대사를 끝까지 읽는 동안 카메라는 영상이 멈춘 자리 그대로
        Vector3 holdPos = cam.transform.position;
        Quaternion holdRot = cam.transform.rotation;
        if (brain != null) brain.enabled = false;
        TimelineManager timelineManager = FindFirstObjectByType<TimelineManager>(FindObjectsInactive.Include);
        while (timelineManager != null && timelineManager.IsImmediateDialoguePlaying)
        {
            cam.transform.SetPositionAndRotation(holdPos, holdRot);
            yield return null;
        }

        Debug.Log($"[FlashbackFreeRoamSegment] '{name}' 통화 뒤 탐색을 시작합니다.");
        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        // 영상 카메라 자리에서 암전 → 전화를 받던 자리에서 다시 걷기
        StartFade(1f, 0.5f, 0);
        yield return new WaitForSeconds(0.5f);

        if (overrideGrading) SetGradingWeight(1f);

        // 전화를 받던 그 자리, 그 방향에서 다시 걷는다. (영상 카메라가 어디에 있든 상관없이)
        if (hasCallPosition)
            cam.transform.SetPositionAndRotation(callFeetPosition + Vector3.up * 1.65f, Quaternion.Euler(0f, callYaw, 0f));
        player.gameObject.SetActive(true);
        player.EyeHeightOverride = eyeHeight;
        player.BeginControl(cam);
        player.AllowSprint = allowSprint;

        FreeRoamFlashlight flashlight = player.GetComponent<FreeRoamFlashlight>();
        if (useFlashlight && flashlight == null)
            flashlight = player.gameObject.AddComponent<FreeRoamFlashlight>();
        if (flashlight != null)
            flashlight.SetAvailable(useFlashlight);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        StartFade(0f, 0.6f, 0);

        if (afterCallResetDoors != null) foreach (var d in afterCallResetDoors) if (d != null) d.ResetState();
        if (afterCallInteract != null) { afterCallInteract.ResetState(); afterCallInteract.enabled = true; }
        if (afterCallScare != null) afterCallScare.ResetState();

        // ── 역 밖으로 → 암전 → 바깥(공중화장실 앞)에서 다시 걷기 ──
        if (afterCallExitSpot != null && afterCallOutsideStart != null && !completeRequested)
        {
            SetStep(afterCallExitHint);
            SetWaypoints(() => One(afterCallExitSpot));
            if (afterCallExitMarker != null) afterCallExitMarker.SetActive(true);
            yield return new WaitUntil(() => completeRequested ||
                Vector3.Distance(player.transform.position, afterCallExitSpot.position) <= afterCallExitRadius);
            if (afterCallExitMarker != null) afterCallExitMarker.SetActive(false);

            if (!completeRequested)
            {
                player.InputPaused = true;
                StartFade(1f, 0.6f, 0);
                yield return new WaitForSeconds(0.7f);
                player.Teleport(afterCallOutsideStart.position, afterCallOutsideStart.eulerAngles.y);
                player.InputPaused = false;
                StartFade(0f, 0.8f, 0);
            }
        }

        // ── 자료 찾기 ──
        if (afterCallInteract != null)
        {
            SetStep(afterCallHint);
            SetWaypoints(() => afterCallInteract.Used ? null : One(afterCallInteract.transform));
            yield return new WaitUntil(() => afterCallInteract.Used || completeRequested);
        }

        // ── 돌아가기 (가는 길에 노크) ──
        if (!completeRequested)
        {
            if (afterCallScare != null) afterCallScare.Arm();
            SetStep(afterCallReturnHint);
            SetWaypoints(() => One(afterCallReturnSpot));
            yield return new WaitUntil(() => completeRequested ||
                ((afterCallScare == null || afterCallScare.Done) &&
                 (afterCallReturnSpot == null || FlatDistance(player.transform.position, afterCallReturnSpot.position) <= afterCallReturnRadius)));
        }

        // ── 마무리: 암전 → 조작 반납 ──
        if (afterCallInteract != null) afterCallInteract.enabled = false;
        player.AllowSprint = false;
        player.EndControl();
        if (flashlight != null) flashlight.SetAvailable(false);
        HideHint();
        StartFade(1f, fadeOutDuration, 0);
        yield return new WaitForSeconds(fadeOutDuration + 0.1f);

        ObjectiveHUD.Instance.HideAll();
        SetGradingWeight(0f);
        player.gameObject.SetActive(false);
        if (brain != null) brain.enabled = true;
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;
        if (CamMove.Instance != null) CamMove.Instance.SyncRotation();

        Debug.Log($"[FlashbackFreeRoamSegment] '{name}' 통화 뒤 탐색을 종료합니다.");
        EndWaypoints();
        IsRunning = false;
        completeRequested = false;
        StartFade(0f, fadeInDuration, 1);
    }

    // 증거마다: 다가가서 바라보면 [E] 살펴보기 → 문서가 펼쳐지고 [F]로 조작.
    // 모두 조작해야 다음 단계로 넘어간다.
    private IEnumerator EvidenceStepRoutine()
    {
        EvidenceViewer viewer = evidenceViewer;
        if (viewer == null)
        {
            viewer = gameObject.GetComponent<EvidenceViewer>();
            if (viewer == null) viewer = gameObject.AddComponent<EvidenceViewer>();
            evidenceViewer = viewer;
        }

        int total = 0;
        foreach (var e in evidenceItems)
            if (e != null) { e.Arm(); total++; }

        int shownCount = -1;
        bool focusShown = false;

        while (!completeRequested)
        {
            int done = 0;
            foreach (var e in evidenceItems)
                if (e != null && e.IsTampered) done++;

            if (done != shownCount)
            {
                shownCount = done;
                SetStep(evidenceHint.Replace("{0}", done.ToString()).Replace("{1}", total.ToString()));
                Debug.Log($"[FlashbackFreeRoamSegment] 증거 {done}/{total}");
            }

            if (done >= total) break;

            // 지금 바라보고 있는, 아직 조작하지 않은 가장 가까운 증거
            EvidenceItem focus = null;
            float best = float.MaxValue;
            foreach (var e in evidenceItems)
            {
                if (e == null || e.IsTampered) continue;
                float score = e.GetFocusScore(player.Head);
                if (score >= 0f && score < best) { best = score; focus = e; }
            }

            if (focus != null && !focusShown && Time.time >= messageUntil)
            {
                ShowHint(evidenceFocusHint + "\n" + focus.Title);
                focusShown = true;
            }
            else if (focus == null && focusShown)
            {
                HideHint();
                focusShown = false;
            }

            if (focus != null && Input.GetKeyDown(KeyCode.E))
            {
                HideHint();
                player.InputPaused = true;
                yield return viewer.Inspect(focus);
                player.InputPaused = false;
                focusShown = false;
                HideHint();
            }

            yield return null;
        }

        if (!completeRequested)
            ShowMessage(evidenceDoneHint);
    }

    // ── 탐색 중 화면 밝기 ──
    private Volume gradingVolume;

    private void SetGradingWeight(float weight)
    {
        if (gradingVolume == null)
        {
            if (weight <= 0f) return;

            var go = new GameObject("ExploreGradingVolume");
            go.transform.SetParent(transform, false);
            gradingVolume = go.AddComponent<Volume>();
            gradingVolume.isGlobal = true;
            gradingVolume.priority = 50f; // 기본 보정(SampleSceneProfile)보다 위

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.overrideState = true;
            ca.postExposure.value = explorePostExposure;
            ca.contrast.overrideState = true;
            ca.contrast.value = exploreContrast;
            gradingVolume.profile = profile;
        }

        gradingVolume.weight = weight;
        gradingVolume.enabled = weight > 0f;
    }

    // ── 목격자에게 들켰을 때: 암전 → 시작 자리로 → 모은 것(필수 단서)을 원래 자리로 ──
    private bool caughtPending;
    private WitnessPatrol caughtBy;
    private void HandleCaught(WitnessPatrol who)
    {
        if (caughtPending) return;
        caughtPending = true;
        caughtBy = who;
    }

    private IEnumerator CaughtWatcher()
    {
        while (true)
        {
            if (caughtPending)
            {
                // 행인: "뭐해요?" → 변명 (반반). 통하면 그냥 지나간다.
                bool excused = false;
                if (caughtBy != null)
                    yield return WitnessConfrontation.Get().Run(player, caughtBy, ok => excused = ok);
                caughtBy = null;

                if (excused)
                {
                    caughtPending = false;
                    yield return null;
                    continue;
                }

                caughtPending = false;
                player.InputPaused = true;
                ShowMessage(caughtHint);
                StartFade(1f, 0.5f, 0);
                yield return new WaitForSeconds(0.9f);

                player.ResetToStart();
                if (HasClues) foreach (var clue in clueItems) if (clue != null) clue.Arm(player.transform);
                if (witnesses != null) foreach (var w in witnesses) if (w != null) w.Arm();

                StartFade(0f, 0.6f, 0);
                player.InputPaused = false;
            }
            yield return null;
        }
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ── 안내 문구 ──
    // ── 목표 위치 표시 (화면 위 노란 마름모 + 거리) ──
    private System.Func<IEnumerable<Transform>> waypointSource;

    private void SetWaypoints(System.Func<IEnumerable<Transform>> source) => waypointSource = source;

    private void LateUpdate()
    {
        if (!IsRunning) return;
        if (waypointSource == null || player == null || !player.IsControlling || player.InputPaused)
            WaypointHUD.Hide();
        else
            WaypointHUD.Show(waypointSource());
    }

    private static IEnumerable<Transform> One(Transform t)
    {
        if (t != null) yield return t;
    }

    private static IEnumerable<Transform> NotPicked(FreeRoamPickupItem[] items)
    {
        if (items == null) yield break;
        foreach (var i in items) if (i != null && !i.IsPicked) yield return i.transform;
    }

    private IEnumerable<Transform> NotTampered()
    {
        if (evidenceItems == null) yield break;
        foreach (var e in evidenceItems) if (e != null && !e.IsTampered) yield return e.transform;
    }

    private void EndWaypoints()
    {
        waypointSource = null;
        WaypointHUD.Hide();
    }

    // 지금 단계의 목표. 왼쪽 위에 뜬다.
    private void SetStep(string hint)
    {
        currentStepHint = hint;
        ObjectiveHUD.Instance.SetObjective(hint);
    }

    // 주운 물건의 문구를 아래에 잠깐 보여주고 지운다.
    private void ShowMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || pickupMessageDuration <= 0f) return;
        messageUntil = Time.time + pickupMessageDuration;
        ShowHint(message);
        StartCoroutine(RestoreStepHintRoutine(messageUntil));
    }

    private IEnumerator RestoreStepHintRoutine(float until)
    {
        yield return new WaitForSeconds(pickupMessageDuration);
        // 그 사이에 새 문구가 떴으면 그쪽이 알아서 지운다.
        if (IsRunning && Mathf.Approximately(until, messageUntil))
            HideHint();
    }

    // 차 몸체 가운데를 바라보도록 카메라만 부드럽게 돌린다.
    private IEnumerator LookAtCarRoutine(Camera cam)
    {
        Transform car = carFinder != null ? carFinder.Car : null;
        if (cam == null || car == null || lookAtCarDuration <= 0f) yield break;

        Vector3 target = carFinder.CarBounds.center;

        Quaternion from = cam.transform.rotation;
        Vector3 dir = target - cam.transform.position;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        Quaternion to = Quaternion.LookRotation(dir, Vector3.up);

        float t = 0f;
        while (t < lookAtCarDuration)
        {
            t += Time.deltaTime;
            cam.transform.rotation = Quaternion.Slerp(from, to, Mathf.SmoothStep(0f, 1f, t / lookAtCarDuration));
            yield return null;
        }
    }

    private IEnumerator BoardCarRoutine()
    {
        AudioSource source = EnsureSfx();
        AudioClip door = carDoorSound != null ? carDoorSound : ProceduralCarAudio.Door();
        AudioClip engine = carEngineSound != null ? carEngineSound : ProceduralCarAudio.EngineStartAndDriveAway();

        Debug.Log("[FlashbackFreeRoamSegment] 차에 탑니다. (문 소리 + 암전 -> 시동 -> 출발)");

        source.PlayOneShot(door, carSoundVolume);
        StartFade(1f, fadeOutDuration, 0);

        if (engineDelayAfterDoor > 0f)
            yield return new WaitForSeconds(engineDelayAfterDoor);

        source.PlayOneShot(engine, carSoundVolume);

        // 시동 소리 → 엔진 소리가 이어지다가 멀어지며 사라진다.
        float waitFade = Mathf.Max(0f, fadeOutDuration - engineDelayAfterDoor);
        if (carDriveSound != null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, engine.length - 0.4f));
            var drive = gameObject.AddComponent<AudioSource>();
            drive.playOnAwake = false;
            drive.spatialBlend = 0f;
            drive.clip = carDriveSound;
            drive.volume = 0f;
            drive.Play();
            float dur = Mathf.Min(carDriveDuration, carDriveSound.length);
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                // 0.4초 동안 올라왔다가 끝으로 갈수록 작아진다.
                float up = Mathf.Clamp01(t / 0.4f);
                drive.volume = carSoundVolume * up * (1f - k * k);
                yield return null;
            }
            drive.Stop();
            Destroy(drive);
            yield return new WaitForSeconds(holdBlackAfterEngine);
        }
        else
        {
            // 화면이 다 어두워지고, 엔진 소리가 멀어져 사라질 때까지 기다린다.
            float waitEngine = engine.length;
            yield return new WaitForSeconds(Mathf.Max(waitEngine, waitFade) + holdBlackAfterEngine);
        }
    }

    private AudioSource EnsureSfx()
    {
        if (sfx != null) return sfx;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 0f;   // 차 안에 탄 사람 입장이라 방향 없이 들린다
        return sfx;
    }

    // ── 암전 ──
    // delayFrames: 몇 프레임 뒤에 시작할지 (타임라인이 먼저 넘어가도록 기다릴 때 사용)
    private void StartFade(float targetAlpha, float duration, int delayFrames)
    {
        Image img = EnsureFadeImage();
        if (img == null) return;
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(img, targetAlpha, duration, delayFrames));
    }

    private IEnumerator FadeRoutine(Image img, float targetAlpha, float duration, int delayFrames)
    {
        for (int i = 0; i < delayFrames; i++)
            yield return null;

        // 주의: Image.canvas는 캔버스가 꺼져 있으면 null을 돌려준다. 저장해 둔 오브젝트로 켠다.
        if (fadeCanvasObject != null) fadeCanvasObject.SetActive(true);
        Color c = img.color;
        float from = c.a;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, targetAlpha, Mathf.SmoothStep(0f, 1f, t / duration));
            img.color = c;
            yield return null;
        }

        c.a = targetAlpha;
        img.color = c;

        // 완전히 투명해지면 캔버스를 꺼서 아무것도 가리지 않게 한다.
        if (targetAlpha <= 0f && fadeCanvasObject != null)
            fadeCanvasObject.SetActive(false);

        fadeRoutine = null;
    }

    private Image EnsureFadeImage()
    {
        if (fadeImage != null)
        {
            if (fadeCanvasObject == null)
            {
                var existing = fadeImage.GetComponentInParent<Canvas>(true);
                fadeCanvasObject = existing != null ? existing.gameObject : fadeImage.gameObject;
            }
            return fadeImage;
        }

        var canvasGO = new GameObject("FreeRoamFade", typeof(Canvas));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;   // 대사/레터박스보다 위

        var imgGO = new GameObject("Black", typeof(RectTransform), typeof(Image));
        imgGO.transform.SetParent(canvasGO.transform, false);
        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        fadeImage = imgGO.GetComponent<Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeImage.raycastTarget = false;
        fadeCanvasObject = canvasGO;
        canvasGO.SetActive(false);
        return fadeImage;
    }

    // Cinemachine 어셈블리를 직접 참조하지 않고 이름으로 찾는다. (CameraGlitchEffect와 같은 방식)
    private static Behaviour FindBrain(Camera cam)
    {
        if (cam == null) return null;
        foreach (var b in cam.GetComponents<Behaviour>())
            if (b != null && b.GetType().Name == "CinemachineBrain")
                return b;
        return null;
    }

    // 아래 가운데 문구는 ObjectiveHUD가 맡는다. (예전 hintText 필드는 쓰지 않음)
    private void ShowHint(string text)
    {
        ObjectiveHUD.Instance.ShowMessage(text, 0f);
    }

    private void HideHint()
    {
        ObjectiveHUD.Instance.HideMessage();
    }
}
