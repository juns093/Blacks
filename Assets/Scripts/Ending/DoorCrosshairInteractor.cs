using UnityEngine;

// 화면 중앙(크로스헤어)에서 레이캐스트를 쏴서 문을 감지하는 스크립트
// 이 스크립트는 Player 또는 Camera 오브젝트에 붙입니다.
// CamMove 등 다른 씬의 카메라 시스템과 완전히 독립적으로, Camera.main만 사용합니다.
public class DoorCrosshairInteractor : MonoBehaviour
{
    [Header("Raycast Settings")]
    [SerializeField] private float interactDistance = 3f;
    [SerializeField] private LayerMask doorLayer = ~0; // 기본값: 모든 레이어 감지 (필요 시 Door 전용 레이어로 제한 권장)
    [SerializeField] private LayerMask phoneLayer = ~0; // 기본값: 모든 레이어 감지 (필요 시 Phone 전용 레이어로 제한 권장)
    [SerializeField] private LayerMask radioLayer = ~0; // 기본값: 모든 레이어 감지 (필요 시 Radio 전용 레이어로 제한 권장)
    [Tooltip("숨겨진 히든 엔딩 오브젝트 전용 레이어. 다른 오브젝트와 겹치지 않도록 전용 레이어로 제한하는 것을 권장합니다.")]
    [SerializeField] private LayerMask hiddenEndingLayer = ~0;

    [Header("Crosshair UI (항상 표시)")]
    [SerializeField] private GameObject crosshairUI; // 조준점 자체. 상시 활성화 상태로 두면 됩니다.

    [Header("Interaction Prompt UI (문을 바라볼 때만 표시)")]
    [SerializeField] private GameObject interactPromptUI; // "Press E" 텍스트/이미지 오브젝트

    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (cam == null)
            Debug.LogWarning("[DoorCrosshairInteractor] Camera.main을 찾을 수 없습니다. Main Camera 태그를 확인하세요.");

        // 크로스헤어는 항상 켜져 있어야 함 (조준 편의용)
        if (crosshairUI != null)
            crosshairUI.SetActive(true);

        // 상호작용 프롬프트는 처음엔 꺼둠
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }

    void Update()
    {
        if (cam == null) return;

        // 화면 정중앙(크로스헤어)에서 카메라 정면으로 레이 발사
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.cyan);

        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, doorLayer))
        {
            DoorInteract door = hit.collider.GetComponentInParent<DoorInteract>();
            if (door != null)
            {
                // 문을 바라보고 있을 때만 "Press E" 표시
                if (interactPromptUI != null)
                    interactPromptUI.SetActive(true);

                door.TryInteract(); // 크로스헤어가 문에 닿아 있는 동안만 E키 입력을 문에 전달
                return;
            }
        }
        if (Physics.Raycast(ray, out RaycastHit phoneHit, interactDistance, phoneLayer))
        {
            PhoneInteract phone = phoneHit.collider.GetComponentInParent<PhoneInteract>();
            if (phone != null)
            {
                // 폰을 바라보고 있을 때만 "Press E" 표시
                if (interactPromptUI != null)
                    interactPromptUI.SetActive(true);
                phone.TryInteract(); // 크로스헤어가 폰에 닿아 있는 동안만 E키 입력을 폰에 전달
                return;
            }
        }
        if (Physics.Raycast(ray, out RaycastHit radioHit, interactDistance, radioLayer))
        {
            RadioInteract radio = radioHit.collider.GetComponentInParent<RadioInteract>();
            if (radio != null)
            {
                if (interactPromptUI != null)
                    interactPromptUI.SetActive(true);
                radio.TryInteract();
                return;
            }
        }
        if (Physics.Raycast(ray, out RaycastHit hiddenHit, interactDistance, hiddenEndingLayer))
        {
            HiddenEndingInteract hiddenEnding = hiddenHit.collider.GetComponentInParent<HiddenEndingInteract>();
            if (hiddenEnding != null)
            {
                // 숨겨진 오브젝트를 바라보고 있을 때만 "Press E" 표시
                if (interactPromptUI != null)
                    interactPromptUI.SetActive(true);
                hiddenEnding.TryInteract();
                return;
            }
        }
        // 문을 안 바라보고 있으면 프롬프트만 숨김 (크로스헤어는 계속 유지)
        if (interactPromptUI != null)
            interactPromptUI.SetActive(false);
    }
}