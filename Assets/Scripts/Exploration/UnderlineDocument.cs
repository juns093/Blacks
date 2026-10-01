using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 문서를 펼쳐서 "중요한 문장"에 직접 줄을 긋는 기믹.
//  - W/S(또는 마우스)로 문장을 고르고 Enter/Space(또는 클릭)로 줄을 긋는다.
//  - 중요한 문장이면 빨간 줄이 그어지고, 아니면 "이건 중요하지 않아" 하고 흔들린다.
//  - 중요한 문장을 전부 그으면 닫힌다. 그은 문장은 기억 노트에 적힌다. (InteractSpot이 처리)
//  - (선택) 중요한 문장을 몇 개 그은 뒤, 갑자기 문을 쾅쾅 두드리는 소리로 놀래킨다. (소리 점프 스퀘어)
//    이어서 대사(관리인: "화장실 마감합니다" / 나: "잠시만요")가 나오고, 남은 줄을 마저 긋는다.
// 내용은 SetContent로 넣는다. 문장 앞에 '*'를 붙이면 중요한 문장이다.
public class UnderlineDocument : MonoBehaviour
{
    [SerializeField] private string title = "사건 자료";
    [TextArea(8, 20)]
    [SerializeField] private string[] lines = { "*중요한 문장", "평범한 문장" };
    [SerializeField] private string header = "- 중요한 곳에 줄을 그어라 -";
    [SerializeField] private string counterFormat = "중요한 곳  {0} / {1}";
    [SerializeField] private string wrongText = "...이건 중요하지 않아.";
    [SerializeField] private string doneText = "...이 정도면 됐어.";

    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip markSound;
    [SerializeField] private AudioClip wrongSound;

    [Header("중간에 문 두드리는 소리 (소리 점프 스퀘어)")]
    [Tooltip("중요한 문장을 이만큼 그으면 문을 쾅쾅 두드린다. 0이면 없음")]
    [SerializeField] private int interruptAfterMarks = 2;
    [SerializeField] private AudioClip knockClip;
    [Range(0f, 1f)] [SerializeField] private float knockVolume = 1f;
    [Tooltip("두드리기 전 정적(초)")]
    [SerializeField] private float silenceBeforeKnock = 1.1f;
    [Tooltip("두드린 뒤 나오는 대사 (\"이름: 대사\" 또는 그냥 대사)")]
    [SerializeField] private string[] interruptLines = { "관리인: 아저씨! 화장실 마감합니다. 나오셔야 해요!", "잠, 잠시만요...!" };
    [SerializeField] private string warningText = "[주의] 곧 깜짝 놀랄 수 있는 소리가 나옵니다";

    private GameObject canvasObject;
    private RectTransform paper;
    private Text titleText;
    private Text counterText;
    private Text feedbackText;
    private RectTransform listRoot;
    private Image cursorBar;
    private readonly List<Text> lineTexts = new List<Text>();
    private readonly List<Image> underlines = new List<Image>();
    private bool[] isKey;
    private bool[] marked;
    private int selected;
    private AudioSource audioSource;
    private AudioSource knockSource;
    private bool forceComplete;
    private bool interrupted;
    private FreeRoamMovement runPlayer;
    private GameObject subtitleBar;
    private Text subtitleText;

    /// <summary>테스트용: 남은 중요한 문장을 모두 그은 것으로 치고 닫는다.</summary>
    public void ForceComplete() => forceComplete = true;

    public void SetContent(string newTitle, string[] newLines, string wrong = null, string done = null)
    {
        title = newTitle;
        lines = newLines;
        if (wrong != null) wrongText = wrong;
        if (done != null) doneText = done;
        if (canvasObject != null) { Destroy(canvasObject); canvasObject = null; }
    }

    /// <summary>중간에 문 두드리는 연출. afterMarks = 몇 개 그은 뒤 (0이면 끔)</summary>
    public void SetInterruption(int afterMarks, string[] newLines)
    {
        interruptAfterMarks = afterMarks;
        if (newLines != null) interruptLines = newLines;
    }

    /// <summary>기억 노트에 적을 글: 줄 그은 문장들 + 덧붙일 말</summary>
    public string BuildNoteText(string extra)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("[줄 그은 곳]\n");
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("*")) sb.Append("- ").Append(lines[i].Substring(1).Trim()).Append('\n');
        if (!string.IsNullOrEmpty(extra)) sb.Append('\n').Append(extra);
        return sb.ToString().TrimEnd();
    }

    /// <summary>중요한 문장을 다 그을 때까지 기다린다.</summary>
    public IEnumerator Run(FreeRoamMovement player)
    {
        if (lines == null || lines.Length == 0) yield break;

        Build();
        runPlayer = player;
        interrupted = false;
        if (subtitleBar != null) subtitleBar.SetActive(false);
        if (player != null) player.InputPaused = true;
        CursorLockMode prevLock = Cursor.lockState;
        bool prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        for (int i = 0; i < marked.Length; i++) { marked[i] = false; SetUnderline(i, 0f); }
        selected = 0;
        feedbackText.text = "";
        RefreshCounter();
        canvasObject.SetActive(true);
        yield return OpenAnimation();

        Vector3 lastMouse = Input.mousePosition;
        forceComplete = false;
        while (true)
        {
            if (forceComplete)
            {
                for (int i = 0; i < marked.Length; i++) if (isKey[i] && !marked[i]) { marked[i] = true; SetUnderline(i, 1f); }
                RefreshCounter();
                break;
            }

            // 마우스를 움직이면 그 줄을 고른다.
            if ((Input.mousePosition - lastMouse).sqrMagnitude > 1f)
            {
                lastMouse = Input.mousePosition;
                int hover = LineUnderMouse();
                if (hover >= 0 && hover != selected) { selected = hover; Play(moveSound, 0.5f); }
            }

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % lines.Length; Play(moveSound, 0.5f); }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + lines.Length) % lines.Length; Play(moveSound, 0.5f); }
            MoveCursorBar();

            bool clicked = Input.GetMouseButtonDown(0) && LineUnderMouse() >= 0;
            if (clicked) selected = LineUnderMouse();

            if (clicked || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (marked[selected])
                {
                    // 이미 그은 줄
                }
                else if (isKey[selected])
                {
                    marked[selected] = true;
                    feedbackText.text = "";
                    Play(markSound != null ? markSound : ScribbleClip(), 0.9f);
                    yield return DrawUnderline(selected);
                    RefreshCounter();
                    if (AllMarked()) break;

                    if (!interrupted && interruptAfterMarks > 0 && MarkedCount() >= interruptAfterMarks)
                    {
                        yield return InterruptRoutine();
                        lastMouse = Input.mousePosition;
                    }
                }
                else
                {
                    Play(wrongSound, 1f);
                    feedbackText.text = wrongText;
                    yield return ShakeLine(selected);
                }
            }

            yield return null;
        }

        feedbackText.text = doneText;
        cursorBar.enabled = false;
        yield return new WaitForSeconds(1.4f);
        cursorBar.enabled = true;

        canvasObject.SetActive(false);
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        if (player != null) player.InputPaused = false;
    }

    private int MarkedCount()
    {
        int n = 0;
        for (int i = 0; i < marked.Length; i++) if (isKey[i] && marked[i]) n++;
        return n;
    }

    // ── 문 두드리는 소리 점프 스퀘어 ──
    //  정적 → 갑자기 쾅쾅쾅 (종이가 튀고 화면이 흔들림) → 역무원 대사 → "잠시만요" → 다시 줄 긋기
    private IEnumerator InterruptRoutine()
    {
        interrupted = true;
        cursorBar.enabled = false;
        feedbackText.text = "";

        if (SettingsManager.WarnBeforeJumpScare)
        {
            feedbackText.text = warningText;
            yield return new WaitForSeconds(1.5f);
            feedbackText.text = "";
        }

        yield return new WaitForSeconds(silenceBeforeKnock);
        Debug.Log("[UnderlineDocument] 문을 두드린다!");

        // 쾅! 쾅! 쾅! - 점점 빨라지며 세게
        float[] gaps = { 0.32f, 0.26f, 0.2f, 0f };
        for (int i = 0; i < gaps.Length; i++)
        {
            Knock(1f - i * 0.03f, 0.82f + Random.Range(-0.04f, 0.04f));
            StartCoroutine(Jolt(30f - i * 4f, 0.25f));
            if (gaps[i] > 0f) yield return new WaitForSeconds(gaps[i]);
        }
        yield return new WaitForSeconds(0.7f);

        foreach (var line in interruptLines)
            yield return ShowSubtitle(line);

        subtitleBar.SetActive(false);
        cursorBar.enabled = true;
    }

    private void Knock(float volume, float pitch)
    {
        if (knockSource == null)
        {
            knockSource = gameObject.AddComponent<AudioSource>();
            knockSource.playOnAwake = false;
            knockSource.spatialBlend = 0f;
        }
        knockSource.pitch = pitch;
        if (knockClip != null) knockSource.PlayOneShot(knockClip, volume * knockVolume);
        knockSource.PlayOneShot(ProceduralSfx.DoorThud(), volume * knockVolume); // 문짝이 울리는 둔한 소리
    }

    // 종이가 확 튀고 뒤의 화면도 흔들린다.
    private IEnumerator Jolt(float strength, float duration)
    {
        Vector2 basePos = Vector2.zero;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - t / duration;
            paper.anchoredPosition = basePos + Random.insideUnitCircle * strength * k;
            paper.localRotation = Quaternion.Euler(0f, 0f, -0.8f + Random.Range(-2f, 2f) * k);
            if (runPlayer != null)
                runPlayer.CameraShakeEuler = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), 0f) * k;
            yield return null;
        }
        paper.anchoredPosition = basePos;
        paper.localRotation = Quaternion.Euler(0f, 0f, -0.8f);
        if (runPlayer != null) runPlayer.CameraShakeEuler = Vector3.zero;
    }

    // 화면 아래 대사 한 줄: 한 글자씩 → 잠깐 기다림 (클릭/Enter로 넘김)
    private IEnumerator ShowSubtitle(string line)
    {
        subtitleBar.SetActive(true);
        subtitleText.text = "";
        yield return null;

        for (int i = 1; i <= line.Length; i++)
        {
            subtitleText.text = line.Substring(0, i);
            if (SkipPressed()) { subtitleText.text = line; break; }
            yield return new WaitForSeconds(0.035f);
        }

        float wait = 1.6f;
        yield return null;
        while (wait > 0f && !SkipPressed())
        {
            wait -= Time.deltaTime;
            yield return null;
        }
    }

    private static bool SkipPressed() =>
        Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);

    private bool AllMarked()
    {
        for (int i = 0; i < isKey.Length; i++) if (isKey[i] && !marked[i]) return false;
        return true;
    }

    private void RefreshCounter()
    {
        int total = 0, done = 0;
        for (int i = 0; i < isKey.Length; i++) if (isKey[i]) { total++; if (marked[i]) done++; }
        counterText.text = counterFormat.Replace("{0}", done.ToString()).Replace("{1}", total.ToString());
    }

    private int LineUnderMouse()
    {
        for (int i = 0; i < lineTexts.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(lineTexts[i].rectTransform, Input.mousePosition, null))
                return i;
        return -1;
    }

    private void MoveCursorBar()
    {
        var target = lineTexts[selected].rectTransform;
        var bar = cursorBar.rectTransform;
        bar.anchoredPosition = Vector2.Lerp(bar.anchoredPosition, target.anchoredPosition, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 25f));
    }

    private void SetUnderline(int i, float k)
    {
        var rt = underlines[i].rectTransform;
        float width = Mathf.Min(lineTexts[i].preferredWidth + 12f, listRoot.rect.width);
        rt.sizeDelta = new Vector2(width * k, rt.sizeDelta.y);
        underlines[i].enabled = k > 0f;
    }

    // 펜으로 긋듯이 왼쪽에서 오른쪽으로
    private IEnumerator DrawUnderline(int i)
    {
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.unscaledDeltaTime;
            SetUnderline(i, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            yield return null;
        }
        SetUnderline(i, 1f);
    }

    private IEnumerator ShakeLine(int i)
    {
        var rt = lineTexts[i].rectTransform;
        Vector2 basePos = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            rt.anchoredPosition = basePos + new Vector2(Random.Range(-1f, 1f) * 9f * (1f - t / 0.3f), 0f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    private IEnumerator OpenAnimation()
    {
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 0.25f);
            paper.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, k);
            paper.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-3f, -0.8f, k));
            yield return null;
        }
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(clip, volume);
    }

    private static AudioClip scribble;

    // 펜으로 "슥-" 긋는 소리
    private static AudioClip ScribbleClip()
    {
        if (scribble != null) return scribble;
        const int rate = 44100;
        int n = (int)(0.35f * rate);
        var data = new float[n];
        var rng = new System.Random(3);
        float hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float noise = (float)rng.NextDouble() * 2f - 1f;
            hp = 0.7f * (hp + noise - prev); prev = noise;
            float env = Mathf.Clamp01(t * 40f) * Mathf.Clamp01((0.35f - t) * 8f);
            float grain = 0.6f + 0.4f * Mathf.Sin(t * 2f * Mathf.PI * 22f);
            data[i] = hp * env * grain * 0.35f;
        }
        scribble = AudioClip.Create("Scribble", n, 1, rate, false);
        scribble.SetData(data, 0);
        return scribble;
    }

    private void Build()
    {
        if (canvasObject != null) return;

        isKey = new bool[lines.Length];
        marked = new bool[lines.Length];
        lineTexts.Clear();
        underlines.Clear();

        canvasObject = UIBuild.OverlayCanvas("UnderlineDocumentCanvas", transform, 440);
        Font font = UIFontUtil.Resolve(null);

        var dim = UIBuild.Image("Dim", canvasObject.transform, new Color(0f, 0f, 0f, 0.78f));
        UIBuild.Stretch(dim.rectTransform);

        var paperImg = UIBuild.Image("Paper", canvasObject.transform, new Color(0.84f, 0.81f, 0.72f, 1f));
        paper = paperImg.rectTransform;
        paper.anchorMin = paper.anchorMax = new Vector2(0.5f, 0.52f);
        paper.sizeDelta = new Vector2(1180f, 860f);

        var headerText = UIBuild.Text("Header", canvasObject.transform, font, 26, new Color(0.7f, 0.7f, 0.7f, 1f), TextAnchor.MiddleCenter);
        var hrt = headerText.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 50f); hrt.anchoredPosition = new Vector2(0f, -10f);
        headerText.text = header;

        titleText = UIBuild.Text("Title", paper, font, 40, new Color(0.12f, 0.1f, 0.08f, 1f), TextAnchor.UpperCenter);
        var trt = titleText.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f); trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(0f, 60f); trt.anchoredPosition = new Vector2(0f, -40f);
        titleText.text = title;

        var rule = UIBuild.Image("Rule", paper, new Color(0.3f, 0.26f, 0.2f, 0.6f));
        var rrt = rule.rectTransform;
        rrt.anchorMin = new Vector2(0.08f, 1f); rrt.anchorMax = new Vector2(0.92f, 1f);
        rrt.sizeDelta = new Vector2(0f, 2f); rrt.anchoredPosition = new Vector2(0f, -110f);

        var listGo = new GameObject("Lines", typeof(RectTransform));
        listGo.transform.SetParent(paper, false);
        listRoot = (RectTransform)listGo.transform;
        listRoot.anchorMin = new Vector2(0.08f, 0.14f); listRoot.anchorMax = new Vector2(0.92f, 1f);
        listRoot.offsetMin = Vector2.zero; listRoot.offsetMax = new Vector2(0f, -135f);

        cursorBar = UIBuild.Image("Cursor", listRoot, new Color(0.35f, 0.25f, 0.1f, 0.13f));
        var crt = cursorBar.rectTransform;
        crt.anchorMin = new Vector2(0f, 1f); crt.anchorMax = new Vector2(1f, 1f); crt.pivot = new Vector2(0.5f, 1f);
        crt.sizeDelta = new Vector2(24f, 50f);

        const float lineHeight = 54f;
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i] ?? "";
            isKey[i] = raw.StartsWith("*");
            string shown = isKey[i] ? raw.Substring(1).Trim() : raw;

            var text = UIBuild.Text("Line" + i, listRoot, font, 30, new Color(0.14f, 0.12f, 0.1f, 1f), TextAnchor.MiddleLeft);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.text = shown;
            text.raycastTarget = true;
            var lrt = text.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(1f, 1f); lrt.pivot = new Vector2(0.5f, 1f);
            lrt.sizeDelta = new Vector2(0f, 50f);
            lrt.anchoredPosition = new Vector2(0f, -i * lineHeight);
            lineTexts.Add(text);

            var ul = UIBuild.Image("Underline", text.transform, new Color(0.72f, 0.08f, 0.06f, 0.9f));
            var urt = ul.rectTransform;
            urt.anchorMin = urt.anchorMax = new Vector2(0f, 0f); urt.pivot = new Vector2(0f, 0.5f);
            urt.sizeDelta = new Vector2(0f, 4f);
            urt.anchoredPosition = new Vector2(-6f, 8f);
            urt.localRotation = Quaternion.Euler(0f, 0f, 0.4f);
            ul.enabled = false;
            underlines.Add(ul);
        }
        crt.anchoredPosition = lineTexts[0].rectTransform.anchoredPosition;

        counterText = UIBuild.Text("Counter", paper, font, 26, new Color(0.45f, 0.1f, 0.08f, 1f), TextAnchor.LowerRight);
        var cort = counterText.rectTransform;
        cort.anchorMin = new Vector2(0f, 0f); cort.anchorMax = new Vector2(0.94f, 0f); cort.pivot = new Vector2(0.5f, 0f);
        cort.sizeDelta = new Vector2(0f, 40f); cort.anchoredPosition = new Vector2(0f, 30f);

        feedbackText = UIBuild.Text("Feedback", canvasObject.transform, font, 30, new Color(0.9f, 0.55f, 0.45f, 1f), TextAnchor.MiddleCenter);
        var frt = feedbackText.rectTransform;
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(0.5f, 0f);
        frt.sizeDelta = new Vector2(0f, 50f); frt.anchoredPosition = new Vector2(0f, 60f);

        // 중간 대사용 자막 띠
        var bar = UIBuild.Image("SubtitleBar", canvasObject.transform, new Color(0.05f, 0.05f, 0.05f, 0.9f));
        subtitleBar = bar.gameObject;
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0.15f, 0f); brt.anchorMax = new Vector2(0.85f, 0f); brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(0f, 110f); brt.anchoredPosition = new Vector2(0f, 110f);
        subtitleText = UIBuild.Text("Subtitle", bar.transform, font, 34, Color.white, TextAnchor.MiddleCenter);
        UIBuild.Stretch(subtitleText.rectTransform);
        subtitleBar.SetActive(false);

        var help = UIBuild.Text("Help", canvasObject.transform, font, 22, new Color(0.6f, 0.6f, 0.6f, 1f), TextAnchor.LowerCenter);
        help.text = "W/S 또는 마우스로 고르기     Enter / 클릭 줄 긋기";
        var hl = help.rectTransform;
        hl.anchorMin = new Vector2(0f, 0f); hl.anchorMax = new Vector2(1f, 0f); hl.pivot = new Vector2(0.5f, 0f);
        hl.sizeDelta = new Vector2(0f, 40f); hl.anchoredPosition = new Vector2(0f, 14f);

        canvasObject.SetActive(false);
    }
}
