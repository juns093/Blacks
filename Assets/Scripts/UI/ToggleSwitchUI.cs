using UnityEngine;
using UnityEngine.UI;

// 점프 스퀘어 경고를 ON / OFF 스위치 모양으로 보여준다.
// 버튼(누르면 켜짐/꺼짐이 바뀜)의 오른쪽 끝에 스위치를 그리고, 설정 값에 맞춰 손잡이가 좌우로 움직인다.
public class ToggleSwitchUI : MonoBehaviour
{
    [SerializeField] private Vector2 trackSize = new Vector2(110f, 40f);
    [SerializeField] private float knobInset = 4f;
    [SerializeField] private float moveSpeed = 14f;

    [SerializeField] private Color onTrackColor = new Color(0.679f, 0.679f, 0.679f, 1f);
    [SerializeField] private Color offTrackColor = new Color(0.12f, 0.12f, 0.12f, 1f);
    [SerializeField] private Color knobColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color onTextColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color offTextColor = new Color(0.679f, 0.679f, 0.679f, 1f);
    [SerializeField] private Font font;
    [SerializeField] private int fontSize = 24;

    private Image track;
    private Image border;
    private RectTransform knob;
    private Text stateText;
    private float knobT = -1f;

    private bool IsOn => SettingsManager.Instance != null
        ? SettingsManager.Instance.JumpScareWarning
        : SettingsManager.WarnBeforeJumpScare;

    private void Awake()
    {
        Build();
    }

    private void Update()
    {
        bool on = IsOn;
        float target = on ? 1f : 0f;
        knobT = knobT < 0f ? target : Mathf.MoveTowards(knobT, target, Time.unscaledDeltaTime * moveSpeed * 0.5f);

        float knobSize = trackSize.y - knobInset * 2f;
        float travel = trackSize.x - knobInset * 2f - knobSize;
        knob.anchoredPosition = new Vector2(knobInset + travel * Mathf.SmoothStep(0f, 1f, knobT), 0f);

        track.color = Color.Lerp(offTrackColor, onTrackColor, knobT);
        stateText.text = on ? "ON" : "OFF";
        stateText.color = on ? onTextColor : offTextColor;

        // 글자는 손잡이 반대편에
        var trt = stateText.rectTransform;
        trt.anchoredPosition = new Vector2(on ? -knobSize * 0.5f : knobSize * 0.5f, 0f);
    }

    private void Build()
    {
        // 테두리
        border = MakeImage("SwitchBorder", transform, onTrackColor);
        var brt = border.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(1f, 0.5f);
        brt.pivot = new Vector2(1f, 0.5f);
        brt.sizeDelta = trackSize + new Vector2(4f, 4f);
        brt.anchoredPosition = new Vector2(2f, 0f);

        // 바탕(트랙)
        track = MakeImage("SwitchTrack", border.transform, offTrackColor);
        var trt = track.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = trackSize;

        // 손잡이
        float knobSize = trackSize.y - knobInset * 2f;
        var k = MakeImage("SwitchKnob", track.transform, knobColor);
        knob = k.rectTransform;
        knob.anchorMin = knob.anchorMax = new Vector2(0f, 0.5f);
        knob.pivot = new Vector2(0f, 0.5f);
        knob.sizeDelta = new Vector2(knobSize, knobSize);

        // ON / OFF 글자
        var go = new GameObject("SwitchText", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(track.transform, false);
        stateText = go.GetComponent<Text>();
        stateText.font = font != null ? font : UIFontUtil.Resolve(null);
        stateText.fontSize = fontSize;
        stateText.alignment = TextAnchor.MiddleCenter;
        stateText.raycastTarget = false;
        var srt = stateText.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.sizeDelta = trackSize;
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
