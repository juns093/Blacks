using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

// 플레이어가 죽을 때마다 기억을 하나씩 돌려주는 스크립트.
//
// ── 기억 파편 모드 (기본, useMemoryFragments) ──
//  - 첫 번째 죽음: 아무것도 없음. 죽는 고통만 보여준다.
//  - 두 번째 죽음부터: 눈을 감은 어두운 화면에 기억 파편이 하나씩 떠오른다. (MemoryFragmentScreen)
//    파편을 누르면 그 기억(타임라인 / 탐색 구간)으로 들어간다.
//    2번째 죽음 -> 기억 1, 3번째 -> 기억 2, 4번째 -> 기억 3, 5번째 -> 기억 4
//  - 순서 제어는 GameStateManager의 사망 연출이 이 스크립트의 코루틴을 기다리는 방식으로 한다.
//
// ── 테이블 아이템 모드 (useMemoryFragments 끔, 예전 방식) ──
// 플레이어가 죽을 때마다 테이블 위에 아이템을 하나씩 올려놓는다.
//
// ── 동작 ──
//  - 플레이어가 1번 죽으면 spawnSpots[0]에, 2번 죽으면 spawnSpots[1]에... 순서대로 놓입니다.
//  - spawnSpots는 인스펙터에서 "반시계 방향" 순서로 넣어두면 그 순서대로 나옵니다.
//  - 놓을 자리가 떨어지면 더 이상 놓지 않습니다.
//
// ── 루프(씬 재로드)와의 관계 ──
// Who가 죽으면 씬이 통째로 다시 로드되지만, 플레이어의 사망 횟수는 static이라 유지됩니다.
// 그래서 씬이 시작될 때 "이미 죽은 횟수만큼" 아이템을 즉시 채워 넣습니다.
// 그렇지 않으면 루프를 돌 때마다 테이블이 비어 보여서 진행 상황이 사라집니다.
public class DeathItemSpawner : MonoBehaviour
{
    [Header("연동")]
    [Tooltip("사망 횟수를 알려줄 GameStateManager")]
    [SerializeField] private GameStateManager gameStateManager;

    [Header("기억 파편 모드")]
    [Tooltip("켜면 테이블에 아이템을 놓지 않고, 죽었을 때 어두운 화면에 기억 파편을 띄웁니다.")]
    [SerializeField] private bool useMemoryFragments = true;

    [Tooltip("기억 파편 화면. 비워두면 실행 중에 자동으로 만듭니다.")]
    [SerializeField] private MemoryFragmentScreen fragmentScreen;

    [Tooltip("몇 번째 죽음부터 파편이 나오는지. 2 = 첫 죽음은 고통만, 두 번째 죽음부터 파편.")]
    [SerializeField] private int firstFragmentDeath = 2;

    [Tooltip("기억(파편) 개수")]
    [SerializeField] private int fragmentCount = 4;

    public bool UsesMemoryFragments => useMemoryFragments;

    [Header("놓을 자리 (반시계 방향 순서로 넣으세요)")]
    [Tooltip("아이템이 놓일 월드 좌표들. 리스트 순서가 곧 등장 순서입니다. " +
             "첫 번째 죽음 -> 0번, 두 번째 죽음 -> 1번 ... " +
             "기존 ItemDropCollider의 자리들은 비활성 오브젝트라 좌표로 직접 넣습니다.")]
    [SerializeField] private List<Vector3> spawnPositions = new List<Vector3>();

    [Tooltip("좌표 대신 씬의 Transform을 쓰고 싶을 때 사용합니다. " +
             "비어 있지 않으면 spawnPositions보다 우선합니다.")]
    [SerializeField] private List<Transform> spawnSpots = new List<Transform>();

    [Header("아이템")]
    [Tooltip("죽는 순서대로 등장할 아이템 프리팹. 리스트가 자리 수보다 짧으면 앞에서부터 다시 씁니다.")]
    [SerializeField] private List<GameObject> itemPrefabs = new List<GameObject>();

    [Tooltip("켜면 순서대로가 아니라 itemPrefabs 중에서 무작위로 하나를 고릅니다.")]
    [SerializeField] private bool randomizeItem = false;

    [Header("연출")]
    [Tooltip("아이템이 나타나기까지의 추가 대기 시간(초). " +
             "아이템은 이미 사망 연출과 대사가 모두 끝난 뒤에 생성되므로 보통 0으로 둡니다. " +
             "0보다 크게 두면 생성이 라운드 리셋보다 늦어져서, 장전(Bullet) 타임라인이 " +
             "아이템보다 먼저 재생되어 버립니다.")]
    [SerializeField] private float spawnDelay = 0f;

    [Tooltip("자리의 회전값을 그대로 쓸지 여부. 끄면 프리팹 원본의 회전을 씁니다.")]
    [SerializeField] private bool useSpotRotation = true;

    [Tooltip("아이템을 자리의 자식으로 붙일지 여부. 켜두면 하이어라키가 깔끔합니다.")]
    [SerializeField] private bool parentToSpot = true;

    [Header("상호작용 (클릭하면 카메라 렌즈가 넓어짐)")]
    [Tooltip("놓인 아이템에 ItemFovInteract를 자동으로 붙여서 클릭할 수 있게 만듭니다.")]
    [SerializeField] private bool attachFovInteract = true;

    [Tooltip("아이템 하나를 쓸 때마다 넓어지는 FOV 양")]
    [SerializeField] private float fovIncreasePerItem = 8f;

    [Tooltip("FOV가 변하는 데 걸리는 시간(초)")]
    [SerializeField] private float fovChangeDuration = 0.6f;

    [Tooltip("아이템을 한 번만 쓸 수 있게 할지 여부")]
    [SerializeField] private bool itemUseOnce = true;

    [Tooltip("쓴 뒤 아이템을 숨길지 여부. 꺼두면 테이블에 그대로 남습니다.")]
    [SerializeField] private bool hideItemAfterUse = false;

    [Header("아이템별 사용 타임라인 (등장 순서와 같은 순서)")]
    [Tooltip("1번째 아이템을 사용할 때 재생할 타임라인. 비워두면 연출 없이 효과만 적용됩니다.")]
    [SerializeField] private PlayableDirector useTimeline1;
    [SerializeField] private PlayableDirector useTimeline2;
    [SerializeField] private PlayableDirector useTimeline3;
    [SerializeField] private PlayableDirector useTimeline4;

    [Tooltip("타임라인이 끝난 뒤 조작을 돌려주기까지의 여유 시간(초)")]
    [SerializeField] private float delayAfterItemTimeline = 0.2f;

    [Header("타임라인 Marker 대사 그룹 (TypeWriter)")]
    [Tooltip("First Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int firstTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseFirst;
    [Tooltip("Second Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int secondTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseSecond;
    [Tooltip("Third Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int thirdTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseThird;
    [Tooltip("Forth Marker에서 재생할 TypeWriter dialogueGroups 인덱스 (-1이면 재생 안 함)")]
    [SerializeField] private int forthTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseForth;

    [Header("아이템별 자유 이동(탐색) 구간 (비워두면 없음)")]
    [Tooltip("1번째 아이템의 회상에서 걸어 다니는 구간")]
    [SerializeField] private FlashbackFreeRoamSegment freeRoamSegment1;
    [Tooltip("2번째 아이템의 회상에서 걸어 다니는 구간")]
    [SerializeField] private FlashbackFreeRoamSegment freeRoamSegment2;
    [Tooltip("3번째 아이템의 회상에서 걸어 다니는 구간")]
    [SerializeField] private FlashbackFreeRoamSegment freeRoamSegment3;
    [Tooltip("4번째 아이템의 회상에서 걸어 다니는 구간 (차 찾기)")]
    [UnityEngine.Serialization.FormerlySerializedAs("forthItemFreeRoamSegment")]
    [SerializeField] private FlashbackFreeRoamSegment freeRoamSegment4;

    [Header("타임라인이 끝난 뒤 회상 대사 (Item_Use_*)")]
    [Tooltip("1번째 아이템의 연출이 끝난 뒤 재생할 대사 그룹 (-1이면 재생 안 함)")]
    [SerializeField] private int firstAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseFirst;
    [Tooltip("2번째 아이템의 연출이 끝난 뒤 재생할 대사 그룹 (-1이면 재생 안 함)")]
    [SerializeField] private int secondAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseSecond;
    [Tooltip("3번째 아이템의 연출이 끝난 뒤 재생할 대사 그룹 (-1이면 재생 안 함)")]
    [SerializeField] private int thirdAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseThird;
    [Tooltip("4번째 아이템의 연출이 끝난 뒤 재생할 대사 그룹 (-1이면 재생 안 함)")]
    [SerializeField] private int forthAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseForth;

    [Header("회상 필름 그레인 글로벌 Volume (타임라인별)")]
    [Tooltip("1번째 아이템 타임라인(useTimeline1)에서 사용할 글로벌 Volume")]
    [SerializeField] private Volume memoryVolume1;
    [Tooltip("2번째 아이템 타임라인(useTimeline2)에서 사용할 글로벌 Volume")]
    [SerializeField] private Volume memoryVolume2;
    [Tooltip("3번째 아이템 타임라인(useTimeline3)에서 사용할 글로벌 Volume")]
    [SerializeField] private Volume memoryVolume3;
    [Tooltip("4번째 아이템 타임라인(useTimeline4)에서 사용할 글로벌 Volume")]
    [SerializeField] private Volume memoryVolume4;

    // 이번 씬에서 실제로 배치한 아이템 수 (중복 배치 방지용)
    private int placedCount = 0;

    private void OnEnable()
    {
        if (gameStateManager != null)
            gameStateManager.OnPlayerDeathCountChanged += HandlePlayerDeathCountChanged;
    }

    private void OnDisable()
    {
        if (gameStateManager != null)
            gameStateManager.OnPlayerDeathCountChanged -= HandlePlayerDeathCountChanged;
    }

    private void Start()
    {
        NormalizeMarkerDialogueGroupIndexes();

        // 기억 파편 모드에서는 테이블에 아무것도 놓지 않는다.
        if (useMemoryFragments)
        {
            ItemFovInteract.ClearPendingItems();
            return;
        }

        if (gameStateManager == null)
        {
            Debug.LogWarning("[DeathItemSpawner] gameStateManager가 연결되어 있지 않습니다! 인스펙터에서 연결하세요.");
            return;
        }

        if (SpotCount == 0)
        {
            Debug.LogWarning("[DeathItemSpawner] 놓을 자리가 비어 있습니다! " +
                             "spawnPositions(또는 spawnSpots)에 반시계 순서로 등록하세요.");
            return;
        }

        // 이전 판에서 남은 대기 아이템 기록을 지운다.
        // (파괴된 아이템은 스스로 빠지지만, 순서가 엇갈려 남아 있으면 총이 영영 안 잡힌다)
        ItemFovInteract.ClearPendingItems();

        // 루프를 돌아 씬이 다시 시작된 경우, 지금까지 죽은 횟수만큼 아이템을 즉시 복원한다.
        int already = gameStateManager.PlayerDeathCount;
        if (already > 0)
        {
            Debug.Log($"[DeathItemSpawner] 이전 루프의 사망 {already}회만큼 아이템을 즉시 배치합니다.");

            // 복원분은 총을 막지 않는다. 막아버리면 루프를 돌 때마다 지금까지 모은 아이템을
            // 전부 다시 써야 총을 잡을 수 있어서 게임 진행이 불가능해진다.
            for (int i = 0; i < already; i++)
                PlaceItemAt(i, false);
        }
    }

    // 플레이어가 죽어서 사망 횟수가 늘어날 때마다 호출됨 (인자: 누적 사망 횟수)
    private void HandlePlayerDeathCountChanged(int deathCount)
    {
        if (useMemoryFragments)
            return; // 파편 모드는 GameStateManager가 코루틴으로 직접 부른다.

        // deathCount번째 죽음 -> 인덱스는 deathCount - 1
        int index = deathCount - 1;

        if (index < placedCount)
            return; // 이미 배치됨 (Start에서 복원한 경우 등)

        // 방금 죽어서 새로 나온 아이템 -> 이건 쓸 때까지 총을 막는다.
        if (spawnDelay > 0f)
            StartCoroutine(PlaceAfterDelay(index));
        else
            PlaceItemAt(index, true);
    }

    // ─────────────────────────────────────────────────────────────
    // 기억 파편
    // ─────────────────────────────────────────────────────────────

    /// <summary>deathCount번째 죽음에서 나오는 파편 번호(0부터). 없으면 -1.</summary>
    public int GetFragmentIndexForDeath(int deathCount)
    {
        if (!useMemoryFragments) return -1;
        int index = deathCount - firstFragmentDeath;
        return (index >= 0 && index < fragmentCount) ? index : -1;
    }

    /// <summary>이번 죽음에 기억 파편이 나오는지 여부.</summary>
    public bool HasFragmentForDeath(int deathCount) => GetFragmentIndexForDeath(deathCount) >= 0;

    /// <summary>모든 기억을 되찾았는지 여부. (마지막 승부 단계)</summary>
    public bool AllFragmentsRecovered(int deathCount) =>
        useMemoryFragments && deathCount >= firstFragmentDeath + fragmentCount - 1;

    /// <summary>
    /// 1단계: 어두운 화면에 지금까지 모은 파편과 이번 파편을 띄웁니다. (클릭은 아직 받지 않음)
    /// 사망 대사는 이 화면 위에서 재생됩니다.
    /// </summary>
    public IEnumerator ShowFragmentScreen(int deathCount)
    {
        int index = GetFragmentIndexForDeath(deathCount);
        if (index < 0) yield break;

        EnsureFragmentScreen();
        yield return fragmentScreen.Show(index, index);
    }

    /// <summary>
    /// 2단계: 파편을 누르면 화면을 걷고 그 기억을 재생합니다. 회상 대사(Item_Use_*)까지 끝나면 돌아옵니다.
    /// </summary>
    public IEnumerator EnterFragmentMemory(int deathCount)
    {
        int index = GetFragmentIndexForDeath(deathCount);
        if (index < 0) yield break;

        EnsureFragmentScreen();
        if (!fragmentScreen.IsShowing)
            yield return fragmentScreen.Show(index, index);

        // 화면이 걷히는 것과 동시에 기억이 시작되도록 병렬로 돌린다.
        Coroutine hide = StartCoroutine(fragmentScreen.WaitForClickAndHide());
        while (fragmentScreen.IsShowing && !fragmentScreen.WasClicked)
            yield return null;

        var runnerObject = new GameObject($"MemoryFragment_{index + 1}");
        var runner = runnerObject.AddComponent<ItemFovInteract>();
        runner.Configure(0f, 0f, true, false, GetUseTimeline(index), delayAfterItemTimeline);
        runner.SetBlocksGunUntilUsed(false);
        runner.SetTimelineMarkerDialogueGroups(
            firstTimelineDialogueGroupIndex,
            secondTimelineDialogueGroupIndex,
            thirdTimelineDialogueGroupIndex,
            forthTimelineDialogueGroupIndex);
        runner.SetAfterTimelineDialogueGroup(GetAfterTimelineDialogueGroupIndex(index));
        runner.SetFreeRoamSegment(GetFreeRoamSegment(index));
        runner.SetFilmGrainVolume(GetMemoryVolume(index));

        Debug.Log($"[DeathItemSpawner] 기억 {index + 1}을(를) 재생합니다.");
        yield return runner.PlayAsMemoryFragment();

        // 화면 걷기가 아직 안 끝났으면 마저 기다린다.
        while (fragmentScreen.IsShowing)
            yield return null;
        if (hide != null) StopCoroutine(hide);

        Destroy(runnerObject);
        Debug.Log($"[DeathItemSpawner] 기억 {index + 1} 종료.");
    }

    /// <summary>
    /// 새 스토리 흐름용: fragmentIndex번 파편을 화면 위쪽에 띄우고, 누르면
    ///  - memoryIndex가 0~3이면: 아이템 대사(beforeDialogueGroup) → 그 기억(타임라인/탐색/추리 질문)
    ///  - memoryIndex가 음수면: 아이템 대사만
    /// 까지 끝난 뒤 돌아온다.
    /// </summary>
    public IEnumerator PlayFragment(int fragmentIndex, int memoryIndex, int beforeDialogueGroup)
    {
        EnsureFragmentScreen();
        if (!fragmentScreen.IsShowing)
            yield return fragmentScreen.Show(fragmentIndex, fragmentIndex);

        Coroutine hide = StartCoroutine(fragmentScreen.WaitForClickAndHide());
        while (fragmentScreen.IsShowing && !fragmentScreen.WasClicked)
            yield return null;

        if (memoryIndex >= 0)
        {
            var runnerObject = new GameObject($"MemoryFragment_{memoryIndex + 1}");
            var runner = runnerObject.AddComponent<ItemFovInteract>();
            runner.Configure(0f, 0f, true, false, GetUseTimeline(memoryIndex), delayAfterItemTimeline);
            runner.SetBlocksGunUntilUsed(false);
            runner.SetTimelineMarkerDialogueGroups(
                firstTimelineDialogueGroupIndex,
                secondTimelineDialogueGroupIndex,
                thirdTimelineDialogueGroupIndex,
                forthTimelineDialogueGroupIndex);
            runner.SetBeforeUseDialogueGroup(beforeDialogueGroup);
            runner.SetAfterTimelineDialogueGroup(-1);
            runner.SetFreeRoamSegment(GetFreeRoamSegment(memoryIndex));
            runner.SetFilmGrainVolume(GetMemoryVolume(memoryIndex));

            Debug.Log($"[DeathItemSpawner] 파편 {fragmentIndex + 1} → 기억 {memoryIndex + 1}을(를) 재생합니다.");
            yield return runner.PlayAsMemoryFragment();

            while (fragmentScreen.IsShowing)
                yield return null;
            if (hide != null) StopCoroutine(hide);
            Destroy(runnerObject);
        }
        else
        {
            while (fragmentScreen.IsShowing)
                yield return null;

            TimelineManager tm = FindFirstObjectByType<TimelineManager>(FindObjectsInactive.Include);
            if (beforeDialogueGroup >= 0 && tm != null)
            {
                bool done = false;
                tm.PlayImmediateDialogue(beforeDialogueGroup, () => done = true);
                yield return new WaitUntil(() => done);
            }
        }

        Debug.Log($"[DeathItemSpawner] 파편 {fragmentIndex + 1} 종료.");
    }

    private void EnsureFragmentScreen()
    {
        if (fragmentScreen == null)
            fragmentScreen = FindFirstObjectByType<MemoryFragmentScreen>(FindObjectsInactive.Include);

        if (fragmentScreen == null)
        {
            Debug.Log("[DeathItemSpawner] MemoryFragmentScreen이 없어 자동으로 만듭니다.");
            fragmentScreen = new GameObject("MemoryFragmentScreen").AddComponent<MemoryFragmentScreen>();
        }

        // 파편 = 아이템. Item Prefabs 순서(망치 -> 피 -> 바이러스 -> 구급상자) 그대로 보여준다.
        fragmentScreen.SetDefaultPrefabs(itemPrefabs);
    }

    private IEnumerator PlaceAfterDelay(int index)
    {
        yield return new WaitForSeconds(spawnDelay);
        PlaceItemAt(index, true);
    }

    // Transform 목록이 있으면 그걸 쓰고, 없으면 좌표 목록을 쓴다.
    private bool UseTransforms => spawnSpots != null && spawnSpots.Count > 0;

    private int SpotCount => UseTransforms ? spawnSpots.Count
                                           : (spawnPositions != null ? spawnPositions.Count : 0);

    // blocksGun: 이 아이템을 쓸 때까지 총을 못 잡게 할지 여부.
    //            방금 죽어서 나온 아이템만 true, 루프 복원분은 false.
    private void PlaceItemAt(int index, bool blocksGun)
    {
        if (index < 0 || index >= SpotCount)
        {
            Debug.Log($"[DeathItemSpawner] {index}번 자리가 없어 아이템을 놓지 않습니다. (자리 {SpotCount}개)");
            return;
        }

        GameObject prefab = PickPrefab(index);
        if (prefab == null)
        {
            Debug.LogWarning("[DeathItemSpawner] 놓을 아이템 프리팹이 없습니다! itemPrefabs를 등록하세요.");
            return;
        }

        Vector3 pos;
        Quaternion rot = prefab.transform.rotation;
        Transform spot = null;

        if (UseTransforms)
        {
            spot = spawnSpots[index];
            if (spot == null)
            {
                Debug.LogWarning($"[DeathItemSpawner] spawnSpots[{index}]가 비어 있습니다!");
                return;
            }
            pos = spot.position;
            if (useSpotRotation) rot = spot.rotation;
        }
        else
        {
            pos = spawnPositions[index];
        }

        GameObject spawned = Instantiate(prefab, pos, rot);
        spawned.SetActive(true);

        // 클릭하면 카메라 렌즈가 넓어지도록 상호작용 컴포넌트를 붙인다.
        // (프리팹 자체는 건드리지 않고 스폰된 인스턴스에만 붙이므로 설정이 여기 한 곳에 모인다)
        if (attachFovInteract)
        {
            var fovInteract = spawned.GetComponent<ItemFovInteract>();
            if (fovInteract == null)
                fovInteract = spawned.AddComponent<ItemFovInteract>();

            fovInteract.Configure(fovIncreasePerItem, fovChangeDuration, itemUseOnce, hideItemAfterUse,
                                  GetUseTimeline(index), delayAfterItemTimeline);
            fovInteract.SetBlocksGunUntilUsed(blocksGun);
            fovInteract.SetTimelineMarkerDialogueGroups(
                firstTimelineDialogueGroupIndex,
                secondTimelineDialogueGroupIndex,
                thirdTimelineDialogueGroupIndex,
                forthTimelineDialogueGroupIndex);
            fovInteract.SetAfterTimelineDialogueGroup(GetAfterTimelineDialogueGroupIndex(index));
            fovInteract.SetFreeRoamSegment(GetFreeRoamSegment(index));
            fovInteract.SetFilmGrainVolume(GetMemoryVolume(index));
        }

        // 비활성 오브젝트의 자식으로 붙이면 아이템까지 같이 안 보이게 되므로,
        // 자리가 활성 상태일 때만 부모로 붙인다.
        if (parentToSpot && spot != null && spot.gameObject.activeInHierarchy)
            spawned.transform.SetParent(spot, true);

        placedCount = Mathf.Max(placedCount, index + 1);

        Debug.Log($"[DeathItemSpawner] {index + 1}번째 아이템 '{prefab.name}'을(를) {pos} 위치에 놓았습니다.");
    }

    // 등장 순서에 맞는 자유 이동 구간. 없으면 null.
    private FlashbackFreeRoamSegment GetFreeRoamSegment(int index)
    {
        switch (index)
        {
            case 0: return freeRoamSegment1;
            case 1: return freeRoamSegment2;
            case 2: return freeRoamSegment3;
            case 3: return freeRoamSegment4;
            default: return null;
        }
    }

    // 등장 순서에 맞는 "연출 종료 후" 회상 대사 그룹(Item_Use_*)을 돌려준다. 없으면 -1.
    private int GetAfterTimelineDialogueGroupIndex(int index)
    {
        switch (index)
        {
            case 0: return firstAfterTimelineDialogueGroupIndex;
            case 1: return secondAfterTimelineDialogueGroupIndex;
            case 2: return thirdAfterTimelineDialogueGroupIndex;
            case 3: return forthAfterTimelineDialogueGroupIndex;
            default: return -1;
        }
    }

    // 등장 순서에 맞는 사용 타임라인을 돌려준다. 지정되지 않았으면 null.
    private PlayableDirector GetUseTimeline(int index)
    {
        switch (index)
        {
            case 0: return useTimeline1;
            case 1: return useTimeline2;
            case 2: return useTimeline3;
            case 3: return useTimeline4;
            default: return null;
        }
    }

    private void NormalizeMarkerDialogueGroupIndexes()
    {
        firstTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseFirst;
        secondTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseSecond;
        thirdTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseThird;
        forthTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseForth;

        firstAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseFirst;
        secondAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseSecond;
        thirdAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseThird;
        forthAfterTimelineDialogueGroupIndex = StoryDialogueIndex.ItemUseForth;
    }

    private Volume GetMemoryVolume(int index)
    {
        switch (index)
        {
            case 0: return memoryVolume1;
            case 1: return memoryVolume2;
            case 2: return memoryVolume3;
            case 3: return memoryVolume4;
            default: return null;
        }
    }

    private GameObject PickPrefab(int index)
    {
        if (itemPrefabs == null || itemPrefabs.Count == 0)
            return null;

        if (randomizeItem)
            return itemPrefabs[Random.Range(0, itemPrefabs.Count)];

        // 순서대로 쓰되, 자리가 프리팹 수보다 많으면 앞에서부터 다시 돈다.
        return itemPrefabs[index % itemPrefabs.Count];
    }
}