using UnityEngine;

public class MouseLook : MonoBehaviour
{
    public float mouseSensitivity = 200f;
    public Transform playerBody;   // Player (좌우 회전)

    private float xRotation = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 시작 시 부모 회전이 틀어져 있어도 카메라 자체는 항상 정면을 보도록 초기화
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f); // 위아래 시야각 제한

        // Z축이 절대 섞이지 않도록 명시적으로 로컬 회전을 고정
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX); // 좌우: Player 전체
    }
}