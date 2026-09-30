using System.Collections;
using UnityEngine;

// Who 사망 연출용 카메라 효과: 약한 흔들림 + 렌즈(FOV) 좁히기.
//
// ── CinemachineBrain 주의 ──
// Main Camera에 CinemachineBrain이 붙어 있으면 카메라의 위치와 FOV를 매 LateUpdate마다
// 가상 카메라 값으로 덮어씁니다. 그래서 이 스크립트가 값을 써봐야 곧바로 되돌아갑니다.
// 연출하는 동안에만 Brain을 꺼서 카메라를 직접 잡고, 끝나면 원래대로 되돌립니다.
// (Cinemachine 타입을 직접 참조하지 않도록 Behaviour로 받습니다. 버전이 바뀌어도 안전합니다.)
public class CameraGlitchEffect : MonoBehaviour
{
    [Header("연동")]
    [Tooltip("연출을 적용할 카메라. 비워두면 Camera.main을 사용합니다.")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Main Camera의 CinemachineBrain 컴포넌트. 연출 동안 잠시 꺼서 카메라를 직접 제어합니다. " +
             "Cinemachine을 쓰지 않는다면 비워두세요.")]
    [SerializeField] private Behaviour cinemachineBrain;

    [Header("흔들림")]
    [Tooltip("흔들림 최대 크기(월드 단위). 작게 둘수록 약하게 떨립니다.")]
    [SerializeField] private float shakeAmplitude = 0.04f;

    [Tooltip("흔들림 속도. 높을수록 잘게 떱니다.")]
    [SerializeField] private float shakeFrequency = 22f;

    [Tooltip("흔들림 세기가 시간에 따라 커지는 정도. 0이면 처음부터 일정하게, 1이면 끝으로 갈수록 강해집니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float shakeRampUp = 1f;

    [Header("렌즈 (FOV)")]
    [Tooltip("연출이 끝날 때 도달할 FOV. 시작 FOV에서 이 값까지 서서히 좁아집니다.")]
    [SerializeField] private float targetFieldOfView = 22f;

    [Tooltip("FOV가 좁아지는 곡선. 기본은 천천히 시작해서 끝에 확 좁아집니다.")]
    [SerializeField]
    private AnimationCurve fovCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 2f, 2f));

    private Coroutine playRoutine;

    // 연출 전 원래 값 (복구용)
    private float originalFov;
    private Vector3 originalPosition;
    private bool brainWasEnabled;
    private bool stateCaptured = false;

    public bool IsPlaying => playRoutine != null;

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;
        targetCamera = Camera.main;
        return targetCamera;
    }

    /// <summary>
    /// 흔들림 + FOV 좁히기를 duration 동안 재생합니다.
    /// 끝나도 원래 값으로 되돌리지 않습니다 (씬을 다시 로드하는 용도).
    /// 되돌리려면 StopAndRestore()를 호출하세요.
    /// </summary>
    public void Play(float duration, System.Action onFinished = null)
    {
        Camera cam = ResolveCamera();
        if (cam == null)
        {
            Debug.LogWarning("[CameraGlitchEffect] 카메라를 찾을 수 없어 연출을 건너뜁니다.");
            onFinished?.Invoke();
            return;
        }

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(PlayRoutine(cam, Mathf.Max(0.01f, duration), onFinished));
    }

    /// <summary>
    /// 연출을 중단하고 카메라를 원래 상태로 되돌립니다.
    /// </summary>
    public void StopAndRestore()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (!stateCaptured) return;

        Camera cam = ResolveCamera();
        if (cam != null)
        {
            cam.fieldOfView = originalFov;
            cam.transform.position = originalPosition;
        }

        if (cinemachineBrain != null)
            cinemachineBrain.enabled = brainWasEnabled;

        stateCaptured = false;
    }

    private IEnumerator PlayRoutine(Camera cam, float duration, System.Action onFinished)
    {
        // 원래 상태 기록
        originalFov = cam.fieldOfView;
        originalPosition = cam.transform.position;
        brainWasEnabled = cinemachineBrain != null && cinemachineBrain.enabled;
        stateCaptured = true;

        // Cinemachine이 매 프레임 카메라를 덮어쓰지 않도록 잠시 끈다.
        if (cinemachineBrain != null)
            cinemachineBrain.enabled = false;
        else
            Debug.Log("[CameraGlitchEffect] cinemachineBrain이 연결되어 있지 않습니다. " +
                      "Cinemachine을 쓰고 있다면 연결하세요. 아니면 무시해도 됩니다.");

        Debug.Log($"[CameraGlitchEffect] 카메라 연출 시작 ({duration:0.00}초, FOV {originalFov:0.#} -> {targetFieldOfView:0.#})");

        // 흔들림이 매번 다른 모양이 되도록 노이즈 시작점을 무작위로
        float seedX = Random.value * 100f;
        float seedY = Random.value * 100f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.Clamp01(elapsed / duration);

            // 렌즈를 서서히 좁힌다.
            cam.fieldOfView = Mathf.Lerp(originalFov, targetFieldOfView, fovCurve.Evaluate(t));

            // 펄린 노이즈로 부드럽게 흔든다. (완전 난수보다 덜 지저분함)
            float ramp = Mathf.Lerp(1f, t, shakeRampUp);
            float n = Time.time * shakeFrequency;
            float offsetX = (Mathf.PerlinNoise(seedX + n, 0f) - 0.5f) * 2f;
            float offsetY = (Mathf.PerlinNoise(0f, seedY + n) - 0.5f) * 2f;

            Transform ct = cam.transform;
            cam.transform.position = originalPosition
                                     + ct.right * (offsetX * shakeAmplitude * ramp)
                                     + ct.up * (offsetY * shakeAmplitude * ramp);

            elapsed += Time.deltaTime;
            yield return null;
        }

        cam.fieldOfView = targetFieldOfView;
        cam.transform.position = originalPosition;

        playRoutine = null;

        Debug.Log("[CameraGlitchEffect] 카메라 연출 종료");

        onFinished?.Invoke();
    }
}
