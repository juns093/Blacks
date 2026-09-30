using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

// 카메라의 렌즈(FOV) 값을 조절하는 중앙 컨트롤러.
//
// ── 왜 이게 필요한가 ──
// Main Camera에 CinemachineBrain이 붙어 있어서, Camera.fieldOfView를 직접 써봐야
// 매 LateUpdate마다 활성 가상 카메라의 렌즈 값으로 덮어써집니다.
// 그래서 FOV를 "계속 유지되게" 바꾸려면 가상 카메라들의 렌즈 값 자체를 바꿔야 합니다.
//
// 씬에 하나만 두고, 아이템 등에서 AddFieldOfView()를 호출하면 됩니다.
public class CameraLensController : MonoBehaviour
{
    public static CameraLensController Instance { get; private set; }

    [Header("대상")]
    [Tooltip("비워두면 씬에 있는 모든 CinemachineVirtualCamera를 자동으로 찾습니다. " +
             "특정 카메라만 바꾸고 싶으면 여기에 직접 등록하세요.")]
    [SerializeField] private List<CinemachineVirtualCamera> virtualCameras = new List<CinemachineVirtualCamera>();

    [Tooltip("Cinemachine을 쓰지 않는 경우를 위한 예비 카메라. 비워두면 Camera.main을 씁니다.")]
    [SerializeField] private Camera fallbackCamera;

    [Header("제한")]
    [Tooltip("FOV가 이 값보다 커지지 않도록 제한합니다.")]
    [SerializeField] private float maxFieldOfView = 110f;

    [Tooltip("FOV가 이 값보다 작아지지 않도록 제한합니다.")]
    [SerializeField] private float minFieldOfView = 10f;

    // 가상 카메라별 원래 FOV (초기화용)
    private readonly Dictionary<CinemachineVirtualCamera, float> baseFov =
        new Dictionary<CinemachineVirtualCamera, float>();

    private float fallbackBaseFov;

    // 지금까지 누적된 FOV 증가량
    public float AccumulatedOffset { get; private set; } = 0f;

    private Coroutine lerpRoutine;

    private void LateUpdate()
    {
        // 아이템 연출(Timeline) 등 외부에서 가상 카메라의 Lens.FieldOfView를 직접 건드리는 경우,
        // 연출이 끝난 뒤에도 그 값이 남아 다음 카메라 전환 시 엉뚱한 FOV(예: 60이어야 하는데 78)로 보일 수 있다.
        // 그래서 매 프레임 "기준 FOV + 현재 오프셋"을 강제로 재적용해 항상 값이 맞게 유지되도록 한다.
        ApplyOffset(AccumulatedOffset);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CameraLensController] 씬에 인스턴스가 두 개 이상 존재합니다. 하나만 두세요.");
            Destroy(this);
            return;
        }
        Instance = this;

        if (virtualCameras == null || virtualCameras.Count == 0)
        {
            var found = FindObjectsByType<CinemachineVirtualCamera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            virtualCameras = new List<CinemachineVirtualCamera>(found);
            Debug.Log($"[CameraLensController] 가상 카메라 {virtualCameras.Count}개를 자동으로 찾았습니다.");
        }

        foreach (var vcam in virtualCameras)
            if (vcam != null && !baseFov.ContainsKey(vcam))
                baseFov[vcam] = vcam.m_Lens.FieldOfView;

        if (fallbackCamera == null) fallbackCamera = Camera.main;
        if (fallbackCamera != null) fallbackBaseFov = fallbackCamera.fieldOfView;
    }

    /// <summary>
    /// 현재 FOV에서 delta만큼 더합니다(음수면 좁아집니다). duration에 걸쳐 부드럽게 변합니다.
    /// </summary>
    public void AddFieldOfView(float delta, float duration = 0.5f)
    {
        SetOffset(AccumulatedOffset + delta, duration);
    }

    /// <summary>
    /// 원래 FOV 대비 offset만큼 벌어진 상태로 맞춥니다.
    /// </summary>
    public void SetOffset(float offset, float duration = 0.5f)
    {
        float from = AccumulatedOffset;
        float to = offset;

        if (lerpRoutine != null)
            StopCoroutine(lerpRoutine);

        Debug.Log($"[CameraLensController] FOV 오프셋 {from:0.#} -> {to:0.#} ({duration:0.00}초)");

        if (duration <= 0f)
        {
            ApplyOffset(to);
            return;
        }

        lerpRoutine = StartCoroutine(LerpOffsetRoutine(from, to, duration));
    }

    /// <summary>
    /// 렌즈를 원래 값으로 되돌립니다.
    /// </summary>
    public void ResetLens(float duration = 0.5f)
    {
        SetOffset(0f, duration);
    }

    private IEnumerator LerpOffsetRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            ApplyOffset(Mathf.Lerp(from, to, t));
            yield return null;
        }

        ApplyOffset(to);
        lerpRoutine = null;
    }

    private void ApplyOffset(float offset)
    {
        AccumulatedOffset = offset;

        foreach (var vcam in virtualCameras)
        {
            if (vcam == null) continue;
            if (!baseFov.TryGetValue(vcam, out float bfov)) continue;

            // m_Lens는 구조체 필드라 이렇게 바로 대입하면 됩니다.
            vcam.m_Lens.FieldOfView = Mathf.Clamp(bfov + offset, minFieldOfView, maxFieldOfView);
        }

        // 가상 카메라가 하나도 없으면 일반 카메라에 직접 적용
        if (virtualCameras.Count == 0 && fallbackCamera != null)
            fallbackCamera.fieldOfView = Mathf.Clamp(fallbackBaseFov + offset, minFieldOfView, maxFieldOfView);
    }
}
