using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 기억이 끝날 때 나오는 추리 질문. 맞혀야 기억이 완성된다.
//  - W/S(↑↓)로 고르고 Enter/Space(또는 클릭)로 답한다. 틀리면 다시 고른다.
//  - Tab으로 기억 노트를 열어 모은 단서를 다시 볼 수 있다.
// 탐색 구간(FlashbackFreeRoamSegment)이 마지막 장소에 도착한 뒤 Ask()를 기다린다.
public class DeductionQuestion : MonoBehaviour
{
    [TextArea(2, 4)] [SerializeField] private string question = "질문";
    [SerializeField] private string[] options = { "보기 1", "보기 2", "보기 3" };
    [SerializeField] private int correctIndex = 0;
    [TextArea] [SerializeField] private string wrongText = "...아니야. 다시 생각해 보자.";
    [TextArea] [SerializeField] private string rightText = "그래... 기억났어.";

    [SerializeField] private AudioClip moveSound;
    [SerializeField] private AudioClip wrongSound;
    [SerializeField] private AudioClip rightSound;

    // 엔딩 판정용: 한 번에(틀리지 않고) 맞힌 질문. 새 게임을 시작하면 비운다.
    private static readonly System.Collections.Generic.HashSet<string> firstTryCorrect = new System.Collections.Generic.HashSet<string>();

    /// <summary>틀리지 않고 한 번에 맞힌 추리 질문 수</summary>
    public static int FirstTryCorrectCount => firstTryCorrect.Count;

    public static void ResetScore() => firstTryCorrect.Clear();

    private GameObject canvasObject;
    private Text questionText;
    private Text optionsText;
    private Text feedbackText;
    private int selected;
    private AudioSource audioSource;

    public void SetContent(string newQuestion, string[] newOptions, int newCorrect, string wrong = null, string right = null)
    {
        question = newQuestion;
        options = newOptions;
        correctIndex = newCorrect;
        if (wrong != null) wrongText = wrong;
        if (right != null) rightText = right;
    }

    /// <summary>맞힐 때까지 기다린다.</summary>
    public IEnumerator Ask(FreeRoamMovement player)
    {
        if (options == null || options.Length == 0) yield break;

        Build();
        if (player != null) player.InputPaused = true;
        CursorLockMode prevLock = Cursor.lockState;
        bool prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        questionText.text = question;
        feedbackText.text = "";
        selected = 0;
        bool missed = false;
        Refresh();
        canvasObject.SetActive(true);
        yield return null;

        while (true)
        {
            var notebook = MemoryNotebook.Instance;
            if (Input.GetKeyDown(KeyCode.Tab)) notebook.SetOpen(!notebook.IsOpen, null);
            if (notebook.IsOpen) { yield return null; continue; }

            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % options.Length; Refresh(); Play(moveSound); }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + options.Length) % options.Length; Refresh(); Play(moveSound); }

            int clicked = ClickedOption();
            if (clicked >= 0) selected = clicked;

            if (clicked >= 0 || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            {
                if (selected == correctIndex)
                {
                    if (!missed) firstTryCorrect.Add(question);
                    Debug.Log($"[DeductionQuestion] '{question}' 정답 ({(missed ? "틀린 뒤" : "한 번에")}). 한 번에 맞힌 수: {FirstTryCorrectCount}");
                    Play(rightSound);
                    feedbackText.text = rightText;
                    yield return new WaitForSeconds(1.6f);
                    break;
                }

                missed = true;
                Play(wrongSound);
                feedbackText.text = wrongText;
                yield return Shake();
            }

            yield return null;
        }

        canvasObject.SetActive(false);
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        if (player != null) player.InputPaused = false;
    }

    private void Refresh()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < options.Length; i++)
        {
            sb.Append(i == selected ? "> " : "   ");
            sb.Append(options[i]);
            if (i < options.Length - 1) sb.Append("\n\n");
        }
        optionsText.text = sb.ToString();
    }

    // 보기 줄을 마우스로 눌렀는지 (보기 글자 영역을 줄 수로 나눠서 판정)
    private int ClickedOption()
    {
        if (!Input.GetMouseButtonDown(0)) return -1;
        var rt = optionsText.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, null, out Vector2 local)) return -1;
        if (!rt.rect.Contains(local)) return -1;

        float lineHeight = optionsText.fontSize * optionsText.lineSpacing * 1.15f;
        float fromTop = rt.rect.yMax - local.y;
        int line = Mathf.FloorToInt(fromTop / lineHeight);
        if (line % 2 == 1) return -1; // 보기 사이 빈 줄
        int index = line / 2;
        return index >= 0 && index < options.Length ? index : -1;
    }

    private IEnumerator Shake()
    {
        var rt = (RectTransform)optionsText.transform.parent;
        Vector2 basePos = rt.anchoredPosition;
        float t = 0f;
        while (t < 0.3f)
        {
            t += Time.unscaledDeltaTime;
            rt.anchoredPosition = basePos + Random.insideUnitCircle * 8f * (1f - t / 0.3f);
            yield return null;
        }
        rt.anchoredPosition = basePos;
    }

    private void Play(AudioClip clip)
    {
        if (clip == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f;
            audioSource.playOnAwake = false;
        }
        audioSource.PlayOneShot(clip);
    }

    private void Build()
    {
        if (canvasObject != null) return;

        canvasObject = UIBuild.OverlayCanvas("DeductionCanvas", transform, 430);
        Font font = UIFontUtil.Resolve(null);

        var dim = UIBuild.Image("Dim", canvasObject.transform, new Color(0f, 0f, 0f, 0.82f));
        UIBuild.Stretch(dim.rectTransform);

        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasObject.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1300f, 760f);

        var header = UIBuild.Text("Header", panel.transform, font, 26, new Color(0.6f, 0.6f, 0.6f, 1f), TextAnchor.UpperCenter);
        header.text = "- 기억을 맞춰 보자 -";
        var hrt = header.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f); hrt.anchorMax = new Vector2(1f, 1f); hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(0f, 40f); hrt.anchoredPosition = Vector2.zero;

        questionText = UIBuild.Text("Question", panel.transform, font, 40, Color.white, TextAnchor.UpperCenter);
        var qrt = questionText.rectTransform;
        qrt.anchorMin = new Vector2(0f, 1f); qrt.anchorMax = new Vector2(1f, 1f); qrt.pivot = new Vector2(0.5f, 1f);
        qrt.sizeDelta = new Vector2(0f, 120f); qrt.anchoredPosition = new Vector2(0f, -60f);

        var optionsRoot = new GameObject("OptionsRoot", typeof(RectTransform));
        optionsRoot.transform.SetParent(panel.transform, false);
        var ort = (RectTransform)optionsRoot.transform;
        ort.anchorMin = new Vector2(0.15f, 0.2f); ort.anchorMax = new Vector2(0.95f, 0.75f);
        ort.offsetMin = Vector2.zero; ort.offsetMax = Vector2.zero;

        optionsText = UIBuild.Text("Options", optionsRoot.transform, font, 32, new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.UpperLeft);
        UIBuild.Stretch(optionsText.rectTransform);
        optionsText.raycastTarget = true;

        feedbackText = UIBuild.Text("Feedback", panel.transform, font, 30, new Color(0.9f, 0.55f, 0.45f, 1f), TextAnchor.MiddleCenter);
        var frt = feedbackText.rectTransform;
        frt.anchorMin = new Vector2(0f, 0f); frt.anchorMax = new Vector2(1f, 0f); frt.pivot = new Vector2(0.5f, 0f);
        frt.sizeDelta = new Vector2(0f, 60f); frt.anchoredPosition = new Vector2(0f, 70f);

        var help = UIBuild.Text("Help", panel.transform, font, 22, new Color(0.55f, 0.55f, 0.55f, 1f), TextAnchor.LowerCenter);
        help.text = "W/S 고르기     Enter 답하기     Tab 기억 노트";
        var hl = help.rectTransform;
        hl.anchorMin = new Vector2(0f, 0f); hl.anchorMax = new Vector2(1f, 0f); hl.pivot = new Vector2(0.5f, 0f);
        hl.sizeDelta = new Vector2(0f, 40f); hl.anchoredPosition = new Vector2(0f, 10f);

        canvasObject.SetActive(false);
    }
}
