using System.Collections;
using UnityEngine;

// 실탄이 터지는 순간 시간을 아주 잠깐 늦췄다가 되돌리는 연출 (히트스톱).
// 타격감을 주는 가장 싼 방법이고, 총을 쏜 직후의 한 박자를 만들어 줍니다.
//
// 씬에 하나만 두세요. GunObject가 static Instance로 찾아서 호출합니다.
public class HitStopEffect : MonoBehaviour
{
    public static HitStopEffect Instance { get; private set; }

    [Header("기본값")]
    [Tooltip("늦출 때의 시간 배속. 0.85면 원래 속도의 85%로 살짝 느려집니다. " +
             "낮출수록 확 느려지고, 1에 가까울수록 미세해집니다.")]
    [Range(0.01f, 1f)]
    [SerializeField] private float slowScale = 0.85f;

    [Tooltip("느려진 상태를 유지하는 시간(초). 실제 시간 기준이라 배속의 영향을 받지 않습니다.")]
    [SerializeField] private float holdDuration = 0.15f;

    [Tooltip("원래 속도로 돌아오는 데 걸리는 시간(초). 0이면 툭 하고 즉시 돌아옵니다.")]
    [SerializeField] private float recoverDuration = 0.35f;

    [Tooltip("물리 업데이트 간격도 같이 조절할지 여부. 켜두면 느린 구간에서도 물리가 매끄럽습니다.")]
    [SerializeField] private bool scaleFixedDeltaTime = true;

    private float baseFixedDeltaTime;
    private Coroutine routine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[HitStopEffect] 씬에 인스턴스가 두 개 이상 존재합니다. 하나만 두세요.");
            Destroy(this);
            return;
        }
        Instance = this;

        baseFixedDeltaTime = Time.fixedDeltaTime;
    }

    private void OnDisable()
    {
        // 느려진 채로 꺼지면 게임 전체가 느려진 상태로 남으므로 반드시 되돌린다.
        RestoreImmediately();
    }

    /// <summary>
    /// 인스펙터 기본값으로 히트스톱을 재생합니다.
    /// </summary>
    public void Play()
    {
        Play(slowScale, holdDuration, recoverDuration);
    }

    /// <summary>
    /// 시간을 scale 배속으로 늦췄다가 recover초에 걸쳐 원래대로 되돌립니다.
    /// </summary>
    public void Play(float scale, float hold, float recover)
    {
        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(HitStopRoutine(Mathf.Clamp(scale, 0.01f, 1f),
                                               Mathf.Max(0f, hold),
                                               Mathf.Max(0f, recover)));
    }

    /// <summary>
    /// 시간 배속을 즉시 원래대로 되돌립니다.
    /// </summary>
    public void RestoreImmediately()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Time.timeScale = 1f;
        if (scaleFixedDeltaTime && baseFixedDeltaTime > 0f)
            Time.fixedDeltaTime = baseFixedDeltaTime;
    }

    private IEnumerator HitStopRoutine(float scale, float hold, float recover)
    {
        Debug.Log($"[HitStopEffect] 히트스톱: 배속 {scale:0.00} / 유지 {hold:0.00}초 / 복귀 {recover:0.00}초");

        SetScale(scale);

        // 느려진 동안에도 정확한 시간을 재야 하므로 Realtime으로 기다린다.
        if (hold > 0f)
            yield return new WaitForSecondsRealtime(hold);

        if (recover > 0f)
        {
            float elapsed = 0f;
            while (elapsed < recover)
            {
                elapsed += Time.unscaledDeltaTime;
                SetScale(Mathf.Lerp(scale, 1f, Mathf.Clamp01(elapsed / recover)));
                yield return null;
            }
        }

        SetScale(1f);
        routine = null;
    }

    private void SetScale(float scale)
    {
        Time.timeScale = scale;

        if (scaleFixedDeltaTime && baseFixedDeltaTime > 0f)
            Time.fixedDeltaTime = baseFixedDeltaTime * scale;
    }
}
