using UnityEngine;
using UnityEngine.UI;

public class GunObject : InteractableObject
{
    [Header("Shoot Settings")]
    [Tooltip("딜러(상대)를 조준할 때 사용하는 타겟 Transform")]
    [SerializeField] private Transform dealerTarget;

    [Tooltip("플레이어(자신)를 조준할 때 사용하는 타겟 Transform")]
    [SerializeField] private Transform playerTarget;

    [Tooltip("총이 조준 위치까지 이동하는 데 걸리는 시간(초). " +
             "이 구간에서는 총을 돌리지 않고 위치만 옮깁니다.")]
    [SerializeField] private float aimDuration = 0.5f;

    [Tooltip("조준 위치에 도착해 자리를 잡은 뒤, 그 자리에서 총구를 대상 쪽으로 돌리는 데 걸리는 시간(초). " +
             "0으로 두면 즉시 돌아갑니다. 값을 키울수록 천천히 겨눠집니다.")]
    [SerializeField] private float aimRotateDuration = 0.35f;

    [Tooltip("이동이 끝나고 회전을 시작하기 전에 그 자리에 멈춰 있는 시간(초). " +
             "'위치 고정 -> 회전' 두 동작을 눈에 띄게 분리해 줍니다. 0이면 곧바로 이어서 돌립니다.")]
    [SerializeField] private float aimSettleDelay = 0.05f;

    [Tooltip("조준이 완전히 끝난 뒤 실제로 방아쇠를 당기기까지 기다리는 시간(초). " +
             "0이면 겨누자마자 즉발로 나갑니다. 1~2초 정도 주면 '겨누고 잠깐 뜸 들이는' 긴장이 생깁니다.")]
    [SerializeField] private float delayBeforeShot = 1.2f;

    [Header("Aiming Hold")]
    [Tooltip("발사한 뒤 총을 내려놓기까지 조준 자세를 유지하는 시간(초). " +
             "공탄이거나 빗나간 경우 등 \"보통의 발사\"에 적용됩니다. " +
             "플레이어가 Who를 실탄으로 맞혀 죽인 경우에만 아래 Death Sequence Aim Hold를 대신 씁니다.")]
    [SerializeField] private float aimHoldDuration = 0.2f;

    [Tooltip("플레이어가 직접 발사했을 때(일반 케이스) 총 복귀 전 고정 대기 시간(초).")]
    [SerializeField] private float playerReturnDelayAfterShot = 2f;

    [Header("Ammo")]
    [Tooltip("실탄/공탄 결정과 발사 횟수 추적을 담당하는 AmmoManager 참조")]
    [SerializeField] private AmmoManager ammoManager;

    [Tooltip("총알이 모두 소진되었을 때 재장전 대사/타임라인 흐름을 지시할 TimelineManager")]
    [SerializeField] private TimelineManager timelineManager;


    [Tooltip("HP(피격 횟수) 및 턴 상태를 관리하는 GameStateManager 참조")]
    [SerializeField] private GameStateManager gameStateManager;

    [Tooltip("사운드 저장(실탄 발사, 공탄 발사")]
    [SerializeField] private AudioClip shootReal;
    [SerializeField] private AudioClip shootFake;

    [Tooltip("발사 사운드를 재생할 AudioSource")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("게임 시작(씬 로드) 시 자동으로 총알을 장전할지 여부")]
    [SerializeField] private bool loadShellsOnStart = true;

    [Header("Table Rotation")]
    public Vector3 tableLocalEuler = Vector3.zero;

    [Header("Held Position")]
    public Vector3 heldLocalPosition = new Vector3(0f, -0.25f, 0.6f);
    public Vector3 heldLocalEuler = new Vector3(0f, 0f, 0f);
    public float holdMoveSpeed = 8f;

    [Header("World Space Choice Objects")]
    [SerializeField] private ShootTargetOption dealerOption;
    [SerializeField] private ShootTargetOption youOption;

    [Header("Aim Offset")]
    public Vector3 aimRotationOffsetPlayer = Vector3.zero;
    public Vector3 aimRotationOffsetWho = Vector3.zero;
    public Transform aimPosPlayer;
    public Transform aimPosWho;

    [Header("Return Animation")]
    public float returnDuration = 0.4f;

    [Header("Death Sequence")]
    [Tooltip("사망 연출이 시작됐을 때 총을 든(조준한) 자세를 유지할 시간(초). " +
             "이 시간이 지나면 총을 내려놓고, 다 내려놓아야 노이즈 연출이 시작됩니다.")]
    [SerializeField] private float deathSequenceAimHold = 2f;

    [Tooltip("플레이어가 자기 자신을 쏴서 죽었을 때, hitted 타임라인이 끝날 때까지 총을 든 채로 기다릴지 여부. " +
             "끄면 타임라인과 동시에 총이 내려갑니다.")]
    [SerializeField] private bool waitForHitTimelineBeforeReturn = false;

    [Tooltip("hitted 타임라인이 끝나고 총을 내려놓기까지의 여유 시간(초)")]
    [SerializeField] private float returnDelayAfterHitTimeline = 0.2f;

    [Tooltip("hitted 타임라인이 끝나기를 기다리는 최대 시간(초). " +
             "연출이 멈춰도 총이 영영 안 내려가는 것을 막는 안전장치입니다.")]
    [SerializeField] private float hitTimelineWaitTimeout = 15f;

    [Header("Status Text")]
    [SerializeField] private Text statusText;
    [SerializeField] private string textOnTable = "SHOOT";
    [SerializeField] private string textCancel = "CANCEL";

    // Gun state
    public enum GunState { OnTable, Selecting, Aiming }
    private GunState state = GunState.OnTable;

    // Original parent/position/rotation
    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    // Movement state
    private bool movingToHeld = false;
    private bool returningToTable = false;
    private bool aimInProgress = false;
    private Vector3 returnStartWorldPos;
    private Quaternion returnStartWorldRot;
    private float returnElapsed = 0f;

    // 턴이 넘어갈 때 외부(턴 매니저 등)에서 구독할 수 있는 이벤트
    // bool: 실탄 여부, Transform: 맞은 대상
    public event System.Action<bool, Transform> OnShotResolved;
    // 턴이 실제로 다음 사람에게 넘어갈 때 호출 (공탄으로 자신을 쐈을 땐 호출되지 않음)
    public event System.Action OnTurnPassed;

    // 복귀 애니메이션 코루틴 핸들 (중복 실행 방지용)
    private Coroutine returnCoroutine;

    // Who의 발사 연출 후 총을 되돌리는 코루틴 핸들
    private Coroutine whoReturnCoroutine;

    // 이 오브젝트의 Animator. [TL]WhoShoot / [TL]WhoShootHim의 Animation Track이 여기에 바인딩되어 있어서
    // 타임라인이 총을 직접 움직입니다. 총을 코드로 되돌리는 동안에는 반드시 꺼야 합니다.
    private Animator selfAnimator;

    // 이번 총알 소진에 대해 이미 재장전 대사 요청했는지 여부 (중복 호출 방지)
    private bool reloadDialogueRequestedForThisDepletion = false;

    // hitted 타임라인 / 즉시 대사가 아직 진행 중이라 재장전 트리거를 잠시 대기해야 하는지 여부
    private bool suppressReloadTriggerUntilHitSequenceDone = false;

    // muzzle light 관련 변수
    [Tooltip("총구 끝에 배치할 Light 컴포넌트 (Point/Spot Light). 평소엔 intensity 0으로 둔 상태로 연결")]
    [SerializeField] private Light muzzleLight;
    [Tooltip("라이트가 켜졌을 때의 최대 intensity")]
    [SerializeField] private float muzzleLightIntensity = 8f;
    [Tooltip("라이트가 최대 밝기까지 도달하는 시간(초)")]
    [SerializeField] private float muzzleLightRiseTime = 0.02f;
    [Tooltip("라이트가 최대 밝기에서 꺼질 때까지 걸리는 시간(초)")]
    [SerializeField] private float muzzleLightFallTime = 0.15f;

    [Tooltip("Who의 발사 후 총을 되돌릴 때, 총구 섬광이 완전히 꺼지고 나서 추가로 기다리는 시간(초). " +
             "섬광이 터지는 도중에 총이 움직이면 빛이 같이 끌려다녀서 어색합니다.")]
    [SerializeField] private float returnDelayAfterMuzzleFlash = 0.15f;

    // 총이 테이블 위(OnTable)가 아닌 상태(Selecting/Aiming)인지 여부.
    // 아이템 시스템(ItemInteract)이 "총을 든 동안 아이템 사용 불가"를 체크하는 데 사용합니다.
    public static bool IsGunHeld { get; private set; } = false;

    // 사망 연출 직후 총을 든 자세를 유지하다가 테이블에 내려놓는 중인지 여부.
    // GameStateManager는 이 값이 false가 될 때까지 기다렸다가 노이즈 연출을 시작합니다.
    public static bool IsSettlingAfterDeathShot { get; private set; } = false;

    // 총구 섬광이 재생되는 동안 true.
    // Who의 발사 후 총을 되돌릴 때, 섬광이 다 꺼진 뒤에 움직이도록 기다리는 데 씁니다.
    public bool IsMuzzleFlashing { get; private set; } = false;

    // 마지막으로 재생한 발사음의 길이(초). 클립이 없으면 0.
    // WhoAiManager가 "발사음이 다 난 뒤에 총을 내려놓기" 위해 참고합니다.
    public float LastShotSoundLength { get; private set; } = 0f;

    protected override void Start()
    {
        base.Start();
        // static 신호는 씬을 다시 로드해도 남으므로 시작할 때 반드시 초기화한다.
        IsGunHeld = false;
        IsSettlingAfterDeathShot = false;

        selfAnimator = GetComponent<Animator>();

        originalParent = transform.parent;
        originalLocalPos = transform.localPosition;

        tableLocalEuler = transform.localEulerAngles;
        originalLocalRot = Quaternion.Euler(tableLocalEuler);

        SetChoiceVisible(false);
        SetStatusTextVisible(false);

        // 평소엔 꺼진 상태로 시작 (GameObject가 꺼져 있으면 intensity를 바꿔도 빛이 안 나오므로 함께 활성화)
        if (muzzleLight != null)
        {
            muzzleLight.gameObject.SetActive(true);
            muzzleLight.intensity = 0f;
            muzzleLight.enabled = true;
        }

        if (ammoManager != null)
            ammoManager.OnReloaded += () => reloadDialogueRequestedForThisDepletion = false;

        if (loadShellsOnStart && ammoManager != null)
            ammoManager.LoadShells();
    }

    protected override void OnUpdate()
    {
        originalLocalRot = Quaternion.Euler(tableLocalEuler);

        if (movingToHeld)
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 targetWorldPos = cam.transform.TransformPoint(heldLocalPosition);
                Quaternion targetWorldRot = cam.transform.rotation * Quaternion.Euler(heldLocalEuler);

                transform.position = Vector3.Lerp(
                    transform.position, targetWorldPos, Time.deltaTime * holdMoveSpeed);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, targetWorldRot, Time.deltaTime * holdMoveSpeed);
            }
        }
    }

    public override void Interact()
    {
        if (state == GunState.Aiming || aimInProgress || returningToTable) return;

        // 턴제: 플레이어의 턴이 아니면(= Who의 턴이거나 연출 진행 중이면) 총을 집을 수 없다.
        if (TurnManager.Instance != null && !TurnManager.Instance.IsPlayerTurn)
        {
            Debug.Log($"[GunObject] 지금은 플레이어의 턴이 아닙니다. (현재 턴: {TurnManager.Instance.Current}, " +
                      $"잠금: {TurnManager.Instance.IsLocked})");
            return;
        }

        // 사망 연출 + 라운드 리셋이 진행 중이면 조작 불가
        if (gameStateManager != null && gameStateManager.IsDeathSequenceRunning) return;

        // 죽어서 테이블에 놓인 아이템을 아직 안 썼다면 총을 잡을 수 없다.
        // 아이템(회상)을 먼저 보게 만들어서 스토리 순서를 강제한다.
        if (ItemFovInteract.HasPendingItem)
        {
            Debug.Log("[GunObject] 아직 사용하지 않은 아이템이 테이블에 있습니다. 먼저 아이템을 사용하세요.");
            return;
        }

        // 총알이 다 떨어졌으면 상호작용 막고 알림만 (필요하면 여기서 다음 라운드 장전 트리거)
        if (ammoManager != null && ammoManager.RemainingBullets <= 0)
        {
            Debug.LogWarning("[GunObject] 총알이 없습니다. 새 라운드를 시작하세요 (ammoManager.LoadShells 호출 필요).");
            return;
        }

        if (state == GunState.OnTable)
            EnterSelectingState();
        else if (state == GunState.Selecting)
            CancelSelecting();
    }

    private void SetStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
            statusText.enabled = true;
        }
    }

    private void SetStatusTextVisible(bool visible)
    {
        if (statusText != null)
            statusText.enabled = visible;
    }

    public override void ShowInteractionUI()
    {
        base.ShowInteractionUI();
        if (state == GunState.OnTable)
            SetStatusText(textOnTable);
        else if (state == GunState.Selecting)
            SetStatusText(textCancel);
    }

    public override void HideInteractionUI()
    {
        base.HideInteractionUI();
        if (state != GunState.OnTable || !returningToTable)
            SetStatusTextVisible(false);
    }

    private void EnterSelectingState()
    {
        state = GunState.Selecting;
        IsGunHeld = true; // ← 추가

        suppressBasePositionLerp = true;
        returningToTable = false;
        movingToHeld = true;

        if (dealerOption == null || youOption == null)
        {
            Debug.LogWarning("[GunObject] dealerOption or youOption is not assigned!");
            return;
        }
        dealerOption.onSelected = OnTargetSelected;
        youOption.onSelected = OnTargetSelected;
        SetChoiceVisible(true);

        Debug.Log("[GunObject] Entered selecting state.");
    }

    private void CancelSelecting()
    {
        state = GunState.OnTable;
        IsGunHeld = false; // <- 추가: 취소하면 즉시 아이템 다시 사용 가능

        movingToHeld = false;

        SetChoiceVisible(false);
        StartReturnAnimation();

        Debug.Log("[GunObject] Cancelled selecting state.");
    }

    private void StartReturnAnimation()
    {
        movingToHeld = false;
        returnStartWorldPos = transform.position;
        returnStartWorldRot = transform.rotation;
        returnElapsed = 0f;
        returningToTable = true;
        suppressBasePositionLerp = true;
        CamMove.blockInteraction = true;

        // 이전에 실행 중이던 복귀 코루틴이 있다면 반드시 정지시키고 새로 시작 (중복 실행 방지)
        if (returnCoroutine != null)
            StopCoroutine(returnCoroutine);

        returnCoroutine = StartCoroutine(ReturnRoutine());
    }

    private System.Collections.IEnumerator ReturnRoutine()
    {
        Vector3 destWorldPos = originalParent != null ? originalParent.TransformPoint(originalLocalPos) : originalLocalPos;
        Quaternion destWorldRot = originalParent != null ? originalParent.rotation * originalLocalRot : originalLocalRot;

        float elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
            transform.position = Vector3.Lerp(returnStartWorldPos, destWorldPos, t);
            transform.rotation = Quaternion.Slerp(returnStartWorldRot, destWorldRot, t);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        transform.localRotation = originalLocalRot;
        suppressBasePositionLerp = false;
        targetPosition = originalLocalPos;
        returningToTable = false;
        returnCoroutine = null;

        if (ammoManager != null && ammoManager.RemainingBullets <= 0)
        {
            // hitted 타임라인/즉시 대사가 아직 끝나지 않았다면, 그것들이 모두 끝날 때까지
            // 재장전 트리거(대사/Bullet 타임라인)를 보류하여 두 타임라인이 겹치지 않게 한다.
            while (suppressReloadTriggerUntilHitSequenceDone)
                yield return null;

            SetStatusTextVisible(false);

            // 사망 시퀀스가 돌고 있다면 라운드 리셋(재장전 포함)을 그쪽이 담당하므로 여기서는 아무것도 하지 않는다.
            if (gameStateManager != null && gameStateManager.IsDeathSequenceRunning)
            {
                Debug.Log("[GunObject] 사망 연출이 진행 중이라 재장전 트리거를 건너뜁니다.");
                IsGunHeld = false;
                yield break;
            }

            // 총알을 다 쏴서 한 라운드가 끝난 시점 -> 여기서만 라운드를 진행시킨다.
            if (reloadDialogueRequestedForThisDepletion)
            {
                Debug.Log("[GunObject] 총알 소진에 대한 재장전 요청이 이미 진행 중이라 중복 실행을 막습니다.");
                yield break;
            }

            reloadDialogueRequestedForThisDepletion = true;

            // 라운드를 진행시키고, 이번 라운드/HP 상태에 맞는 "단 하나의" 대사 인덱스를 결정한다.
            // 조건에 맞는 특정 대사가 없으면 null -> 기본 재장전 대사(1)만 재생됨.
            int? dialogueGroupIndex = null;
            if (gameStateManager != null)
            {
                gameStateManager.AdvanceRound();
                dialogueGroupIndex = gameStateManager.ConsumeDialogueGroupIndexForCurrentState();
            }

            if (timelineManager != null)
            {
                Debug.Log($"[GunObject] 총알이 모두 소진됨. 카메라 고정 상태에서 대사(groupIndex={dialogueGroupIndex?.ToString() ?? "기본(1)"})를 재생합니다.");

                if (dialogueGroupIndex.HasValue)
                    timelineManager.TriggerReloadDialogue(dialogueGroupIndex.Value);
                else
                    timelineManager.TriggerReloadDialogue();
            }
            else
            {
                Debug.LogWarning("[GunObject] timelineManager가 할당되어 있지 않아 재장전 대사를 재생할 수 없습니다!");
            }
            IsGunHeld = false; // <- 추가
            yield break;
        }

        // 사망 연출이 진행 중이면 카메라를 계속 고정해 둬야 한다.
        // (여기서 풀어버리면 노이즈가 덮이는 동안 화면이 움직인다)
        if (gameStateManager != null && gameStateManager.IsDeathSequenceRunning)
        {
            SetStatusTextVisible(false);
            IsGunHeld = false;
            yield break;
        }

        // 총알이 남아있는 정상 상황이라도, 이미 턴이 Who에게 넘어갔다면 카메라를 풀면 안 된다.
        // (복귀 애니메이션이 0.4초 걸리는 동안 ResolveShot이 먼저 실행돼 턴이 넘어가 있다)
        TurnManager.ApplyCameraBlockForCurrentTurn();
        SetStatusTextVisible(false);
    }

    // 조준 연출 전체에 걸리는 시간 (이동 -> 자리 잡기 -> 회전).
    // 카메라가 조준 방향으로 도는 시간을 여기에 맞춰야, 총이 다 겨눠지는 순간 화면도 같이 정렬된다.
    private float TotalAimDuration =>
        aimDuration + Mathf.Max(0f, aimSettleDelay) + Mathf.Max(0f, aimRotateDuration);

    private void OnTargetSelected(Transform target)
    {
        if (aimInProgress || returningToTable) return;

        SetChoiceVisible(false);
        movingToHeld = false;
        state = GunState.Aiming;
        aimInProgress = true;

        Transform pose = (target == dealerTarget) ? aimPosWho : aimPosPlayer;
        CamMove camMove = CamMove.Instance;
        if (camMove != null && pose != null)
        {
            // 카메라 고정 시간에는 "겨눈 채 뜸 들이는 시간"을 반드시 포함시켜야 한다.
            // 빠뜨리면 총이 발사되기도 전에 카메라 조작이 풀려 화면이 돌아가 버린다.
            //
            // 그리고 이 고정이 풀리는 시점은 총이 복귀를 끝내는 시점(ReturnRoutine 끝의
            // ApplyCameraBlockForCurrentTurn)보다 반드시 앞서야 한다. 순서가 뒤집히면
            // Who의 턴인데도 CamMove가 맨 마지막에 blockLook을 풀어버린다.
            float holdAfterShot = Mathf.Max(Mathf.Max(0f, aimHoldDuration), Mathf.Max(0f, playerReturnDelayAfterShot));
            float camHold = Mathf.Max(0f, delayBeforeShot) + holdAfterShot;
            camMove.LookAtRotationAndHold(pose.rotation, TotalAimDuration, camHold);
        }
        else
            CamMove.blockLook = true;

        StartCoroutine(AimAt(target));
    }

    private void SetChoiceVisible(bool visible)
    {
        if (dealerOption != null) dealerOption.gameObject.SetActive(visible);
        if (youOption != null) youOption.gameObject.SetActive(visible);
    }

    private System.Collections.IEnumerator AimAt(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[GunObject] AimAt target is null");
            StartReturnAnimation();
            state = GunState.OnTable;
            aimInProgress = false;
            yield break;
        }

        movingToHeld = false;

        Transform pose;
        if (target == dealerTarget) pose = aimPosWho;
        else if (target == playerTarget) pose = aimPosPlayer;
        else
        {
            Debug.LogWarning("[GunObject] AimAt target is not recognized");
            pose = null;
        }

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 targetPos;
        Quaternion targetRot;

        if (pose != null)
        {
            targetPos = pose.position;
            targetRot = pose.rotation * Quaternion.Euler(target == dealerTarget ? aimRotationOffsetWho : aimRotationOffsetPlayer);
        }
        else
        {
            targetPos = transform.position;
            Vector3 dir = (target.position - transform.position).normalized;
            targetRot = dir != Vector3.zero ? Quaternion.LookRotation(dir) * Quaternion.Euler(target == dealerTarget ? aimRotationOffsetWho : aimRotationOffsetPlayer) : startRot;
        }

        // ── 1단계: 회전은 그대로 둔 채, 조준 위치까지 "이동만" 한다 ──
        // 위치와 회전을 동시에 보간하면 총이 허공에서 휙 도는 것처럼 보인다.
        // 그래서 이동 중에는 매 프레임 startRot을 다시 써서 총의 각도를 완전히 고정한다.
        float elapsed = 0f;
        while (elapsed < aimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / aimDuration));
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = startRot;
            yield return null;
        }

        // 위치를 목표값에 정확히 맞춰 고정한다. 이 시점 이후로 총의 위치는 더 이상 변하지 않는다.
        transform.position = targetPos;
        transform.rotation = startRot;

        // 자리를 잡았다는 게 눈에 보이도록 회전 직전에 아주 짧게 멈춘다.
        if (aimSettleDelay > 0f)
            yield return new WaitForSeconds(aimSettleDelay);

        // ── 2단계: 위치를 고정한 채, 그 자리에서 총구만 대상 쪽으로 돌린다 ──
        if (aimRotateDuration > 0f)
        {
            elapsed = 0f;
            while (elapsed < aimRotateDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / aimRotateDuration));

                // 회전하는 동안에도 위치를 매 프레임 다시 고정한다.
                // (호버 애니메이션이나 타임라인이 끼어들어도 총이 흔들리지 않게 하는 안전장치)
                transform.position = targetPos;
                transform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
        }

        transform.position = targetPos;
        transform.rotation = targetRot;

        // 겨눈 채로 한 박자 뜸을 들인다. 즉발로 나가면 긴장감이 없다.
        // 이 동안에도 총은 위치와 회전을 그대로 유지한다.
        if (delayBeforeShot > 0f)
        {
            float waited = 0f;
            while (waited < delayBeforeShot)
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // ===== 발사 & 판정 처리 =====
        bool shotSelfWithLive;
        bool shotSelf;
        bool wasLive;
        bool passTurn = Shoot(target, out shotSelfWithLive, out shotSelf, out wasLive);

        // 플레이어 HP는 1이라 자신에게 실탄을 쏘면 곧바로 사망 시퀀스가 시작된다.
        // 그때는 GameStateManager가 finalHitted 연출과 사망 대사, 라운드 리셋까지 전부 담당하므로
        // 아래의 hitted 연출 분기는 건너뛴다.
        bool deathSequenceTookOver = gameStateManager != null && gameStateManager.IsDeathSequenceRunning;

        if (deathSequenceTookOver)
        {
            // 같은 사망이라도 누가 죽었느냐에 따라 총을 내리는 타이밍이 다르다.
            //  - Who를 쏴서 죽인 경우: 겨눈 자세로 잠깐 버틴다. 이 동안 노이즈 사운드가 작게 깔리기
            //    시작하고, 총을 다 내려놓아야 노이즈 화면이 올라온다.
            //  - 자기 자신을 쏴서 죽은 경우: 버티지 않는다. RegisterHit 시점에 hitted 타임라인이
            //    이미 돌기 시작했으므로, 총을 곧바로 내려놓아야 연출이 총에 가리지 않는다.
            bool killedWho = !shotSelf;

            if (killedWho)
            {
                IsSettlingAfterDeathShot = true;

                if (deathSequenceAimHold > 0f)
                    yield return new WaitForSeconds(deathSequenceAimHold);
            }
            else if (waitForHitTimelineBeforeReturn)
            {
                // 자기 자신을 쏴서 죽은 경우: hitted 타임라인이 최우선이다.
                // 이 타임라인은 카메라를 PlayerCamera_Hitted 앵글로 바꿔버리기 때문에,
                // 연출 도중에 총을 내려놓으면 바뀐 화면에서 총만 혼자 미끄러져 내려간다.
                yield return StartCoroutine(WaitForHitTimelineRoutine());
            }

            // 총을 테이블에 내려놓는다.
            StartReturnAnimation();
            movingToHeld = false;
            aimInProgress = false;
            state = GunState.OnTable;

            // 내려놓는 애니메이션이 끝날 때까지 기다린 뒤에 "정리 완료"로 표시한다.
            // GameStateManager가 이 신호를 받고 노이즈를 시작한다.
            yield return new WaitForSeconds(returnDuration);

            IsGunHeld = false;
            IsSettlingAfterDeathShot = false;
            yield break;
        }

        if (shotSelfWithLive)
        {
            // 자신에게 실탄을 쐈을 때의 특수 처리: 즉시상태 전환 및 히트 리액션 시작
            // (뒤에서 대사 및 재장전 처리가 자연스럽게 이어져야 하므로, 다른 분기와 다르게 코루틴이 아님)
            suppressReloadTriggerUntilHitSequenceDone = true;
            StartReturnAnimation();

            // 피격 연출(화면 왜곡 + 환각)은 GameStateManager의 사망 처리가 맡는다.

            // 실탄에 맞아 HP가 1 남은 경우 등, "즉시 재생"이 필요한 대사가 있으면
            // (hitted 타임라인 이후에) 재생한다.
            if (gameStateManager != null)
            {
                int? immediateIndex = gameStateManager.ConsumeImmediateDialogueGroupIndex();
                if (immediateIndex.HasValue)
                {
                    if (timelineManager != null)
                    {
                        bool dialogueDone = false;
                        Debug.Log($"[GunObject] HP 위험 대사(groupIndex={immediateIndex.Value})를 재생합니다.");
                        timelineManager.PlayImmediateDialogue(immediateIndex.Value, () => dialogueDone = true);
                        yield return new WaitUntil(() => dialogueDone);
                        Debug.Log("[GunObject] 즉시 대사 재생 완료. 턴을 계속 이어갑니다.");
                    }
                    else
                    {
                        Debug.LogWarning("[GunObject] timelineManager가 할당되어 있지 않아 즉시 대사를 재생할 수 없습니다!");
                    }
                }
            }

            // hitted 타임라인 + 즉시 대사가 모두 끝난 지금부터는 재장전 트리거를 진행해도 안전하다.
            suppressReloadTriggerUntilHitSequenceDone = false;

            aimInProgress = false;
            state = GunState.OnTable;
            IsGunHeld = false; // <- 추가
        }
        else
        {
            // 그 외(공탄 등)의 경우: 발사 사운드 후 지정 시간(기본 2초) 대기 후 복귀한다.
            float holdDelay = Mathf.Max(Mathf.Max(0f, aimHoldDuration), Mathf.Max(0f, playerReturnDelayAfterShot));
            if (holdDelay > 0f)
                yield return new WaitForSeconds(holdDelay);

            StartReturnAnimation();
            movingToHeld = false;
            aimInProgress = false;
            state = GunState.OnTable;
        }

        // 턴제 처리: 벅샷 룰(자기 자신 + 공탄 = 턴 유지)에 따라 TurnManager가 다음 턴을 결정한다.
        // 총알이 모두 소진된 경우에는 재장전 흐름이 라운드를 새로 시작하므로 여기서 턴을 넘기지 않는다.
        if (TurnManager.Instance != null && ammoManager != null && ammoManager.RemainingBullets > 0)
            TurnManager.Instance.ResolveShot(TurnManager.Turn.Player, shotSelf, wasLive);

        // 턴이 실제로 넘어갈 때만 외부 이벤트 발생
        // (자기자신에게 공탄을 쐈을 때 passTurn == false 라 외부에는 알리지 않음)
        if (passTurn)
            OnTurnPassed?.Invoke();
    }

    // hitted(피격) 타임라인이 완전히 끝날 때까지 기다린다.
    // timelineManager가 없거나 연출이 멈춰버린 경우를 대비해 타임아웃을 둔다.
    private System.Collections.IEnumerator WaitForHitTimelineRoutine()
    {
        if (timelineManager == null)
        {
            Debug.LogWarning("[GunObject] timelineManager가 없어 피격 연출을 기다리지 못하고 바로 총을 내려놓습니다.");
            yield break;
        }

        // PlayableDirector.Play()는 호출됐지만 아직 재생 상태로 들어가기 전일 수 있다.
        // 한 프레임 양보하지 않으면 "재생 중이 아님"으로 오판해서 그냥 지나쳐 버린다.
        yield return null;

        float waited = 0f;
        while (timelineManager.IsHitTimelinePlaying && waited < hitTimelineWaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (waited >= hitTimelineWaitTimeout)
            Debug.LogWarning("[GunObject] hitted 타임라인이 끝나기를 기다리다 시간 초과. 그냥 총을 내려놓습니다.");
        else
            Debug.Log($"[GunObject] hitted 타임라인 종료({waited:0.00}초). 이제 총을 내려놓습니다.");

        if (returnDelayAfterHitTimeline > 0f)
            yield return new WaitForSeconds(returnDelayAfterHitTimeline);
    }

    // 발사 처리 로직. true이면 다음 사람이 "턴 넘김", false이면 "턴 유지(공탄 자기 자신)"
    // shotSelfWithLive: 자기 자신에게 실탄을 쐈는지 여부
    // shotSelf: 자기 자신을 겨눴는지 여부, wasLive: 실탄이었는지 여부 (턴 판정용)
    private bool Shoot(Transform target, out bool shotSelfWithLive, out bool shotSelf, out bool wasLive)
    {
        shotSelfWithLive = false;
        shotSelf = (target == playerTarget);
        wasLive = false;

        if (ammoManager == null)
        {
            Debug.LogWarning("[GunObject] ammoManager가 연결되어 있지 않습니다!");
            return true;
        }

        bool isLive = ammoManager.FireNextShell();
        wasLive = isLive;

        Debug.Log($"[GunObject] '{target.name}' 대상으로 발사 -> {(isLive ? "실탄" : "공탄")} (남은 총알: {ammoManager.RemainingBullets})");

        PlayShootSound(isLive);

        if (isLive)
        {
            if (muzzleLight != null)
                StartCoroutine(MuzzleLightFlashRoutine());
            else
                Debug.LogWarning("[GunObject] muzzleLight가 연결되어 있지 않습니다. 총구 섬광 Light(Point/Spot)를 연결해주세요.");

            // 실탄이 터지는 순간 시간을 살짝 늦췄다가 되돌린다.
            if (HitStopEffect.Instance != null)
                HitStopEffect.Instance.Play();
        }

        // 실탄 발사 시 HP(피격 횟수)를 등록.
        if (isLive && gameStateManager != null)
            gameStateManager.RegisterHit(shotSelf ? GameStateManager.Actor.Player : GameStateManager.Actor.Enemy);

        shotSelfWithLive = isLive && shotSelf;

        OnShotResolved?.Invoke(isLive, target);

        // 턴 규칙: 자기 자신에게 공탄을 쐈을 때만 턴을 유지한다.
        bool passTurn = !(shotSelf && !isLive);
        IsGunHeld = false;
        return passTurn;
    }

    /// <summary>
    /// Who의 발사 타임라인이 총을 옮겨 놓은 뒤, 총을 원래 테이블 위치로 부드럽게 되돌립니다.
    /// 일반 복귀(ReturnRoutine)와 달리 재장전 트리거나 카메라 잠금은 전혀 건드리지 않고,
    /// 순수하게 위치/회전만 되돌립니다.
    /// </summary>
    /// <summary>
    /// Who의 발사 연출이 시작되기 직전에 호출하세요.
    ///
    /// 베이스 클래스(InteractableObject)의 Update()는 suppressBasePositionLerp가 꺼져 있으면
    /// 매 프레임 localPosition을 targetPosition(테이블 자리)으로 Lerp합니다.
    /// 이 상태로 두면 타임라인이 Who 손으로 옮겨 놓은 총이 연출이 끝나자마자 혼자
    /// 테이블로 스르륵 끌려갑니다. 그래서 아무리 대기 시간을 줘도 "쏘기 전에 이미 돌아가 있는"
    /// 현상이 생깁니다. Who 차례 전체 동안 이 보간을 꺼둬야 합니다.
    /// </summary>
    public void BeginWhoShotSequence()
    {
        suppressBasePositionLerp = true;

        // 되돌리기 중에 껐던 Animator를 다시 켜야 타임라인이 총을 움직일 수 있다.
        if (selfAnimator != null)
            selfAnimator.enabled = true;

        Debug.Log("[GunObject] Who의 발사 연출 시작 - 베이스 위치 보간을 끕니다.");
    }

    /// <summary>
    /// Bullet/기타 타임라인이 총을 움직여야 할 때 Animator를 다시 켭니다.
    /// Who 복귀 루틴에서 꺼진 상태가 남아 있으면 타임라인이 소리만 나고 총은 안 움직일 수 있습니다.
    /// </summary>
    public void PrepareForTimelineAnimation()
    {
        if (selfAnimator != null && !selfAnimator.enabled)
        {
            selfAnimator.enabled = true;
            Debug.Log("[GunObject] 타임라인 재생을 위해 Animator를 다시 켭니다.");
        }
    }

    /// <param name="delay">되돌리기 시작 전 대기 시간(초). 공탄 사운드가 들릴 시간을 벌어줍니다.</param>
    /// <param name="waitForMuzzleFlash">
    /// 총구 섬광이 다 꺼질 때까지 기다릴지 여부.
    /// 플레이어가 맞아서 hitted 타임라인이 시작된 경우에는 false를 넘겨서
    /// 총 복귀가 연출을 기다리지 않고 동시에 진행되게 합니다.
    /// </param>
    public void ReturnToTableAfterWhoShot(float delay = 0f, bool waitForMuzzleFlash = true)
    {
        if (whoReturnCoroutine != null)
            StopCoroutine(whoReturnCoroutine);

        whoReturnCoroutine = StartCoroutine(WhoReturnRoutine(delay, waitForMuzzleFlash));
    }

    private System.Collections.IEnumerator WhoReturnRoutine(float delay, bool waitForMuzzleFlash)
    {
        // 이 코루틴은 발사 직후에 시작되므로, 지금 섬광이 켜져 있다면 실탄이었다는 뜻이다.
        // 대기 시간이 섬광보다 길어서 나중에는 이미 꺼져 있을 수 있으므로 여기서 미리 기억해 둔다.
        bool hadMuzzleFlash = waitForMuzzleFlash && IsMuzzleFlashing;

        // 대기 전에 먼저 타임라인 제어를 끊어야 "틱 소리 후 대기" 동안 총이 미리 내려가지 않는다.
        // 1) 타임라인이 바꿔 둔 부모를 원래대로 되돌려 리그/애니메이션 영향에서 분리
        // 2) Animator를 즉시 꺼서 대기 중 트랜스폼이 덮어써지지 않게 고정
        if (originalParent != null && transform.parent != originalParent)
            transform.SetParent(originalParent, true);

        if (selfAnimator != null && selfAnimator.enabled)
        {
            selfAnimator.enabled = false;
            Debug.Log("[GunObject] 대기 중 총 자세 고정을 위해 Animator를 먼저 끕니다.");
        }

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        // 실탄이면 "쏜다 -> 섬광이 터진다 -> 총을 내려놓는다" 순서가 지켜져야 한다.
        // 빛이 남아 있는 상태에서 총이 움직이면 섬광이 같이 끌려다녀서 어색하다.
        if (hadMuzzleFlash)
        {
            yield return new WaitUntil(() => !IsMuzzleFlashing);

            if (returnDelayAfterMuzzleFlash > 0f)
                yield return new WaitForSeconds(returnDelayAfterMuzzleFlash);

            Debug.Log("[GunObject] 총구 섬광이 끝났습니다. 이제 총을 내려놓습니다.");
        }

        // 베이스 위치 보간이 끼어들지 않도록 잠시 끈다.
        suppressBasePositionLerp = true;

        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 destWorldPos = originalParent != null
            ? originalParent.TransformPoint(originalLocalPos)
            : originalLocalPos;
        Quaternion destWorldRot = originalParent != null
            ? originalParent.rotation * originalLocalRot
            : originalLocalRot;

        Debug.Log($"[GunObject] Who의 발사 연출이 끝나 총을 원래 위치로 되돌립니다 ({returnDuration:0.00}초)");

        float elapsed = 0f;
        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / returnDuration));
            transform.position = Vector3.Lerp(startPos, destWorldPos, t);
            transform.rotation = Quaternion.Slerp(startRot, destWorldRot, t);
            yield return null;
        }

        transform.localPosition = originalLocalPos;
        transform.localRotation = originalLocalRot;
        targetPosition = originalLocalPos;
        suppressBasePositionLerp = false;

        whoReturnCoroutine = null;
    }

    /// <summary>
    /// Who(딜러)의 턴에 WhoAiManager가 호출하는 발사 처리.
    /// 총을 실제로 들어올리는 연출은 하지 않고, 탄약 소모 / 사운드 / 총구 섬광 / 데미지만 처리합니다.
    /// (Who가 총을 드는 연출이 필요하면 WhoAiManager의 shootPlayerTimeline / shootSelfTimeline에 타임라인을 연결하세요.)
    /// </summary>
    /// <param name="targetSelf">Who가 자기 자신에게 쏘는지 여부. false면 플레이어를 쏨</param>
    /// <returns>실탄이었으면 true</returns>
    public bool FireAsWho(bool targetSelf)
    {
        if (ammoManager == null)
        {
            Debug.LogWarning("[GunObject] ammoManager가 연결되어 있지 않아 Who의 발사를 처리할 수 없습니다!");
            return false;
        }

        if (ammoManager.RemainingBullets <= 0)
        {
            Debug.LogWarning("[GunObject] 남은 총알이 없어 Who가 발사할 수 없습니다.");
            return false;
        }

        bool isLive = ammoManager.FireNextShell();

        Debug.Log($"[GunObject] Who가 {(targetSelf ? "자기 자신" : "플레이어")}에게 발사 -> " +
                  $"{(isLive ? "실탄" : "공탄")} (남은 총알: {ammoManager.RemainingBullets})");

        PlayShootSound(isLive);

        if (isLive)
        {
            if (muzzleLight != null)
                StartCoroutine(MuzzleLightFlashRoutine());

            if (HitStopEffect.Instance != null)
                HitStopEffect.Instance.Play();
        }

        // Who가 자신을 쏘면 Enemy가, 플레이어를 쏘면 Player가 피해를 입는다.
        if (isLive && gameStateManager != null)
            gameStateManager.RegisterHit(targetSelf ? GameStateManager.Actor.Enemy : GameStateManager.Actor.Player);

        OnShotResolved?.Invoke(isLive, targetSelf ? dealerTarget : playerTarget);

        return isLive;
    }

    // 총구 Light의 intensity를 순간적으로 확 올렸다가 빠르게 0으로 떨어뜨림
    private System.Collections.IEnumerator MuzzleLightFlashRoutine()
    {
        IsMuzzleFlashing = true;

        float elapsed = 0f;
        while (elapsed < muzzleLightRiseTime)
        {
            elapsed += Time.deltaTime;
            muzzleLight.intensity = Mathf.Lerp(0f, muzzleLightIntensity, elapsed / muzzleLightRiseTime);
            yield return null;
        }
        muzzleLight.intensity = muzzleLightIntensity;

        elapsed = 0f;
        while (elapsed < muzzleLightFallTime)
        {
            elapsed += Time.deltaTime;
            muzzleLight.intensity = Mathf.Lerp(muzzleLightIntensity, 0f, elapsed / muzzleLightFallTime);
            yield return null;
        }
        muzzleLight.intensity = 0f;

        IsMuzzleFlashing = false;
    }

    /// <summary>연출용: 총은 움직이지 않고 총성만 낸다. (화면 밖에서 누군가 쏘는 장면 등)</summary>
    public void PlayShotSoundOnly(bool isLive) => PlayShootSound(isLive);

    // 사격/공포탄에 따라 다른 사운드를 재생한다.
    private void PlayShootSound(bool isLive)
    {
        AudioClip clip = isLive ? shootReal : shootFake;
        if (clip == null)
        {
            Debug.LogWarning($"[GunObject] {(isLive ? "shootReal" : "shootFake")} 클립이 할당되어 있지 않습니다!");
            LastShotSoundLength = 0f;
            return;
        }

        // 이 길이만큼 기다렸다가 총을 내려놓으면 "틱" 소리가 다 난 뒤에 움직인다.
        LastShotSoundLength = clip.length;

        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip);
        }
        else
        {
            // AudioSource가 따로 없으면 위치 기반 재생으로 대체
            AudioSource.PlayClipAtPoint(clip, transform.position);
        }
    }
}