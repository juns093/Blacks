using UnityEngine;
using System.Collections;

public class CamMove : MonoBehaviour
{
    [Header("Mouse Look Speed")]
    [SerializeField] private float lookSpeed; // 마우스 회전 속도
    [SerializeField] private float damping;    // 회전 가속/감속 (낮으면 마우스에 즉각 반응, 높으면 부드럽게 감속)

    [Header("Look Angle Limits")]
    [SerializeField] private float minLookAngleX; // 위쪽 최대 시야각 제한
    [SerializeField] private float maxLookAngleX; // 아래쪽 최대 시야각 제한
    [SerializeField] private float maxLookAngleY; // 좌우 최대 시야각 제한

    [Header("Dead Zone Boundaries (0 to 0.5)")]
    [Range(0f, 0.5f)][SerializeField] private float screenBoundaryX; // 화면 가장자리 기준 회전 시작 구역
    [Range(0f, 0.5f)][SerializeField] private float screenBoundaryY;

    [Header("Gizmo Settings")]
    [SerializeField] private bool showGizmo = true;

    [Header("Interaction Settings")]
    [SerializeField] private LayerMask interactableLayer; // 상호작용 감지할 레이어
    [SerializeField] private float interactDistance = 10f; // 상호작용 감지 최대 거리

    [Header("Cinemachine 연동")]
    [Tooltip("실제 렌더링에 쓰이는 Camera 컴포넌트를 연결하세요 (Cinemachine이 최종적으로 움직이는 Main Camera). " +
             "비워두면 Camera.main을 자동으로 사용합니다. " +
             "※ 회전 자체는 이 컴포넌트가 붙은 오브젝트(리그)를 제어하며, 이 필드는 레이캐스트/뷰포트 계산에만 사용됩니다.")]
    [SerializeField] private Camera targetCamera;

    [Header("Look At & Hold Settings")]
    public Vector3 lookTargetEuler;        // 카메라가 바라볼 목표 회전값 (Euler, Inspector에서 직접 입력)
    public float lookMoveDuration = 0.4f;  // 목표 회전까지 이동하는 시간(초)
    public float lookHoldDuration = 1.0f;  // 목표 도달 후 카메라를 고정할 시간(초)

    private float targetRotationX;
    private float targetRotationY;
    private float currentRotationX;
    private float currentRotationY;
    private float startRotationY;
    private InteractableObject lastHoveredObject;
    private float savedRotationX;
    private float savedRotationY;
    private float savedStartRotationY;
    private bool hasSavedRotation = false;

    // 외부에서 상호작용 감지를 일시 정지할 때 사용 (예: 컷씬 재생 중)
    public static bool blockInteraction = false;
    // 외부에서 카메라 회전을 일시 정지할 때 사용 (예: 조준 애니메이션 중)
    public static bool blockLook = false;

    // 싱글턴: Player Camera가 Main Camera의 자식이 아니어도(형제 관계여도)
    // 다른 스크립트(GunObject 등)에서 CamMove.Instance로 바로 접근 가능
    public static CamMove Instance { get; private set; }

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // 마우스가 프레임과 상관없이 화면 밖으로 나가지 않도록 설정
        Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = true;

        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            Debug.LogWarning("[CamMove] targetCamera가 지정되지 않았고 Camera.main도 찾을 수 없습니다! 인스펙터에서 실제 렌더링용 Camera를 연결해주세요.");

        SyncRotation();
    }

    // 타임라인 등 외부에서 카메라 회전이 강제로 변경된 후, 
    // 마우스 회전값(변수)을 현재 카메라 회전 상태와 동기화하여 화면이 튀는 것을 방지합니다.
    public void SyncRotation()
    {
        startRotationY = transform.localEulerAngles.y;

        targetRotationX = transform.localEulerAngles.x;
        if (targetRotationX > 180f) targetRotationX -= 360f;
        currentRotationX = targetRotationX;

        targetRotationY = 0f;
        currentRotationY = 0f;
    }

    // 카메라를 목표 회전까지 부드럽게 이동 후 holdDuration 동안 고정 후 복귀
    // euler/moveDuration/holdDuration 생략 시 Inspector 필드값 사용
    public void LookAtAndHold(Vector3? euler = null, float moveDuration = -1f, float holdDuration = -1f)
    {
        Quaternion destRot = Quaternion.Euler(euler ?? lookTargetEuler);
        float move = moveDuration < 0f ? lookMoveDuration : moveDuration;
        float hold = holdDuration < 0f ? lookHoldDuration : holdDuration;
        StartCoroutine(LookAtAndHoldCoroutine(destRot, move, hold));
    }

    // 회전(Quaternion) 값을 직접 전달하는 버전
    public void LookAtRotationAndHold(Quaternion destWorldRot, float moveDuration = -1f, float holdDuration = -1f)
    {
        float move = moveDuration < 0f ? lookMoveDuration : moveDuration;
        float hold = holdDuration < 0f ? lookHoldDuration : holdDuration;
        StartCoroutine(LookAtAndHoldCoroutine(destWorldRot, move, hold));
    }

    private IEnumerator LookAtAndHoldCoroutine(Quaternion destWorldRot, float moveDuration, float holdDuration)
    {
        blockLook = true;
        blockInteraction = true;

        // hold가 끝난 뒤 원래 각도(예: 초기 카메라 자세 Vector3(4,0,0))로 되돌아가기 위해 저장해둔다.
        // 저장이 이미 되어있는 상태(중첩 호출)라면 덮어쓰지 않는다.
        if (!hasSavedRotation)
        {
            savedRotationX = currentRotationX;
            savedRotationY = currentRotationY;
            savedStartRotationY = startRotationY;
            hasSavedRotation = true;
        }

        // 목표 worldRot을 내부 X/Y 상대값으로 변환
        Vector3 destEuler = destWorldRot.eulerAngles;
        float destX = destEuler.x > 180f ? destEuler.x - 360f : destEuler.x;
        float destY = destEuler.y - startRotationY;

        float fromX = currentRotationX;
        float fromY = currentRotationY;

        // 최단 경로로 회전하도록 목표값을 fromX/fromY 기준 최단 각도 차이로 재계산
        float deltaX = Mathf.DeltaAngle(fromX, destX);
        float deltaY = Mathf.DeltaAngle(fromY, destY);
        float shortestDestX = fromX + deltaX;
        float shortestDestY = fromY + deltaY;

        float elapsed = 0f;

        // 목표 회전까지 부드럽게 이동
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, moveDuration)));
            currentRotationX = Mathf.Lerp(fromX, shortestDestX, t);
            currentRotationY = Mathf.Lerp(fromY, shortestDestY, t);
            targetRotationX = currentRotationX;
            targetRotationY = currentRotationY;
            transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);
            yield return null;
        }

        // 목표 회전 고정
        currentRotationX = shortestDestX;
        currentRotationY = shortestDestY;
        targetRotationX = shortestDestX;
        targetRotationY = shortestDestY;
        transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);

        // holdDuration 동안 대기
        if (holdDuration > 0f)
            yield return new WaitForSeconds(holdDuration);

        // hold가 끝나면 원래 각도(예: Vector3(4,0,0))로 부드럽게 복귀한다.
        yield return StartCoroutine(ReturnFromHoldCoroutine(moveDuration));
    }
    /// <summary>
    /// 리그의 로컬 회전을 지정한 값(예: 정면 Vector3(4,0,0))으로 부드럽게 돌립니다.
    /// 도는 동안과 끝난 뒤에도 카메라는 잠긴 상태로 둡니다. (잠금 해제는 턴/연출 쪽이 담당)
    /// </summary>
    public IEnumerator RotateToLocalEuler(Vector3 localEuler, float duration)
    {
        blockLook = true;
        blockInteraction = true;
        hasSavedRotation = false;

        Quaternion from = transform.localRotation;
        Quaternion to = Quaternion.Euler(localEuler);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration)));
            transform.localRotation = Quaternion.Slerp(from, to, t);
            yield return null;
        }

        transform.localRotation = to;

        // 마우스 회전 기준을 새 자세에 맞춘다. (다음에 풀렸을 때 화면이 튀지 않게)
        SyncRotation();
    }

    // euler(Vector3) 버전
    public void LookAtAndHoldIndefinite(Vector3? euler = null, float moveDuration = -1f)
    {
        Quaternion destRot = Quaternion.Euler(euler ?? lookTargetEuler);
        float move = moveDuration < 0f ? lookMoveDuration : moveDuration;
        StartCoroutine(LookAtAndHoldIndefiniteCoroutine(destRot, move));
    }

    // Quaternion(월드 회전값) 버전 - 예: 상자를 바라보는 Transform.rotation을 그대로 넘길 때 사용
    public void LookAtRotationAndHoldIndefinite(Quaternion destWorldRot, float moveDuration = -1f)
    {
        float move = moveDuration < 0f ? lookMoveDuration : moveDuration;
        StartCoroutine(LookAtAndHoldIndefiniteCoroutine(destWorldRot, move));
    }

    private IEnumerator LookAtAndHoldIndefiniteCoroutine(Quaternion destWorldRot, float moveDuration)
    {
        blockLook = true;
        blockInteraction = true;

        // 나중에 ReturnFromHold()가 돌아갈 기준점을 저장 (이미 저장된 상태라면 덮어쓰지 않음 -> 중첩 호출 방지)
        if (!hasSavedRotation)
        {
            savedRotationX = currentRotationX;
            savedRotationY = currentRotationY;
            savedStartRotationY = startRotationY;
            hasSavedRotation = true;
        }

        Vector3 destEuler = destWorldRot.eulerAngles;
        float destX = destEuler.x > 180f ? destEuler.x - 360f : destEuler.x;
        float destY = destEuler.y - startRotationY;

        float fromX = currentRotationX;
        float fromY = currentRotationY;

        // 최단 경로로 회전
        float deltaX = Mathf.DeltaAngle(fromX, destX);
        float deltaY = Mathf.DeltaAngle(fromY, destY);
        float shortestDestX = fromX + deltaX;
        float shortestDestY = fromY + deltaY;

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, moveDuration)));
            currentRotationX = Mathf.Lerp(fromX, shortestDestX, t);
            currentRotationY = Mathf.Lerp(fromY, shortestDestY, t);
            targetRotationX = currentRotationX;
            targetRotationY = currentRotationY;
            transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);
            yield return null;
        }

        currentRotationX = shortestDestX;
        currentRotationY = shortestDestY;
        targetRotationX = shortestDestX;
        targetRotationY = shortestDestY;
        transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);

        // 여기서는 blockLook/blockInteraction을 풀지 않습니다.
        // ReturnFromHold()가 호출될 때까지 카메라는 이 자리에 계속 고정되어 있습니다.
    }

    // 외부(ItemBoxInteract 등)에서 "뽑기가 모두 끝났다"고 판단했을 때 호출하세요.
    // 뽑기 시작 전 저장해둔 원래 회전으로 부드럽게 복귀한 뒤 카메라 잠금을 해제합니다.
    public void ReturnFromHold(float moveDuration = -1f)
    {
        if (!hasSavedRotation)
        {
            // 저장된 원래 회전이 없으면(비정상 호출) 잠금만 풀어줌
            blockLook = false;
            blockInteraction = false;
            return;
        }

        float move = moveDuration < 0f ? lookMoveDuration : moveDuration;
        StartCoroutine(ReturnFromHoldCoroutine(move));
    }

    private IEnumerator ReturnFromHoldCoroutine(float moveDuration)
    {
        float fromX = currentRotationX;
        float fromY = currentRotationY;
        float toX = savedRotationX;
        float toY = savedRotationY;

        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, moveDuration)));
            currentRotationX = Mathf.Lerp(fromX, toX, t);
            currentRotationY = Mathf.Lerp(fromY, toY, t);
            targetRotationX = currentRotationX;
            targetRotationY = currentRotationY;
            transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);
            yield return null;
        }

        currentRotationX = toX;
        currentRotationY = toY;
        targetRotationX = toX;
        targetRotationY = toY;
        transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);

        hasSavedRotation = false;

        // 카메라 회전 및 상호작용 재활성화
        blockLook = false;
        blockInteraction = false;
    }

    void Update()
    {
        if (!blockLook)
            HandleDeadZoneRotation();
        HandleInteraction(); // 마우스 오버시 상호작용 대상 감지

        // ESC로 마우스 커서 일시 해제
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Confined ? CursorLockMode.None : CursorLockMode.Confined;
        }
    }

    void HandleDeadZoneRotation()
    {
        if (targetCamera == null) return;

        // 현재 마우스의 뷰포트 좌표 구하기 (0,0 ~ 1,1)
        Vector3 mousePos = Input.mousePosition;
        if (float.IsInfinity(mousePos.x) || float.IsInfinity(mousePos.y) ||
            float.IsNaN(mousePos.x) || float.IsNaN(mousePos.y)) return;

        Vector3 viewportPos = targetCamera.ScreenToViewportPoint(mousePos);
        if (float.IsNaN(viewportPos.x) || float.IsNaN(viewportPos.y)) return;

        float rotateAmountX = 0f;
        float rotateAmountY = 0f;

        // X축 회전 검사 (마우스가 화면 좌우 경계선을 넘었을 때)
        if (viewportPos.x < screenBoundaryX)
        {
            float intensity = (screenBoundaryX - viewportPos.x) / screenBoundaryX;
            rotateAmountY = -intensity * lookSpeed * Time.deltaTime * 100f;
        }
        else if (viewportPos.x > 1f - screenBoundaryX)
        {
            float intensity = (viewportPos.x - (1f - screenBoundaryX)) / screenBoundaryX;
            rotateAmountY = intensity * lookSpeed * Time.deltaTime * 100f;
        }

        // Y축 회전 검사 (마우스가 화면 상하 경계선을 넘었을 때)
        if (viewportPos.y < screenBoundaryY)
        {
            float intensity = (screenBoundaryY - viewportPos.y) / screenBoundaryY;
            rotateAmountX = intensity * lookSpeed * Time.deltaTime * 100f;
        }
        else if (viewportPos.y > 1f - screenBoundaryY)
        {
            float intensity = (viewportPos.y - (1f - screenBoundaryY)) / screenBoundaryY;
            rotateAmountX = -intensity * lookSpeed * Time.deltaTime * 100f;
        }

        // 목표 회전값 누적 후 각도 제한(Clamp)
        targetRotationY += rotateAmountY;
        targetRotationX += rotateAmountX;

        targetRotationX = Mathf.Clamp(targetRotationX, minLookAngleX, maxLookAngleX);
        targetRotationY = Mathf.Clamp(targetRotationY, -maxLookAngleY, maxLookAngleY);

        // Lerp를 사용해 현재 회전값이 목표 회전값으로 부드럽게 감쇠(Damping)하며 이동
        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, damping * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, damping * Time.deltaTime);

        // 카메라 자신이 아니라 "이 오브젝트(리그)"의 로컬 회전을 갱신합니다.
        // Cinemachine vcam은 이 리그의 자식이며 Aim/Body가 Do Nothing이므로
        // 이 회전값을 그대로 상속받아 최종적으로 Main Camera까지 전달됩니다.
        transform.localRotation = Quaternion.Euler(currentRotationX, startRotationY + currentRotationY, 0f);
    }

    void HandleInteraction()
    {
        if (targetCamera == null) return;

        // 조준 중에는 레이캐스트 자체 중지
        if (blockInteraction)
        {
            if (lastHoveredObject != null)
            {
                lastHoveredObject.HideInteractionUI();
                lastHoveredObject = null;
            }
            return;
        }

        Vector3 mp = Input.mousePosition;
        if (float.IsInfinity(mp.x) || float.IsInfinity(mp.y) ||
            float.IsNaN(mp.x) || float.IsNaN(mp.y)) return;

        // 마우스 포인터 위치로부터 화면 안쪽으로 Ray를 발사
        Ray ray = targetCamera.ScreenPointToRay(mp);
        RaycastHit hit;

        // 레이의 방향을 시각화 (Game/Scene 뷰에서 노란색으로 보임)
        Debug.DrawRay(ray.origin, ray.direction * interactDistance, Color.yellow);

        if (Physics.Raycast(ray, out hit, interactDistance, interactableLayer))
        {
            // 충돌한 객체에서 InteractableObject 컴포넌트 획득
            InteractableObject hoverObject = hit.collider.GetComponent<InteractableObject>();

            // Mesh Collider가 자식 오브젝트에 붙어있는 경우, 부모 오브젝트에서 찾아도 시도합니다.
            if (hoverObject == null)
            {
                hoverObject = hit.collider.GetComponentInParent<InteractableObject>();
            }

            if (hoverObject != null)
            {
                // 이전에 바라보던 객체와 다른 새로운 객체일 경우
                if (lastHoveredObject != hoverObject)
                {
                    if (lastHoveredObject != null)
                    {
                        lastHoveredObject.HideInteractionUI();
                    }
                    hoverObject.ShowInteractionUI();
                    lastHoveredObject = hoverObject;
                    Debug.Log($"[CamMove] 마우스 호버: {hoverObject.gameObject.name}");
                }

                // 마우스 클릭 시 상호작용 작동
                if (Input.GetMouseButtonDown(0))
                {
                    hoverObject.Interact();
                }
            }
            else
            {
                Debug.LogWarning($"[CamMove] '{hit.collider.gameObject.name}' 오브젝트가 '{LayerMask.LayerToName(hit.collider.gameObject.layer)}' 레이어에 있지만, InteractableObject 컴포넌트가 없습니다!");
            }
        }
        else
        {
            // 마우스 포인터가 아무 것도 가리키지 않을 때 UI 해제
            if (lastHoveredObject != null)
            {
                lastHoveredObject.HideInteractionUI();
                lastHoveredObject = null;
            }
        }
    }

    void OnDrawGizmos()
    {
        if (!showGizmo)
            return;

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null)
            return;

        // 초록색 사각형: 안전 구역 (이 안에서는 마우스가 움직여도 화면이 회전하지 않음)
        Gizmos.color = Color.green;
        Vector3 safeMin = cam.ViewportToWorldPoint(new Vector3(screenBoundaryX, screenBoundaryY, 10f));
        Vector3 safeMax = cam.ViewportToWorldPoint(new Vector3(1f - screenBoundaryX, 1f - screenBoundaryY, 10f));
        DrawRectangle(safeMin, safeMax);

        // 빨간색 사각형: 화면 바깥 경계
        Gizmos.color = Color.red;
        Vector3 screenMin = cam.ViewportToWorldPoint(new Vector3(0f, 0f, 10f));
        Vector3 screenMax = cam.ViewportToWorldPoint(new Vector3(1f, 1f, 10f));
        DrawRectangle(screenMin, screenMax);

        // 노란색 구체: 카메라 위치 기준 상호작용 감지 반경
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }

    void DrawRectangle(Vector3 min, Vector3 max)
    {
        Vector3[] corners = new Vector3[4]
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, max.y, max.z),
            new Vector3(min.x, max.y, max.z)
        };

        for (int i = 0; i < 4; i++)
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
    }
}