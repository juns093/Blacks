using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// 진행 순서:
// 1) 플레이와 동시에 Wakeup 타임라인 재생
// 2) Wakeup 타임라인이 끝나면 TypeWriter가 대사 재생 로직을 실행 (Intro 그룹)
// 3) TypeWriter의 대사가 전부 끝나면 Bullet 타임라인 재생
// 4) 총알이 모두 소진되면(AmmoManager.OnAmmoDepleted) 카메라/상호작용만 고정시키고 자동 진행 없이 대기.
//    이후 외부(GunObject)에서 TriggerReloadDialogue(groupIndex)를 호출하면
//    그때 대사를 재생하고, 대사가 끝나면 재장전 + Bullet 타임라인을 재생함
public class TimelineManager : MonoBehaviour
{
    [Header("피격 연출 타이밍")]
    [Tooltip("발사음이 다 난 뒤에 hitted 타임라인을 시작할지 여부. " +
             "켜두면 '조준 -> 총성 -> 피격 연출' 순서가 지켜집니다. " +
             "끄면 아래 고정 지연만 두고 바로 시작합니다.")]
    [SerializeField] private bool waitForShotSoundBeforeHitTimeline = true;

    [Tooltip("hitted 타임라인을 시작하기까지의 추가 지연(초). " +
             "위 옵션이 켜져 있으면 '발사음 길이 + 이 값'만큼 기다립니다.")]
    [SerializeField] private float hitTimelineStartDelay = 0.2f;

    [Tooltip("발사음 길이를 알려줄 총. 비워두면 위 고정 지연만 사용합니다.")]
    [SerializeField] private GunObject gun;

    [Header("Timelines")]
    [SerializeField] private PlayableDirector wakeup;
    [SerializeField] private PlayableDirector bullet;
    [SerializeField] private PlayableDirector hitted;
    [Tooltip("플레이어 HP가 1 남은 상태에서(치명타로) 맞았을 때 재생할 별도의 타임라인. 일반 hitted와 다른 연출을 넣을 때 사용")]
    [SerializeField] private PlayableDirector finalHitted;

    [Header("Dialogue")]
    [SerializeField] private TypeWriter typeWriter;

    [Header("Ammo")]
    [SerializeField] private AmmoManager ammoManager;

    [Header("Game State")]
    [Tooltip("루프(Who 사망 재시작)마다 다른 인트로 대사를 재생하기 위한 참조. " +
             "비워두면 항상 dialogueGroups[0]을 재생합니다.")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("Turn")]
    [Tooltip("Bullet 타임라인이 끝나 새 라운드가 시작될 때 턴을 플레이어부터 다시 시작시키기 위한 참조")]
    [SerializeField] private TurnManager turnManager;

    // 총알이 소진되어 카메라가 고정된 상태인지 여부
    private bool waitingForReloadTrigger = false;

    // 턴 중도에 끼어드는 "즉시 대사" 재생 중인지 여부 (재생 끝나도 재장전/Bullet 타임라인을 진행하지 않음)
    private bool immediateDialogueActive = false;
    private System.Action pendingImmediateFinishCallback;

    // hitted 타임라인이 끝났을 때 호출할 콜백 (외부에서 완료 시점을 기다릴 수 있도록)
    private System.Action pendingHitFinishCallback;
    // finalHitted(치명타) 타임라인이 끝났을 때 호출할 콜백
    private System.Action pendingFinalHitFinishCallback;

    void Start()
    {
        if (wakeup != null)
            wakeup.stopped += OnWakeupFinished;
        else
            Debug.LogWarning("[TimelineManager] wakeup PlayableDirector가 연결되어 있지 않습니다!");

        if (bullet != null)
            bullet.stopped += OnBulletTimelineFinished;
        else
            Debug.LogWarning("[TimelineManager] bullet PlayableDirector가 연결되어 있지 않습니다!");

        if (hitted != null)
            hitted.stopped += OnHittedTimelineFinished;
        else
            Debug.LogWarning("[TimelineManager] hitted PlayableDirector가 연결되어 있지 않습니다!");

        if (finalHitted != null)
            finalHitted.stopped += OnFinalHittedTimelineFinished;
        else
            Debug.LogWarning("[TimelineManager] finalHitted PlayableDirector가 연결되어 있지 않습니다! (치명타 전용 타임라인이 필요 없다면 무시해도 됩니다)");

        if (typeWriter != null)
            typeWriter.OnSequenceFinished += OnDialogueFinished;
        else
            Debug.LogWarning("[TimelineManager] typeWriter가 연결되어 있지 않습니다!");

        if (ammoManager != null)
            ammoManager.OnAmmoDepleted += OnAmmoDepleted;
        else
            Debug.LogWarning("[TimelineManager] ammoManager가 연결되어 있지 않아 총알 소진 시 카메라 고정을 실행할 수 없습니다!");

        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        // 타임라인이 재생되는 동안엔 BGM을 줄여서 연출/대사가 더 잘 들리게 한다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.RequestDuck();

        EnsureStoryDialogues();

        if (wakeup != null)
            wakeup.Play();
    }

    void OnDestroy()
    {
        if (wakeup != null) wakeup.stopped -= OnWakeupFinished;
        if (bullet != null) bullet.stopped -= OnBulletTimelineFinished;
        if (hitted != null) hitted.stopped -= OnHittedTimelineFinished;
        if (finalHitted != null) finalHitted.stopped -= OnFinalHittedTimelineFinished;
        if (typeWriter != null) typeWriter.OnSequenceFinished -= OnDialogueFinished;
        if (ammoManager != null) ammoManager.OnAmmoDepleted -= OnAmmoDepleted;
    }

    // 하드코딩 대사(GameSceneStoryDialogues)가 이 typeWriter에 확실히 들어가 있도록 보장한다.
    // - 컴포넌트가 다른 오브젝트에 붙어 있거나 아예 빠져 있으면 인스펙터의 옛 대사가 그대로 나온다.
    // - 그래서 없으면 자동으로 붙이고, 재생 직전에 한 번 더 주입한다.
    private void EnsureStoryDialogues()
    {
        if (typeWriter == null) return;

        var story = typeWriter.GetComponent<GameSceneStoryDialogues>();
        if (story == null)
        {
            Debug.LogWarning("[TimelineManager] 이 TypeWriter에 GameSceneStoryDialogues가 붙어 있지 않아 " +
                             "자동으로 추가합니다. (다른 오브젝트의 TypeWriter에 붙어 있었을 수 있습니다)");
            story = typeWriter.gameObject.AddComponent<GameSceneStoryDialogues>();
        }

        story.ApplyStoryDefaultDialogues();
    }

    private void OnWakeupFinished(PlayableDirector director)
    {
        EnsureStoryDialogues();

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        // 루프마다 다른 인트로 대사를 재생한다.
        // (Who가 죽어서 씬이 다시 시작될 때 이전과 다른 텍스트가 나오도록)
        int introIndex = gameStateManager != null ? gameStateManager.GetIntroDialogueGroupIndex() : StoryDialogueIndex.IntroMain;

        // 음수면 인트로를 생략하고 바로 게임 흐름(탄창 타임라인)으로 진행한다.
        if (introIndex < 0)
        {
            Debug.Log("[TimelineManager] 이번 진입은 인트로 텍스트를 생략합니다.");
            StartRoundFlow();
            return;
        }

        if (typeWriter == null)
        {
            Debug.LogWarning("[TimelineManager] typeWriter가 없어 인트로를 생략하고 바로 진행합니다.");
            StartRoundFlow();
            return;
        }

        Debug.Log($"[TimelineManager] Wakeup 타임라인 종료. 인트로 대사 그룹 {introIndex}번을 재생합니다.");
        typeWriter.PlayDialogueGroup(introIndex);
    }

    // typeWriter.OnSequenceFinished에 연결된 단일 핸들러.
    private void OnDialogueFinished()
    {
        // 턴 중도에 끼어든 "즉시 대사"였다면, 재장전/Bullet 흐름을 건드리지 않음
        // 카메라만 풀어준 뒤 호출자(GunObject)에게 콜백으로 완료를 알린다.
        if (immediateDialogueActive)
        {
            immediateDialogueActive = false;

            TurnManager.ApplyCameraBlockForCurrentTurn();

            var callback = pendingImmediateFinishCallback;
            pendingImmediateFinishCallback = null;
            callback?.Invoke();
            return;
        }

        if (waitingForReloadTrigger)
        {
            waitingForReloadTrigger = false;

            if (ammoManager != null)
                ammoManager.LoadShells();
        }

        StartRoundFlow();
    }

    // 새 라운드 시작 = 장전(Bullet) 타임라인.
    private void StartRoundFlow()
    {
        PlayBulletTimeline();
    }

    // 총알이 모두 소진되었을 때(AmmoManager.OnAmmoDepleted) 호출됨
    private void OnAmmoDepleted()
    {
        Debug.Log("[TimelineManager] 총알이 모두 소진됨. 카메라를 고정하고 TriggerReloadDialogue() 호출을 기다립니다.");

        waitingForReloadTrigger = true;

        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();
    }

    // 총알을 모두 쏘고 카메라가 고정된 후, 단 하나의 대사(groupIndex)만 재생.
    // 대사가 끝나면 재장전 + Bullet 타임라인이 이어짐.
    public void TriggerReloadDialogue(int groupIndex = 1)
    {
        if (typeWriter == null)
        {
            Debug.LogWarning("[TimelineManager] typeWriter가 연결되어 있지 않아 대사를 재생할 수 없습니다!");
            return;
        }

        Debug.Log($"[TimelineManager] TriggerReloadDialogue(groupIndex={groupIndex}) 호출됨.");
        typeWriter.PlayDialogueGroup(groupIndex);
    }

    /// <summary>
    /// 턴 진행 도중(예: HP가 1 남았을 때) 카메라를 고정하고 대사 하나만 재생한 뒤,
    /// 재장전/Bullet 타임라인 흐름에 영향을 주지 않고 onFinished 콜백으로 완료를 알립니다.
    /// </summary>
    public void PlayImmediateDialogue(int groupIndex, System.Action onFinished)
    {
        if (typeWriter == null)
        {
            Debug.LogWarning("[TimelineManager] typeWriter가 연결되어 있지 않아 대사를 재생할 수 없습니다!");
            onFinished?.Invoke();
            return;
        }

        Debug.Log($"[TimelineManager] PlayImmediateDialogue(groupIndex={groupIndex}) 호출됨. 카메라 고정 후 대사 재생.");

        immediateDialogueActive = true;
        pendingImmediateFinishCallback = onFinished;

        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        typeWriter.PlayDialogueGroup(groupIndex);
    }

    public void TriggerReloadDialogueWithLines(string[] lines)
    {
        if (typeWriter == null)
        {
            Debug.LogWarning("[TimelineManager] typeWriter가 연결되어 있지 않아 대사를 재생할 수 없습니다!");
            return;
        }

        typeWriter.SetDialogues(lines);
        Debug.Log("[TimelineManager] TriggerReloadDialogueWithLines 호출됨.");
        typeWriter.StartDialogueSequence();
    }

    /// <summary>
    /// 대사 없이 곧바로 재장전 + Bullet 타임라인을 재생해 새 라운드를 시작합니다.
    /// 사망 후 라운드 리셋(GameStateManager)에서 사용합니다.
    /// </summary>
    public void StartNewRoundImmediately()
    {
        Debug.Log("[TimelineManager] StartNewRoundImmediately() 호출됨. 대사 없이 새 라운드를 시작합니다.");

        // 총알 소진으로 걸려 있던 대기 상태를 해제 (사망 리셋이 재장전을 대신 처리하므로)
        waitingForReloadTrigger = false;
        immediateDialogueActive = false;
        pendingImmediateFinishCallback = null;

        if (ammoManager != null)
            ammoManager.LoadShells();
        else
            Debug.LogWarning("[TimelineManager] ammoManager가 없어 재장전하지 못했습니다!");

        // 사망으로 테이블에 아이템이 올라왔다면, 장전 화면은 그 아이템을 쓰고
        // 회상 대사까지 끝난 뒤에 보여준다. (ItemFovInteract가 ReplayBulletTimeline을 호출)
        // 여기서 바로 틀면 아이템을 만지기도 전에 장전 연출이 먼저 나와 버린다.
        if (ItemFovInteract.HasPendingItem)
        {
            Debug.Log("[TimelineManager] 아직 사용하지 않은 아이템이 있어 Bullet 타임라인을 미룹니다. " +
                      "아이템 사용 연출이 끝난 뒤에 재생됩니다.");

            // Bullet 타임라인이 하던 뒷정리(턴 리셋 + 잠금 해제)를 대신 해준다.
            // 이걸 빼먹으면 조작이 잠긴 채로 남아서 아이템을 클릭할 수 없다.
            TurnManager tm = turnManager != null ? turnManager : TurnManager.Instance;
            if (tm != null)
            {
                tm.ResetToRoundStart();
                tm.SetLocked(false);
            }
            TurnManager.ApplyCameraBlockForCurrentTurn();
            return;
        }

        StartRoundFlow();
    }

    private void PlayBulletTimeline()
    {
        if (bullet == null)
        {
            Debug.LogWarning("[TimelineManager] bullet PlayableDirector가 연결되어 있지 않습니다!");
            CamMove.blockInteraction = false;
            return;
        }

        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        // Who 복귀 루틴에서 Gun Animator가 꺼진 채 남아 있을 수 있으므로,
        // 재장전/Bullet 타임라인 시작 전에 반드시 복구한다.
        if (gun != null)
            gun.PrepareForTimelineAnimation();

        if (ammoManager != null)
            ammoManager.SpawnBulletVisual();
        else
            Debug.LogWarning("[TimelineManager] 인스펙터에 AmmoManager가 연결되어 있지 않아 총알 표시를 스킵합니다!");

        bullet.Play();
    }

    private void OnBulletTimelineFinished(PlayableDirector director)
    {
        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        // 새 라운드가 시작된 시점 -> 턴을 라운드 시작 상태(기본: 플레이어)로 되돌리고 잠금을 푼다.
        // 카메라는 턴 정리가 끝난 뒤에 그 상태에 맞춰 풀어야 한다.
        TurnManager tm = turnManager != null ? turnManager : TurnManager.Instance;
        if (tm != null)
        {
            tm.ResetToRoundStart();
            tm.SetLocked(false);
        }

        TurnManager.ApplyCameraBlockForCurrentTurn();

        var bulletCallback = pendingBulletFinishCallback;
        pendingBulletFinishCallback = null;
        bulletCallback?.Invoke();
    }

    // 실탄으로 자기 자신을 쐈을 때 등, 외부(GunObject)에서 즉시 연출 타임라인을 실행하고 싶을 때 호출.
    // 총알 소진/재장전 흐름과는 무관하게 별도의 "hitted" 타임라인을 즉시 재생합니다.
    // onFinished: 타임라인이 끝난 뒤 호출할 콜백 (예: 이어서 HP 위험 대사를 재생할 때 사용)
    // 총성이 끝나기를 기다리는 중이거나 실제로 재생 중일 때 true.
    // 시작을 지연시키더라도 외부에서는 "피격 연출이 진행 중"으로 보여야 하므로 별도 플래그를 둔다.
    // (이게 없으면 지연 구간에 IsHitTimelinePlaying이 false라 대기하던 쪽이 그냥 지나쳐 버린다)
    private bool hitTimelineActive = false;

    private Coroutine hitTimelineRoutine;
    private bool forceImmediateHitTimelineStart = false;

    /// <summary>
    /// hitted(피격) 타임라인이 예약되었거나 재생 중인지 여부.
    /// WhoAiManager / GunObject가 "피격 연출이 다 끝난 뒤에 총을 내려놓기" 위해 참고합니다.
    /// </summary>
    public bool IsHitTimelinePlaying =>
        hitTimelineActive || (hitted != null && hitted.state == PlayState.Playing);

    /// <summary>
    /// 턴 중간에 끼워 넣은 "즉시 대사"가 지금 재생 중인지 여부.
    /// 아이템 연출처럼 대사를 연달아 틀어야 할 때, 앞 대사가 끝나기를 기다리는 데 씁니다.
    /// </summary>
    public bool IsImmediateDialoguePlaying => immediateDialogueActive;

    // Bullet 타임라인이 끝났을 때 한 번만 호출할 콜백 (아이템 연출 등 외부 대기용)
    private System.Action pendingBulletFinishCallback;

    /// <summary>
    /// 재장전은 하지 않고 Bullet(장전) 타임라인만 다시 재생합니다.
    /// 아이템 사용 연출이 끝난 뒤 "지금 챔버 상태"를 다시 보여줄 때 사용합니다.
    /// </summary>
    public void ReplayBulletTimeline(System.Action onFinished = null)
    {
        Debug.Log("[TimelineManager] ReplayBulletTimeline() 호출됨. 재장전 없이 장전 화면만 다시 보여줍니다.");
        pendingBulletFinishCallback = onFinished;
        PlayBulletTimeline();
    }

    public void PlayHitTimeline(System.Action onFinished = null, bool immediateStart = false)
    {
        if (hitted == null)
        {
            Debug.LogWarning("[TimelineManager] hitted PlayableDirector가 연결되어 있지 않아 연출 타임라인을 재생할 수 없습니다!");
            onFinished?.Invoke();
            return;
        }

        pendingHitFinishCallback = onFinished;
        hitTimelineActive = true;
        forceImmediateHitTimelineStart = immediateStart;

        // hitted 타임라인이 재생되는 동안엔 BGM을 줄인다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.RequestDuck();

        // 연출 대기 중에도 화면은 미리 고정해 둔다. 총성이 울리는 동안 카메라가 돌면 안 된다.
        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        if (hitTimelineRoutine != null)
            StopCoroutine(hitTimelineRoutine);

        hitTimelineRoutine = StartCoroutine(PlayHitTimelineRoutine());
    }

    // 총성이 먼저 울리고 나서 피격 연출이 들어가야 자연스럽다.
    // 같은 프레임에 둘 다 터뜨리면 총성이 연출 사운드에 묻힌다.
    private IEnumerator PlayHitTimelineRoutine()
    {
        float delay = Mathf.Max(0f, hitTimelineStartDelay);

        if (!forceImmediateHitTimelineStart && waitForShotSoundBeforeHitTimeline && gun != null && gun.LastShotSoundLength > 0f)
            delay = gun.LastShotSoundLength + Mathf.Max(0f, hitTimelineStartDelay);

        if (forceImmediateHitTimelineStart)
            delay = 0f;

        Debug.Log($"[TimelineManager] PlayHitTimeline() 호출됨. 발사음 " +
                  $"{(gun != null ? gun.LastShotSoundLength : 0f):0.00}초 -> " +
                  $"{delay:0.00}초 뒤에 hitted 타임라인을 시작합니다.");

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        forceImmediateHitTimelineStart = false;
        hitTimelineRoutine = null;
        hitted.Play();
    }

    private void OnHittedTimelineFinished(PlayableDirector director)
    {
        hitTimelineActive = false;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        // hitted 타임라인이 끝났으니 줄여뒀던 BGM 볼륨을 되돌린다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.ReleaseDuck();

        TurnManager.ApplyCameraBlockForCurrentTurn();

        var callback = pendingHitFinishCallback;
        pendingHitFinishCallback = null;
        callback?.Invoke();
    }

    // HP가 1 남은 상태에서 맞아 치명타(HP 0)가 되었을 때 재생하는 전용 타임라인.
    // 일반 hitted와 별도로 연출(카메라 앵글, 이펙트 등)을 다르게 넣고 싶을 때 사용.
    // onFinished: 타임라인이 끝난 뒤 호출할 콜백 (예: 이어서 엔딩 씬 전환 등)
    public void PlayFinalHitTimeline(System.Action onFinished = null)
    {
        if (finalHitted == null)
        {
            Debug.LogWarning("[TimelineManager] finalHitted PlayableDirector가 연결되어 있지 않아 일반 hitted 타임라인으로 대체합니다.");
            PlayHitTimeline(onFinished);
            return;
        }

        Debug.Log("[TimelineManager] PlayFinalHitTimeline() 호출됨. finalHitted 타임라인을 재생합니다.");

        pendingFinalHitFinishCallback = onFinished;

        // finalHitted 타임라인이 재생되는 동안엔 BGM을 줄인다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.RequestDuck();

        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        finalHitted.Play();
    }

    private void OnFinalHittedTimelineFinished(PlayableDirector director)
    {
        if (CamMove.Instance != null)
            CamMove.Instance.SyncRotation();

        // finalHitted 타임라인이 끝났으니 줄여뒀던 BGM 볼륨을 되돌린다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.ReleaseDuck();

        TurnManager.ApplyCameraBlockForCurrentTurn();

        var callback = pendingFinalHitFinishCallback;
        pendingFinalHitFinishCallback = null;
        callback?.Invoke();
    }
}