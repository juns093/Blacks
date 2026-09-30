using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 증거 문서를 화면 가운데에 크게 펼쳐 보여주고, 조작 연출을 재생하는 화면.
//
//  [F] 조작 : Rewrite  -> 글자가 한 자씩 검게 지워지고(██), 조작한 내용이 새로 적힌다.
//             Destroy  -> 종이가 떨리다 찢어지듯 사라지고, 한 줄만 남는다.
//  [E] 닫기
//
// UI는 실행 중에 코드로 만듭니다. FlashbackFreeRoamSegment가 Inspect()를 기다립니다.
public class EvidenceViewer : MonoBehaviour
{
    [Header("모양")]
    [Tooltip("비워두면 대사창과 같은 폰트(DOSMyungjo)를 씁니다.")]
    [SerializeField] private Font font;

    [SerializeField] private Color paperColor = new Color(0.86f, 0.83f, 0.74f, 1f);
    [SerializeField] private Color inkColor = new Color(0.12f, 0.1f, 0.09f, 1f);
    [SerializeField] private Color tamperInkColor = new Color(0.1f, 0.12f, 0.35f, 1f);
    [SerializeField] private Vector2 paperSize = new Vector2(820f, 560f);

    [Header("연출 속도")]
    [Tooltip("한 글자를 지우는 시간(초)")]
    [SerializeField] private float redactCharInterval = 0.012f;

    [Tooltip("조작한 내용을 한 글자 적는 시간(초)")]
    [SerializeField] private float writeCharInterval = 0.03f;

    [Header("소리 (비워두면 코드로 만든 소리)")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip scribbleSound;
    [SerializeField] private AudioClip tearSound;
    [Range(0f, 1f)] [SerializeField] private float volume = 0.8f;

    [Header("키")]
    [SerializeField] private KeyCode tamperKey = KeyCode.F;
    [SerializeField] private KeyCode closeKey = KeyCode.E;

    [SerializeField] private int sortingOrder = 450; // 대사보다 위, 암전(500)보다 아래

    private GameObject canvasObject;
    private RectTransform paper;
    private Image paperImage;
    private Text titleText;
    private Text bodyText;
    private Text promptText;
    private AudioSource audioSource;

    public bool IsOpen { get; private set; }

    /// <summary>문서를 펼치고, 플레이어가 닫을 때까지 기다립니다.</summary>
    public IEnumerator Inspect(EvidenceItem evidence)
    {
        if (evidence == null) yield break;

        Build();
        IsOpen = true;

        titleText.text = evidence.Title;
        bodyText.text = evidence.IsTampered && evidence.Style == EvidenceItem.TamperStyle.Rewrite
            ? evidence.TamperedText
            : evidence.OriginalText;
        bodyText.color = evidence.IsTampered ? tamperInkColor : inkColor;
        paper.anchoredPosition = Vector2.zero;
        paper.localRotation = Quaternion.identity;
        SetPaperAlpha(1f);
        canvasObject.SetActive(true);

        Play(openSound != null ? openSound : GetPaperClip(), 0.7f);
        yield return PopIn();

        // E를 누른 그 프레임에 열렸으므로 한 프레임 넘겨서 "닫기"로 오인하지 않게 한다.
        yield return null;

        while (true)
        {
            promptText.text = evidence.IsTampered
                ? $"[{closeKey}] 닫기"
                : $"[{tamperKey}] {evidence.ActionLabel}        [{closeKey}] 닫기";

            if (!evidence.IsTampered && Input.GetKeyDown(tamperKey))
            {
                promptText.text = "";
                if (evidence.Style == EvidenceItem.TamperStyle.Rewrite)
                    yield return RewriteRoutine(evidence);
                else
                    yield return DestroyRoutine(evidence);

                evidence.MarkTampered();
                yield return null;
                continue;
            }

            if (Input.GetKeyDown(closeKey) || Input.GetKeyDown(KeyCode.Escape))
                break;

            yield return null;
        }

        canvasObject.SetActive(false);
        IsOpen = false;
        evidence.OnViewerClosed();
    }

    // ── 조작 연출 ──

    // 원래 글자를 앞에서부터 한 자씩 검게 지우고, 그 자리에 조작한 내용을 새로 적는다.
    private IEnumerator RewriteRoutine(EvidenceItem evidence)
    {
        string original = evidence.OriginalText;
        var sb = new StringBuilder(original);

        AudioClip scribble = scribbleSound != null ? scribbleSound : GetScribbleClip();
        audioSource.clip = scribble;
        audioSource.loop = true;
        audioSource.volume = volume * 0.6f;
        audioSource.Play();

        // 1) 검게 지우기
        for (int i = 0; i < sb.Length; i++)
        {
            if (sb[i] == '\n' || sb[i] == ' ') continue;
            sb[i] = '█';
            bodyText.text = sb.ToString();
            if (redactCharInterval > 0f)
                yield return new WaitForSeconds(redactCharInterval);
        }

        audioSource.Stop();
        yield return new WaitForSeconds(0.35f);

        // 2) 새로 적기 (다른 색 잉크)
        bodyText.color = tamperInkColor;
        string tampered = evidence.TamperedText;
        audioSource.Play();
        for (int i = 1; i <= tampered.Length; i++)
        {
            bodyText.text = tampered.Substring(0, i);
            if (writeCharInterval > 0f && tampered[i - 1] != ' ' && tampered[i - 1] != '\n')
                yield return new WaitForSeconds(writeCharInterval);
        }
        audioSource.Stop();
        audioSource.loop = false;

        yield return ShowThought(evidence);
    }

    // 종이가 떨리다가 찢겨 사라지고, 한 줄만 남는다.
    private IEnumerator DestroyRoutine(EvidenceItem evidence)
    {
        float t = 0f;
        const float shake = 0.45f;
        while (t < shake)
        {
            t += Time.deltaTime;
            paper.anchoredPosition = Random.insideUnitCircle * 8f;
            paper.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-2f, 2f));
            yield return null;
        }

        Play(tearSound != null ? tearSound : GetTearClip(), 1f);

        t = 0f;
        const float fall = 0.6f;
        while (t < fall)
        {
            t += Time.deltaTime;
            float k = t / fall;
            paper.anchoredPosition = new Vector2(0f, -k * k * 300f);
            paper.localRotation = Quaternion.Euler(0f, 0f, -k * 18f);
            SetPaperAlpha(1f - k);
            yield return null;
        }
        SetPaperAlpha(0f);

        // 남는 한 줄
        titleText.text = "";
        bodyText.text = "";
        promptText.text = evidence.TamperedText;
        yield return new WaitForSeconds(1.4f);

        paper.anchoredPosition = Vector2.zero;
        paper.localRotation = Quaternion.identity;

        yield return ShowThought(evidence);
    }

    private IEnumerator ShowThought(EvidenceItem evidence)
    {
        if (string.IsNullOrEmpty(evidence.AfterTamperThought)) yield break;
        promptText.text = evidence.AfterTamperThought;
        yield return new WaitForSeconds(1.8f);
    }

    private IEnumerator PopIn()
    {
        float t = 0f;
        const float d = 0.18f;
        while (t < d)
        {
            t += Time.deltaTime;
            float s = Mathf.Lerp(0.85f, 1f, t / d);
            paper.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        paper.localScale = Vector3.one;
    }

    private void SetPaperAlpha(float a)
    {
        Color p = paperColor; p.a = paperColor.a * a; paperImage.color = p;
        Color t = titleText.color; t.a = a; titleText.color = t;
        Color b = bodyText.color; b.a = a; bodyText.color = b;
    }

    private void Play(AudioClip clip, float scale)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, volume * scale);
    }

    // ── UI ──

    private void Build()
    {
        if (canvasObject != null) return;

        canvasObject = new GameObject("EvidenceViewerCanvas");
        canvasObject.transform.SetParent(transform, false);

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        audioSource = canvasObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        Font uiFont = UIFontUtil.Resolve(font);

        // 뒤를 살짝 어둡게
        var dim = MakeImage("Dim", canvasObject.transform, new Color(0f, 0f, 0f, 0.6f));
        Stretch((RectTransform)dim.transform);

        // 종이
        paperImage = MakeImage("Paper", canvasObject.transform, paperColor);
        paper = (RectTransform)paperImage.transform;
        paper.anchorMin = paper.anchorMax = new Vector2(0.5f, 0.5f);
        paper.sizeDelta = paperSize;

        titleText = MakeText("Title", paper, uiFont, 34, FontStyle.Bold, TextAnchor.UpperCenter, inkColor);
        var titleRt = (RectTransform)titleText.transform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-80f, 60f);
        titleRt.anchoredPosition = new Vector2(0f, -40f);

        bodyText = MakeText("Body", paper, uiFont, 28, FontStyle.Normal, TextAnchor.UpperLeft, inkColor);
        var bodyRt = (RectTransform)bodyText.transform;
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(60f, 50f);
        bodyRt.offsetMax = new Vector2(-60f, -120f);
        bodyText.lineSpacing = 1.2f;

        // 안내 (종이 아래)
        promptText = MakeText("Prompt", canvasObject.transform, uiFont, 26, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
        var promptRt = (RectTransform)promptText.transform;
        promptRt.anchorMin = promptRt.anchorMax = new Vector2(0.5f, 0.5f);
        promptRt.sizeDelta = new Vector2(1400f, 60f);
        promptRt.anchoredPosition = new Vector2(0f, -paperSize.y * 0.5f - 50f);

        canvasObject.SetActive(false);
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

    private static Text MakeText(string name, Transform parent, Font font, int size, FontStyle style, TextAnchor anchor, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = anchor;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── 코드로 만든 소리 ──

    private static AudioClip cachedPaper, cachedScribble, cachedTear;

    // 종이 펼치는 소리: 짧은 바스락
    private static AudioClip GetPaperClip()
    {
        if (cachedPaper != null) return cachedPaper;
        cachedPaper = MakeNoiseClip("EvidencePaper", 0.25f, (t, len) =>
        {
            float env = Mathf.Sin(Mathf.PI * t / len);
            return env * env * 0.5f;
        }, 0.35f);
        return cachedPaper;
    }

    // 펜으로 박박 긋는 소리: 빠르게 끊기는 노이즈 (반복 재생용)
    private static AudioClip GetScribbleClip()
    {
        if (cachedScribble != null) return cachedScribble;
        cachedScribble = MakeNoiseClip("EvidenceScribble", 0.6f, (t, len) =>
        {
            float stroke = Mathf.Abs(Mathf.Sin(t * 2f * Mathf.PI * 9f));
            return Mathf.Pow(stroke, 3f) * 0.6f;
        }, 0.55f);
        return cachedScribble;
    }

    // 종이 찢는 소리: 길게 긁히다 끊기는 노이즈
    private static AudioClip GetTearClip()
    {
        if (cachedTear != null) return cachedTear;
        cachedTear = MakeNoiseClip("EvidenceTear", 0.7f, (t, len) =>
        {
            float k = t / len;
            float env = k < 0.1f ? k / 0.1f : Mathf.Pow(1f - (k - 0.1f) / 0.9f, 1.5f);
            float crackle = 0.6f + 0.4f * Mathf.PerlinNoise(t * 120f, 0.3f);
            return env * crackle * 0.8f;
        }, 0.2f);
        return cachedTear;
    }

    // smoothing: 0에 가까울수록 날카롭고, 1에 가까울수록 먹먹하다.
    private static AudioClip MakeNoiseClip(string name, float length, System.Func<float, float, float> envelope, float smoothing)
    {
        const int rate = 44100;
        int n = Mathf.CeilToInt(length * rate);
        var data = new float[n];
        var rng = new System.Random(name.GetHashCode());
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = Mathf.Lerp(white, prev, smoothing);
            data[i] = prev * envelope(t, length);
        }

        var clip = AudioClip.Create(name, n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
