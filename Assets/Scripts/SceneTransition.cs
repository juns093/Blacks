using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransition : MonoBehaviour
{
    public enum TransitionType { Wipe, Fade }

    public static SceneTransition Instance;

    [Header("공통 설정")]
    [SerializeField] private TransitionType transitionType = TransitionType.Fade;
    [SerializeField] private Image overlayImage;
    [SerializeField] private float duration = 0.4f;
    [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Color overlayColor = Color.black;

    [Header("Wipe 전용 설정")]
    [SerializeField] private RectTransform wipePanel;

    private RectTransform canvasRect;
    private bool isTransitioning = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        canvasRect = GetComponent<RectTransform>();
        overlayImage.color = overlayColor;
        SetAlpha(0f);
        SetPanelX(canvasRect.rect.width);
    }

    // 타입을 직접 지정해서 씬 로드 (미지정 시 Inspector 설정값 사용)
    public void LoadScene(int sceneIndex, TransitionType? type = null, AsyncOperation pendingOp = null)
    {
        if (isTransitioning) return;

        TransitionType resolvedType = type ?? transitionType;

        switch (resolvedType)
        {
            case TransitionType.Wipe:
                StartCoroutine(WipeRoutine(sceneIndex, pendingOp));
                break;
            case TransitionType.Fade:
                StartCoroutine(FadeRoutine(sceneIndex, pendingOp));
                break;
        }
    }

    // ─── Wipe 루틴 ───────────────────────────────────────
    private IEnumerator WipeRoutine(int sceneIndex, AsyncOperation pendingOp)
    {
        isTransitioning = true;
        float width = canvasRect.rect.width;

        SetAlpha(1f);
        SetPanelX(width);

        yield return StartCoroutine(AnimatePanelX(width, 0f));

        if (pendingOp != null)
        {
            pendingOp.allowSceneActivation = true;
            while (!pendingOp.isDone) yield return null;
        }
        else
        {
            SceneManager.LoadScene(sceneIndex);
            yield return null;
        }

        yield return StartCoroutine(AnimatePanelX(0f, -width));

        SetPanelX(width);
        SetAlpha(0f);
        isTransitioning = false;
    }

    // ─── Fade 루틴 ───────────────────────────────────────
    private IEnumerator FadeRoutine(int sceneIndex, AsyncOperation pendingOp)
    {
        isTransitioning = true;

        SetPanelX(0f);

        yield return StartCoroutine(AnimateAlpha(0f, 1f));

        if (pendingOp != null)
        {
            pendingOp.allowSceneActivation = true;
            while (!pendingOp.isDone) yield return null;
        }
        else
        {
            SceneManager.LoadScene(sceneIndex);
            yield return null;
        }

        yield return StartCoroutine(AnimateAlpha(1f, 0f));

        SetAlpha(0f);
        SetPanelX(canvasRect.rect.width);
        isTransitioning = false;
    }

    // ─── 공통 애니메이션 ──────────────────────────────────
    private IEnumerator AnimatePanelX(float fromX, float toX)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetPanelX(Mathf.Lerp(fromX, toX, t));
            yield return null;
        }
        SetPanelX(toX);
    }

    private IEnumerator AnimateAlpha(float fromAlpha, float toAlpha)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = curve.Evaluate(Mathf.Clamp01(elapsed / duration));
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, t));
            yield return null;
        }
        SetAlpha(toAlpha);
    }

    private void SetPanelX(float x)
    {
        if (wipePanel == null) return;
        Vector2 pos = wipePanel.anchoredPosition;
        pos.x = x;
        wipePanel.anchoredPosition = pos;
    }

    private void SetAlpha(float alpha)
    {
        Color c = overlayColor;
        c.a = alpha;
        overlayImage.color = c;
    }

    void OnValidate()
    {
        if (overlayImage != null)
        {
            Color c = overlayColor;
            c.a = overlayImage.color.a;
            overlayImage.color = c;
        }
    }
}

