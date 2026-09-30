using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    [Header("UI & Visual Settings")]
    [SerializeField] private GameObject highlightUI; // 마우스 오버 시 나타날 UI 컴포넌트나 가이드 오브젝트 (아이템 이름/설명 등)

    [Header("Hover Animation Settings")]
    [SerializeField] private float hoverHeight = 0.15f; // 마우스 오버 시 위로 이동할 높이
    [SerializeField] private float moveSpeed = 10f;     // 올라가고 내려가는 속도

    protected Vector3 originalPosition;
    protected Vector3 targetPosition;
    public bool isHovered = false;

    // true일 때 베이스 localPosition 보간을 멈춤 (자식 클래스에서 직접 이동 제어 시 사용)
    protected bool suppressBasePositionLerp = false;

    protected virtual void Start()
    {
        // 시작 위치 저장
        originalPosition = transform.localPosition;
        targetPosition = originalPosition;

        // 시작할 때는 UI를 꺼둡니다.
        if (highlightUI != null)
        {
            highlightUI.SetActive(false);
        }
    }

    void Update()
    {
        // suppressBasePositionLerp가 false일 때만 베이스 위치 보간 실행
        if (!suppressBasePositionLerp)
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
        OnUpdate();
    }

    protected virtual void OnUpdate() { }

    // 마우스 올렸을 때
    public virtual void ShowInteractionUI()
    {
        isHovered = true;
        // 위로 살짝 들리도록 타겟 위치 설정 (로컬 좌표 기준 위쪽 Y)
        targetPosition = originalPosition + Vector3.up * hoverHeight;

        if (highlightUI != null)
        {
            highlightUI.SetActive(true);  // "하이라이트 표시" (예: "HANDCUFFS DEALER SKIPS THE NEXT TURN")
        }
    }

    // 마우스 벗어났을 때
    public virtual void HideInteractionUI()
    {
        isHovered = false;
        // 원래 위치로 복원
        targetPosition = originalPosition;
        if (highlightUI != null)
        {
            highlightUI.SetActive(false);  // 사라짐
        }
    }

    // 마우스 클릭했을 때 (상호작용)
    public virtual void Interact()
    {
        Debug.Log(gameObject.name + " 획득 및 사용!");  

        // 클릭되었을 때의 동작 (예: 화면 앞이나 카메라 쪽으로 부드럽게 들어 올렸다가 사라지게 하는 등의 비주얼)
        // 여기에 아이템이 실제로 사용되는 코드를 여기에 연결하거나, 상속받아 세부 기능을 추가합니다.
    }
}