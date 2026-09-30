using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 게임 전역 설정(사운드 볼륨 / 밝기 / 텍스트 크기 / 점프 스퀘어 경고)을 관리하는 매니저.
// - 씬이 바뀌어도 유지되도록 DontDestroyOnLoad로 동작하는 싱글턴.
// - PlayerPrefs에 값을 저장하고, 다음 실행 시 그대로 복원함.
// - 밝기는 현재 씬에 있는 Global Volume의 ColorAdjustments.postExposure 값을 조절해서 반영.
// - 텍스트 크기는 OnTextScaleChanged 이벤트로 알려서 ScalableText가 각자 자기 폰트 크기를 갱신함.
public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    private const string KeyVolume = "Settings_MasterVolume";
    private const string KeyBrightness = "Settings_Brightness";
    private const string KeyTextScale = "Settings_TextScale";
    private const string KeyJumpScareWarning = "Settings_JumpScareWarning";

    [Header("기본값")]
    [Range(0f, 1f)][SerializeField] private float defaultVolume = 1f;
    [Tooltip("-3 ~ 3 사이의 노출값(EV). 0이 기본 밝기")]
    [Range(-3f, 3f)][SerializeField] private float defaultBrightness = 0f;
    [Range(0.5f, 2f)][SerializeField] private float defaultTextScale = 1f;
    [Tooltip("점프 스퀘어가 나오기 직전에 경고를 띄울지 기본값")]
    [SerializeField] private bool defaultJumpScareWarning = false;

    [Tooltip("밝기 조절에 사용할 Global Volume. 비워두면 씬에서 자동으로 찾음")]
    [SerializeField] private Volume targetVolume;

    public float MasterVolume { get; private set; }
    public float Brightness { get; private set; }
    public float TextScale { get; private set; }
    public bool JumpScareWarning { get; private set; }

    public static event Action<float> OnVolumeChanged;
    public static event Action<float> OnBrightnessChanged;
    public static event Action<float> OnTextScaleChanged;
    public static event Action<bool> OnJumpScareWarningChanged;

    /// <summary>점프 스퀘어 경고를 켰는지. 매니저가 없으면 저장된 값을 읽는다.</summary>
    public static bool WarnBeforeJumpScare =>
        Instance != null ? Instance.JumpScareWarning : PlayerPrefs.GetInt(KeyJumpScareWarning, 0) == 1;

    private ColorAdjustments colorAdjustments;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Load();
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    // 씬이 내려가면 그 씬의 Volume을 붙잡고 있지 않도록 놓아 준다.
    // (이 매니저는 씬을 넘어 살아남기 때문에, 지워진 Volume을 계속 가리키면 MissingReference가 난다)
    private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene)
    {
        targetVolume = null;
        colorAdjustments = null;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 새 씬으로 넘어가면 밝기 대상 Volume을 다시 찾아서 재적용
        targetVolume = null;
        ApplyBrightness(Brightness);
    }

    private void Load()
    {
        MasterVolume = PlayerPrefs.GetFloat(KeyVolume, defaultVolume);
        Brightness = PlayerPrefs.GetFloat(KeyBrightness, defaultBrightness);
        TextScale = PlayerPrefs.GetFloat(KeyTextScale, defaultTextScale);
        JumpScareWarning = PlayerPrefs.GetInt(KeyJumpScareWarning, defaultJumpScareWarning ? 1 : 0) == 1;

        ApplyVolume(MasterVolume);
        ApplyBrightness(Brightness);
        ApplyTextScale(TextScale);
    }

    public void SetVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyVolume(MasterVolume);
        PlayerPrefs.SetFloat(KeyVolume, MasterVolume);
    }

    public void SetBrightness(float value)
    {
        Brightness = Mathf.Clamp(value, -3f, 3f);
        ApplyBrightness(Brightness);
        PlayerPrefs.SetFloat(KeyBrightness, Brightness);
    }

    public void SetTextScale(float value)
    {
        TextScale = Mathf.Clamp(value, 0.5f, 2f);
        ApplyTextScale(TextScale);
        PlayerPrefs.SetFloat(KeyTextScale, TextScale);
    }

    public void SetJumpScareWarning(bool on)
    {
        JumpScareWarning = on;
        PlayerPrefs.SetInt(KeyJumpScareWarning, on ? 1 : 0);
        OnJumpScareWarningChanged?.Invoke(on);
    }

    private void ApplyVolume(float value)
    {
        AudioListener.volume = value;
        OnVolumeChanged?.Invoke(value);
    }

    private void ApplyBrightness(float value)
    {
        if (targetVolume == null || !targetVolume.gameObject.scene.isLoaded)
            targetVolume = FindSceneVolume();

        colorAdjustments = null;
        if (targetVolume != null && targetVolume.profile != null)
        {
            targetVolume.profile.TryGet(out colorAdjustments);
        }

        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = value;
        }

        OnBrightnessChanged?.Invoke(value);
    }

    // 지금 로드되어 있는 씬의 Volume만 고른다. (내려가는 중인 이전 씬의 Volume은 제외)
    private static Volume FindSceneVolume()
    {
        Volume fallback = null;
        var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            if (v == null || !v.gameObject.scene.isLoaded) continue;
            if (v.gameObject.scene == active) return v;
            if (fallback == null) fallback = v;
        }
        return fallback;
    }

    private void ApplyTextScale(float value)
    {
        OnTextScaleChanged?.Invoke(value);
    }
}
