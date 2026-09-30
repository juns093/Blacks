using UnityEngine;
using UnityEngine.UI;

// UI Text에 붙여서 SettingsManager의 텍스트 크기 설정을 따라가게 만드는 컴포넌트.
// 최초 폰트 크기를 기준값으로 캐싱해두고, 설정이 바뀔 때마다 그 값에 배율을 곱해서 적용함.
[RequireComponent(typeof(Text))]
public class ScalableText : MonoBehaviour
{
    private Text label;
    private int baseFontSize;

    private void Awake()
    {
        label = GetComponent<Text>();
        baseFontSize = label.fontSize;
    }

    private void OnEnable()
    {
        SettingsManager.OnTextScaleChanged += ApplyScale;

        if (SettingsManager.Instance != null)
        {
            ApplyScale(SettingsManager.Instance.TextScale);
        }
    }

    private void OnDisable()
    {
        SettingsManager.OnTextScaleChanged -= ApplyScale;
    }

    private void ApplyScale(float scale)
    {
        if (label == null) return;
        label.fontSize = Mathf.RoundToInt(baseFontSize * scale);
    }
}
