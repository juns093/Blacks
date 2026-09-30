using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// HP / 사망 / 라운드 진행을 관리하는 클래스.
//
// ── 스토리 규칙 ──
//  - 플레이어의 HP는 무조건 1. 실탄 한 발이면 즉사합니다.
//  - 플레이어가 죽어도 씬은 넘어가지 않습니다. 사망 연출 후 라운드가 리셋되고 게임이 계속됩니다.
//  - 플레이어가 requiredPlayerDeaths(기본 5)번 죽기 전까지는 게임이 절대 끝나지 않습니다.
//    이 횟수를 채우기 전에 Who를 죽여도 Who는 부활하고 라운드가 계속됩니다.
//  - 플레이어가 5번을 다 채운 뒤에 Who가 죽으면, 그때 비로소 엔딩 씬으로 전환됩니다.
//
// ── 기억 파편 (DeathItemSpawner가 파편 모드일 때) ──
//  - 1번째 죽음: 고통만. 2~5번째 죽음: 어두운 화면에 기억 파편 -> 사망 대사 -> 파편 클릭 -> 기억 재생.
//  - 5번째 죽음의 기억(마지막 기억)이 끝나면 마지막 승부가 시작된다.
//  - 마지막 승부에서 지면 체크포인트처럼 그 승부만 다시 한다. 이기면 엔딩 씬(StoryScene)으로.
//
// ── 대사 연동 ──
//  - HP가 1 남았을 때의 대사는 "즉시" 재생되어야 하므로 ConsumeImmediateDialogueGroupIndex()로 분리 관리.
//    (플레이어는 HP가 1이라 이 경로를 타지 않고, Who의 whoMaxHits가 2 이상일 때만 의미가 있습니다.)
//  - 특정 라운드 트리거 대사는 총알 소진 시점에 ConsumeDialogueGroupIndexForCurrentState()로 확인.
//  - 사망할 때마다 재생할 대사는 playerDeathDialogueGroupIndexes로 몇 번째 죽음인지에 따라 지정.
public class GameStateManager : MonoBehaviour
{
    public enum Actor { Player, Enemy }

    [System.Serializable]
    public class TurnDialogueTrigger
    {
        [Tooltip("인스펙터에서 구분하기 쉽도록 붙이는 이름 (로직에는 영향 없음)")]
        public string label;

        [Tooltip("이 라운드 수와 정확히 일치할 때만 대사를 재생함")]
        public int turnNumber;

        [Tooltip("이 라운드에 도달하면 재생할 TypeWriter의 dialogueGroups 인덱스 (직접 지정)")]
        public int dialogueGroupIndex;

        [System.NonSerialized] public bool triggered;
    }

    [Header("연동")]
    [SerializeField] private TimelineManager timelineManager;
    [SerializeField] private AmmoManager ammoManager;
    [SerializeField] private TurnManager turnManager;

    [Tooltip("죽을 때마다 기억 파편을 띄워줄 스포너. 비워두면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private DeathItemSpawner deathItemSpawner;

    // 5판(마지막 ???와의 판)이 시작될 때의 사망 횟수. (1~3판 ???, 4판 트레일, 5판 ???)
    private const int FinalRoundDeaths = 4;

    [Tooltip("5판에서 ???를 죽였을 때, 추리 질문을 한 번에 맞힌 수가 이 이상이면 자수 엔딩, 아니면 괴물 엔딩")]
    [SerializeField] private int confessionRequiredCorrect = 4;

    [Header("HP 설정")]
    [Tooltip("플레이어의 HP. 요구사항에 따라 1로 고정되어 있으며 변경할 수 없습니다.")]
    private const int PlayerMaxHits = 1;

    [Tooltip("Who(딜러)가 죽기까지 맞아야 하는 실탄 횟수. 플레이어와 동일하게 1이 기본값")]
    [SerializeField] private int whoMaxHits = 1;

    [Header("스토리 진행 (사망 횟수)")]
    [Tooltip("게임이 끝날 수 있게 되기까지 플레이어가 죽어야 하는 횟수. 이 횟수를 채우기 전에는 " +
             "Who를 죽여도 게임이 끝나지 않고 라운드가 계속됩니다.")]
    [SerializeField] private int requiredPlayerDeaths = 5;

    [Tooltip("플레이어가 죽을 때마다 재생할 TypeWriter의 dialogueGroups 인덱스. " +
             "0번째 = 첫 번째 죽음, 1번째 = 두 번째 죽음... 비워두면 대사 없이 넘어갑니다.")]
    [SerializeField] private List<int> playerDeathDialogueGroupIndexes = new List<int>();

    [Tooltip("Who가 죽어서 씬을 다시 시작할 때마다 재생할 인트로 대사 인덱스. " +
             "0번째 = 최초 시작, 1번째 = 첫 재시작, 2번째 = 두 번째 재시작... " +
             "루프가 목록보다 길어지면 마지막 값을 계속 씁니다. 비워두면 항상 0번 그룹을 씁니다.")]
    [SerializeField] private List<int> loopIntroDialogueGroupIndexes = new List<int>();

    [Header("Who 사망 연출 (발사 -> 대기 -> 노이즈+쉐이크+FOV -> 씬 재로드)")]
    [Tooltip("Who가 죽었을 때 화면을 덮을 노이즈 연출. 비워두면 노이즈 없이 진행합니다.")]
    [SerializeField] private GlitchNoiseEffect glitchEffect;

    [Tooltip("노이즈와 함께 재생할 카메라 연출(약한 쉐이크 + FOV 좁히기). 비워두면 카메라 연출을 건너뜁니다.")]
    [SerializeField] private CameraGlitchEffect cameraGlitchEffect;

    [Tooltip("총을 쏜 뒤 노이즈가 시작되기까지 기다리는 시간(초). " +
             "플레이어가 총을 들고 쏜 경우에는 이 값 대신 '총을 내려놓을 때까지'를 기다립니다. " +
             "(총 드는 시간은 GunObject의 Death Sequence Aim Hold에서 조절) " +
             "Who가 자기 자신을 쐈을 때처럼 총이 손에 없는 경우에만 이 값이 쓰입니다.")]
    [SerializeField] private float whoDeathDelayBeforeGlitch = 1f;

    [Tooltip("총이 내려가기를 기다리는 최대 시간(초). 이 시간이 지나면 그냥 진행합니다. (안전장치)")]
    [SerializeField] private float whoDeathGunSettleTimeout = 6f;

    [Header("Who 사망 시 피 연출")]
    [Tooltip("Who가 죽는 순간 번져 나가는 핏자국. 비워두면 건너뜁니다. " +
             "아래 whoDeathDelayBeforeGlitch 동안 번지다가 노이즈가 덮습니다.")]
    [SerializeField] private DeskBloodPool deskBloodPool;

    [Tooltip("노이즈 소리가 0에서 최대 볼륨까지 커지는 데 걸리는 시간(초). " +
             "총을 쏜 직후부터 시작되므로, (총 드는 시간 + 내려놓는 시간 + 노이즈 페이드) 정도로 잡으면 " +
             "노이즈가 화면을 완전히 덮는 순간 소리도 최대가 됩니다.")]
    [SerializeField] private float whoDeathSoundRampDuration = 5.5f;

    [Tooltip("노이즈 알파가 0에서 1까지 올라가는 시간(초). 카메라 쉐이크/FOV도 같은 시간 동안 진행됩니다.")]
    [SerializeField] private float whoDeathGlitchFadeIn = 3f;

    [Tooltip("알파가 1이 된 뒤 씬을 다시 로드하기까지 더 기다리는 시간(초). 0이면 곧바로 로드합니다.")]
    [SerializeField] private float whoDeathGlitchHold = 0f;

    [Header("HP 관련 대사 (전용, 직접 지정, 맞은 즉시 재생)")]
    [Tooltip("Who가 1번 남았을 때 재생할 TypeWriter의 dialogueGroups 인덱스 (whoMaxHits가 2 이상일 때만 의미 있음). " +
             "플레이어는 HP가 1이라 '위험' 상태 없이 바로 사망 시퀀스로 갑니다.")]
    [SerializeField] private int enemyLowHpDialogueGroupIndex = 3;

    [Header("라운드 관련 대사 트리거 목록 (특정 라운드 수에만 발동, 총알 소진 시 확인)")]
    [SerializeField] private List<TurnDialogueTrigger> turnDialogueTriggers = new List<TurnDialogueTrigger>();

    [Header("엔딩 씬 전환")]
    [Tooltip("플레이어가 사망 횟수를 다 채운 뒤에도 계속 죽었을 때 엔딩 씬으로 보낼지 여부. " +
             "기본값 false = 플레이어는 아무리 죽어도 씬이 넘어가지 않고 계속 반복됩니다.")]
    [SerializeField] private bool playerDeathEndsGameAfterQuota = false;

    [Tooltip("위 옵션이 켜져 있을 때 플레이어 사망으로 이동할 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string playerDeadEndingSceneName;

    [Tooltip("플레이어가 사망 횟수를 다 채운 뒤 Who(딜러)가 죽었을 때 이동할 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    [SerializeField] private string enemyDeadEndingSceneName;

    [Tooltip("사망 판정 후 엔딩 씬으로 전환되기까지 대기 시간(초). 대사/연출이 끝날 시간을 벌어줌")]
    [SerializeField] private float delayBeforeEndingSceneChange = 1.5f;

    [Header("디버그")]
    [Tooltip("켜면 R키로 플레이어를 즉사시켜 사망 횟수를 테스트할 수 있습니다.")]
    [SerializeField] private bool enableDebugKillKey = true;

    private int playerHitsTaken = 0;
    private int enemyHitsTaken = 0;

    private bool enemyLowHpDialoguePlayed = false;
    private bool enemyLowHpPending = false;

    // 현재 라운드(장전 단위) 수. 1라운드부터 시작하며 총알이 모두 소진되어야 증가함.
    public int CurrentRound { get; private set; } = 1;

    // 플레이어가 지금까지 죽은 횟수.
    // Who가 죽으면 씬을 처음부터 다시 로드하기 때문에, 이 값은 씬을 넘어서도 유지되어야 한다.
    // 그래서 static으로 들고 있고, 메뉴 등에서 GameScene에 새로 들어올 때만 0으로 초기화한다.
    private static int persistentPlayerDeathCount = 0;

    // 다음 씬 로드가 "Who 사망으로 인한 재시작"인지 여부.
    // true면 사망 횟수를 유지하고, false면 새 판으로 보고 0부터 시작한다.
    private static bool keepStoryProgressOnNextLoad = false;

    // Who 사망으로 씬을 다시 시작한 횟수. 0 = 최초 시작.
    // 루프마다 다른 인트로 대사를 재생하는 데 사용한다.
    private static int loopIndex = 0;

    /// <summary>
    /// 현재 몇 번째 루프인지 (0 = 최초 시작).
    /// </summary>
    public int LoopIndex => loopIndex;

    public int PlayerDeathCount => persistentPlayerDeathCount;

    // 사망 횟수를 다 채워서 이제 게임이 끝날 수 있는 상태인지 여부.
    // 기억 파편 모드에서는 "기억을 전부 되찾았는지"로 판단한다. (= 마지막 승부 단계)
    public bool CanGameEnd
    {
        get
        {
            DeathItemSpawner spawner = GetDeathItemSpawner();
            if (spawner != null && spawner.UsesMemoryFragments)
                return spawner.AllFragmentsRecovered(PlayerDeathCount);
            return PlayerDeathCount >= requiredPlayerDeaths;
        }
    }

    // 사망 연출 + 라운드 리셋이 진행 중인지 여부.
    // GunObject는 이 값이 true이면 자신의 hitted 연출과 재장전 트리거를 건너뜁니다.
    public bool IsDeathSequenceRunning { get; private set; } = false;

    public int PlayerRemainingHits => Mathf.Max(0, PlayerMaxHits - playerHitsTaken);
    public int EnemyRemainingHits => Mathf.Max(0, whoMaxHits - enemyHitsTaken);

    // 누군가 죽었을 때 발생 (UI/사운드 등 외부 연출용)
    public event System.Action<Actor> OnActorDied;

    // 플레이어의 사망 횟수가 바뀔 때마다 발생 (인자: 현재 사망 횟수)
    public event System.Action<int> OnPlayerDeathCountChanged;

    /// <summary>
    /// 실탄에 맞았을 때 호출하세요. HP를 깎고, 죽었다면 사망 시퀀스를 시작합니다.
    /// </summary>
    public void RegisterHit(Actor target)
    {
        if (IsDeathSequenceRunning)
        {
            Debug.Log("[GameStateManager] 이미 사망 연출이 진행 중이라 추가 피격을 무시합니다.");
            return;
        }

        if (target == Actor.Player)
        {
            playerHitsTaken++;
            Debug.Log($"[GameStateManager] 플레이어 피격! ({playerHitsTaken}/{PlayerMaxHits})");

            if (playerHitsTaken >= PlayerMaxHits)
                StartDeathSequence(Actor.Player);
        }
        else
        {
            enemyHitsTaken++;
            Debug.Log($"[GameStateManager] Who 피격! ({enemyHitsTaken}/{whoMaxHits})");

            if (EnemyRemainingHits == 1 && !enemyLowHpDialoguePlayed)
            {
                enemyLowHpDialoguePlayed = true;
                enemyLowHpPending = true;
            }

            if (enemyHitsTaken >= whoMaxHits)
                StartDeathSequence(Actor.Enemy);
        }
    }

    /// <summary>
    /// 총알을 모두 소모해 한 라운드가 끝났을 때만 호출하세요. 라운드 수만 증가시킵니다.
    /// </summary>
    public void AdvanceRound()
    {
        CurrentRound++;
        Debug.Log($"[GameStateManager] AdvanceRound() 호출됨. CurrentRound={CurrentRound}");
    }

    /// <summary>
    /// 맞은 직후 즉시 재생해야 할 HP 위험 대사 인덱스가 있으면 반환하고 대기 상태를 해제합니다. 없으면 null.
    /// </summary>
    public int? ConsumeImmediateDialogueGroupIndex()
    {
        if (enemyLowHpPending)
        {
            enemyLowHpPending = false;
            Debug.Log($"[GameStateManager] Who HP 위험 대사 즉시 재생 (groupIndex={enemyLowHpDialogueGroupIndex})");
            return enemyLowHpDialogueGroupIndex;
        }

        return null;
    }

    /// <summary>
    /// 총알이 모두 소진되어 카메라가 고정된 시점에 호출하세요.
    /// 현재 라운드 수와 정확히 일치하는 트리거가 있으면 그 groupIndex를 반환합니다. 없으면 null.
    /// </summary>
    public int? ConsumeDialogueGroupIndexForCurrentState()
    {
        foreach (var trigger in turnDialogueTriggers)
        {
            if (trigger.triggered) continue;
            if (trigger.turnNumber != CurrentRound) continue;

            trigger.triggered = true;

            // 사망 대사와 같은 이유로 여기에도 0~3 금지 규칙을 건다.
            // 이 트리거가 아이템 그룹을 가리키고 있으면 라운드가 바뀔 때 아이템 대사가 튀어나온다.
            int groupIndex = StoryDialogueIndex.Normalize(trigger.dialogueGroupIndex);
            if (groupIndex >= 0 && groupIndex <= 3)
            {
                Debug.LogWarning($"[GameStateManager] 라운드 트리거 \"{trigger.label}\"의 그룹 {groupIndex}은(는) " +
                                 $"사용 금지(0~3)입니다. 엉뚱한 텍스트가 나오지 않도록 건너뜁니다.");
                return null;
            }

            Debug.Log($"[GameStateManager] 라운드 대사 트리거 발동: \"{trigger.label}\" " +
                      $"(round={trigger.turnNumber}, groupIndex={groupIndex})");
            return groupIndex;
        }

        return null;
    }

    // ─────────────────────────────────────────────────────────────
    // 사망 시퀀스
    // ─────────────────────────────────────────────────────────────

    private void StartDeathSequence(Actor dead)
    {
        if (IsDeathSequenceRunning) return;

        IsDeathSequenceRunning = true;

        if (dead == Actor.Player)
        {
            persistentPlayerDeathCount++;
            Debug.Log($"[GameStateManager] 플레이어 사망! (누적 {PlayerDeathCount}/{requiredPlayerDeaths})");

            // 아이템 생성 알림(OnPlayerDeathCountChanged)은 여기서 바로 쏘지 않는다.
            // 여기서 쏘면 hitted 연출이 한창일 때 아이템이 툭 튀어나온다.
            // 연출과 사망 대사가 모두 끝난 뒤 PlayerDeathRoutine에서 알린다.
        }
        else
        {
            Debug.Log($"[GameStateManager] Who 사망! (플레이어 사망 횟수 {PlayerDeathCount}/{requiredPlayerDeaths})");
        }

        OnActorDied?.Invoke(dead);

        StartCoroutine(DeathSequenceRoutine(dead));
    }

    private IEnumerator DeathSequenceRoutine(Actor dead)
    {
        // 사망 연출이 끝날 때까지 플레이어 입력과 AI 행동을 모두 잠근다.
        if (turnManager != null)
            turnManager.SetLocked(true);

        if (dead == Actor.Enemy)
        {
            yield return StartCoroutine(WhoDeathRoutine());
            yield break;
        }

        yield return StartCoroutine(PlayerDeathRoutine());
    }

    // ── 플레이어 사망 ──
    //  총성 → 화면이 확 일그러짐 → 환각 → 위쪽에 아이템 → (누르면) 아이템 대사 → 기억 → 다음 판 대사 → 새 판
    //
    //  1판(???)  : 법정 환각 → "괜찮아?" → 휴대폰(지하철)                     → 2판 대사
    //  2판(???)  : 병원 환각 → 혈액(병원) → 약(병원2)                          → 3판 대사
    //  3판(???)  : 연구실 환각 → 구급상자(거리) → 트레일 등장                  → 4판(트레일)
    //  4판(트레일): 봉투 환각 → "내가 그랬구나" → 17번 기록 → 트레일의 시체      → 5판(???)
    //  5판(???)  : "끝났어." → 사망 엔딩
    private IEnumerator PlayerDeathRoutine()
    {
        DeathDistortion fx = DeathDistortion.Get();
        int deaths = PlayerDeathCount;
        DeathItemSpawner spawner = GetDeathItemSpawner();

        // 1) 총성이 울린 직후 화면이 확 일그러진다. (예전 피격 타임라인 대신)
        yield return new WaitForSeconds(0.15f);
        yield return fx.Hit();

        // 5판에서 죽음 → 사망 엔딩
        if (deaths >= FinalRoundDeaths + 1)
        {
            yield return fx.FadeBlack(1f, 1.2f);
            yield return PlayDialogue(StoryDialogueIndex.PlayerDeathFifth);
            yield return EndingCard.Show("ENDING", "사망");
            yield break;
        }

        // 2) 짧은 환각 → 화면이 깨지듯 현재로
        int hallucination = GetPlayerDeathDialogueIndex() ?? -1;
        fx.SetHallucination(true);
        if (hallucination >= 0)
            yield return PlayDialogue(hallucination);
        yield return fx.Shatter();

        if (deaths == 1) yield return PlayDialogue(StoryDialogueIndex.BackToPresent);
        if (deaths == 4) yield return PlayDialogue(StoryDialogueIndex.AfterFourthDeath);

        // 3) 화면 위쪽에 아이템이 뜬다 → 누르면 아이템 대사 → 그 기억
        OpponentPresenter presenter = OpponentPresenter.Instance;
        if (spawner != null)
        {
            switch (deaths)
            {
                case 1:
                    yield return spawner.PlayFragment(0, 0, StoryDialogueIndex.ItemUseFirst);
                    break;
                case 2:
                    yield return spawner.PlayFragment(1, 1, StoryDialogueIndex.ItemUseSecond);
                    yield return spawner.PlayFragment(2, 2, StoryDialogueIndex.ItemUseThird);
                    break;
                case 3:
                    yield return spawner.PlayFragment(3, 3, StoryDialogueIndex.ItemUseForth);
                    break;
                case 4:
                    yield return spawner.PlayFragment(4, -1, StoryDialogueIndex.NewItem);
                    break;
            }
        }

        // 4) 다음 판으로 넘어가는 장면
        switch (deaths)
        {
            case 1:
                yield return PlayDialogue(StoryDialogueIndex.RoundStartSecond);
                break;
            case 2:
                yield return PlayDialogue(StoryDialogueIndex.RoundStartThird);
                break;
            case 3:
                // 암전 → 문이 열리고 트레일이 맞은편에 앉는다. ???는 옆에 서서 지켜본다.
                yield return fx.FadeBlack(1f, 0.8f);
                PlayOneShot2D(ProceduralSfx.DoorCreak(), 0.9f);
                yield return new WaitForSeconds(0.9f);
                if (presenter != null) presenter.SetStage(OpponentPresenter.Opponent.Trail, true);
                yield return fx.FadeBlack(0f, 0.8f);
                yield return PlayDialogue(StoryDialogueIndex.TrailEnter);
                yield return PlayDialogue(StoryDialogueIndex.RoundStartFourth);
                break;
            case 4:
                // 암전 → 총성 한 발 → 밝아지면 트레일은 시체, ???가 다시 맞은편에
                yield return fx.FadeBlack(1f, 0.8f);
                yield return new WaitForSeconds(0.6f);
                GunObject gun = FindFirstObjectByType<GunObject>();
                if (gun != null) gun.PlayShotSoundOnly(true);
                yield return new WaitForSeconds(1.2f);
                if (presenter != null) presenter.SetStage(OpponentPresenter.Opponent.Who, false);
                yield return fx.FadeBlack(0f, 1f);
                yield return PlayDialogue(StoryDialogueIndex.RoundStartFifth);
                break;
        }

        fx.ClearAll();
        OnPlayerDeathCountChanged?.Invoke(deaths);
        yield return null;

        Debug.Log($"[GameStateManager] 플레이어 사망 {deaths}. {deaths + 1}판을 시작합니다.");
        ResetRound();

        IsDeathSequenceRunning = false;

        // timelineManager가 있으면 Bullet 타임라인이 끝나는 시점에 TimelineManager가
        // 턴 리셋과 잠금 해제를 담당한다. (연출 도중에 턴이 시작되는 것을 막기 위함)
        if (turnManager != null && timelineManager == null)
        {
            turnManager.ResetToRoundStart();
            turnManager.SetLocked(false);
        }
    }

    // ── 상대 사망 ──
    //  맞은편 사람이 책상에 머리를 박으며 쓰러진다.
    //  1~3판(???)  : 노이즈가 화면을 덮고 처음부터 다시 (감 익히는 판)
    //  4판(트레일) : 엔딩 2
    //  5판(???)    : 추리 질문을 한 번에 맞힌 수에 따라 자수 엔딩 / 괴물 엔딩
    private IEnumerator WhoDeathRoutine()
    {
        CamMove.blockLook = true;
        CamMove.blockInteraction = true;

        if (deskBloodPool != null)
            deskBloodPool.Play();

        // 총을 내려놓을 때까지 기다린다.
        if (GunObject.IsSettlingAfterDeathShot)
        {
            float waited = 0f;
            while (GunObject.IsSettlingAfterDeathShot && waited < whoDeathGunSettleTimeout)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // 책상에 머리를 박으며 쓰러진다.
        OpponentPresenter presenter = OpponentPresenter.Instance;
        if (presenter != null)
            yield return presenter.PlayOpponentDeath();
        yield return new WaitForSeconds(1.2f);

        int deaths = PlayerDeathCount;
        DeathDistortion fx = DeathDistortion.Get();

        // 4판: 트레일을 죽임 → 엔딩 2
        if (presenter != null && presenter.Current == OpponentPresenter.Opponent.Trail)
        {
            yield return PlayDialogue(StoryDialogueIndex.TrailKilledEnding);
            yield return fx.FadeBlack(1f, 1.5f);
            yield return EndingCard.Show("ENDING 2", "");
            yield break;
        }

        // 5판: ???를 죽임 → 문서 확인 → 자수 / 괴물
        if (deaths >= FinalRoundDeaths)
        {
            yield return PlayDialogue(StoryDialogueIndex.WhoKilledFinal);

            int correct = DeductionQuestion.FirstTryCorrectCount;
            bool confess = correct >= confessionRequiredCorrect;
            Debug.Log($"[GameStateManager] 추리 질문을 한 번에 맞힌 수 {correct}/{confessionRequiredCorrect} → {(confess ? "자수" : "괴물")} 엔딩");

            if (confess)
            {
                yield return PlayDialogue(StoryDialogueIndex.ConfessionA);
                yield return fx.FadeBlack(1f, 1.5f);
                yield return PlayDialogue(StoryDialogueIndex.ConfessionB);
                yield return EndingCard.Show("ENDING", "자수");
            }
            else
            {
                yield return PlayDialogue(StoryDialogueIndex.MonsterA);
                fx.SetHallucination(true);
                yield return new WaitForSeconds(1f);
                yield return PlayDialogue(StoryDialogueIndex.MonsterB);
                yield return fx.FadeBlack(1f, 1.5f);
                yield return EndingCard.Show("ENDING", "괴물");
            }
            yield break;
        }

        // 1~3판: 노이즈가 화면을 덮고 처음부터 다시
        if (glitchEffect != null)
            glitchEffect.StartSound(whoDeathSoundRampDuration * 0.5f);
        if (cameraGlitchEffect != null)
            cameraGlitchEffect.Play(whoDeathGlitchFadeIn);
        if (glitchEffect != null)
        {
            bool glitchDone = false;
            glitchEffect.Play(whoDeathGlitchFadeIn, whoDeathGlitchHold, () => glitchDone = true);
            yield return new WaitUntil(() => glitchDone);
        }
        else
        {
            yield return new WaitForSeconds(whoDeathGlitchFadeIn + whoDeathGlitchHold);
        }

        Debug.Log($"[GameStateManager] {deaths + 1}판에서 ???를 쐈다. 처음부터 다시 시작합니다.");
        RestartFromBeginning();
    }

    // 대사 그룹 하나를 재생하고 끝날 때까지 기다린다.
    private IEnumerator PlayDialogue(int groupIndex)
    {
        if (timelineManager == null || groupIndex < 0) yield break;
        bool done = false;
        timelineManager.PlayImmediateDialogue(groupIndex, () => done = true);
        yield return new WaitUntil(() => done);
        CamMove.blockLook = true;
        CamMove.blockInteraction = true;
    }

    private AudioSource sfx2D;
    private void PlayOneShot2D(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (sfx2D == null)
        {
            sfx2D = gameObject.AddComponent<AudioSource>();
            sfx2D.playOnAwake = false;
            sfx2D.spatialBlend = 0f;
        }
        sfx2D.PlayOneShot(clip, volume);
    }

    // 현재 씬을 처음부터 다시 로드한다. 사망 횟수(스토리 진행도)는 static이라 그대로 유지된다.
    private void RestartFromBeginning()
    {
        keepStoryProgressOnNextLoad = true;
        loopIndex++;

        Scene active = SceneManager.GetActiveScene();
        Debug.Log($"[GameStateManager] '{active.name}' 씬을 처음부터 다시 로드합니다. " +
                  $"(사망 횟수 {PlayerDeathCount} 유지)");

        SceneManager.LoadScene(active.buildIndex >= 0 ? active.buildIndex : SceneManager.GetActiveScene().buildIndex);
    }

    // 몇 번째 죽음인지에 따라 재생할 플레이어 사망 대사 인덱스를 고른다. 없으면 null.
    //
    // dialogueGroups는 하나의 평평한 배열을 여러 시스템(인트로 / 아이템 사용 / 사망)이 나눠 쓴다.
    // 그래서 인덱스가 겹치면 "죽었는데 아이템 대사가 나오는" 식으로 상황에 맞지 않는 텍스트가 재생된다.
    // 인트로 경로(GetIntroDialogueGroupIndex)에는 이미 0~3 금지 규칙이 있었는데
    // 사망 경로에만 빠져 있어서 Item1~Item4가 사망 대사로 흘러나왔다.
    private int? GetPlayerDeathDialogueIndex()
    {
        int i = PlayerDeathCount - 1; // 첫 번째 죽음 = 0번 인덱스
        if (playerDeathDialogueGroupIndexes == null || i < 0 || i >= playerDeathDialogueGroupIndexes.Count)
            return null;

        // 인스펙터에 구버전 인덱스(10~16)가 남아있을 수 있으므로 항상 정규화한다.
        // (Normalize를 빠뜨리면 리인덱싱 이전 값이 엉뚱한 그룹을 가리켜 대사 순서가 뒤섞인다)
        int groupIndex = StoryDialogueIndex.Normalize(playerDeathDialogueGroupIndexes[i]);

        // 음수는 "이번 죽음엔 대사 없음"을 뜻한다.
        if (groupIndex < 0)
            return null;

        // 0~3은 레거시 슬롯으로 사용 금지
        if (groupIndex <= 3)
        {
            Debug.LogWarning($"[GameStateManager] 사망 대사 그룹 {groupIndex}은(는) 사용 금지(0~3)입니다. " +
                             $"({PlayerDeathCount}번째 사망) 엉뚱한 텍스트가 나오지 않도록 대사를 건너뜁니다.");
            return null;
        }

        // 아이템 사용 / 인트로가 쓰는 그룹을 사망 대사로 재사용하면 같은 텍스트가 두 번 나온다.
        if (groupIndex == StoryDialogueIndex.IntroMain ||
            (groupIndex >= StoryDialogueIndex.ItemUseFirst && groupIndex <= StoryDialogueIndex.ItemUseForth))
        {
            Debug.LogWarning($"[GameStateManager] 사망 대사 그룹 {groupIndex}은(는) 이미 " +
                             $"{(groupIndex == StoryDialogueIndex.IntroMain ? "인트로" : "아이템 사용")}가 쓰는 그룹입니다. " +
                             $"중복 재생을 막기 위해 건너뜁니다. 사망 전용 그룹을 따로 만들어 지정하세요.");
            return null;
        }

        return groupIndex;
    }

    private DeathItemSpawner GetDeathItemSpawner()
    {
        if (deathItemSpawner == null)
            deathItemSpawner = FindFirstObjectByType<DeathItemSpawner>(FindObjectsInactive.Include);
        return deathItemSpawner;
    }

    // 양쪽 HP를 되돌리고 탄약을 새로 장전해서 라운드를 처음부터 다시 시작
    private void ResetRound()
    {
        playerHitsTaken = 0;
        enemyHitsTaken = 0;
        enemyLowHpDialoguePlayed = false;
        enemyLowHpPending = false;

        if (timelineManager != null)
        {
            // 재장전 + Bullet 타임라인 재생까지 한 번에 처리
            timelineManager.StartNewRoundImmediately();
        }
        else if (ammoManager != null)
        {
            Debug.LogWarning("[GameStateManager] timelineManager가 없어 탄약만 재장전합니다 (Bullet 연출 생략).");
            ammoManager.LoadShells();
        }
        else
        {
            Debug.LogWarning("[GameStateManager] timelineManager와 ammoManager가 모두 없어 라운드를 리셋하지 못했습니다!");
        }
    }

    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!enableDebugKillKey) return;
        if (IsDeathSequenceRunning) return;

        // R키를 누르면 테스트용으로 플레이어를 즉사시킴 (사망 횟수 누적 확인용)
        if (Input.GetKeyDown(KeyCode.R))
            RegisterHit(Actor.Player);
    }

    private void Awake()
    {
        // Who 사망으로 인한 재시작이면 사망 횟수를 그대로 이어받고,
        // 그렇지 않으면(메뉴에서 새로 들어온 경우 등) 새 판으로 보고 0부터 시작한다.
        if (keepStoryProgressOnNextLoad)
        {
            keepStoryProgressOnNextLoad = false;
            Debug.Log($"[GameStateManager] Who 사망 재시작으로 진입. 사망 횟수 {persistentPlayerDeathCount} 유지.");
        }
        else
        {
            persistentPlayerDeathCount = 0;
            loopIndex = 0;
            MemoryNotebook.ClearAll();
            DeductionQuestion.ResetScore();
            Debug.Log("[GameStateManager] 새 판으로 진입. 사망 횟수와 루프 수를 0으로 초기화합니다.");
        }

        ApplyDefaultLoopIntroGroupsIfNeeded();
        ApplyDefaultPlayerDeathDialogueGroupsIfNeeded();
        NormalizeLoopIntroDialogueGroupIndexes();
    }

    private void ApplyDefaultLoopIntroGroupsIfNeeded()
    {
        // 인스펙터에 저장된 옛 인덱스가 남아 있어도 무시하고 항상 상수로 덮어쓴다.
        loopIntroDialogueGroupIndexes = new List<int>
        {
            StoryDialogueIndex.IntroMain,
            StoryDialogueIndex.WhoKilledFirst,
            StoryDialogueIndex.WhoKilledSecond
        };
    }

    private void NormalizeLoopIntroDialogueGroupIndexes()
    {
        if (loopIntroDialogueGroupIndexes == null)
            return;

        for (int i = 0; i < loopIntroDialogueGroupIndexes.Count; i++)
            loopIntroDialogueGroupIndexes[i] = StoryDialogueIndex.Normalize(loopIntroDialogueGroupIndexes[i]);
    }

    private void ApplyDefaultPlayerDeathDialogueGroupsIfNeeded()
    {
        playerDeathDialogueGroupIndexes = new List<int>
        {
            StoryDialogueIndex.PlayerDeathFirst,
            StoryDialogueIndex.PlayerDeathSecond,
            StoryDialogueIndex.PlayerDeathThird,
            StoryDialogueIndex.PlayerDeathFourth,
            StoryDialogueIndex.PlayerDeathFifth
        };
    }

    /// <summary>
    /// 메뉴로 나가는 등 스토리 진행도를 완전히 초기화하고 싶을 때 호출하세요.
    /// </summary>
    public static void ResetStoryProgress()
    {
        persistentPlayerDeathCount = 0;
        keepStoryProgressOnNextLoad = false;
        loopIndex = 0;
    }

    /// <summary>
    /// 이번 루프에서 재생할 인트로 대사 그룹 인덱스를 돌려줍니다.
    /// TimelineManager가 wakeup 타임라인이 끝난 뒤 이 값을 물어봅니다.
    /// </summary>
    public int GetIntroDialogueGroupIndex()
    {
        // 주의: 예전에는 여기서 loopIndex > 0이면 무조건 -1을 반환했다.
        // 그런데 Who를 죽였을 때 나와야 하는 대사(Who_Killed_First/Second)가 바로 그
        // "재시작 루프"에 배정되어 있어서, 그 대사들이 영원히 재생되지 않았다.
        // "재시작 때 긴 인트로를 다시 틀지 않는다"는 원래 요구사항은
        // 아래에서 IntroMain일 때만 걸러내는 것으로 충분하다.

        if (loopIntroDialogueGroupIndexes == null || loopIntroDialogueGroupIndexes.Count == 0)
            return loopIndex == 0 ? StoryDialogueIndex.IntroMain : -1;

        // 루프가 목록보다 길어지면 마지막 값을 계속 사용
        int i = Mathf.Clamp(loopIndex, 0, loopIntroDialogueGroupIndexes.Count - 1);
        int groupIndex = StoryDialogueIndex.Normalize(loopIntroDialogueGroupIndexes[i]);

        // 음수는 "이번 루프엔 대사 없음"
        if (groupIndex < 0)
        {
            Debug.Log($"[GameStateManager] {loopIndex}번째 루프는 대사가 지정되어 있지 않습니다. 건너뜁니다.");
            return -1;
        }

        // 0~3은 레거시 슬롯으로 사용 금지
        if (groupIndex <= 3)
        {
            Debug.LogWarning($"[GameStateManager] 인트로 그룹 {groupIndex}은(는) 사용 금지(0~3)입니다. 인트로를 건너뜁니다.");
            return -1;
        }

        // 재시작 루프에서 인트로 본문(10줄)을 다시 트는 것만 막는다.
        if (loopIndex > 0 && groupIndex == StoryDialogueIndex.IntroMain)
        {
            Debug.Log($"[GameStateManager] {loopIndex}번째 루프에 인트로 본문이 배정되어 있어 건너뜁니다. " +
                      $"(재시작 때는 인트로를 반복하지 않습니다)");
            return -1;
        }

        Debug.Log($"[GameStateManager] {loopIndex}번째 루프의 대사 그룹: {groupIndex}");
        return groupIndex;
    }

    private void Start()
    {
        // 몇 번 죽었는지에 맞춰 맞은편 사람을 앉힌다. (1~3판 ???, 4판 트레일, 5판 ??? + 트레일 시체)
        OpponentPresenter presenter = OpponentPresenter.Instance != null
            ? OpponentPresenter.Instance
            : FindFirstObjectByType<OpponentPresenter>(FindObjectsInactive.Include);
        if (presenter != null) presenter.ApplyForDeaths(PlayerDeathCount);

        Debug.Log($"[GameStateManager] 현재 씬 빌드 인덱스: {SceneManager.GetActiveScene().buildIndex} / " +
                  $"플레이어 HP={PlayerMaxHits}, Who HP={whoMaxHits}, 필요 사망 횟수={requiredPlayerDeaths}");
    }

    private IEnumerator LoadEndingSceneRoutine(string targetSceneName)
    {
        if (string.IsNullOrEmpty(targetSceneName))
        {
            Debug.LogWarning("[GameStateManager] 엔딩 씬 이름이 비어있습니다! 인스펙터에서 지정하세요.");
            yield break;
        }

        // 마지막 승부에서 이겨 엔딩으로 넘어갈 때는 BGM을 서서히 페이드아웃한다.
        if (BGMManager.Instance != null)
            BGMManager.Instance.FadeOutAndStop(Mathf.Max(0.1f, delayBeforeEndingSceneChange));

        if (delayBeforeEndingSceneChange > 0f)
            yield return new WaitForSeconds(delayBeforeEndingSceneChange);

        if (SceneTransition.Instance != null)
        {
            int buildIndex = GetBuildIndexByName(targetSceneName);
            if (buildIndex >= 0)
            {
                SceneTransition.Instance.LoadScene(buildIndex);
                yield break;
            }

            Debug.LogWarning($"[GameStateManager] '{targetSceneName}' 씬을 Build Settings에서 찾지 못해 " +
                             "SceneTransition을 사용하지 못했습니다. 이름으로 직접 로드합니다.");
        }

        SceneManager.LoadScene(targetSceneName);
    }

    // Build Settings에 등록된 씬 이름으로 build index를 찾는 헬퍼
    private int GetBuildIndexByName(string name)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameAtIndex = Path.GetFileNameWithoutExtension(path);
            if (sceneNameAtIndex == name)
                return i;
        }
        return -1;
    }
}