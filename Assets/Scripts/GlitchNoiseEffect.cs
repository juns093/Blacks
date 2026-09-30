using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 화면 전체에 TV 지지직 노이즈(아날로그 스노우)를 씌우는 연출.
//
// 텍스처 에셋이나 포스트 프로세싱 설정 없이, 런타임에 흑백 난수 텍스처를 만들어 씁니다.
//
// ── 입자를 화면 픽셀 크기로 맞추는 방법 ──
// 텍스처를 화면 전체로 늘리면 입자가 뭉텅이로 커져서 레퍼런스와 달라집니다.
// 그래서 텍스처는 그대로 두고 RawImage의 uvRect를 (화면크기 / 텍스처크기)로 잡아
// 텍셀 1개 = 화면 픽셀 1개가 되도록 타일링합니다. 그러면 사진처럼 곱고 균일한 그레인이 나옵니다.
//
// ── 성능 ──
// 매 프레임 난수를 새로 채우면 비싸므로, 시작할 때 노이즈 텍스처 여러 장을 미리 만들어두고
// 프레임마다 그중 하나를 무작위로 고른 뒤 uvRect 오프셋까지 흔들어 줍니다.
// 실제로는 반복이 보이지 않으면서 런타임 비용은 거의 0입니다.
public class GlitchNoiseEffect : MonoBehaviour
{
    [Header("연동")]
    [Tooltip("화면 전체를 덮는 RawImage. 평소에는 꺼져 있다가 연출 중에만 켜집니다.")]
    [SerializeField] private RawImage target;

    [Header("노이즈 모양")]
    [Tooltip("미리 만들어 둘 노이즈 텍스처 한 변의 픽셀 수. 클수록 반복이 덜 보이지만 메모리를 더 씁니다.")]
    [Range(128, 1024)]
    [SerializeField] private int noiseResolution = 512;

    [Tooltip("미리 만들어 둘 노이즈 텍스처 장수. 많을수록 패턴 반복이 덜 느껴집니다.")]
    [Range(2, 16)]
    [SerializeField] private int noiseFrameCount = 8;

    [Tooltip("노이즈가 새로 바뀌는 초당 횟수. 낮으면 뚝뚝 끊기고, 높으면 매끄럽게 지지직거립니다.")]
    [Range(5, 60)]
    [SerializeField] private int noiseFps = 30;

    [Tooltip("입자 크기 배율. 1이면 텍셀 1개 = 화면 픽셀 1개(레퍼런스와 동일). " +
             "2로 올리면 입자가 2배 굵어집니다.")]
    [Range(0.5f, 8f)]
    [SerializeField] private float grainScale = 1f;

    [Tooltip("노이즈 밝기의 최소/최대 범위 (0~1). 기본값이 사진처럼 균일한 흑백 스노우입니다.")]
    [SerializeField] private Vector2 brightnessRange = new Vector2(0f, 1f);

    [Header("타이밍")]
    [Tooltip("알파가 0에서 1까지 올라가는 데 걸리는 시간(초)")]
    [SerializeField] private float fadeInDuration = 0.4f;

    [Tooltip("알파가 1이 된 뒤 그대로 유지하는 시간(초). 이 시간이 지나면 onFinished가 호출됩니다.")]
    [SerializeField] private float holdDuration = 0.5f;

    [Header("사운드 (선택)")]
    [Tooltip("노이즈 소리를 재생할 AudioSource")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("지지직거리는 노이즈 소리 클립. 루프되는 클립을 넣으세요.")]
    [SerializeField] private AudioClip noiseClip;

    [Tooltip("소리가 도달할 최대 볼륨 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float soundMaxVolume = 1f;

    private Texture2D[] noiseFrames;
    private Coroutine playRoutine;
    private Coroutine soundRoutine;
    private bool hasDuckedBgm = false;

    public bool IsPlaying => playRoutine != null;

    private void Awake()
    {
        if (target == null)
        {
            Debug.LogWarning("[GlitchNoiseEffect] target(RawImage)이 연결되어 있지 않습니다! 인스펙터에서 연결하세요.");
            return;
        }

        BuildNoiseFrames();

        // 시작할 땐 완전히 투명하고 꺼진 상태
        Color c = target.color;
        c.a = 0f;
        target.color = c;
        target.enabled = false;
    }

    // 노이즈 텍스처들을 미리 생성해 둔다.
    private void BuildNoiseFrames()
    {
        noiseFrames = new Texture2D[noiseFrameCount];

        int res = noiseResolution;
        int pixelCount = res * res;
        var buffer = new Color32[pixelCount];

        byte min = (byte)Mathf.Clamp(Mathf.RoundToInt(brightnessRange.x * 255f), 0, 255);
        byte max = (byte)Mathf.Clamp(Mathf.RoundToInt(brightnessRange.y * 255f), 0, 255);
        if (max < min) (min, max) = (max, min);

        for (int f = 0; f < noiseFrameCount; f++)
        {
            for (int i = 0; i < pixelCount; i++)
            {
                // 균일한 흑백 난수만 사용 (찢어짐 줄이나 컬러 깨짐 없음)
                byte v = (byte)Random.Range(min, max + 1);
                buffer[i] = new Color32(v, v, v, 255);
            }

            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
            {
                // 입자가 뭉개지지 않고 또렷하게 보이도록 Point 필터
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };
            tex.SetPixels32(buffer);
            tex.Apply(false);

            noiseFrames[f] = tex;
        }

        Debug.Log($"[GlitchNoiseEffect] 노이즈 텍스처 {noiseFrameCount}장 생성 완료 ({res}x{res})");
    }

    /// <summary>
    /// 노이즈를 알파 0에서 1까지 올린 뒤, holdDuration만큼 유지하고 onFinished를 호출합니다.
    /// 연출이 끝나도 노이즈는 화면에 그대로 남습니다 (씬을 다시 로드하는 용도라 걷어내지 않습니다).
    /// 직접 걷어내려면 StopImmediately()를 호출하세요.
    /// </summary>
    /// <param name="fadeIn">알파가 올라가는 시간(초). 0 이하면 인스펙터 값 사용</param>
    /// <param name="hold">알파 1을 유지하는 시간(초). 0 미만이면 인스펙터 값 사용</param>
    /// <param name="onFinished">유지 시간까지 끝난 뒤 호출할 콜백</param>
    public void Play(float fadeIn = -1f, float hold = -1f, System.Action onFinished = null)
    {
        if (target == null)
        {
            Debug.LogWarning("[GlitchNoiseEffect] target이 없어 노이즈 연출을 재생할 수 없습니다. 콜백만 호출합니다.");
            onFinished?.Invoke();
            return;
        }

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        float fi = fadeIn > 0f ? fadeIn : fadeInDuration;
        float hd = hold >= 0f ? hold : holdDuration;

        playRoutine = StartCoroutine(PlayRoutine(fi, hd, onFinished));
    }

    /// <summary>
    /// 연출을 즉시 중단하고 노이즈를 걷어냅니다.
    /// </summary>
    public void StopImmediately()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (target != null)
        {
            Color c = target.color;
            c.a = 0f;
            target.color = c;
            target.enabled = false;
        }

        StopSound();
    }

    /// <summary>
    /// 노이즈 소리를 볼륨 0에서 최대까지 서서히 키우며 재생합니다.
    /// 노이즈 화면(Play)보다 먼저 호출해서, 총을 든 상태부터 소리가 깔리도록 씁니다.
    /// </summary>
    /// <param name="rampDuration">볼륨이 0에서 최대까지 올라가는 데 걸리는 시간(초)</param>
    public void StartSound(float rampDuration)
    {
        if (audioSource == null || noiseClip == null)
        {
            Debug.LogWarning("[GlitchNoiseEffect] audioSource 또는 noiseClip이 비어 있어 노이즈 소리를 재생하지 않습니다.");
            return;
        }

        if (soundRoutine != null)
            StopCoroutine(soundRoutine);

        // 노이즈 소리가 나오는 동안엔 BGM과 겹치지 않도록 볼륨을 줄인다.
        if (BGMManager.Instance != null && !hasDuckedBgm)
        {
            hasDuckedBgm = true;
            BGMManager.Instance.RequestDuck();
        }

        soundRoutine = StartCoroutine(SoundRampRoutine(Mathf.Max(0.01f, rampDuration)));
    }

    /// <summary>
    /// 노이즈 소리를 즉시 멈춥니다.
    /// </summary>
    public void StopSound()
    {
        if (soundRoutine != null)
        {
            StopCoroutine(soundRoutine);
            soundRoutine = null;
        }

        if (audioSource != null && noiseClip != null && audioSource.clip == noiseClip)
        {
            audioSource.Stop();
            audioSource.loop = false;
        }

        // 노이즈 소리가 끝났으니 줄여뒀던 BGM 볼륨을 되돌린다.
        if (hasDuckedBgm && BGMManager.Instance != null)
        {
            hasDuckedBgm = false;
            BGMManager.Instance.ReleaseDuck();
        }
    }

    private IEnumerator SoundRampRoutine(float rampDuration)
    {
        Debug.Log($"[GlitchNoiseEffect] 노이즈 소리 시작 (볼륨 0 -> {soundMaxVolume:0.00}, {rampDuration:0.00}초에 걸쳐 상승)");

        audioSource.clip = noiseClip;
        audioSource.loop = true;
        audioSource.volume = 0f;
        audioSource.Play();

        float elapsed = 0f;
        while (elapsed < rampDuration)
        {
            // 노이즈 알파가 오르는 것과 같은 방식으로 볼륨도 선형으로 키운다.
            audioSource.volume = Mathf.Lerp(0f, soundMaxVolume, elapsed / rampDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        audioSource.volume = soundMaxVolume;
        soundRoutine = null;
    }

    private IEnumerator PlayRoutine(float fadeIn, float hold, System.Action onFinished)
    {
        Debug.Log($"[GlitchNoiseEffect] 노이즈 시작 (알파 0->1 {fadeIn:0.00}초, 유지 {hold:0.00}초)");

        target.enabled = true;
        SetAlpha(0f);

        float noiseInterval = 1f / Mathf.Max(1, noiseFps);
        float nextNoiseTime = 0f;
        float elapsed = 0f;

        // 1) 알파를 0에서 1까지 올린다.
        while (elapsed < fadeIn)
        {
            if (elapsed >= nextNoiseTime)
            {
                ShuffleNoise();
                nextNoiseTime = elapsed + noiseInterval;
            }

            SetAlpha(Mathf.Clamp01(elapsed / fadeIn));

            elapsed += Time.deltaTime;
            yield return null;
        }

        SetAlpha(1f);

        // 2) 알파 1인 상태로 hold만큼 유지한다.
        float holdElapsed = 0f;
        nextNoiseTime = 0f;
        while (holdElapsed < hold)
        {
            if (holdElapsed >= nextNoiseTime)
            {
                ShuffleNoise();
                nextNoiseTime = holdElapsed + noiseInterval;
            }

            holdElapsed += Time.deltaTime;
            yield return null;
        }

        playRoutine = null;

        Debug.Log("[GlitchNoiseEffect] 노이즈 유지 종료. 콜백을 호출합니다.");

        onFinished?.Invoke();
    }

    private void SetAlpha(float a)
    {
        Color c = target.color;
        c.a = a;
        target.color = c;
    }

    // 미리 만들어 둔 텍스처 중 하나를 고르고, uvRect를 화면 픽셀에 맞춰 타일링 + 무작위 오프셋
    private void ShuffleNoise()
    {
        if (noiseFrames == null || noiseFrames.Length == 0) return;

        target.texture = noiseFrames[Random.Range(0, noiseFrames.Length)];

        // 텍셀 1개가 화면 픽셀 grainScale개를 덮도록 타일 수를 계산
        Rect r = target.rectTransform.rect;
        float scale = Mathf.Max(0.01f, grainScale);
        float tilesX = Mathf.Max(1f, r.width / (noiseResolution * scale));
        float tilesY = Mathf.Max(1f, r.height / (noiseResolution * scale));

        target.uvRect = new Rect(Random.value, Random.value, tilesX, tilesY);
    }

    private void OnDestroy()
    {
        if (noiseFrames == null) return;

        foreach (var tex in noiseFrames)
            if (tex != null) Destroy(tex);
    }
}
