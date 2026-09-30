using UnityEngine;
using UnityEngine.Playables;

// 폰(Phone) 오브젝트 상호작용 스크립트
// DoorCrosshairInteractor가 크로스헤어로 폰을 감지하면 TryInteract()를 호출합니다.
// E키를 누르면 엔딩 타임라인(PlayableDirector)을 재생합니다.
// 이 스크립트는 폰 오브젝트 자신에 붙습니다. Collider(실제 충돌용, Is Trigger 체크 X)만 있으면 됩니다.
public class PhoneInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Ending Timeline")]
    [SerializeField] private PlayableDirector endingTimeline; // 엔딩 타임라인을 재생할 PlayableDirector
    [SerializeField] private bool disableInteractionAfterPlay = true; // 한 번 재생 후 재상호작용 방지

    private bool hasInteracted = false;

    // 크로스헤어(레이캐스트)가 이 폰을 가리키고 있을 때, DoorCrosshairInteractor가 호출합니다.
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
            endingTimeline.Play();
        }
        else
        {
            Debug.LogWarning("[PhoneInteract] endingTimeline이 할당되지 않았습니다. Inspector에서 PlayableDirector를 연결하세요.");
        }

        if (disableInteractionAfterPlay)
            hasInteracted = true;
    }
}