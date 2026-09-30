using UnityEngine;

// 독립적인 문(Door) 상호작용 스크립트
// 기존 CamMove / InteractableObject 시스템과 전혀 무관하게 동작합니다.
// 이 스크립트는 문 오브젝트 자신에 붙입니다. Collider(Is Trigger 체크 X, 실제 충돌용)만 있으면 됩니다.
public class DoorInteract : MonoBehaviour
{
    [Header("Door Rotation Settings")]
    [SerializeField] private float openAngle = 90f;      // 문이 열렸을 때 회전 각도
    [SerializeField] private float openSpeed = 3f;        // 여닫는 속도
    [SerializeField] private Vector3 rotationAxis = Vector3.up; // 회전 축 (Y축 힌지)

    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private Quaternion targetRotation;
    private bool isOpen = false;

    void Start()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(rotationAxis.normalized * openAngle);
        targetRotation = closedRotation;
    }

    void Update()
    {
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, Time.deltaTime * openSpeed);
    }

    // 크로스헤어(레이캐스트)가 이 문을 가리키고 있을 때, DoorCrosshairInteractor가 호출합니다.
    public void TryInteract()
    {
        if (Input.GetKeyDown(interactKey))
        {
            ToggleDoor();
        }
    }

    void ToggleDoor()
    {
        isOpen = !isOpen;
        targetRotation = isOpen ? openRotation : closedRotation;
    }
}