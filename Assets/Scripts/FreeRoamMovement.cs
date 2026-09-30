using UnityEngine;

// 회상 장소에서 WASD로 걸어 다니는 1인칭 컨트롤러.
//
// GameScene의 Main Camera는 평소 CinemachineBrain이 움직입니다.
// 탐색 구간 동안에는 FlashbackFreeRoamSegment가 Brain을 꺼두고, 이 스크립트가
// 매 프레임 머리(head) 위치/회전을 Main Camera에 그대로 옮겨 적습니다.
// (Cinemachine 가상 카메라로 우선순위 싸움을 하지 않아도 되고, 타임라인이 멈춰 있어도 안전합니다)
//
// 평소에는 오브젝트째 꺼져 있다가 탐색 구간에서만 켜집니다.
[RequireComponent(typeof(CharacterController))]
public class FreeRoamMovement : MonoBehaviour
{
    [Header("이동")]
    [Tooltip("걷는 속도 (초당 유닛)")]
    [SerializeField] private float walkSpeed = 5.5f;

    [Tooltip("중력 가속도. 바닥에 붙어 있게 해줍니다.")]
    [SerializeField] private float gravity = -9.81f;

    [Header("시점")]
    [Tooltip("눈 높이 역할을 하는 자식 Transform. 카메라가 이 위치/회전을 따라갑니다.")]
    [SerializeField] private Transform head;

    [Tooltip("마우스 감도")]
    [SerializeField] private float mouseSensitivity = 2f;

    [Tooltip("위아래로 볼 수 있는 최대 각도")]
    [SerializeField] private float pitchLimit = 80f;

    [Tooltip("눈 높이(m). 발에서 카메라까지. 0이면 탐색을 시작할 때의 카메라 높이를 그대로 씁니다.")]
    [SerializeField] private float eyeHeight = 2.2f;

    [Header("발소리")]
    [Tooltip("걸을 때 재생할 발소리 (FootStepSF)")]
    [SerializeField] private AudioClip footstepClip;
    [Tooltip("이만큼(m) 걸을 때마다 한 번 발소리")]
    [SerializeField] private float stepDistance = 3.2f;
    [Range(0f, 1f)] [SerializeField] private float footstepVolume = 0.55f;
    [SerializeField] private Vector2 footstepPitch = new Vector2(0.88f, 1.08f);

    [Tooltip("바닥을 못 찾았을 때 쓸 눈 높이")]
    [SerializeField] private float defaultEyeHeight = 1.65f;

    [Tooltip("시작 시 카메라 높이를 눈 높이로 쓸 때의 허용 범위")]
    [SerializeField] private Vector2 eyeHeightRange = new Vector2(1.4f, 1.9f);

    private CharacterController controller;
    private Camera targetCamera;
    private Vector3 verticalVelocity;
    private float yaw;
    private float pitch;
    private bool controlling = false;

    // 시작할 때 발밑에 바닥 콜라이더가 없으면(맵 제작 중) 중력 없이 그 높이로 걷는다.
    private bool hasGround = true;
    private Vector3 startFeetPosition;
    private float startYaw;

    [Tooltip("시작 위치보다 이만큼 아래로 떨어지면 시작 위치로 되돌립니다. (맵 구멍 대비)")]
    [SerializeField] private float fallResetDepth = 15f;

    private AudioSource footSource;
    private float stepAccum;

    public bool IsControlling => controlling;

    /// <summary>문서를 읽는 동안 등, 카메라는 붙여 둔 채 이동과 시점만 멈출 때 씁니다.</summary>
    public bool InputPaused { get; set; }

    /// <summary>화면 흔들림. 카메라 회전에만 더해지고 시점 값은 바꾸지 않습니다.</summary>
    public Vector3 CameraShakeEuler { get; set; }

    public void GetLook(out float currentYaw, out float currentPitch)
    {
        currentYaw = yaw;
        currentPitch = pitch;
    }

    /// <summary>연출에서 시선을 강제로 돌릴 때 씁니다. (점프 스퀘어 등)</summary>
    public void SetLook(float newYaw, float newPitch)
    {
        yaw = newYaw;
        pitch = Mathf.Clamp(newPitch, -pitchLimit, pitchLimit);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    /// <summary>카메라 기준점. 소리 거리 계산 등에 씁니다.</summary>
    public Transform Head => head != null ? head : transform;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (head == null)
        {
            var h = new GameObject("Head").transform;
            h.SetParent(transform, false);
            h.localPosition = new Vector3(0f, defaultEyeHeight, 0f);
            head = h;
        }
    }

    /// <summary>
    /// 지금 카메라가 보고 있는 자리 그대로 몸을 세우고 조작을 넘겨받습니다.
    /// "타임라인이 보여준 그 위치에서" 걷기 시작하게 하려는 것입니다.
    /// </summary>
    public void BeginControl(Camera cam)
    {
        targetCamera = cam;
        if (controller == null) controller = GetComponent<CharacterController>();

        Vector3 viewPos = cam.transform.position;
        Vector3 viewEuler = cam.transform.eulerAngles;

        // 카메라 바로 아래 바닥을 찾아 발을 붙인다.
        // CharacterController가 켜진 채로 옮기면 위치 변경이 무시될 수 있어 잠깐 끈다.
        controller.enabled = false;

        float groundY = viewPos.y - defaultEyeHeight;
        hasGround = Physics.Raycast(viewPos + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 10f,
                                    ~0, QueryTriggerInteraction.Ignore);
        if (hasGround)
            groundY = hit.point.y;
        else
            Debug.LogWarning("[FreeRoamMovement] 발밑에 바닥 콜라이더가 없습니다. 중력 없이 이 높이로 걷습니다. " +
                             "(맵에 바닥 콜라이더를 넣으면 자동으로 중력이 적용됩니다)");

        float eye = eyeHeight > 0f
            ? eyeHeight
            : Mathf.Clamp(viewPos.y - groundY, eyeHeightRange.x, eyeHeightRange.y);
        head.localPosition = new Vector3(0f, eye, 0f);

        yaw = viewEuler.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, viewEuler.x), -pitchLimit, pitchLimit);

        transform.SetPositionAndRotation(new Vector3(viewPos.x, groundY, viewPos.z),
                                         Quaternion.Euler(0f, yaw, 0f));
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        controller.enabled = true;
        verticalVelocity = Vector3.zero;
        startFeetPosition = transform.position;
        startYaw = yaw;
        controlling = true;

        ApplyToCamera();
        Debug.Log($"[FreeRoamMovement] 조작 시작 - 위치 {transform.position}, 눈 높이 {eye:0.00}");
    }

    /// <summary>탐색을 시작했던 자리/방향으로 되돌립니다. (목격자에게 들켰을 때 등)</summary>
    public void ResetToStart()
    {
        controller.enabled = false;
        transform.position = startFeetPosition;
        controller.enabled = true;
        verticalVelocity = Vector3.zero;
        SetLook(startYaw, 0f);
    }

    /// <summary>조작을 멈춥니다. 카메라는 더 이상 건드리지 않습니다.</summary>
    public void EndControl()
    {
        controlling = false;
        InputPaused = false;
        CameraShakeEuler = Vector3.zero;
        targetCamera = null;
        verticalVelocity = Vector3.zero;
    }

    private void Update()
    {
        if (!controlling || InputPaused) return;

        // ── 시점: 좌우는 몸 전체, 위아래는 머리만 ──
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -pitchLimit, pitchLimit);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // ── 이동: W/S = Vertical, A/D = Horizontal (Unity 기본 Input Manager 축) ──
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * h + transform.forward * v;
        if (move.sqrMagnitude > 1f)
            move.Normalize();

        if (hasGround)
        {
            if (controller.isGrounded && verticalVelocity.y < 0f)
                verticalVelocity.y = -2f; // 바닥에 살짝 눌러 붙여서 isGrounded가 계속 true가 되게 함

            verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = Vector3.zero;
        }

        Vector3 before = transform.position;
        controller.Move((move * walkSpeed + verticalVelocity) * Time.deltaTime);
        UpdateFootsteps(before);

        // 맵 구멍으로 떨어지면 시작 위치로 되돌린다.
        if (transform.position.y < startFeetPosition.y - fallResetDepth)
        {
            Debug.LogWarning("[FreeRoamMovement] 너무 아래로 떨어져 시작 위치로 되돌립니다.");
            controller.enabled = false;
            transform.position = startFeetPosition;
            controller.enabled = true;
            verticalVelocity = Vector3.zero;
        }
    }

    // 실제로 움직인 거리만큼 쌓아서 일정 거리마다 발소리를 낸다. (벽에 막혀 있으면 안 난다)
    private void UpdateFootsteps(Vector3 before)
    {
        if (footstepClip == null) return;

        Vector3 delta = transform.position - before;
        delta.y = 0f;
        bool grounded = !hasGround || controller.isGrounded;
        if (!grounded || delta.sqrMagnitude < 0.000001f)
        {
            // 멈추면 다음 첫 걸음이 바로 나도록 절반쯤 채워 둔다.
            stepAccum = Mathf.Min(stepAccum, stepDistance * 0.5f);
            return;
        }

        stepAccum += delta.magnitude;
        if (stepAccum < stepDistance) return;
        stepAccum = 0f;

        if (footSource == null)
        {
            footSource = gameObject.AddComponent<AudioSource>();
            footSource.playOnAwake = false;
            footSource.spatialBlend = 0f;
        }
        footSource.pitch = Random.Range(footstepPitch.x, footstepPitch.y);
        footSource.PlayOneShot(footstepClip, footstepVolume * Random.Range(0.85f, 1f));
    }

    // 이동이 모두 끝난 뒤에 카메라를 옮겨야 떨림이 없다.
    private void LateUpdate()
    {
        if (!controlling) return;
        ApplyToCamera();
    }

    private void ApplyToCamera()
    {
        if (targetCamera == null) return;
        targetCamera.transform.SetPositionAndRotation(head.position, head.rotation * Quaternion.Euler(CameraShakeEuler));
    }
}
