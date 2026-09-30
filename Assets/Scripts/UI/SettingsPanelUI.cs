using System;
using UnityEngine;
using UnityEngine.UI;

// Settings 패널의 UI를 SettingsManager와 연결하는 컨트롤러.
//  - 슬라이더: 볼륨 / 밝기 / 텍스트 크기
//  - 토글 버튼: 점프 스퀘어 경고 켜기/끄기
//  - 볼륨을 움직이면 한 칸(volumeTickStep)마다 "틱" 소리가 나서, 지금 크기가 어느 정도인지 귀로 알 수 있다.
//  - 확인 버튼을 누르면 OnConfirm이 불린다. (메뉴에서는 장전 소리 + 카메라 복귀를 SceneChange가 맡음)
public class SettingsPanelUI : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("이 패널 오브젝트 자체(보통 SettingsPanel). 비워두면 이 스크립트가 붙은 오브젝트 사용")]
    [SerializeField] private GameObject panelRoot;

    [Header("슬라이더")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider brightnessSlider;
    [SerializeField] private Slider textScaleSlider;

    [Header("점프 스퀘어 경고 (버튼을 누를 때마다 켜짐/꺼짐)")]
    [SerializeField] private Button jumpScareWarningButton;
    [SerializeField] private Text jumpScareWarningLabel;
    [SerializeField] private string jumpScareWarningFormat = "점프 스퀘어 경고: {0}";
    [SerializeField] private string onText = "켜짐";
    [SerializeField] private string offText = "꺼짐";

    [Header("확인 버튼")]
    [SerializeField] private Button confirmButton;

    [Header("볼륨 틱 소리")]
    [Tooltip("비워두면 코드로 만든 짧은 틱 소리를 씁니다.")]
    [SerializeField] private AudioClip tickSound;
    [Tooltip("이만큼 움직일 때마다 틱 소리가 한 번 납니다.")]
    [SerializeField] private float volumeTickStep = 0.05f;

    /// <summary>확인 버튼을 눌렀을 때. 등록된 곳이 없으면 그냥 패널을 닫는다.</summary>
    public event Action OnConfirm;

    private AudioSource tickSource;
    private int lastTickStep = int.MinValue;

    private void Awake()
    {
        if (panelRoot == null) panelRoot = gameObject;

        if (jumpScareWarningButton != null)
            jumpScareWarningButton.onClick.AddListener(ToggleJumpScareWarning);

        if (confirmButton != null)
        {
            // 예전에 인스펙터에서 Close()를 연결해 두었어도, 확인은 여기서 한 번만 처리한다.
            confirmButton.onClick = new Button.ButtonClickedEvent();
            confirmButton.onClick.AddListener(Confirm);
        }
    }

    private void OnEnable()
    {
        RefreshFromSettings();
    }

    private void RefreshFromSettings()
    {
        var settings = SettingsManager.Instance;
        if (settings == null) return;

        if (volumeSlider != null) volumeSlider.SetValueWithoutNotify(settings.MasterVolume);
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(settings.Brightness);
        if (textScaleSlider != null) textScaleSlider.SetValueWithoutNotify(settings.TextScale);

        lastTickStep = StepOf(settings.MasterVolume);
        RefreshJumpScareLabel();
    }

    public void Open()
    {
        panelRoot.SetActive(true);
        RefreshFromSettings();
    }

    public void Close()
    {
        panelRoot.SetActive(false);
    }

    /// <summary>확인 버튼. 메뉴에서는 SceneChange가 받아서 장전 연출을 한다.</summary>
    public void Confirm()
    {
        PlayerPrefs.Save();

        if (OnConfirm != null)
            OnConfirm.Invoke();
        else
            Close();
    }

    // 각 슬라이더의 OnValueChanged(float)에 연결
    public void OnVolumeSliderChanged(float value)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SetVolume(value);

        // 한 칸 넘어갈 때마다 틱. 볼륨이 바뀐 뒤에 울리므로 그 크기 그대로 들린다.
        int step = StepOf(value);
        if (step != lastTickStep)
        {
            lastTickStep = step;
            PlayTick(value);
        }
    }

    public void OnBrightnessSliderChanged(float value)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SetBrightness(value);
    }

    public void OnTextScaleSliderChanged(float value)
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.SetTextScale(value);
    }

    public void ToggleJumpScareWarning()
    {
        if (SettingsManager.Instance == null) return;
        SettingsManager.Instance.SetJumpScareWarning(!SettingsManager.Instance.JumpScareWarning);
        RefreshJumpScareLabel();
        PlayTick(1f);
    }

    private void RefreshJumpScareLabel()
    {
        if (jumpScareWarningLabel == null || SettingsManager.Instance == null) return;
        jumpScareWarningLabel.text = string.Format(jumpScareWarningFormat,
            SettingsManager.Instance.JumpScareWarning ? onText : offText);
    }

    private int StepOf(float value) => Mathf.RoundToInt(value / Mathf.Max(0.01f, volumeTickStep));

    private void PlayTick(float level)
    {
        if (tickSource == null)
        {
            tickSource = gameObject.AddComponent<AudioSource>();
            tickSource.playOnAwake = false;
            tickSource.spatialBlend = 0f;
        }

        // 크게 할수록 살짝 높은 음으로 → 올라가는지 내려가는지도 들린다.
        tickSource.pitch = Mathf.Lerp(0.8f, 1.3f, Mathf.Clamp01(level));
        tickSource.PlayOneShot(tickSound != null ? tickSound : GetTickClip(), 0.9f);
    }

    private static AudioClip cachedTick;
    private static AudioClip GetTickClip()
    {
        if (cachedTick != null) return cachedTick;
        const int rate = 44100;
        int n = (int)(0.03f * rate);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 260f);
            data[i] = Mathf.Sin(2f * Mathf.PI * 1800f * t) * env * 0.7f;
        }
        cachedTick = AudioClip.Create("SettingsTick", n, 1, rate, false);
        cachedTick.SetData(data, 0);
        return cachedTick;
    }
}
