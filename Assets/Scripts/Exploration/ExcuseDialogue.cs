using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 목격자에게 불려 세워졌을 때 화면 아래에 뜨는 대화 + 변명 고르기.
//  - W/S(↑↓) 또는 숫자 1~3으로 고르고 Enter/Space(또는 클릭)로 말한다.
//  - 결과(통했는지)는 부른 쪽(FlashbackFreeRoamSegment)이 정한다. 이 창은 보여 주고 고르게만 한다.
// 필요할 때 자동으로 만들어진다. (ExcuseDialogue.Instance)
public class ExcuseDialogue : MonoBehaviour
{
    private static ExcuseDialogue instance;

    public static ExcuseDialogue Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("ExcuseDialogue").AddComponent<ExcuseDialogue>();
            return instance;
        }
    }

    private GameObject canvasObject;
    private Text lineText;
    private Text optionsText;
    private Text helpText;
    private string[] options;
    private int selected;

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        Build();
    }

    /// <summary>한 줄을 보여 주고 duration초 기다린다. (보기는 숨김)</summary>
    public IEnumerator ShowLine(string line, float duration)
    {
        Build();
        canvasObject.SetActive(true);
        lineText.text = line;
        optionsText.gameObject.SetActive(false);
        helpText.gameObject.SetActive(false);

        // 클릭하면 조금 빨리 넘긴다.
        float t = 0f;
        yield return null;
        while (t < duration)
        {
            if (t > 0.4f && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>질문(line)을 보여 주고 보기 중 하나를 고를 때까지 기다린다.</summary>
    public IEnumerator Ask(string line, string[] choices, System.Action<int> onChosen)
    {
        Build();
        options = (choices != null && choices.Length > 0) ? choices : new[] { "..." };
        selected = 0;

        CursorLockMode prevLock = Cursor.lockState;
        bool prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        canvasObject.SetActive(true);
        lineText.text = line;
        optionsText.gameObject.SetActive(true);
        helpText.gameObject.SetActive(true);
        Refresh();
        yield return null;

        int chosen = -1;
        while (chosen < 0)
        {
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % options.Length; Refresh(); }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + options.Length) % options.Length; Refresh(); }

            for (int i = 0; i < options.Length && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                    chosen = i;

            int clicked = ClickedOption();
            if (clicked >= 0) chosen = clicked;

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                chosen = selected;

            yield return null;
        }

        selected = chosen;
        Refresh();

        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        onChosen?.Invoke(chosen);
    }

    public void Hide()
    {
        if (canvasObject != null) canvasObject.SetActive(false);
    }

    private void Refresh()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < options.Length; i++)
        {
            sb.Append(i == selected ? "> " : "   ");
            sb.Append(i + 1).Append(". ").Append(options[i]);
            if (i < options.Length - 1) sb.Append('\n');
        }
        optionsText.text = sb.ToString();
    }

    // 보기 줄을 마우스로 눌렀는지 (보기 글자 영역을 줄 수로 나눠서 판정)
    private int ClickedOption()
    {
        if (!Input.GetMouseButtonDown(0) || options == null) return -1;
        var rt = optionsText.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, null, out Vector2 local)) return -1;
        if (!rt.rect.Contains(local)) return -1;

        float lineHeight = optionsText.fontSize * optionsText.lineSpacing * 1.15f;
        int index = Mathf.FloorToInt((rt.rect.yMax - local.y) / lineHeight);
        return index >= 0 && index < options.Length ? index : -1;
    }

    private void Build()
    {
        if (canvasObject != null) return;

        // 조작 안내/목표(ObjectiveHUD)와 기억 노트보다 위, 암전(500)보다 아래
        canvasObject = UIBuild.OverlayCanvas("ExcuseCanvas", transform, 420);
        Font font = UIFontUtil.Resolve(null);

        var panel = UIBuild.Image("Panel", canvasObject.transform, new Color(0f, 0f, 0f, 0.78f));
        var prt = panel.rectTransform;
        prt.anchorMin = new Vector2(0.18f, 0.06f);
        prt.anchorMax = new Vector2(0.82f, 0.36f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;

        lineText = UIBuild.Text("Line", panel.transform, font, 34, Color.white, TextAnchor.UpperLeft);
        var lrt = lineText.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0.62f); lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(40f, 0f); lrt.offsetMax = new Vector2(-40f, -24f);

        optionsText = UIBuild.Text("Options", panel.transform, font, 28, new Color(0.85f, 0.85f, 0.85f, 1f), TextAnchor.UpperLeft);
        optionsText.lineSpacing = 1.1f;
        optionsText.raycastTarget = true;
        var ort = optionsText.rectTransform;
        ort.anchorMin = new Vector2(0f, 0.14f); ort.anchorMax = new Vector2(1f, 0.6f);
        ort.offsetMin = new Vector2(70f, 0f); ort.offsetMax = new Vector2(-40f, 0f);

        helpText = UIBuild.Text("Help", panel.transform, font, 20, new Color(0.55f, 0.55f, 0.55f, 1f), TextAnchor.LowerRight);
        helpText.text = "W/S 또는 1~3 고르기     Enter 말하기";
        var hrt = helpText.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0.14f);
        hrt.offsetMin = new Vector2(40f, 8f); hrt.offsetMax = new Vector2(-30f, 0f);

        canvasObject.SetActive(false);
    }
}
