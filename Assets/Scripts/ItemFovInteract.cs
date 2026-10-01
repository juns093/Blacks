using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

// 플레이어가 죽을 때마다 테이블에 놓이는 아이템에 붙는 스크립트.
// 클릭(상호작용)하면 카메라 렌즈(FOV)를 넓힙니다.
//
// InteractableObject를 상속하므로 CamMove의 레이캐스트 호버/클릭이 자동으로 동작합니다.
// (아이템 프리팹은 Interactable 레이어 + Collider를 이미 가지고 있습니다)
//
// DeathItemSpawner가 아이템을 놓을 때 이 컴포넌트를 자동으로 붙이고 값을 채워줍니다.
public class ItemFovInteract : InteractableObject
{
    [Header("렌즈 효과")]
    [Tooltip("한 번 사용할 때 넓어지는 FOV 양. 양수면 시야가 넓어집니다(줌 아웃).")]
    [SerializeField] private float fovIncrease = 8f;

    [Tooltip("FOV가 변하는 데 걸리는 시간(초)")]
    [SerializeField] private float fovChangeDuration = 0.6f;

    [Header("사용 연출")]
    [Tooltip("아이템을 사용할 때 재생할 1번째 타임라인")]
    [SerializeField] private PlayableDirector useTimeline1;
    [Tooltip("아이템을 사용할 때 재생할 2번째 타임라인")]
    [SerializeField] private PlayableDirector useTimeline2;
    [Tooltip("아이템을 사용할 때 재생할 3번째 타임라인")]
    [SerializeField] private PlayableDirector useTimeline3;
    [Tooltip("아이템을 사용할 때 재생할 4번째 타임라인")]
    [SerializeField] private PlayableDirector useTimeline4;

    [Tooltip("타임라인이 끝난 뒤 조작을 돌려주기까지의 여유 시간(초)")]
    [SerializeField] private float delayAfterTimeline = 0.2f;

    [Header("타임라인 Marker 대사 그룹 (TypeWriter)")]
    [Tooltip("First Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int firstMarkerDialogueGroupIndex = -1;
    [Tooltip("Second Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int secondMarkerDialogueGroupIndex = -1;
    [Tooltip("Third Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int thirdMarkerDialogueGroupIndex = -1;
    [Tooltip("Forth Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int forthMarkerDialogueGroupIndex = -1;

    [Tooltip("이 아이템의 사용 타임라인이 끝난 뒤 재생할 대사 그룹 (Item_Use_*). -1이면 재생 안 함. " +
             "DeathItemSpawner가 등장 순서에 맞춰 채워줍니다.")]
    [SerializeField] private int afterTimelineDialogueGroupIndex = -1;

    [Tooltip("아이템 사용 연출(타임라인)이 시작되기 전에 먼저 재생할 대사 그룹. -1이면 재생 안 함.")]
    [SerializeField] private int beforeUseDialogueGroupIndex = -1;

    [Tooltip("회상 대사까지 모두 끝난 뒤 장전(Bullet) 타임라인을 다시 보여줄지 여부.")]
    [SerializeField] private bool replayBulletTimelineAfterUse = true;

    [Tooltip("회상 타임라인 도중에 끼워 넣을 자유 이동(차 찾기) 구간. 4번째 아이템에만 DeathItemSpawner가 채워줍니다.")]
    [SerializeField] private FlashbackFreeRoamSegment freeRoamSegment;

    [Header("회상 필름 그레인")]
    [Tooltip("아이템 사용 타임라인 동안 필름 그레인을 적용할 Volume. 비워두면 씬에서 자동 탐색합니다.")]
    [SerializeField] private Volume filmGrainVolume;

    [Tooltip("타임라인 회상 연출의 필름 그레인 강도(0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float filmGrainTargetIntensity = 0.4f;

    [Tooltip("필름 그레인 페이드 인 시간(초)")]
    [SerializeField] private float filmGrainFadeInDuration = 0.25f;

    [Tooltip("필름 그레인 페이드 아웃 시간(초)")]
    [SerializeField] private float filmGrainFadeOutDuration = 0.25f;

    [Tooltip("필름 그레인 연출 시 Volume weight도 함께 올릴지 여부")]
    [SerializeField] private bool fadeVolumeWeight = true;

    [Tooltip("필름 그레인 연출 중 Volume weight 목표값")]
    [Range(0f, 1f)]
    [SerializeField] private float filmGrainVolumeWeight = 1f;

    [Header("사용 제한")]
    [Tooltip("한 번만 사용할 수 있게 할지 여부")]
    [SerializeField] private bool useOnce = true;

    [Tooltip("사용한 뒤 아이템을 화면에서 숨길지 여부. 꺼두면 테이블에 그대로 남습니다.")]
    [SerializeField] private bool hideAfterUse = false;

    [Tooltip("이 아이템을 쓸 때까지 총을 못 잡게 막을지 여부. " +
             "방금 죽어서 새로 나온 아이템에만 켭니다. " +
             "루프로 씬이 다시 시작되면서 복원된 아이템은 꺼둬야 게임을 계속할 수 있습니다.")]
    [SerializeField] private bool blocksGunUntilUsed = false;

    private bool used = false;

    // 기억 파편에서 호출된 경우. 잠금/장전 화면/렌즈는 사망 연출(GameStateManager)이 맡는다.
    private bool runAsMemoryFragment = false;

    // 아직 사용하지 않은 채 테이블에 놓여 있는 아이템들.
    // 아이템을 쓰기 전에는 총을 잡지 못하게 하려고 GunObject가 이 목록을 참고합니다.
    // 씬을 다시 로드해도 static은 남지만, 파괴된 아이템은 OnDestroy에서 스스로 빠집니다.
    private static readonly HashSet<ItemFovInteract> pendingItems = new HashSet<ItemFovInteract>();

    /// <summary>아직 사용하지 않은 아이템이 하나라도 테이블에 남아 있는지 여부.</summary>
    public static bool HasPendingItem
    {
        get
        {
            pendingItems.RemoveWhere(x => x == null);
            return pendingItems.Count > 0;
        }
    }

    /// <summary>씬을 새로 시작할 때 이전 판의 찌꺼기를 지웁니다. (DeathItemSpawner가 호출)</summary>
    public static void ClearPendingItems()
    {
        pendingItems.Clear();
    }

    /// <summary>
    /// 이 아이템이 "쓸 때까지 총을 막는" 아이템인지 설정합니다.
    /// DeathItemSpawner가 스폰 직후(=Start보다 먼저) 호출합니다.
    /// </summary>
    public void SetBlocksGunUntilUsed(bool blocks)
    {
        blocksGunUntilUsed = blocks;

        if (blocks && !used)
            pendingItems.Add(this);
        else
            pendingItems.Remove(this);
    }

    /// <summary>이 아이템을 이미 사용했는지 여부.</summary>
    public bool IsUsed => used;
    private readonly List<FilmGrain> cachedFilmGrains = new List<FilmGrain>();
    private Coroutine filmGrainRoutine;
    private PlayableDirector runtimeUseTimeline;
    private float originalVolumeWeight = 1f;
    private bool hasOriginalVolumeWeight = false;

    protected override void Start()
    {
        base.Start();

        // 방금 죽어서 나온 아이템만 총을 막는다.
        // 루프 복원분까지 막으면 씬이 다시 시작될 때마다 총을 못 잡아 게임이 진행되지 않는다.
        if (blocksGunUntilUsed && !used)
            pendingItems.Add(this);
    }

    private void OnDestroy()
    {
        pendingItems.Remove(this);
    }

    /// <summary>
    /// DeathItemSpawner가 스폰 직후 값을 채워줄 때 사용합니다.
    /// </summary>
    public void Configure(float increase, float duration, bool once, bool hide,
                          PlayableDirector timeline = null, float afterTimelineDelay = 0.2f)
    {
        fovIncrease = increase;
        fovChangeDuration = duration;
        useOnce = once;
        hideAfterUse = hide;
        runtimeUseTimeline = timeline;
        delayAfterTimeline = afterTimelineDelay;
    }

    /// <summary>회상 도중 자유 이동 구간을 지정합니다. null이면 없음.</summary>
    public void SetFreeRoamSegment(FlashbackFreeRoamSegment segment)
    {
        freeRoamSegment = segment;
    }

    /// <summary>타임라인이 끝난 뒤 재생할 대사 그룹을 지정합니다.</summary>
    public void SetAfterTimelineDialogueGroup(int groupIndex)
    {
        afterTimelineDialogueGroupIndex = groupIndex;
    }

    /// <summary>아이템 사용 연출이 시작되기 전에 재생할 대사 그룹을 지정합니다.</summary>
    public void SetBeforeUseDialogueGroup(int groupIndex)
    {
        beforeUseDialogueGroupIndex = groupIndex;
    }

    // 아이템 대사가 끝나고 회상(타임라인/탐색)에 들어가기 직전에 불린다. (환각 끄기 등)
    private System.Action onBeforeUseDialogueDone;
    public void SetOnBeforeUseDialogueDone(System.Action callback)
    {
        onBeforeUseDialogueDone = callback;
    }

    public void SetTimelineMarkerDialogueGroups(int first, int second, int third, int forth)
    {
        firstMarkerDialogueGroupIndex = first;
        secondMarkerDialogueGroupIndex = second;
        thirdMarkerDialogueGroupIndex = third;
        forthMarkerDialogueGroupIndex = forth;
    }

    public void SetFilmGrainVolume(Volume volume)
    {
        filmGrainVolume = volume;
        cachedFilmGrains.Clear();

        if (filmGrainVolume != null)
        {
            originalVolumeWeight = filmGrainVolume.weight;
            hasOriginalVolumeWeight = true;
        }
        else
        {
            hasOriginalVolumeWeight = false;
        }
    }

    // Timeline Signal Receiver에서 호출할 메서드들
    public void PlayFirstTimelineDialogue() => PlayMarkerDialogue(firstMarkerDialogueGroupIndex, "First");
    public void PlaySecondTimelineDialogue() => PlayMarkerDialogue(secondMarkerDialogueGroupIndex, "Second");
    public void PlayThirdTimelineDialogue() => PlayMarkerDialogue(thirdMarkerDialogueGroupIndex, "Third");
    public void PlayForthTimelineDialogue() => PlayMarkerDialogue(forthMarkerDialogueGroupIndex, "Forth");

    public override void ShowInteractionUI()
    {
        if (useOnce && used) return; // 이미 쓴 아이템은 호버 반응도 하지 않음
        base.ShowInteractionUI();
    }

    public override void Interact()
    {
        if (useOnce && used)
        {
            Debug.Log($"[ItemFovInteract] '{gameObject.name}'은(는) 이미 사용했습니다.");
            return;
        }

        // 렌즈 컨트롤러가 없다고 여기서 return하면 아이템이 "안 쓴 상태"로 남아
        // 총을 영영 못 잡는 교착이 생긴다. 경고만 남기고 연출은 그대로 진행한다.
        var lens = CameraLensController.Instance;
        if (lens == null)
        {
            Debug.LogWarning("[ItemFovInteract] 씬에 CameraLensController가 없습니다! " +
                             "빈 오브젝트에 CameraLensController를 붙여주세요. " +
                             "이번에는 FOV 효과 없이 연출만 재생합니다.");
        }

        used = true;

        // 사용하는 순간 대기 목록에서 빠진다. 이때부터 다시 총을 잡을 수 있다.
        // (연출이 도는 동안에는 아래에서 TurnManager를 잠그므로 어차피 조작은 막힌다)
        pendingItems.Remove(this);

        HideInteractionUI();

        StartCoroutine(UseRoutine(lens));
    }

    /// <summary>
    /// 기억 파편을 눌렀을 때 호출합니다. 회상(탐색 + 타임라인 + 회상 대사)만 재생하고 끝납니다.
    /// 조작 잠금과 장전(Bullet) 화면은 호출한 쪽(사망 연출)이 맡습니다.
    /// </summary>
    public IEnumerator PlayAsMemoryFragment()
    {
        used = true;
        runAsMemoryFragment = true;
        pendingItems.Remove(this);
        yield return StartCoroutine(UseRoutine(null));
    }

    // 타임라인을 재생하고, 끝나면 효과를 적용한 뒤 조작을 플레이어에게 돌려준다.
    private IEnumerator UseRoutine(CameraLensController lens)
    {
        TurnManager tm = runAsMemoryFragment ? null : TurnManager.Instance;

        // 연출이 도는 동안에는 총을 집거나 다른 아이템을 쓰지 못하게 잠근다.
        if (runAsMemoryFragment)
        {
            CamMove.blockLook = true;
            CamMove.blockInteraction = true;
        }
        else if (tm != null)
        {
            tm.SetLocked(true);
        }
        else
        {
            CamMove.blockLook = true;
            CamMove.blockInteraction = true;
        }

        // 연출(타임라인) 시작 전에 먼저 나오는 대사. 끝날 때까지 기다린 뒤 다음으로 진행한다.
        if (beforeUseDialogueGroupIndex >= 0)
            yield return StartCoroutine(PlayUseDialogueRoutine(beforeUseDialogueGroupIndex));
        onBeforeUseDialogueDone?.Invoke();

        PlayableDirector activeTimeline = GetActiveUseTimeline();

        // BeforeTimeline 방식: 먼저 회상 장소를 걸어 다니며 찾고, 마지막 장소에 닿으면 그때 타임라인을 튼다.
        bool roamFirst = freeRoamSegment != null && freeRoamSegment.RunsBeforeTimeline;
        bool inMemory = activeTimeline != null || roamFirst;

        // 회상에 들어가는 순간부터 필름 그레인 + BGM 음소거. (탐색 구간도 회상의 일부)
        if (inMemory)
        {
            EnsureFilmGrainBinding();
            StartFilmGrainFade(filmGrainTargetIntensity, filmGrainFadeInDuration);

            if (BGMManager.Instance != null)
                BGMManager.Instance.RequestMute();
        }

        if (roamFirst)
        {
            Debug.Log($"[ItemFovInteract] '{gameObject.name}' 회상 장소 탐색을 먼저 진행합니다.");
            yield return freeRoamSegment.StartCoroutine(freeRoamSegment.RunSegment());
        }

        if (activeTimeline != null)
        {
            bool finished = false;
            void OnStopped(PlayableDirector d)
            {
                activeTimeline.stopped -= OnStopped;
                finished = true;
            }

            activeTimeline.stopped += OnStopped;

            activeTimeline.Play();

            // 탐색을 먼저 했으면 타임라인 앞의 테이블 장면은 건너뛰고 회상 장면부터 튼다.
            if (roamFirst)
            {
                float startAt = freeRoamSegment.TimelineStartAt;
                if (startAt > 0f && startAt < activeTimeline.duration)
                {
                    activeTimeline.time = startAt;
                    activeTimeline.Evaluate();
                }
            }

            Debug.Log($"[ItemFovInteract] '{gameObject.name}' 사용 연출 재생: {activeTimeline.name} ({activeTimeline.time:0.0}초부터)");

            // 자유 이동 구간이 있으면, 지정한 시점에서 타임라인을 멈추고 탐색을 진행한 뒤 이어서 튼다.
            bool segmentDone = roamFirst || freeRoamSegment == null || freeRoamSegment.PauseTimelineAt < 0f;

            // 전화를 받는 기억: 통화 장면에서 영상을 멈추고 두 번째 탐색(화장실 등)을 한 뒤 테이블 복귀 구간부터 이어 튼다.
            bool afterCallDone = !(roamFirst && freeRoamSegment.HasAfterCall);

            // 타임라인을 고쳐서 길이가 줄면 멈출 시점이 끝을 넘어가 탐색이 통째로 빠진다.
            // 그럴 땐 끝나기 직전에 멈춘다.
            float endGuard = Mathf.Max(0f, (float)activeTimeline.duration - 0.15f);
            float callPauseAt = 0f, segmentPauseAt = 0f;
            if (freeRoamSegment != null)
            {
                callPauseAt = Mathf.Min(freeRoamSegment.AfterCallPauseAt, endGuard);
                segmentPauseAt = Mathf.Min(freeRoamSegment.PauseTimelineAt, endGuard);
                if (!afterCallDone && freeRoamSegment.AfterCallPauseAt > endGuard)
                    Debug.LogWarning($"[ItemFovInteract] 통화 뒤 탐색 시점({freeRoamSegment.AfterCallPauseAt:0.00}초)이 타임라인 길이({activeTimeline.duration:0.00}초)를 넘습니다. {callPauseAt:0.00}초에서 멈춥니다.");
                if (!segmentDone && freeRoamSegment.PauseTimelineAt > endGuard)
                    Debug.LogWarning($"[ItemFovInteract] 탐색 시작 시점({freeRoamSegment.PauseTimelineAt:0.00}초)이 타임라인 길이({activeTimeline.duration:0.00}초)를 넘습니다. {segmentPauseAt:0.00}초에서 멈춥니다.");
            }

            while (!finished)
            {
                if (!afterCallDone && activeTimeline.time >= callPauseAt)
                {
                    afterCallDone = true;
                    Debug.Log($"[ItemFovInteract] 통화 장면 {activeTimeline.time:0.0}초에서 멈추고 이어서 탐색합니다.");

                    // Pause()를 하면 Cinemachine이 카메라를 놓아 테이블 화면으로 돌아가 버린다.
                    // 속도만 0으로 두어 통화 장면(카메라, 휴대폰)을 그대로 붙잡아 둔다.
                    var rootPlayable = activeTimeline.playableGraph.IsValid()
                        ? activeTimeline.playableGraph.GetRootPlayable(0)
                        : UnityEngine.Playables.Playable.Null;
                    if (rootPlayable.IsValid()) rootPlayable.SetSpeed(0d);
                    else activeTimeline.Pause();

                    yield return freeRoamSegment.StartCoroutine(freeRoamSegment.RunAfterCall());

                    float resumeAt = freeRoamSegment.AfterCallResumeAt;
                    if (resumeAt > activeTimeline.time && resumeAt < activeTimeline.duration)
                    {
                        activeTimeline.time = resumeAt;
                        activeTimeline.Evaluate();
                    }
                    if (rootPlayable.IsValid()) rootPlayable.SetSpeed(1d);
                    else activeTimeline.Resume();
                }

                if (!segmentDone && activeTimeline.time >= segmentPauseAt)
                {
                    segmentDone = true;
                    Debug.Log($"[ItemFovInteract] 타임라인 {activeTimeline.time:0.0}초에서 멈추고 탐색 구간을 시작합니다.");

                    activeTimeline.Pause();
                    yield return freeRoamSegment.StartCoroutine(freeRoamSegment.RunSegment());

                    // 테이블로 돌아가는 전환 구간으로 건너뛰어서 이어 튼다.
                    float resumeAt = freeRoamSegment.ResumeTimelineAt;
                    if (resumeAt >= 0f && resumeAt > activeTimeline.time && resumeAt < activeTimeline.duration)
                    {
                        activeTimeline.time = resumeAt;
                        activeTimeline.Evaluate();
                    }

                    Debug.Log($"[ItemFovInteract] 탐색 종료. 타임라인을 {activeTimeline.time:0.0}초부터 이어서 재생합니다.");
                    activeTimeline.Resume();
                }

                yield return null;
            }

            // 멈출 시점을 음수로 두었거나 타임라인이 그보다 짧으면, 타임라인이 끝난 뒤에 진행한다.
            if (!roamFirst && freeRoamSegment != null && !segmentDone)
                yield return freeRoamSegment.StartCoroutine(freeRoamSegment.RunSegment());
            else if (!roamFirst && freeRoamSegment != null && freeRoamSegment.PauseTimelineAt < 0f)
                yield return freeRoamSegment.StartCoroutine(freeRoamSegment.RunSegment());
        }

        if (inMemory)
        {
            if (BGMManager.Instance != null)
                BGMManager.Instance.ReleaseMute();

            StartFilmGrainFade(0f, filmGrainFadeOutDuration);

            if (delayAfterTimeline > 0f)
                yield return new WaitForSeconds(delayAfterTimeline);
        }

        if (lens != null)
        {
            Debug.Log($"[ItemFovInteract] '{gameObject.name}' 효과 적용 -> FOV {fovIncrease:+0.#;-0.#}");
            lens.AddFieldOfView(fovIncrease, fovChangeDuration);
        }

        // 연출이 끝난 뒤의 회상 대사(Item_Use_*). 마커 대사와는 별개다.
        if (afterTimelineDialogueGroupIndex >= 0)
            yield return StartCoroutine(PlayUseDialogueRoutine(afterTimelineDialogueGroupIndex));

        // 기억 파편은 여기서 끝. 이어지는 라운드 리셋이 장전 화면을 보여준다.
        if (runAsMemoryFragment)
            yield break;

        // 대사까지 전부 끝났으면 장전 화면을 다시 보여준다.
        if (replayBulletTimelineAfterUse)
            yield return StartCoroutine(ReplayBulletRoutine());

        // 잠금을 먼저 풀고 나서 오브젝트를 숨긴다.
        // (순서가 반대면 이 코루틴이 중간에 멈춰서 잠금이 영영 안 풀린다)
        if (tm != null)
            tm.SetLocked(false);
        else
            TurnManager.ApplyCameraBlockForCurrentTurn();

        if (hideAfterUse)
            gameObject.SetActive(false);
    }

    private PlayableDirector GetActiveUseTimeline()
    {
        if (runtimeUseTimeline != null)
            return runtimeUseTimeline;

        if (useTimeline1 != null) return useTimeline1;
        if (useTimeline2 != null) return useTimeline2;
        if (useTimeline3 != null) return useTimeline3;
        if (useTimeline4 != null) return useTimeline4;

        return null;
    }

    private void EnsureFilmGrainBinding()
    {
        if (cachedFilmGrains.Count > 0)
            return;

        if (filmGrainVolume != null)
            BindFilmGrainFromVolume(filmGrainVolume);

        if (cachedFilmGrains.Count == 0)
            Debug.LogWarning("[ItemFovInteract] FilmGrain Volume이 설정되지 않았거나 프로필에 FilmGrain이 없습니다. DeathItemSpawner에서 글로벌 Volume을 연결하세요.");
    }

    private void BindFilmGrainFromVolume(Volume volume)
    {
        if (volume == null)
            return;

        var profile = volume.profile != null ? volume.profile : volume.sharedProfile;
        if (profile == null)
            return;

        if (!profile.TryGet(out FilmGrain fg))
            fg = profile.Add<FilmGrain>(true);

        if (fg != null && !cachedFilmGrains.Contains(fg))
            cachedFilmGrains.Add(fg);

        volume.enabled = true;
    }

    private void StartFilmGrainFade(float target, float duration)
    {
        if (cachedFilmGrains.Count == 0)
            return;

        if (filmGrainRoutine != null)
            StopCoroutine(filmGrainRoutine);

        filmGrainRoutine = StartCoroutine(FilmGrainFadeRoutine(target, duration));
    }

    private IEnumerator FilmGrainFadeRoutine(float target, float duration)
    {
        float clampedTarget = Mathf.Clamp01(target);
        bool controlWeight = fadeVolumeWeight && filmGrainVolume != null;
        float startWeight = 0f;
        float endWeight = 0f;

        if (controlWeight)
        {
            startWeight = filmGrainVolume.weight;
            endWeight = clampedTarget > 0f ? Mathf.Clamp01(filmGrainVolumeWeight) : (hasOriginalVolumeWeight ? originalVolumeWeight : 0f);
            filmGrainVolume.enabled = true;
        }

        float[] fromValues = new float[cachedFilmGrains.Count];
        for (int i = 0; i < cachedFilmGrains.Count; i++)
        {
            var fg = cachedFilmGrains[i];
            fromValues[i] = fg.intensity.value;
            fg.active = true;
            fg.intensity.overrideState = true;
        }

        if (duration <= 0f)
        {
            for (int i = 0; i < cachedFilmGrains.Count; i++)
                cachedFilmGrains[i].intensity.value = clampedTarget;

            if (controlWeight)
                filmGrainVolume.weight = endWeight;

            filmGrainRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < cachedFilmGrains.Count; i++)
                cachedFilmGrains[i].intensity.value = Mathf.Lerp(fromValues[i], clampedTarget, t);

            if (controlWeight)
                filmGrainVolume.weight = Mathf.Lerp(startWeight, endWeight, t);

            yield return null;
        }

        for (int i = 0; i < cachedFilmGrains.Count; i++)
            cachedFilmGrains[i].intensity.value = clampedTarget;

        if (controlWeight)
            filmGrainVolume.weight = endWeight;

        filmGrainRoutine = null;
    }

    // 타임라인 Signal이 호출하는 지점. 마커 대사는 연출 도중 그 자리에서 바로 나와야 한다.
    //
    // 다만 TypeWriter를 직접 돌리면 안 된다. 대사가 끝나는 순간
    // TimelineManager.OnDialogueFinished가 "라운드가 끝난 것"으로 오해하고
    // Bullet 타임라인을 시작해서 회상 연출 위에 장전 화면이 덮인다.
    // PlayImmediateDialogue를 거치면 그 분기를 타지 않는다.
    private void PlayMarkerDialogue(int dialogueGroupIndex, string label)
    {
        if (dialogueGroupIndex < 0)
            return;

        TimelineManager timelineManager = FindTimelineManager();
        if (timelineManager == null)
        {
            Debug.LogWarning("[ItemFovInteract] TimelineManager를 찾지 못했습니다. " +
                             "TypeWriter로 직접 재생하면 Bullet 타임라인이 끼어들 수 있습니다.");
            TypeWriter typeWriter = FindFirstObjectByType<TypeWriter>(FindObjectsInactive.Include);
            if (typeWriter != null)
                typeWriter.PlayDialogueGroup(dialogueGroupIndex);
            return;
        }

        Debug.Log($"[ItemFovInteract] {label} Marker 대사 즉시 재생 (groupIndex={dialogueGroupIndex})");
        timelineManager.PlayImmediateDialogue(dialogueGroupIndex, null);
    }

    private static TimelineManager cachedTimelineManager;

    private TimelineManager FindTimelineManager()
    {
        if (cachedTimelineManager == null)
            cachedTimelineManager = FindFirstObjectByType<TimelineManager>(FindObjectsInactive.Include);
        return cachedTimelineManager;
    }

    // 타임라인이 끝난 뒤의 회상 대사(Item_Use_*). 끝날 때까지 기다린다.
    private IEnumerator PlayUseDialogueRoutine(int groupIndex)
    {
        TimelineManager timelineManager = FindTimelineManager();

        if (timelineManager == null)
        {
            Debug.LogWarning("[ItemFovInteract] TimelineManager를 찾지 못해 회상 대사를 재생하지 못했습니다.");
            yield break;
        }

        // 마커 대사가 아직 돌고 있으면 먼저 끝나기를 기다린다.
        // 겹쳐서 틀면 앞 대사가 중간에 잘린다.
        if (timelineManager.IsImmediateDialoguePlaying)
        {
            Debug.Log("[ItemFovInteract] 마커 대사가 아직 진행 중입니다. 끝나기를 기다립니다.");
            yield return new WaitUntil(() => !timelineManager.IsImmediateDialoguePlaying);
        }

        bool done = false;
        Debug.Log($"[ItemFovInteract] '{gameObject.name}' 회상 대사 재생 (groupIndex={groupIndex})");
        timelineManager.PlayImmediateDialogue(groupIndex, () => done = true);
        yield return new WaitUntil(() => done);
        Debug.Log($"[ItemFovInteract] '{gameObject.name}' 회상 대사 종료.");
    }

    // 모든 대사가 끝난 뒤 장전 화면을 다시 보여준다.
    private IEnumerator ReplayBulletRoutine()
    {
        TimelineManager timelineManager = FindTimelineManager();
        if (timelineManager == null)
            yield break;

        bool done = false;
        Debug.Log($"[ItemFovInteract] '{gameObject.name}' 장전(Bullet) 타임라인을 다시 재생합니다.");
        timelineManager.ReplayBulletTimeline(() => done = true);
        yield return new WaitUntil(() => done);
    }
}
