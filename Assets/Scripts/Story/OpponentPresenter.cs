using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 테이블 건너편에 누가 앉아 있는지 보여 준다. (???, 트레일, 트레일 시체)
//
//  - 1~2판 : ???가 맞은편에 서 있다.
//  - 3판   : 트레일이 맞은편, ???는 옆에 서서 지켜본다.
//  - 4판   : ???가 다시 맞은편, 옆에는 책상에 엎어진 트레일의 시체.
// 누가 죽으면 SlumpableBody로 책상에 머리를 박으며 쓰러진다.
public class OpponentPresenter : MonoBehaviour
{
    public enum Opponent { Who, Trail }

    [Header("모델 (루트 = 발밑, forward = 플레이어 쪽)")]
    [SerializeField] private SlumpableBody whoBody;
    [SerializeField] private SlumpableBody trailBody;

    [Header("자리")]
    [Tooltip("맞은편 자리")]
    [SerializeField] private Transform seatPoint;
    [Tooltip("트레일과 할 때 ???가 서 있는 자리")]
    [SerializeField] private Transform watchPoint;
    [Tooltip("5판에서 트레일 시체가 엎어져 있는 자리")]
    [SerializeField] private Transform corpsePoint;

    [Header("이름표 (조준할 때 뜨는 월드 글자)")]
    [SerializeField] private Text opponentLabel;
    [SerializeField] private string whoLabel = "???";
    [SerializeField] private string trailLabel = "트레일";

    public Opponent Current { get; private set; } = Opponent.Who;

    public static OpponentPresenter Instance { get; private set; }

    private void Awake() => Instance = this;

    /// <summary>
    /// 플레이어가 몇 번 죽었는지에 맞춰 자리를 잡는다. (씬 시작/재시작 시)
    /// trailRoundDeaths = 트레일과의 판이 시작될 때의 사망 횟수
    /// </summary>
    public void ApplyForDeaths(int deaths, int trailRoundDeaths)
    {
        if (deaths == trailRoundDeaths) SetStage(Opponent.Trail, trailAlive: true);
        else if (deaths > trailRoundDeaths) SetStage(Opponent.Who, trailAlive: false);
        else SetStage(Opponent.Who, trailAlive: null);
    }

    /// <summary>
    /// trailAlive: null = 트레일 없음, true = 트레일이 맞은편 (???는 옆), false = 트레일은 시체 (???가 맞은편)
    /// </summary>
    public void SetStage(Opponent opponent, bool? trailAlive)
    {
        Current = opponent;

        if (whoBody != null)
        {
            whoBody.gameObject.SetActive(true);
            whoBody.ResetPose();
            Place(whoBody.transform, opponent == Opponent.Who ? seatPoint : watchPoint);
        }

        if (trailBody != null)
        {
            trailBody.gameObject.SetActive(trailAlive.HasValue);
            if (trailAlive == true)
            {
                trailBody.ResetPose();
                Place(trailBody.transform, seatPoint);
            }
            else if (trailAlive == false)
            {
                Place(trailBody.transform, corpsePoint);
                trailBody.SetSlumpedInstant();
            }
        }

        if (opponentLabel != null)
            opponentLabel.text = opponent == Opponent.Trail ? trailLabel : whoLabel;
    }

    /// <summary>지금 맞은편에 앉은 사람이 책상에 머리를 박으며 쓰러진다.</summary>
    public IEnumerator PlayOpponentDeath()
    {
        SlumpableBody body = Current == Opponent.Trail ? trailBody : whoBody;
        if (body != null && body.gameObject.activeInHierarchy)
            yield return body.Slam();
    }

    private static void Place(Transform t, Transform point)
    {
        if (point == null) return;
        t.SetPositionAndRotation(point.position, point.rotation);
    }
}
