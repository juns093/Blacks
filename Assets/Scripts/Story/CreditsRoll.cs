using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 엔딩 뒤에 올라가는 크레딧.
//  - 검은 화면에 글자가 아래에서 위로 천천히 올라간다.
//  - 마우스/스페이스를 누르고 있으면 빨리 감기, Esc로 건너뛰기.
//  - 마지막에 IdiotGames 로고를 잠깐 보여 주고 끝난다.
public class CreditsRoll : MonoBehaviour
{
    [SerializeField] private float scrollSpeed = 70f;     // 기준 해상도(1080p)에서 초당 픽셀
    [SerializeField] private float fastMultiplier = 5f;

    // (글자, 크기, 색 종류) 0 = 흰색, 1 = 회색(역할), 2 = 붉은색(제목)
    private static readonly (string text, int size, int tone)[] Lines =
    {
        ("BLANKS", 84, 2),
        ("", 40, 0),
        ("IdiotGames", 60, 0),
        ("", 120, 0),

        ("Game Design & Development", 26, 1),
        ("park hyeon jun", 40, 0),
        ("", 70, 0),

        ("Sound Effects  (freesound.org)", 26, 1),
        ("freekit", 30, 0),
        ("bolkmar", 30, 0),
        ("ferrettomato", 30, 0),
        ("blou27", 30, 0),
        ("qubodup", 30, 0),
        ("glitchedtones", 30, 0),
        ("", 70, 0),

        ("Music  (freesound.org)", 26, 1),
        ("christmaskrumble666", 30, 0),
        ("colinleblancsound", 30, 0),
        ("", 70, 0),

        ("Made with Unity", 26, 1),
        ("", 160, 0),

        ("Thank you for playing.", 34, 0),
    };

    public static IEnumerator Run()
    {
        var go = new GameObject("CreditsRoll");
        var roll = go.AddComponent<CreditsRoll>();
        yield return roll.Play();
        Destroy(go);
    }

    private IEnumerator Play()
    {
        var canvas = UIBuild.OverlayCanvas("CreditsCanvas", transform, 950);
        var canvasRect = (RectTransform)canvas.transform;
        var bg = UIBuild.Image("Black", canvas.transform, Color.black);
        UIBuild.Stretch(bg.rectTransform);

        Font font = UIFontUtil.Resolve(null);

        // 글자를 세로로 쌓은 묶음
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(canvas.transform, false);
        content.anchorMin = content.anchorMax = new Vector2(0.5f, 0f);
        content.pivot = new Vector2(0.5f, 1f);

        float y = 0f;
        foreach (var line in Lines)
        {
            float h = line.size * 1.5f;
            if (!string.IsNullOrEmpty(line.text))
            {
                Color c = line.tone == 1 ? new Color(0.6f, 0.6f, 0.6f) : line.tone == 2 ? new Color(0.72f, 0.12f, 0.1f) : Color.white;
                var t = UIBuild.Text("Line", content, font, line.size, c, TextAnchor.MiddleCenter);
                var rt = t.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(1600f, h);
                rt.anchoredPosition = new Vector2(0f, -y);
                t.text = line.text;
            }
            y += h;
        }
        content.sizeDelta = new Vector2(1600f, y);

        // 화면 아래에서 시작해서, 마지막 줄이 화면 가운데에 올 때까지
        float screenH = canvasRect.rect.height;
        float startY = 0f;                          // 내용 맨 위가 화면 아래 끝
        float endY = y + screenH * 0.5f - 40f;      // 마지막 줄이 가운데쯤
        content.anchoredPosition = new Vector2(0f, startY);

        yield return null;
        float pos = startY;
        while (pos < endY)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) break;
            bool fast = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
            pos += scrollSpeed * (fast ? fastMultiplier : 1f) * Time.unscaledDeltaTime;
            content.anchoredPosition = new Vector2(0f, Mathf.Min(pos, endY));
            yield return null;
        }

        yield return Wait(2.5f);

        // 천천히 어두워지며 끝
        var group = content.gameObject.AddComponent<CanvasGroup>();
        float f = 0f;
        while (f < 1.5f)
        {
            f += Time.unscaledDeltaTime;
            group.alpha = 1f - f / 1.5f;
            yield return null;
        }
        yield return Wait(0.5f);
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }
}
