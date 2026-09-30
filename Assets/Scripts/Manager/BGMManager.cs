using System.Collections;
using UnityEngine;

// 게임 씬(GameScene) 전용 배경음악(BGM) 매니저.
//
// ── 사용 방식 ──
//  - Managers/AudioManager 같은 오브젝트에 AudioSource와 함께 붙여서 씁니다.
//  - 노이즈(Glitch)나 타임라인/대사가 재생되는 동안에는 RequestDuck()으로 볼륨을 줄이고,
//    끝나면 ReleaseDuck()으로 되돌립니다. 여러 군데서 동시에 덕킹을 요청할 수 있으므로
//    참조 카운트(duckRequestCount)로 관리해서, 하나라도 남아있으면 계속 줄어든 상태를 유지합니다.
//  - 마지막 승부에서 이겨서 엔딩으로 넘어갈 때는 FadeOutAndStop()으로 자연스럽게 페이드 아웃합니다.
[RequireComponent(typeof(AudioSource))]
public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;
    [Range(0f, 1f)][SerializeField] private float baseVolume = 0.5f;
    [SerializeField] private bool loop = true;
    [Tooltip("씬 시작과 동시에 자동으로 재생할지 여부")]
    [SerializeField] private bool playOnAwake = true;

    [Header("덕킹(볼륨 낮추기) 설정")]
    [Tooltip("덕킹 상태일 때 baseVolume에 곱해질 배율 (0.25면 25%로 줄어듦)")]
    [Range(0f, 1f)][SerializeField] private float duckVolumeMultiplier = 0.25f;
    [Tooltip("덕킹 볼륨으로 줄어들거나 복구되는 데 걸리는 기본 시간(초)")]
    [SerializeField] private float duckFadeDuration = 0.5f;

    private AudioSource source;
            private int duckRequestCount = 0;
    private int muteRequestCount = 0;
    private Coroutine fadeRoutine;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        source = GetComponent<AudioSource>();

        if (bgmClip != null)
        {
            source.clip = bgmClip;
            source.loop = loop;
            source.volume = baseVolume;

            if (playOnAwake)
                source.Play();
        }
        else
        {
            Debug.LogWarning("[BGMManager] bgmClip이 연결되어 있지 않습니다.");
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// BGM 볼륨을 낮춰달라고 요청합니다. (참조 카운트 증가)
    /// 노이즈/타임라인/대사 등 여러 군데에서 동시에 호출해도 안전하며,
    /// 모든 요청이 ReleaseDuck()으로 해제되어야 원래 볼륨으로 돌아갑니다.
    /// </summary>
        public void RequestDuck(float customFadeDuration = -1f)
    {
        duckRequestCount++;
        if (duckRequestCount == 1 && muteRequestCount == 0)
            StartFade(baseVolume * duckVolumeMultiplier, customFadeDuration > 0f ? customFadeDuration : duckFadeDuration);
    }

    /// <summary>
    /// 타임라인 재생중처럼 BGM을 완전히 무음으로 끄야 할 때 호출합니다. (참조 카운트 증가)
    /// RequestDuck과 독립적으로 동작하며, 둘 중 더 작은 볼륨(mute)이 우선됩니다.
    /// </summary>
    public void RequestMute(float customFadeDuration = -1f)
    {
        muteRequestCount++;
        if (muteRequestCount == 1)
            StartFade(0f, customFadeDuration > 0f ? customFadeDuration : duckFadeDuration);
    }

    /// <summary>
    /// RequestMute()로 요청했던 것을 하나 해제합니다. 모든 요청이 해제되면 덤킹/원래 볼륨으로 복귀됩니다.
    /// </summary>
    public void ReleaseMute(float customFadeDuration = -1f)
    {
        if (muteRequestCount <= 0) return;

        muteRequestCount--;
        if (muteRequestCount == 0)
        {
            float target = duckRequestCount > 0 ? baseVolume * duckVolumeMultiplier : baseVolume;
            StartFade(target, customFadeDuration > 0f ? customFadeDuration : duckFadeDuration);
                }
    }

    /// <summary>
    /// RequestDuck()으로 요청했던 것을 하나 해제합니다. 모든 요청이 해제되면 원래 볼륨으로 복구됩니다.
    /// </summary>
        public void ReleaseDuck(float customFadeDuration = -1f)
    {
        if (duckRequestCount <= 0) return;

        duckRequestCount--;
        if (duckRequestCount == 0 && muteRequestCount == 0)
            StartFade(baseVolume, customFadeDuration > 0f ? customFadeDuration : duckFadeDuration);
    }

    /// <summary>
    /// BGM을 서서히 무음으로 페이드아웃한 뒤 정지합니다. (승리 엔딩 전환 등에 사용)
    /// </summary>
    public void FadeOutAndStop(float duration, System.Action onComplete = null)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(0f, duration, () =>
        {
            source.Stop();
            onComplete?.Invoke();
        }));
    }

    /// <summary>
    /// 무음 상태에서 다시 재생하며 서서히 볼륨을 원래 값(baseVolume)까지 올립니다. (승리 엔딩 페이드 인 등에 사용)
    /// </summary>
    public void FadeIn(float duration, AudioClip clip = null)
    {
        if (clip != null)
            source.clip = clip;

        if (!source.isPlaying)
        {
            source.volume = 0f;
            source.Play();
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(baseVolume, duration, null));
    }

    private void StartFade(float target, float duration)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(target, duration, null));
    }

    private IEnumerator FadeRoutine(float target, float duration, System.Action onComplete)
    {
        float start = source.volume;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(start, target, elapsed / safeDuration);
            yield return null;
        }

        source.volume = target;
        fadeRoutine = null;
        onComplete?.Invoke();
    }
}
