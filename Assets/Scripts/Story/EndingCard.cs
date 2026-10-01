using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 엔딩 이름을 검은 화면에 띄우고 메뉴로 돌아간다.
//   ENDING
//   ─ 자수 ─
public class EndingCard : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MenuScene";
    [SerializeField] private float holdDuration = 4f;

    public static IEnumerator Show(string title, string subtitle)
    {
        var go = new GameObject("EndingCard");
        var card = go.AddComponent<EndingCard>();
        yield return card.Run(title, subtitle);
    }

    private IEnumerator Run(string title, string subtitle)
    {
        Debug.Log($"[EndingCard] {title} {subtitle}");

        var canvas = UIBuild.OverlayCanvas("EndingCanvas", transform, 900);
        var black = UIBuild.Image("Black", canvas.transform, new Color(0f, 0f, 0f, 0f));
        UIBuild.Stretch(black.rectTransform);

        Font font = UIFontUtil.Resolve(null);
        var titleText = UIBuild.Text("Title", canvas.transform, font, 64, new Color(0.85f, 0.85f, 0.85f, 0f), TextAnchor.MiddleCenter);
        var trt = titleText.rectTransform;
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.55f);
        trt.sizeDelta = new Vector2(1400f, 100f);
        titleText.text = title;

        var subText = UIBuild.Text("Subtitle", canvas.transform, font, 40, new Color(0.7f, 0.2f, 0.18f, 0f), TextAnchor.MiddleCenter);
        var srt = subText.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.45f);
        srt.sizeDelta = new Vector2(1400f, 80f);
        subText.text = string.IsNullOrEmpty(subtitle) ? "" : "- " + subtitle + " -";

        if (BGMManager.Instance != null)
            BGMManager.Instance.FadeOutAndStop(1.5f);

        // 까맣게 → 글자가 천천히
        yield return Fade(black, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(0.5f);
        float t = 0f;
        while (t < 1.5f)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / 1.5f);
            titleText.color = new Color(0.85f, 0.85f, 0.85f, a);
            subText.color = new Color(0.7f, 0.2f, 0.18f, a);
            yield return null;
        }

        yield return new WaitForSeconds(holdDuration);

        // 엔딩 이름이 사라지고 크레딧
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t);
            titleText.color = new Color(0.85f, 0.85f, 0.85f, a);
            subText.color = new Color(0.7f, 0.2f, 0.18f, a);
            yield return null;
        }
        yield return CreditsRoll.Run();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        CamMove.blockLook = false;
        CamMove.blockInteraction = false;

        if (SceneTransition.Instance != null)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == menuSceneName)
                {
                    SceneTransition.Instance.LoadScene(i);
                    yield break;
                }
            }
        }
        SceneManager.LoadScene(menuSceneName);
    }

    private static IEnumerator Fade(Image img, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            img.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        img.color = new Color(0f, 0f, 0f, to);
    }
}
