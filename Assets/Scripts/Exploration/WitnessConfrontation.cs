using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 행인에게 들켰을 때의 짧은 대화.
//   행인: 저기요. / 거기서 뭐 해요?
//   → 변명을 고른다 (W/S + Enter 또는 클릭)
//   → 50% 확률로 넘어간다. 실패하면 행인이 수상하게 여기고 처음부터 다시.
public class WitnessConfrontation : MonoBehaviour
{
    [SerializeField] private string speaker = "행인";
    [SerializeField] private string[] openingLines = { "저기요.", "거기서 뭐 해요?" };
    [SerializeField] private string[] excuses =
    {
        "\"아, 떨어뜨린 걸 찾고 있어서요...\"",
        "\"차를 어디 세워 뒀는지 깜빡해서요.\"",
        "\"그냥... 산책 중이에요.\""
    };
    [SerializeField] private string[] successReplies =
    {
        "...아, 네. 늦었는데 조심히 들어가세요.",
        "...그래요? 이 동네 밤엔 위험해요.",
        "...네. 수고하세요."
    };
    [SerializeField] private string[] failReplies =
    {
        "...이상한데. 경찰 불러야겠네요.",
        "...거짓말하지 마세요. 거기 서요!",
        "...잠깐만요, 얼굴 좀 봅시다."
    };
    [Range(0f, 1f)] [SerializeField] private float successChance = 0.5f;
    [SerializeField] private float lineDuration = 1.6f;

    private GameObject canvasObject;
    private Text lineText;
    private Text optionsText;
    private Text helpText;
    private GameObject optionsPanel;

    /// <summary>테스트용: 0 이상이면 그 보기를 고른 것으로 친다.</summary>
    public static int DebugChoice = -1;
    private int selected;

    public static WitnessConfrontation Get()
    {
        var c = FindFirstObjectByType<WitnessConfrontation>();
        if (c == null) c = new GameObject("WitnessConfrontation").AddComponent<WitnessConfrontation>();
        return c;
    }

    /// <summary>대화를 진행하고 결과(변명 성공 여부)를 돌려준다.</summary>
    public IEnumerator Run(FreeRoamMovement player, WitnessPatrol witness, System.Action<bool> result)
    {
        Build();
        WitnessPatrol.GlobalPause = true;
        player.InputPaused = true;
        witness.Confront(player.transform);

        // 행인 쪽으로 고개를 돌린다.
        yield return TurnToward(player, witness.transform.position + Vector3.up * 1.6f, 0.45f);

        canvasObject.SetActive(true);
        optionsPanel.SetActive(false);

        foreach (string l in openingLines)
        {
            lineText.text = speaker + ": " + l;
            yield return Wait(lineDuration);
        }

        // 변명 고르기
        CursorLockMode prevLock = Cursor.lockState;
        bool prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        selected = 0;
        optionsPanel.SetActive(true);
        Refresh();
        yield return null;

        while (true)
        {
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) { selected = (selected + 1) % excuses.Length; Refresh(); }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) { selected = (selected - 1 + excuses.Length) % excuses.Length; Refresh(); }
            if (DebugChoice >= 0) { selected = Mathf.Clamp(DebugChoice, 0, excuses.Length - 1); DebugChoice = -1; break; }
            int clicked = ClickedOption();
            if (clicked >= 0) { selected = clicked; break; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) break;
            yield return null;
        }

        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        optionsPanel.SetActive(false);

        lineText.text = excuses[selected];
        yield return Wait(lineDuration);

        bool ok = Random.value < successChance;
        Debug.Log($"[WitnessConfrontation] 변명 '{excuses[selected]}' → {(ok ? "통했다" : "안 통했다")}");
        string[] replies = ok ? successReplies : failReplies;
        lineText.text = speaker + ": " + replies[Mathf.Clamp(selected, 0, replies.Length - 1)];
        yield return Wait(lineDuration + 0.3f);

        canvasObject.SetActive(false);
        WitnessPatrol.GlobalPause = false;
        if (ok)
        {
            witness.Release(6f);
            player.InputPaused = false;
        }
        result?.Invoke(ok);
    }

    private static IEnumerator TurnToward(FreeRoamMovement player, Vector3 point, float duration)
    {
        player.GetLook(out float yaw0, out float pitch0);
        Vector3 dir = point - player.Head.position;
        float yaw1 = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float pitch1 = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
        yaw1 = yaw0 + Mathf.DeltaAngle(yaw0, yaw1);
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / duration);
            player.SetLook(Mathf.Lerp(yaw0, yaw1, k), Mathf.Lerp(pitch0, pitch1, k));
            yield return null;
        }
        player.SetLook(yaw1, pitch1);
    }

    // 클릭하면 조금 빨리 넘어간다.
    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        yield return null;
        while (t < seconds)
        {
            if (Input.GetMouseButtonDown(0) && t > 0.3f) break;
            t += Time.deltaTime;
            yield return null;
        }
    }

    private void Refresh()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < excuses.Length; i++)
        {
            sb.Append(i == selected ? "> " : "   ");
            sb.Append(excuses[i]);
            if (i < excuses.Length - 1) sb.Append("\n\n");
        }
        optionsText.text = sb.ToString();
    }

    private int ClickedOption()
    {
        if (!Input.GetMouseButtonDown(0)) return -1;
        var rt = optionsText.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, null, out Vector2 local)) return -1;
        if (!rt.rect.Contains(local)) return -1;
        float lineHeight = optionsText.fontSize * optionsText.lineSpacing * 1.15f;
        int line = Mathf.FloorToInt((rt.rect.yMax - local.y) / lineHeight);
        if (line % 2 == 1) return -1;
        int index = line / 2;
        return index >= 0 && index < excuses.Length ? index : -1;
    }

    private void Build()
    {
        if (canvasObject != null) return;

        canvasObject = UIBuild.OverlayCanvas("WitnessTalkCanvas", transform, 420);
        Font font = UIFontUtil.Resolve(null);

        // 아래쪽 대사 띠
        var bar = UIBuild.Image("Bar", canvasObject.transform, new Color(0f, 0f, 0f, 0.75f));
        var brt = bar.rectTransform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f); brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(0f, 110f); brt.anchoredPosition = new Vector2(0f, 110f); // 아래 조작 안내와 겹치지 않게

        lineText = UIBuild.Text("Line", bar.transform, font, 34, Color.white, TextAnchor.MiddleCenter);
        UIBuild.Stretch(lineText.rectTransform);

        // 가운데 변명 보기
        var optionsRoot = new GameObject("Options", typeof(RectTransform));
        optionsRoot.transform.SetParent(canvasObject.transform, false);
        var ort = (RectTransform)optionsRoot.transform;
        ort.anchorMin = ort.anchorMax = new Vector2(0.5f, 0.5f);
        ort.sizeDelta = new Vector2(1100f, 300f);
        ort.anchoredPosition = new Vector2(0f, 20f);
        optionsPanel = optionsRoot;
        var dim = optionsRoot.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);

        optionsText = UIBuild.Text("OptionsText", optionsRoot.transform, font, 30, new Color(0.9f, 0.9f, 0.9f, 1f), TextAnchor.UpperLeft);
        var otr = optionsText.rectTransform;
        otr.anchorMin = Vector2.zero; otr.anchorMax = Vector2.one;
        otr.offsetMin = new Vector2(60f, 30f); otr.offsetMax = new Vector2(-40f, -40f);
        optionsText.raycastTarget = true;

        helpText = UIBuild.Text("Help", optionsRoot.transform, font, 20, new Color(0.6f, 0.6f, 0.6f, 1f), TextAnchor.LowerCenter);
        var hrt = helpText.rectTransform;
        hrt.anchorMin = new Vector2(0f, 0f); hrt.anchorMax = new Vector2(1f, 0f); hrt.pivot = new Vector2(0.5f, 0f);
        hrt.sizeDelta = new Vector2(0f, 30f); hrt.anchoredPosition = new Vector2(0f, 6f);
        helpText.text = "변명한다  -  W/S 고르기   Enter 말하기   (통할지는 반반)";

        canvasObject.SetActive(false);
    }
}
