using UnityEngine;
using UnityEngine.UI;

// 탐색 구간 화면 표시.
//  - 왼쪽 위: 지금 할 일(목표). 바뀔 때마다 살짝 밀려 들어오며 강조된다.
//  - 아래 가운데: 조작 안내(WASD 등). WASD를 한 번이라도 누르면 서서히 사라진다.
// 필요할 때 자동으로 만들어진다. (ObjectiveHUD.Instance)
public class ObjectiveHUD : MonoBehaviour
{
    private static ObjectiveHUD instance;

    public static ObjectiveHUD Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ObjectiveHUD").AddComponent<ObjectiveHUD>();
            return instance;
        }
    }

    [SerializeField] private string headerText = "목표";
    [SerializeField] private int objectiveFontSize = 30;
    [SerializeField] private int controlsFontSize = 24;
    [SerializeField] private Color objectiveColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private Color headerColor = new Color(0.65f, 0.65f, 0.65f, 1f);

    private GameObject canvasObject;
    private CanvasGroup objectiveGroup;
    private RectTransform objectiveRoot;
    private Text objectiveText;
    private Text controlsText;
    private Text promptText;
    private Text messageText;
    private float messageUntil = -1f; // 0 이하면 계속 띄움

    private float slideT = 1f;
    private bool controlsVisible = false;
    private float controlsAlpha = 0f;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    /// <summary>왼쪽 위 목표를 바꾼다. 비우면 숨긴다.</summary>
    public void SetObjective(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            objectiveRoot.gameObject.SetActive(false);
            return;
        }

        bool changed = objectiveText.text != text || !objectiveRoot.gameObject.activeSelf;
        objectiveText.text = text;
        objectiveRoot.gameObject.SetActive(true);
        if (changed) slideT = 0f;
    }

    /// <summary>아래 조작 안내를 띄운다. WASD를 누르면 사라진다.</summary>
    public void ShowControls(string text)
    {
        if (string.IsNullOrEmpty(text)) { HideControls(); return; }
        controlsText.text = text;
        controlsVisible = true;
        controlsAlpha = 1f;
        controlsText.gameObject.SetActive(true);
    }

    public void HideControls()
    {
        controlsVisible = false;
    }

    /// <summary>[E] 같은 상호작용 안내. 비우면 숨긴다.</summary>
    public void SetPrompt(string text)
    {
        if (promptText == null) return;
        promptText.text = text ?? "";
        promptText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    /// <summary>화면 아래에 문구를 띄운다. duration이 0 이하면 HideMessage 전까지 계속.</summary>
    public void ShowMessage(string text, float duration)
    {
        if (messageText == null) return;
        if (string.IsNullOrEmpty(text)) { HideMessage(); return; }
        messageText.text = text;
        messageText.gameObject.SetActive(true);
        var c = messageText.color; c.a = 1f; messageText.color = c;
        messageUntil = duration > 0f ? Time.unscaledTime + duration : -1f;
    }

    public void HideMessage()
    {
        if (messageText != null) messageText.gameObject.SetActive(false);
    }

    public void HideAll()
    {
        SetPrompt(null);
        HideMessage();
        SetObjective(null);
        controlsVisible = false;
        controlsAlpha = 0f;
        if (controlsText != null) controlsText.gameObject.SetActive(false);
    }

    private void Update()
    {
        // 목표가 바뀌면 왼쪽에서 살짝 밀려 들어오고, 글자가 잠깐 밝게 빛난다.
        if (slideT < 1f)
        {
            slideT = Mathf.Min(1f, slideT + Time.unscaledDeltaTime * 3f);
            float k = 1f - Mathf.Pow(1f - slideT, 3f);
            objectiveRoot.anchoredPosition = new Vector2(Mathf.Lerp(-40f, 32f, k), -28f);
            objectiveGroup.alpha = k;
            objectiveText.color = Color.Lerp(Color.white, objectiveColor, k);
        }

        // WASD를 한 번 누르면 조작 안내는 사라진다.
        if (controlsVisible && (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)))
            controlsVisible = false;

        if (!controlsVisible && controlsAlpha > 0f)
        {
            controlsAlpha = Mathf.Max(0f, controlsAlpha - Time.unscaledDeltaTime * 2f);
            if (controlsAlpha <= 0f) controlsText.gameObject.SetActive(false);
        }
        var c = controlsText.color;
        c.a = controlsAlpha * 0.85f;
        controlsText.color = c;

        // 시간이 정해진 문구는 끝나기 0.5초 전부터 흐려진다.
        if (messageText.gameObject.activeSelf && messageUntil > 0f)
        {
            float left = messageUntil - Time.unscaledTime;
            var mc = messageText.color; mc.a = Mathf.Clamp01(left / 0.5f); messageText.color = mc;
            if (left <= 0f) messageText.gameObject.SetActive(false);
        }
    }

    private void Build()
    {
        canvasObject = new GameObject("ObjectiveHUDCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = UIFontUtil.Resolve(null);

        // ── 왼쪽 위 목표 ──
        var root = new GameObject("Objective", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(canvasObject.transform, false);
        objectiveRoot = (RectTransform)root.transform;
        objectiveRoot.anchorMin = objectiveRoot.anchorMax = new Vector2(0f, 1f);
        objectiveRoot.pivot = new Vector2(0f, 1f);
        objectiveRoot.sizeDelta = new Vector2(760f, 110f);
        objectiveRoot.anchoredPosition = new Vector2(32f, -28f);
        objectiveGroup = root.GetComponent<CanvasGroup>();
        objectiveGroup.blocksRaycasts = false;

        // 왼쪽 세로 줄 (포인트)
        var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(root.transform, false);
        var brt = (RectTransform)bar.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.sizeDelta = new Vector2(4f, -20f);
        brt.anchoredPosition = Vector2.zero;
        var bimg = bar.GetComponent<Image>();
        bimg.color = new Color(0.8f, 0.8f, 0.8f, 0.8f);
        bimg.raycastTarget = false;

        var header = MakeText("Header", root.transform, font, 20, headerColor, TextAnchor.UpperLeft);
        var hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0f, 1f);
        hrt.sizeDelta = new Vector2(-18f, 28f);
        hrt.anchoredPosition = new Vector2(16f, -6f);
        header.text = headerText;

        objectiveText = MakeText("Text", root.transform, font, objectiveFontSize, objectiveColor, TextAnchor.UpperLeft);
        var ort = objectiveText.rectTransform;
        ort.anchorMin = Vector2.zero; ort.anchorMax = Vector2.one;
        ort.offsetMin = new Vector2(16f, 0f);
        ort.offsetMax = new Vector2(0f, -36f);
        var shadow = objectiveText.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        shadow.effectDistance = new Vector2(2f, -2f);
        root.SetActive(false);

        // ── 아래 가운데 조작 안내 ──
        controlsText = MakeText("Controls", canvasObject.transform, font, controlsFontSize, new Color(1f, 1f, 1f, 0f), TextAnchor.MiddleCenter);
        var crt = controlsText.rectTransform;
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.sizeDelta = new Vector2(1200f, 40f);
        crt.anchoredPosition = new Vector2(0f, 36f);
        var cs = controlsText.gameObject.AddComponent<Shadow>();
        cs.effectColor = new Color(0f, 0f, 0f, 0.9f);
        cs.effectDistance = new Vector2(2f, -2f);
        controlsText.gameObject.SetActive(false);

        // ── 아래 가운데: 잠깐 뜨는 문구 (주웠을 때 등) ──
        messageText = MakeText("Message", canvasObject.transform, font, 30, Color.white, TextAnchor.LowerCenter);
        var mrt = messageText.rectTransform;
        mrt.anchorMin = mrt.anchorMax = new Vector2(0.5f, 0f);
        mrt.pivot = new Vector2(0.5f, 0f);
        mrt.sizeDelta = new Vector2(1400f, 120f);
        mrt.anchoredPosition = new Vector2(0f, 150f);
        var ms = messageText.gameObject.AddComponent<Shadow>();
        ms.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ms.effectDistance = new Vector2(2f, -2f);
        messageText.gameObject.SetActive(false);

        // ── 화면 가운데 조금 아래: [E] 안내 ──
        promptText = MakeText("Prompt", canvasObject.transform, font, 30, new Color(1f, 1f, 1f, 0.95f), TextAnchor.MiddleCenter);
        var prt = promptText.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(900f, 50f);
        prt.anchoredPosition = new Vector2(0f, -120f);
        var ps = promptText.gameObject.AddComponent<Shadow>();
        ps.effectColor = new Color(0f, 0f, 0f, 0.9f);
        ps.effectDistance = new Vector2(2f, -2f);
        promptText.gameObject.SetActive(false);
    }

    private static Text MakeText(string name, Transform parent, Font font, int size, Color color, TextAnchor anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
