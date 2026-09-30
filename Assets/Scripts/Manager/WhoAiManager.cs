using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

// Who(딜러)의 AI.
//
// 자기 턴이 되면 "남은 탄약 상황"을 보고 자신에게 가장 유리한 선택을 합니다.
// AI가 보는 정보는 플레이어가 볼 수 있는 것과 동일합니다 (남은 실탄 수 / 남은 공탄 수).
// 챔버의 실제 순서를 훔쳐보지는 않으므로, 확정 상황이 아닌 이상 확률로 판단합니다.
//
// ── 판단 기준 ──
// 자기 자신에게 공탄을 쏘면 턴을 한 번 더 얻습니다. 그래서 Who는 공탄일 것 같으면
// 자기 머리에 쏴서 턴 이득을 노립니다. 물론 실탄이 걸리면 그대로 죽습니다.
//
// 자기 자신을 쏠 확률 = 공탄 확률 x selfShootBias
//   예) selfShootBias = 0.7 일 때
//       공탄 75% -> 52% 확률로 자해
//       공탄 50% -> 35% 확률로 자해
//       공탄 25% -> 17% 확률로 자해   <- 확률이 낮아도 가끔은 자기를 쏨
//
// 남은 탄이 전부 공탄이면 무조건 자기를 쏘고, 전부 실탄이면 무조건 플레이어를 쏩니다.
// 자해가 실탄에 걸리면 Who가 죽고, GameStateManager가 노이즈 연출 후 루프를 리셋합니다.
public class WhoAiManager : MonoBehaviour
{
    [Header("연동")]
    [SerializeField] private AmmoManager ammoManager;
    [SerializeField] private GameStateManager gameStateManager;
    [SerializeField] private TurnManager turnManager;

    [Tooltip("Who가 마지막 총알을 쐈을 때 재장전 대사 흐름을 트리거하기 위한 참조")]
    [SerializeField] private TimelineManager timelineManager;

    [Tooltip("실제 발사 처리(탄약 소모, 사운드, 데미지)를 위임할 총 오브젝트")]
    [SerializeField] private GunObject gun;

    [Header("연출 (선택 사항 - 비워두면 연출 없이 즉시 발사)")]
    [Tooltip("Who가 플레이어를 쏠 때 재생할 타임라인")]
    [SerializeField] private PlayableDirector shootPlayerTimeline;

    [Tooltip("Who가 자기 자신을 쏠 때 재생할 타임라인")]
    [SerializeField] private PlayableDirector shootSelfTimeline;

    [Header("발사 전 뜸 들이기")]
    [Tooltip("연출 타임라인이 끝나고 실제로 방아쇠를 당기기까지 기다리는 시간(초). " +
             "Who가 플레이어를 겨눈 채 버티는 구간입니다. 0이면 즉발로 나갑니다.")]
    [SerializeField] private float delayBeforeShotAtPlayer = 1.2f;

    [Tooltip("Who가 자기 자신을 겨눈 채 버티는 시간(초).")]
    [SerializeField] private float delayBeforeShotAtSelf = 1.2f;

    [Tooltip("타임라인이 없을 때, 발사 후 다음 턴으로 넘어가기까지의 여유 시간(초)")]
    [SerializeField] private float postShotDelay = 1.0f;

    [Tooltip("발사음('틱' 소리)이 다 난 뒤에 총을 내려놓을지 여부. " +
             "켜두면 아래 고정 대기 시간 대신 실제 사운드 길이만큼 기다립니다. " +
             "끄면 발사음과 상관없이 Gun Return Delay After Shot 만큼만 기다리고 바로 내려놓습니다. " +
             "어느 쪽이든 플레이어가 맞은 경우에는 이 대기를 건너뛰고 hitted 타임라인과 동시에 내려놓습니다.")]
    [SerializeField] private bool waitForShotSoundBeforeReturn = true;

    [Tooltip("발사음이 끝난 뒤 총을 내려놓기까지 추가로 두는 여유 시간(초)")]
    [SerializeField] private float extraDelayAfterShotSound = 0.2f;

    [Tooltip("플레이어가 맞았을 때 hitted 타임라인이 끝날 때까지 총을 그대로 둘지 여부. " +
             "끄면(기본) 피격 타임라인과 동시에 총을 내려놓습니다.")]
    [SerializeField] private bool waitForHitTimelineBeforeReturn = false;

    [Tooltip("위 옵션을 켰을 때: hitted 타임라인이 완전히 끝나고 총을 내려놓기까지의 여유 시간(초)")]
    [SerializeField] private float gunReturnDelayAfterHitTimeline = 0.2f;

    [Tooltip("hitted 타임라인이 끝나기를 기다리는 최대 시간(초). 연출이 멈춰도 총이 영영 안 내려가는 것을 막는 안전장치입니다.")]
    [SerializeField] private float hitTimelineWaitTimeout = 15f;

    [Tooltip("Who가 발사한 직후(틱 소리 재생 시작 직후) 총을 되돌리기까지 기다릴 고정 시간(초).")]
    [SerializeField] private float gunReturnDelayAfterShot = 2.0f;

    [Tooltip("Who가 공탄을 발사했을 때 총을 되돌리기까지 기다릴 시간(초).")]
    [SerializeField] private float blankReturnDelayAfterShot = 2.0f;

    [Header("쏘기 전 카메라")]
    [Tooltip("Who가 쏘기 전에 카메라를 아래 회전으로 부드럽게 돌릴지 여부")]
    [SerializeField] private bool faceWhoBeforeShot = true;

    [Tooltip("Who를 정면으로 보는 카메라 리그의 로컬 회전")]
    [SerializeField] private Vector3 whoFacingEuler = new Vector3(3.99999952f, 0f, 0f);

    [Tooltip("카메라가 돌아가는 시간(초)")]
    [SerializeField] private float faceWhoDuration = 0.8f;

    [Header("AI 성향")]
    [Tooltip("Who가 자기 자신을 쏘려는 성향 (0~1). 실제 자해 확률 = 공탄 확률 x 이 값. " +
             "0이면 절대 자기를 쏘지 않고, 1이면 공탄 확률 그대로 자해합니다. " +
             "값이 높을수록 [TL]WhoShootHim 연출이 자주 나오고 Who가 자멸하는 일도 늘어납니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float selfShootBias = 0.7f;

    [Tooltip("공탄이 1발 이상 남아 있을 때 보장되는 최소 자해 확률 (0~1). " +
             "공탄 확률이 아무리 낮아도 살아남을 가능성이 남아 있는 한 이 확률만큼은 도박하듯 자기 머리에 쏩니다. " +
             "공탄이 0발이면 확정 자살이라 이 값과 무관하게 절대 자기를 쏘지 않습니다. " +
             "0으로 두면 순수하게 공탄 확률에만 비례합니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float minSelfShootChance = 0.25f;

    private bool isActing = false;

    /// <summary>
    /// TurnManager가 Who의 턴이 되었을 때 호출합니다.
    /// </summary>
    public void TakeTurn()
    {
        if (isActing)
        {
            Debug.LogWarning("[WhoAiManager] 이미 행동 중입니다. 중복 호출을 무시합니다.");
            return;
        }

        if (ammoManager == null || gun == null)
        {
            Debug.LogWarning("[WhoAiManager] ammoManager 또는 gun이 연결되어 있지 않아 행동할 수 없습니다!");
            return;
        }

        if (ammoManager.RemainingBullets <= 0)
        {
            Debug.Log("[WhoAiManager] 남은 총알이 없어 행동하지 않습니다. (재장전 흐름이 처리합니다)");
            return;
        }

        bool shootSelf = DecideShootSelf();
        StartCoroutine(ActRoutine(shootSelf));
    }

    // 쏘기 전에 카메라를 Who 정면으로 부드럽게 돌린다.
    private IEnumerator FaceWhoRoutine()
    {
        if (!faceWhoBeforeShot || CamMove.Instance == null)
            yield break;

        yield return CamMove.Instance.RotateToLocalEuler(whoFacingEuler, faceWhoDuration);
    }

    /// <summary>
    /// 남은 탄약 상황을 보고 자기 자신에게 쏠지(true) 플레이어에게 쏠지(false) 결정합니다.
    /// </summary>
    private bool DecideShootSelf()
    {
        int live = ammoManager.RemainingLive;
        int blank = ammoManager.RemainingBlank;
        float liveChance = ammoManager.LiveProbability;

        bool decision = DecideOptimal(live, blank, liveChance, out string reason);

        Debug.Log($"[WhoAiManager] 판단 - 실탄 {live}발 / 공탄 {blank}발 (실탄 확률 {liveChance:P0}) " +
                  $"-> {(decision ? "자기 자신" : "플레이어")}에게 발사. 이유: {reason}");

        return decision;
    }

    private bool DecideOptimal(int live, int blank, float liveChance, out string reason)
    {
        // 1. 남은 탄이 전부 공탄 -> 위험이 0이므로 무조건 자기 자신을 쏜다.
        if (live == 0)
        {
            reason = "남은 탄이 전부 공탄이라 안전하게 자기 자신을 쏨";
            return true;
        }

        // 2. 남은 탄이 전부 실탄 -> 자해는 곧 자살이므로 무조건 플레이어를 쏜다.
        if (blank == 0)
        {
            reason = "남은 탄이 전부 실탄이라 플레이어를 사살";
            return false;
        }

        // 3. 그 외에는 공탄 확률에 비례해서 도박한다.
        //    공탄이면 턴을 한 번 더 얻으므로 이득이고, 실탄이면 죽는다.
        //
        //    다만 확률에만 맡기면 실탄이 많은 판에서는 Who가 절대 자기를 쏘지 않아 밋밋해진다.
        //    그래서 공탄이 1발이라도 남아 있는 한(= 살아남을 가능성이 있는 상황) minSelfShootChance
        //    만큼은 아무리 불리해도 도박을 걸도록 바닥을 깔아 준다.
        //    판단 기준은 실탄 수가 아니라 공탄 수다. 공탄이 0발이면 자해는 도박이 아니라
        //    확정 자살이므로 위의 blank == 0 분기에서 이미 걸러진다.
        float blankChance = 1f - liveChance;
        float selfChance = blankChance * selfShootBias;

        bool gambling = false;
        if (blank >= 1 && selfChance < minSelfShootChance)
        {
            selfChance = minSelfShootChance;
            gambling = true;
        }

        float roll = Random.value;

        if (roll < selfChance)
        {
            reason = gambling
                ? $"불리하지만 도박 (보장 자해 확률 {selfChance:P0}, 공탄 확률 {blankChance:P0}, 굴림 {roll:0.00})"
                : $"자해 도박 성공 (공탄 확률 {blankChance:P0} x 성향 {selfShootBias:0.00} " +
                  $"= {selfChance:P0}, 굴림 {roll:0.00})";
            return true;
        }

        reason = $"자해 확률 미달로 공격 (자해 확률 {selfChance:P0}, 굴림 {roll:0.00})";
        return false;
    }

    // 총알이 모두 소진된 시점에 라운드를 진행시키고 재장전 대사를 트리거한다.
    // GunObject.ReturnRoutine()이 플레이어 쪽에서 하던 것과 동일한 처리다.
    private void TriggerReloadFlow()
    {
        int? dialogueGroupIndex = null;
        if (gameStateManager != null)
        {
            gameStateManager.AdvanceRound();
            dialogueGroupIndex = gameStateManager.ConsumeDialogueGroupIndexForCurrentState();
        }

        if (timelineManager == null)
        {
            Debug.LogWarning("[WhoAiManager] timelineManager가 연결되어 있지 않아 재장전 대사를 트리거할 수 없습니다! " +
                             "인스펙터에서 반드시 연결하세요. 연결하지 않으면 Who가 마지막 탄을 쏜 뒤 게임이 멈춥니다.");
            return;
        }

        if (dialogueGroupIndex.HasValue)
            timelineManager.TriggerReloadDialogue(dialogueGroupIndex.Value);
        else
            timelineManager.TriggerReloadDialogue();
    }

    // 피격 연출(hitted 타임라인)이 끝날 때까지 기다렸다가 총을 내려놓는다.
    // ActRoutine과 별도의 코루틴으로 돌려야 턴 정리가 연출에 묶이지 않는다.
    private IEnumerator ReturnGunAfterHitTimelineRoutine()
    {
        if (timelineManager == null)
        {
            Debug.LogWarning("[WhoAiManager] timelineManager가 없어 피격 연출을 기다리지 못하고 바로 총을 내려놓습니다.");
            gun.ReturnToTableAfterWhoShot(gunReturnDelayAfterHitTimeline, false);
            yield break;
        }

        // 타임라인이 실제로 재생 상태로 들어갈 한 프레임을 준다.
        // (PlayableDirector.Play()는 호출됐지만 아직 평가 전일 수 있다)
        yield return null;

        float waited = 0f;
        while (timelineManager.IsHitTimelinePlaying && waited < hitTimelineWaitTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (waited >= hitTimelineWaitTimeout)
            Debug.LogWarning("[WhoAiManager] hitted 타임라인이 끝나기를 기다리다 시간 초과. 그냥 총을 내려놓습니다.");
        else
            Debug.Log($"[WhoAiManager] hitted 타임라인 종료({waited:0.00}초). 총을 내려놓습니다.");

        gun.ReturnToTableAfterWhoShot(gunReturnDelayAfterHitTimeline, false);
    }

    private IEnumerator ActRoutine(bool shootSelf)
    {
        isActing = true;

        // 행동하는 동안에는 플레이어 입력을 막는다.
        if (turnManager != null)
            turnManager.SetLocked(true);

        // 쏘기 전에 카메라를 Who 정면으로 돌린다.
        yield return FaceWhoRoutine();

        // 총에게 "지금부터 연출이 내가 널 옮긴다"고 알린다.
        // 이걸 빼먹으면 InteractableObject의 위치 보간이 총을 매 프레임 테이블로 끌어당겨서,
        // 연출이 끝나는 순간 총이 혼자 제자리로 미끄러진다. (대기 시간을 얼마로 주든 소용없음)
        gun.BeginWhoShotSequence();

        // 연출 타임라인이 연결되어 있으면 먼저 재생하고 끝날 때까지 기다린다.
        PlayableDirector director = shootSelf ? shootSelfTimeline : shootPlayerTimeline;
        if (director != null)
        {
            bool finished = false;
            void OnStopped(PlayableDirector d)
            {
                director.stopped -= OnStopped;
                finished = true;
            }
            director.stopped += OnStopped;
            director.Play();

            yield return new WaitUntil(() => finished);
        }

        // WhoShoot 타임라인이 있으면 타임라인 끝에서 바로 발사한다.
        // (공탄=틱, 실탄=펑+피격 연출)
        // 타임라인이 없을 때만 기존 뜸 들이기 지연을 사용한다.
        if (director == null)
        {
            float preShotDelay = shootSelf ? delayBeforeShotAtSelf : delayBeforeShotAtPlayer;
            if (preShotDelay > 0f)
            {
                Debug.Log($"[WhoAiManager] {(shootSelf ? "자기 자신" : "플레이어")}을(를) 겨눈 채 " +
                          $"{preShotDelay:0.00}초 뜸을 들입니다.");
                yield return new WaitForSeconds(preShotDelay);
            }
        }

        // 실제 발사: 탄약 소모 + 사운드 + 데미지 처리는 GunObject가 담당한다.
        bool wasLive = gun.FireAsWho(shootSelf);

        // 타임라인이 옮겨 놓은 총을 원래 테이블 위치로 되돌린다.
        //
        // 플레이어가 실탄에 맞은 경우에는 FireAsWho 안에서 이미 hitted 타임라인이 시작됐다.
        // 이때는 피격 연출이 최우선이다. 연출이 다 끝난 뒤에야 총을 내려놓는다.
        // 연출 도중에 총이 움직이면 시선이 그쪽으로 끌려가 피격이 묻힌다.
        bool playerGotHit = !shootSelf && wasLive;
        bool whoKilledSelfWithLive = shootSelf && wasLive;

        // Who의 발사는 "틱 소리 먼저 -> 고정 시간 후 총 복귀" 규칙을 사용한다.
        float shotFirstReturnDelay = Mathf.Max(0f, gunReturnDelayAfterShot);

        if (playerGotHit && waitForHitTimelineBeforeReturn)
        {
            Debug.Log("[WhoAiManager] 플레이어 피격 -> hitted 타임라인을 먼저 끝까지 재생합니다.");
            StartCoroutine(ReturnGunAfterHitTimelineRoutine());
        }
        else if (playerGotHit)
        {
            Debug.Log("[WhoAiManager] 플레이어 피격 -> hitted 타임라인과 동시에 총을 내려놓습니다.");
            gun.ReturnToTableAfterWhoShot(0f, false);
        }
        else if (whoKilledSelfWithLive)
        {
            Debug.Log($"[WhoAiManager] Who 자해(실탄) -> 틱 소리 후 {shotFirstReturnDelay:0.00}초 뒤 총을 내려놓습니다.");
            gun.ReturnToTableAfterWhoShot(shotFirstReturnDelay, false);
        }
        else
        {
            float blankDelay = Mathf.Max(0f, blankReturnDelayAfterShot);
            Debug.Log($"[WhoAiManager] Who 공탄 발사 -> 틱 소리 후 {blankDelay:0.00}초 뒤 총을 내려놓습니다.");

            gun.ReturnToTableAfterWhoShot(blankDelay);
        }

        if (director == null && postShotDelay > 0f)
            yield return new WaitForSeconds(postShotDelay);

        isActing = false;

        // 사망 시퀀스가 시작되었다면 그쪽에서 잠금 해제와 턴 정리를 담당하므로 여기선 손대지 않는다.
        if (gameStateManager != null && gameStateManager.IsDeathSequenceRunning)
            yield break;

        // 탄약이 모두 소진되었다면 재장전 흐름으로 넘긴다.
        // 플레이어가 마지막 탄을 쐈을 때는 GunObject가 이 처리를 하지만,
        // Who가 마지막 탄을 쐈을 때는 GunObject의 복귀 코루틴이 돌지 않으므로 여기서 직접 트리거해야 한다.
        // (이걸 빠뜨리면 TimelineManager가 TriggerReloadDialogue를 영원히 기다리며 게임이 멈춘다.)
        if (ammoManager.RemainingBullets <= 0)
        {
            Debug.Log("[WhoAiManager] Who가 마지막 탄을 소모했습니다. 재장전 대사 흐름을 직접 트리거합니다.");
            TriggerReloadFlow();
            yield break;
        }

        if (turnManager != null)
        {
            // 반드시 ResolveShot을 먼저 호출해서 턴을 확정한 뒤 잠금을 푼다.
            // 순서가 반대면 잠금이 풀리는 순간 Who의 턴이 그대로라 AI가 한 번 더 행동해 버린다.
            turnManager.ResolveShot(TurnManager.Turn.Who, shootSelf, wasLive);
            turnManager.SetLocked(false);
        }
    }
}
