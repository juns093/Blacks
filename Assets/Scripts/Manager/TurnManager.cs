using UnityEngine;

// 플레이어 <-> Who(딜러) 턴제 진행을 관리하는 클래스.
//
// 턴 규칙 (벅샷 룰렛 기본 룰):
//  - 상대에게 쐈다        -> 실탄/공탄 상관없이 턴이 넘어감
//  - 자기 자신에게 공탄   -> 턴을 그대로 유지 (한 번 더 행동)
//  - 자기 자신에게 실탄   -> 피해를 입고 턴이 넘어감
//
// "자기 자신에게 공탄이면 턴 유지"가 이 게임의 핵심 선택지다.
// 자해는 도박이지만 성공하면 턴을 벌고, 실패하면 죽는다.
//
// 씬에 하나만 두세요. GunObject / WhoAiManager / GameStateManager가 이 클래스를 참조합니다.
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    public enum Turn { Player, Who }

    [Header("연동")]
    [Tooltip("Who(딜러)의 턴이 되었을 때 행동을 위임할 AI 매니저")]
    [SerializeField] private WhoAiManager whoAi;

    [Header("설정")]
    [Tooltip("라운드가 시작될 때(장전 직후, 사망 리셋 직후) 누구의 턴부터 시작할지")]
    [SerializeField] private Turn startingTurn = Turn.Player;

    [Tooltip("턴이 Who로 넘어간 뒤 AI가 실제로 행동하기까지의 대기 시간(초). 연출 호흡용")]
    [SerializeField] private float whoTurnStartDelay = 0.8f;

    // 현재 턴의 주인
    public Turn Current { get; private set; } = Turn.Player;

    // 연출(타임라인/대사/사망 시퀀스)이 진행 중이라 아무도 행동하면 안 되는 상태
    public bool IsLocked { get; private set; } = false;

    // GunObject가 "지금 플레이어가 총을 집어도 되는가"를 판단할 때 사용
    public bool IsPlayerTurn => Current == Turn.Player && !IsLocked;

    // 턴이 바뀔 때마다 발생 (UI 표시 등에 사용)
    public event System.Action<Turn> OnTurnChanged;

    private Coroutine whoTurnRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TurnManager] 씬에 인스턴스가 두 개 이상 존재합니다. 하나만 두세요.");
            Destroy(this);
            return;
        }
        Instance = this;
        Current = startingTurn;
    }

    /// <summary>
    /// 한 발을 쏜 뒤 호출하세요. 벅샷 룰렛 룰에 따라 턴을 유지할지 넘길지 결정합니다.
    /// </summary>
    /// <param name="shooter">방금 쏜 사람</param>
    /// <param name="shotSelf">자기 자신에게 쐈는지 여부</param>
    /// <param name="wasLive">실탄이었는지 여부</param>
    public void ResolveShot(Turn shooter, bool shotSelf, bool wasLive)
    {
        // 자기 자신에게 공탄 -> 턴 유지 (한 번 더 행동)
        bool keepTurn = shotSelf && !wasLive;

        if (keepTurn)
        {
            Debug.Log($"[TurnManager] {shooter}가 자신에게 공탄 -> 턴을 유지합니다.");
            SetTurn(shooter);
            return;
        }

        Turn next = shooter == Turn.Player ? Turn.Who : Turn.Player;

        Debug.Log($"[TurnManager] {shooter}가 {(shotSelf ? "자신" : "상대")}에게 " +
                  $"{(wasLive ? "실탄" : "공탄")} 발사 -> {next}에게 턴을 넘깁니다.");

        SetTurn(next);
    }

    /// <summary>
    /// 라운드 시작/사망 리셋 등에서 턴을 강제로 지정할 때 사용합니다.
    /// </summary>
    public void ForceTurn(Turn turn)
    {
        Debug.Log($"[TurnManager] 턴을 강제로 {turn}(으)로 설정합니다.");
        SetTurn(turn);
    }

    /// <summary>
    /// 라운드가 새로 시작될 때(장전 직후) 호출하세요. startingTurn으로 되돌립니다.
    /// </summary>
    public void ResetToRoundStart()
    {
        ForceTurn(startingTurn);
    }

    /// <summary>
    /// 타임라인/대사/사망 연출 중에는 true로 잠가서 플레이어 입력과 AI 행동을 모두 막습니다.
    /// 잠금이 풀리는 순간 Who의 턴이라면 AI가 이어서 행동합니다.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if (IsLocked == locked) return;

        IsLocked = locked;
        Debug.Log($"[TurnManager] 잠금 상태 변경: {locked}");

        ApplyCameraBlockForCurrentTurn();

        if (!locked && Current == Turn.Who)
            BeginWhoTurn();
        else if (locked)
            CancelWhoTurn();
    }

    /// <summary>
    /// 현재 턴 상태에 맞춰 카메라 회전/상호작용을 허용하거나 막습니다.
    /// 플레이어의 턴이 아니면(= Who의 턴이거나 연출 잠금 중이면) 화면이 움직이지 않습니다.
    ///
    /// 타임라인이 끝나서 카메라를 풀어주려는 쪽(TimelineManager, GunObject)은
    /// CamMove 플래그를 직접 false로 쓰지 말고 이 메서드를 호출하세요.
    /// 그래야 "Who의 턴인데 카메라가 풀려버리는" 상황이 생기지 않습니다.
    /// </summary>
    public static void ApplyCameraBlockForCurrentTurn()
    {
        bool block = Instance != null && !Instance.IsPlayerTurn;

        CamMove.blockLook = block;
        CamMove.blockInteraction = block;
    }

    private void SetTurn(Turn turn)
    {
        CancelWhoTurn();

        Current = turn;
        OnTurnChanged?.Invoke(turn);

        // 턴이 바뀌는 즉시 카메라 잠금 상태를 맞춘다.
        // (Who의 턴이 되면 플레이어는 화면을 돌릴 수 없다)
        ApplyCameraBlockForCurrentTurn();

        if (turn == Turn.Who && !IsLocked)
            BeginWhoTurn();
    }

    private void BeginWhoTurn()
    {
        if (whoAi == null)
        {
            Debug.LogWarning("[TurnManager] whoAi가 연결되어 있지 않아 Who의 턴을 진행할 수 없습니다! " +
                             "인스펙터에서 WhoAiManager를 연결하세요.");
            return;
        }

        CancelWhoTurn();
        whoTurnRoutine = StartCoroutine(WhoTurnRoutine());
    }

    private void CancelWhoTurn()
    {
        if (whoTurnRoutine != null)
        {
            StopCoroutine(whoTurnRoutine);
            whoTurnRoutine = null;
        }
    }

    private System.Collections.IEnumerator WhoTurnRoutine()
    {
        if (whoTurnStartDelay > 0f)
            yield return new WaitForSeconds(whoTurnStartDelay);

        whoTurnRoutine = null;

        // 대기하는 사이에 상황이 바뀌었을 수 있으므로 다시 확인
        if (IsLocked || Current != Turn.Who)
            yield break;

        whoAi.TakeTurn();
    }
}
