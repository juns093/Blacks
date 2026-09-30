using UnityEngine;
using UnityEngine.Playables;

// 숨겨진(히든) 오브젝트 상호작용 스크립트.
// 조건 없이, 이 오브젝트를 크로스헤어로 찾아서 E키를 누르면 바로 히든 엔딩 타임라인이 재생됩니다.
// DoorCrosshairInteractor가 크로스헤어로 이 오브젝트를 감지하면 TryInteract()를 호출합니다.
// 이 스크립트는 숨겨진 오브젝트 자신에 붙입니다. Collider(실제 충돌용, Is Trigger 체크 X)만 있으면 됩니다.
public class HiddenEndingInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Hidden Ending Timeline")]
    [SerializeField] private PlayableDirector endingTimeline; // 히든 엔딩 타임라인을 재생할 PlayableDirector
    [SerializeField] private bool disableInteractionAfterPlay = true; // 한 번 재생 후 재상호작용 방지

    private bool hasInteracted = false;

    // 크로스헤어(레이캐스트)가 이 오브젝트를 가리키고 있을 때, DoorCrosshairInteractor가 호출합니다.
    public void TryInteract()
    {
        if (hasInteracted && disableInteractionAfterPlay)
            return;

        if (Input.GetKeyDown(interactKey))
        {
            PlayEndingTimeline();
        }
    }

    private void PlayEndingTimeline()
    {
        if (endingTimeline != null)
        {
            Debug.Log("[HiddenEndingInteract] 히든 엔딩 타임라인을 재생합니다.");
            endingTimeline.Play();
        }
        else
        {
            Debug.LogWarning("[HiddenEndingInteract] endingTimeline이 할당되지 않았습니다. Inspector에서 PlayableDirector를 연결하세요.");
        }

        if (disableInteractionAfterPlay)
            hasInteracted = true;
    }
}