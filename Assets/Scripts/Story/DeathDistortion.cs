using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 총에 맞았을 때의 화면 왜곡 + 환각 화면.
//
//  Hit()          : 총에 맞는 순간 화면이 확 일그러지고 색이 빠진다. 귀가 울린다. (피격 타임라인 대신)
//  SetHallucination: 환각 대사가 나오는 동안 흐릿하게 일렁이는 화면을 유지한다.
//  Shatter()      : "화면이 깨지듯" 번쩍이며 현재로 돌아온다.
//  Blackout()     : 잠깐 암전.
// 전부 전용 Volume 하나로 처리한다. (씬의 다른 보정 위에 덮어씀)
public class DeathDistortion : MonoBehaviour
{
    public static DeathDistortion Instance { get; private set; }

    [SerializeField] private float hitDuration = 1.6f;
    [Range(0f, 1f)] [SerializeField] private float ringVolume = 0.35f;

    private Volume volume;
    private LensDistortion lens;
    private ChromaticAberration chroma;
    private ColorAdjustments color;
    private Vignette vignette;

    private float hallucination;      // 0~1 (환각 화면 세기)
    private float hallucinationTarget;
    private float spike;              // 순간 왜곡 (0~1)
    private AudioSource ring;

    private GameObject blackCanvas;
    private Image black;

    public static DeathDistortion Get()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("DeathDistortion");
        return go.AddComponent<DeathDistortion>();
    }

    private void Awake()
    {
        Instance = this;
        Build();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Build()
    {
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 200f;
        volume.weight = 0f;

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        lens = profile.Add<LensDistortion>(true);
        lens.intensity.overrideState = true;
        lens.scale.overrideState = true;
        chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.overrideState = true;
        color = profile.Add<ColorAdjustments>(true);
        color.saturation.overrideState = true;
        color.postExposure.overrideState = true;
        color.colorFilter.overrideState = true;
        vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.6f;
        volume.profile = profile;

        ring = gameObject.AddComponent<AudioSource>();
        ring.playOnAwake = false;
        ring.loop = true;
        ring.spatialBlend = 0f;
        ring.volume = 0f;
        ring.clip = TinnitusClip();

        blackCanvas = UIBuild.OverlayCanvas("DeathBlackout", transform, 6); // 대사창(10)보다 아래
        black = UIBuild.Image("Black", blackCanvas.transform, new Color(0f, 0f, 0f, 0f));
        UIBuild.Stretch(black.rectTransform);
        blackCanvas.SetActive(false);
    }

    private void Update()
    {
        hallucination = Mathf.MoveTowards(hallucination, hallucinationTarget, Time.deltaTime * 0.8f);
        spike = Mathf.MoveTowards(spike, 0f, Time.deltaTime / Mathf.Max(0.1f, hitDuration));

        float wobble = Mathf.Sin(Time.time * 1.7f) * 0.12f + Mathf.Sin(Time.time * 3.1f) * 0.05f;
        float h = hallucination;
        float s = spike;

        lens.intensity.value = Mathf.Clamp(-0.25f * h + wobble * h - 0.7f * s, -1f, 1f);
        lens.scale.value = 1f - 0.08f * s;
        chroma.intensity.value = Mathf.Clamp01(0.45f * h + s);
        color.saturation.value = -55f * h - 80f * s;
        color.postExposure.value = -0.4f * h - 1.4f * s;
        color.colorFilter.value = Color.Lerp(Color.white, new Color(1f, 0.82f, 0.78f), Mathf.Max(h * 0.6f, s));
        vignette.intensity.value = Mathf.Clamp01(0.35f * h + 0.5f * s);

        float w = Mathf.Max(h, s);
        volume.weight = w;
        volume.enabled = w > 0.001f;

        ring.volume = ringVolume * Mathf.Max(s, h * 0.35f);
        if (ring.volume > 0.001f && !ring.isPlaying) ring.Play();
        else if (ring.volume <= 0.001f && ring.isPlaying) ring.Stop();
    }

    /// <summary>총에 맞는 순간: 확 일그러졌다가 서서히 풀린다.</summary>
    public IEnumerator Hit()
    {
        spike = 1f;
        yield return new WaitForSeconds(hitDuration * 0.75f);
    }

    public void SetHallucination(bool on) => hallucinationTarget = on ? 1f : 0f;

    /// <summary>화면이 깨지듯 번쩍이며 끝난다.</summary>
    public IEnumerator Shatter()
    {
        for (int i = 0; i < 6; i++)
        {
            spike = i % 2 == 0 ? 1f : 0.3f;
            SetBlack(i % 3 == 2 ? 0.85f : 0f);
            yield return new WaitForSeconds(0.06f);
        }
        SetBlack(0f);
        hallucinationTarget = 0f;
        hallucination = 0f;
        spike = 0.5f;
    }

    public IEnumerator FadeBlack(float to, float duration)
    {
        blackCanvas.SetActive(true);
        float from = black.color.a, t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetBlack(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetBlack(to);
    }

    public void SetBlack(float a)
    {
        black.color = new Color(0f, 0f, 0f, a);
        blackCanvas.SetActive(a > 0.001f);
    }

    public void ClearAll()
    {
        hallucinationTarget = 0f;
        hallucination = 0f;
        spike = 0f;
        SetBlack(0f);
    }

    // 귀가 삐- 하고 울리는 소리 (4초 반복)
    private static AudioClip TinnitusClip()
    {
        const int rate = 44100;
        int n = rate * 4;
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float beat = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);
            data[i] = (Mathf.Sin(2f * Mathf.PI * 3520f * t) * 0.5f + Mathf.Sin(2f * Mathf.PI * 3527f * t) * 0.5f) * 0.25f * beat;
        }
        var clip = AudioClip.Create("Tinnitus", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
