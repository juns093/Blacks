using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 총알(실탄/공탄) 장전과 발사를 관리하는 클래스
// GunObject에서 이 컴포넌트를 참조해서 사용합니다.
public class AmmoManager : MonoBehaviour
{
    [Header("Ammo Settings")]
    [Tooltip("한 라운드에 장전되는 총알의 최소 개수")]
    [SerializeField] private int minTotalBullets = 2;

    [Tooltip("한 라운드에 장전되는 총알의 최대 개수 (맥스)")]
    [SerializeField] private int maxTotalBullets = 8;

    [Header("실탄 개수 고정")]
    [Tooltip("켜면 실탄 개수를 아래 값으로 고정합니다. 나머지는 전부 공탄이 됩니다. " +
             "실탄이 적을수록 공탄 확률이 높아져서 Who가 자기 자신을 쏘는 빈도가 올라갑니다.")]
    [SerializeField] private bool useFixedLiveRounds = true;

    [Tooltip("고정할 실탄 개수. 최소 1발은 실탄, 최소 1발은 공탄이 되도록 자동으로 보정됩니다.")]
    [SerializeField] private int fixedLiveRounds = 1;

    [Tooltip("켜면 실탄 개수가 절대로 공탄 개수를 넘지 못하게 합니다(실탄 <= 공탄). " +
             "공탄 확률이 항상 50% 이상이 되므로 Who가 자기 자신을 쏘는 도박을 걸 여지가 생깁니다.")]
    [SerializeField] private bool neverMoreLiveThanBlank = true;

    [Header("Bullet Visual (Bullet 타임라인 연출용)")]
    [Tooltip("실탄일 때 waypoint에 소환할 프리팹")]
    [SerializeField] private GameObject liveBulletPrefab;

    [Tooltip("공탄일 때 waypoint에 소환할 프리팹")]
    [SerializeField] private GameObject blankBulletPrefab;

    [Tooltip("총알이 소환될 위치(Vector3) 목록 (챔버 슬롯 순서대로, 최대 8개 = maxTotalBullets). " +
             "인스펙터에서 X, Y, Z 좌표 값을 직접 입력하세요.")]
    [SerializeField] private List<Vector3> bulletPositions = new List<Vector3>();

    [Tooltip("소환된 총알 프리팹들이 화면에 보여지는 시간(초). 이 시간이 지나면 자동으로 전부 제거됨")]
    [SerializeField] private float bulletShowDuration = 0.4f;

    // 현재 소환되어 있는 총알 프리팹 인스턴스 목록 (제거 처리를 위해 추적)
    private List<GameObject> spawnedBulletVisuals = new List<GameObject>();

    // 실제 총알 순서 리스트 (true = 실탄, false = 공탄), 섞은 뒤 순서대로 소비
    private List<bool> chamber = new List<bool>();
    private int currentIndex = 0;

    [Tooltip("이번 라운드에 장전된 총알 총 개수 (읽기 전용, 자동 계산됨)")]
    public int TotalBullets { get; private set; }

    [Tooltip("이번 라운드의 실탄 개수 (읽기 전용, 자동 계산됨)")]
    public int LiveRounds { get; private set; }

    [Tooltip("이번 라운드의 공탄 개수 (읽기 전용, 자동 계산됨)")]
    public int BlankRounds { get; private set; }

    // 아직 발사되지 않고 남은 총알 개수
    public int RemainingBullets => chamber.Count - currentIndex;

    // ── Who(AI)의 판단용: 아직 발사되지 않은 탄 중 실탄/공탄이 각각 몇 발 남았는지 ──
    // 챔버의 "순서"는 알려주지 않고 "개수"만 알려줍니다.
    // 즉 AI는 플레이어와 동일한 정보(남은 실탄 수 / 남은 공탄 수)만 보고 확률로 판단합니다.
    public int RemainingLive
    {
        get
        {
            int count = 0;
            for (int i = currentIndex; i < chamber.Count; i++)
                if (chamber[i]) count++;
            return count;
        }
    }

    public int RemainingBlank => RemainingBullets - RemainingLive;

    // 다음 한 발이 실탄일 확률 (0~1). 남은 총알이 없으면 0.
    public float LiveProbability
    {
        get
        {
            int remaining = RemainingBullets;
            return remaining <= 0 ? 0f : (float)RemainingLive / remaining;
        }
    }

    // 마지막 총알까지 전부 소진되었을 때 알려주는 이벤트
    // (TimelineManager 등에서 구독해서 재장전 + 타임라인 재생을 트리거)
    public event System.Action OnAmmoDepleted;

    // 재장전이 완료되었을 때 발생하는 이벤트 (LoadShells가 끝난 직후)
    public event System.Action OnReloaded;

    // 라운드 시작 시(또는 총알이 다 떨어졌을 때) 새로 장전
    public void LoadShells()
    {
        // 1. 총알 총 개수: 2~8 랜덤
        TotalBullets = Random.Range(minTotalBullets, maxTotalBullets + 1); // Random.Range(int,int)는 max 미포함이라 +1

        // 2. 실탄 개수 결정
        if (useFixedLiveRounds)
        {
            // 실탄을 고정한다. 단 실탄 0발이나 공탄 0발이 되지 않도록 범위를 제한한다.
            // (실탄 0발이면 아무도 죽지 않고, 공탄 0발이면 첫 발에 바로 죽는다)
            LiveRounds = Mathf.Clamp(fixedLiveRounds, 1, TotalBullets - 1);
        }
        else
        {
            // 기존 밸런스: 실탄과 공탄의 차이가 1발 이하가 되도록
            int half = TotalBullets / 2;
            if (TotalBullets % 2 == 0)
                LiveRounds = half;                        // 짝수면 완벽한 50:50
            else
                LiveRounds = half + Random.Range(0, 2);   // 홀수면 어느 한쪽이 1발 많게
        }

        // 실탄이 공탄보다 많아지면 공탄 확률이 50% 밑으로 떨어져서
        // Who가 자기 자신을 쏠 이유가 사라진다. 그래서 실탄을 절반 이하로 묶는다.
        if (neverMoreLiveThanBlank)
        {
            int maxLive = Mathf.Max(1, TotalBullets / 2);
            if (LiveRounds > maxLive)
            {
                Debug.Log($"[AmmoManager] 실탄 {LiveRounds}발 -> {maxLive}발로 제한 (실탄이 공탄보다 많아지지 않도록)");
                LiveRounds = maxLive;
            }
        }

        BlankRounds = TotalBullets - LiveRounds;

        // 3. 총알 리스트 구성 후 셔플 (순서를 랜덤하게 섞음)
        chamber.Clear();
        for (int i = 0; i < LiveRounds; i++) chamber.Add(true);
        for (int i = 0; i < BlankRounds; i++) chamber.Add(false);
        Shuffle(chamber);

        currentIndex = 0;

        Debug.Log($"[AmmoManager] 장전 완료 - 총 {TotalBullets}발 (실탄 {LiveRounds} / 공탄 {BlankRounds})");

        OnReloaded?.Invoke(); // ← 추가
    }

    // Fisher-Yates 셔플 알고리즘
    private void Shuffle(List<bool> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // 다음 총알을 발사하고 실탄 여부(true=실탄, false=공탄)를 반환
    // 총알이 없으면 false를 반환하며 경고 로그를 남김 (호출 전에 RemainingBullets로 확인 권장)
    // 이 발사로 인해 남은 총알이 0이 되면 OnAmmoDepleted 이벤트를 발생시켜
    // 재장전 + 타임라인 재연출을 트리거할 수 있게 함
    public bool FireNextShell()
    {
        if (RemainingBullets <= 0)
        {
            Debug.LogWarning("[AmmoManager] 남은 총알이 없습니다! LoadShells()로 다시 장전하세요.");
            return false;
        }

        bool isLive = chamber[currentIndex];
        currentIndex++;

        // 방금 발사한 총알이 마지막 한 발이었다면(= 이제 남은 총알이 0) 소진 이벤트 발생
        if (RemainingBullets <= 0)
        {
            Debug.Log("[AmmoManager] 총알이 모두 소진되었습니다. OnAmmoDepleted 이벤트 발생.");
            OnAmmoDepleted?.Invoke();
        }

        return isLive;
    }

    // ── Bullet 타임라인 연출용: 장전된 총알(chamber) 순서대로 각 waypoint에 실탄/공탄 프리팹을 소환하고,
    //    bulletShowDuration 후 소환된 것들을 전부 자동 제거 ──
    // Bullet 타임라인 안의 Signal Emitter(트랙)에 Signal Receiver를 붙여서
    // 이 메서드를 호출하도록 연결하면 됩니다. (또는 TimelineManager에서 bullet.Play() 시점에 직접 호출)
    // 소환된 총알 비주얼을 자동으로 제거하는 코루틴 핸들 (중복 호출 시 이전 코루틴을 정지시키기 위해 보관)
    private Coroutine spawnBulletCoroutine;

    public void SpawnBulletVisual()
    {
        if (bulletPositions == null || bulletPositions.Count == 0)
        {
            Debug.LogWarning("[AmmoManager] bulletPositions가 비어있습니다! 인스펙터에서 직접 좌표를 등록하세요.");
            return;
        }

        if (liveBulletPrefab == null || blankBulletPrefab == null)
        {
            Debug.LogWarning("[AmmoManager] liveBulletPrefab 또는 blankBulletPrefab이 연결되어 있지 않습니다!");
            return;
        }

        // 이전에 진행 중이던 소환/제거 코루틴이 있다면 먼저 정지시켜서
        // 늦게 실행되는 ClearSpawnedBulletVisuals()가 방금 생성한 총알을 지워버리는 것을 방지
        if (spawnBulletCoroutine != null)
            StopCoroutine(spawnBulletCoroutine);

        spawnBulletCoroutine = StartCoroutine(SpawnBulletRoutine());
    }

    private IEnumerator SpawnBulletRoutine()
    {
        // 혹시 이전에 소환된 게 남아있다면 정리 후 새로 소환
        ClearSpawnedBulletVisuals();

        // 챔버(장전된 총알) 순서대로 설정된 위치 좌표에 실탄/공탄 프리팹을 배치
        // 총알 개수(TotalBullets)와 입력한 좌표 개수 중 더 작은 쪽까지만 소환
        int count = Mathf.Min(chamber.Count, bulletPositions.Count);
        if (count == 0)
        {
            Debug.LogWarning($"[AmmoManager] 소환할 총알이 없습니다. chamber.Count={chamber.Count}, " +
                              "positions.Count={bulletPositions.Count}. LoadShells()가 먼저 호출되었는지 확인하세요.");
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = bulletPositions[i];
            bool isLive = chamber[i];
            GameObject prefab = isLive ? liveBulletPrefab : blankBulletPrefab;
            if (prefab == null) continue;

            // 공탄 프리팹의 중심축이 쏠려있는 현상(오프셋) 보정
            if (!isLive)
            {
                pos.x -= 0.262000024f;
            }

            // 인스펙터에 입력해주신 절대 좌표(World Space) 그대로 스폰!
            GameObject spawned = Instantiate(prefab, pos, prefab.transform.rotation);
            // 프리팹 원본이 비활성 상태로 저장되어 있어도 화면에 보이도록 강제로 활성화
            spawned.SetActive(true);
            spawnedBulletVisuals.Add(spawned);
        }

        yield return new WaitForSeconds(bulletShowDuration);

        ClearSpawnedBulletVisuals();
        spawnBulletCoroutine = null;
    }

    // 타임라인 등에서 수동으로 켜고 끄기 위한 메서드
    public void ShowBulletVisual()
    {
        if (bulletPositions == null || bulletPositions.Count == 0)
        {
            Debug.LogWarning("[AmmoManager] bulletPositions가 비어있습니다! 인스펙터에서 좌표를 등록해주세요.");
            return;
        }
        if (liveBulletPrefab == null || blankBulletPrefab == null)
        {
            Debug.LogWarning("[AmmoManager] 프리팹(liveBulletPrefab 또는 blankBulletPrefab)이 없습니다!");
            return;
        }

        Debug.Log($"[AmmoManager] ShowBulletVisual 호출됨. chamber.Count={chamber.Count}, positions={bulletPositions.Count}");

        if (spawnBulletCoroutine != null)
        {
            StopCoroutine(spawnBulletCoroutine);
            spawnBulletCoroutine = null;
        }

        ClearSpawnedBulletVisuals();

        int count = Mathf.Min(chamber.Count, bulletPositions.Count);
        for (int i = 0; i < count; i++)
        {
            Vector3 pos = bulletPositions[i];
            bool isLive = chamber[i];
            GameObject prefab = isLive ? liveBulletPrefab : blankBulletPrefab;
            if (prefab == null) continue;

            // 공탄 프리팹의 중심축이 쏠려있는 현상(오프셋) 보정
            if (!isLive)
            {
                pos.x -= 0.262000024f;
            }

            // 인스펙터에 입력해주신 절대 좌표(World Space) 그대로 스폰!
            GameObject spawned = Instantiate(prefab, pos, prefab.transform.rotation);
            spawned.SetActive(true);
            spawnedBulletVisuals.Add(spawned);
        }
    }

    public void HideBulletVisual()
    {
        if (spawnBulletCoroutine != null)
        {
            StopCoroutine(spawnBulletCoroutine);
            spawnBulletCoroutine = null;
        }
        ClearSpawnedBulletVisuals();
    }

    // 소환되어 있던 총알 프리팹 인스턴스들을 전부 제거
    private void ClearSpawnedBulletVisuals()
    {
        for (int i = 0; i < spawnedBulletVisuals.Count; i++)
        {
            if (spawnedBulletVisuals[i] != null)
                Destroy(spawnedBulletVisuals[i]);
        }
        spawnedBulletVisuals.Clear();
    }
}
