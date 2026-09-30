using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 기억 노트. 탐색하며 주운 단서가 여기에 쌓이고, Tab으로 열어서 다시 읽을 수 있다.
//  - 단서는 게임 한 판 동안 유지된다. (루프로 씬이 다시 로드돼도 남음, 새 게임이면 비움)
//  - 기억 끝의 추리 질문(DeductionQuestion)에 답할 때 참고한다.
//  - 탐색 구간에서만 Tab이 동작한다. (FreeRoamMovement 조작 중)
public class MemoryNotebook : MonoBehaviour
{
    public class Clue
    {
        public string id;
        public string title;
        public string text;
        public int memory; // 몇 번째 기억에서 얻었는지 (1~4, 0이면 모름)
    }

    private static readonly List<Clue> clues = new List<Clue>();
    private static MemoryNotebook instance;

    public static IReadOnlyList<Clue> Clues => clues;

    public static MemoryNotebook Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("MemoryNotebook").AddComponent<MemoryNotebook>();
            return instance;
        }
    }

    /// <summary>새 게임을 시작할 때 비운다. (GameStateManager가 호출)</summary>
    public static void ClearAll() => clues.Clear();

    public static bool Has(string id)
    {
        foreach (var c in clues) if (c.id == id) return true;
        return false;
    }

    /// <summary>단서를 노트에 적는다. 이미 있으면 무시하고 false.</summary>
    public static bool Add(string id, string title, string text, int memory = 0)
    {
        if (string.IsNullOrEmpty(id)) id = title;
        if (string.IsNullOrEmpty(id) || Has(id)) return false;
        clues.Add(new Clue { id = id, title = title, text = text, memory = memory });
        Debug.Log($"[MemoryNotebook] 단서 기록: {title}");
        Instance.Flash(title);
        return true;
    }

    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

    private GameObject canvasObject;
    private GameObject notebookRoot;
    private Text listText;
    private Text detailTitle;
    private Text detailText;
    private Text toastText;
    private float toastUntil;
    private int selected;
    private bool open;
    private FreeRoamMovement pausedPlayer;

    public bool IsOpen => open;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    private void Update()
    {
        // 기록 알림
        if (toastText.gameObject.activeSelf)
        {
            float left = toastUntil - Time.unscaledTime;
            var c = toastText.color; c.a = Mathf.Clamp01(left / 0.5f); toastText.color = c;
            if (left <= 0f) toastText.gameObject.SetActive(false);
        }

        FreeRoamMovement player = FindActivePlayer();
        bool canOpen = player != null && (open || !player.InputPaused);

        if (Input.GetKeyDown(toggleKey) && canOpen)
            SetOpen(!open, player);
        else if (open && Input.GetKeyDown(KeyCode.Escape))
            SetOpen(false, player);

        if (!open) return;

        if (clues.Count > 0)
        {
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % clues.Count; Refresh(); }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + clues.Count) % clues.Count; Refresh(); }
        }
    }

    private static FreeRoamMovement FindActivePlayer()
    {
        var p = FindFirstObjectByType<FreeRoamMovement>();
        return p != null && p.IsControlling ? p : null;
    }

    public void SetOpen(bool value, FreeRoamMovement player)
    {
        open = value;
        notebookRoot.SetActive(open);

        if (open)
        {
            pausedPlayer = player;
            if (pausedPlayer != null) pausedPlayer.InputPaused = true;
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, clues.Count - 1));
            Refresh();
        }
        else if (pausedPlayer != null)
        {
            pausedPlayer.InputPaused = false;
            pausedPlayer = null;
        }
    }

    private void Refresh()
    {
        if (clues.Count == 0)
        {
            listText.text = "(아직 적힌 것이 없다)";
            detailTitle.text = "";
            detailText.text = "";
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < clues.Count; i++)
        {
            sb.Append(i == selected ? "> " : "   ");
            sb.Append(clues[i].title);
            sb.Append('\n');
        }
        listText.text = sb.ToString();
        detailTitle.text = clues[selected].title;
        detailText.text = clues[selected].text;
    }

    private void Flash(string title)
    {
        toastText.text = "기억 노트에 적었다 : " + title + "   [Tab]";
        toastText.gameObject.SetActive(true);
        var c = toastText.color; c.a = 1f; toastText.color = c;
        toastUntil = Time.unscaledTime + 3f;
        if (open) Refresh();
    }

    private void Build()
    {
        canvasObject = new GameObject("MemoryNotebookCanvas");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 440;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = UIFontUtil.Resolve(null);

        // 기록 알림 (오른쪽 위)
        toastText = UIBuild.Text("Toast", canvasObject.transform, font, 24, new Color(0.9f, 0.9f, 0.9f, 1f), TextAnchor.UpperRight);
        var trt = toastText.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(1f, 1f);
        trt.sizeDelta = new Vector2(900f, 40f);
        trt.anchoredPosition = new Vector2(-32f, -32f);
        toastText.gameObject.AddComponent<Shadow>().effectColor = Color.black;
        toastText.gameObject.SetActive(false);

        // 노트
        notebookRoot = new GameObject("Notebook", typeof(RectTransform));
        notebookRoot.transform.SetParent(canvasObject.transform, false);
        UIBuild.Stretch((RectTransform)notebookRoot.transform);

        var dim = UIBuild.Image("Dim", notebookRoot.transform, new Color(0f, 0f, 0f, 0.7f));
        UIBuild.Stretch(dim.rectTransform);

        var paper = UIBuild.Image("Paper", notebookRoot.transform, new Color(0.86f, 0.83f, 0.74f, 1f));
        var prt = paper.rectTransform;
        prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1300f, 720f);

        Color ink = new Color(0.12f, 0.1f, 0.09f, 1f);
        var title = UIBuild.Text("Title", paper.transform, font, 40, ink, TextAnchor.UpperCenter);
        title.text = "기억 노트";
        var tt = title.rectTransform;
        tt.anchorMin = new Vector2(0f, 1f); tt.anchorMax = new Vector2(1f, 1f); tt.pivot = new Vector2(0.5f, 1f);
        tt.sizeDelta = new Vector2(0f, 60f); tt.anchoredPosition = new Vector2(0f, -30f);

        listText = UIBuild.Text("List", paper.transform, font, 28, ink, TextAnchor.UpperLeft);
        var lrt = listText.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f); lrt.anchorMax = new Vector2(0.38f, 1f);
        lrt.offsetMin = new Vector2(50f, 80f); lrt.offsetMax = new Vector2(0f, -110f);
        listText.lineSpacing = 1.3f;

        var divider = UIBuild.Image("Divider", paper.transform, new Color(0.3f, 0.25f, 0.2f, 0.5f));
        var drt = divider.rectTransform;
        drt.anchorMin = new Vector2(0.4f, 0f); drt.anchorMax = new Vector2(0.4f, 1f);
        drt.sizeDelta = new Vector2(3f, -180f);

        detailTitle = UIBuild.Text("DetailTitle", paper.transform, font, 32, ink, TextAnchor.UpperLeft);
        var dtt = detailTitle.rectTransform;
        dtt.anchorMin = new Vector2(0.43f, 1f); dtt.anchorMax = new Vector2(1f, 1f); dtt.pivot = new Vector2(0f, 1f);
        dtt.sizeDelta = new Vector2(-50f, 50f); dtt.anchoredPosition = new Vector2(0f, -110f);

        detailText = UIBuild.Text("DetailText", paper.transform, font, 26, ink, TextAnchor.UpperLeft);
        var dxt = detailText.rectTransform;
        dxt.anchorMin = new Vector2(0.43f, 0f); dxt.anchorMax = new Vector2(1f, 1f);
        dxt.offsetMin = new Vector2(0f, 80f); dxt.offsetMax = new Vector2(-50f, -175f);
        detailText.lineSpacing = 1.25f;

        var help = UIBuild.Text("Help", paper.transform, font, 22, new Color(0.3f, 0.26f, 0.22f, 1f), TextAnchor.LowerCenter);
        help.text = "W/S 넘기기     Tab 닫기";
        var hrt = help.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(0f, 40f); hrt.anchoredPosition = new Vector2(0f, 24f);

        notebookRoot.SetActive(false);
    }
}
