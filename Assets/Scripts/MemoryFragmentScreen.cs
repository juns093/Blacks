using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// 게임 화면 위쪽 띠에 기억 파편(아이템)을 나란히 띄우는 화면. (검은 화면 없이 테이블이 그대로 보인다)
//
//  - 파편은 아이템 프리팹(휴대폰 -> 혈액 -> 약 -> 구급상자 -> 17번 기록) 순서대로 왼쪽부터 한 칸씩 채워집니다.
//  - 지금까지 모은 파편은 처음부터 떠 있고, 이번 죽음에서 얻은 파편이 새로 떠오릅니다.
//  - 마우스를 올리면 커지고, 떼면 원래 크기로 돌아옵니다.
//  - 새 파편을 클릭하면 화면이 걷히면서 그 기억(타임라인)으로 넘어갑니다.
//
// 아이템은 월드 멀리 떨어진 무대(stageOrigin)에 복제해 두고, 전용 카메라가 투명 배경으로 찍어
// 화면 위쪽 띠(RawImage)에 겹쳐 보여 줍니다.
// 프리팹 목록은 DeathItemSpawner의 Item Prefabs를 그대로 받아 씁니다.
public class MemoryFragmentScreen : MonoBehaviour
{
    [Header("파편 (비워두면 DeathItemSpawner의 Item Prefabs 사용)")]
    [SerializeField] private List<GameObject> fragmentPrefabs = new List<GameObject>();

    [Tooltip("파편 칸 수 (기억 수)")]
    [SerializeField] private int slotCount = 5;

    [Tooltip("화면 위쪽 띠의 높이 (화면 높이 대비)")]
    [Range(0.1f, 0.5f)] [SerializeField] private float stripHeight = 0.26f;

    [Header("배치")]
    [Tooltip("아이템을 복제해 둘 무대 위치. 게임 화면에 안 걸리도록 멀리 둡니다.")]
    [SerializeField] private Vector3 stageOrigin = new Vector3(0f, -1000f, 0f);

    [Tooltip("아이템 하나의 크기 (가장 긴 변 기준, 월드 단위)")]
    [SerializeField] private float itemSize = 0.8f;

    [Tooltip("아이템 사이 간격 (중심 간 거리)")]
    [SerializeField] private float itemSpacing = 1.1f;

    [Tooltip("아이템이 천천히 도는 속도 (0이면 정지)")]
    [SerializeField] private float spinSpeed = 25f;

    [Tooltip("아이템을 살짝 기울여 보여주는 각도")]
    [SerializeField] private Vector3 displayTilt = new Vector3(50f, 0f, 0f);

    [Header("카메라")]
    [SerializeField] private float cameraFov = 25f;

    [Tooltip("화면 가장자리 여백 (월드 단위)")]
    [SerializeField] private float screenMargin = 0.35f;

    [Header("조명")]
    [SerializeField] private Color lightColor = new Color(1f, 0.85f, 0.8f);
    [SerializeField] private float lightIntensity = 20f;

    [Header("마우스 반응")]
    [Tooltip("마우스를 올렸을 때 커지는 배율")]
    [SerializeField] private float hoverScale = 1.3f;

    [Tooltip("커지고 작아지는 속도")]
    [SerializeField] private float scaleSpeed = 12f;

    [Header("연출 시간(초)")]
    [SerializeField] private float blackFadeInDuration = 0.6f;
    [SerializeField] private float oldFragmentsAppearDuration = 0.4f;
    [SerializeField] private float newFragmentAppearDuration = 1.4f;
    [SerializeField] private float fadeOutDuration = 0.9f;

    [Header("안내 문구")]
    [SerializeField] private string clickHint = "기억 파편을 눌러 기억을 되찾으세요";

    [Tooltip("비워두면 대사창과 같은 폰트(DOSMyungjo)를 씁니다.")]
    [SerializeField] private Font font;

    [Header("소리 (선택)")]
    [SerializeField] private AudioClip appearSound;
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("그리기 순서")]
    [Tooltip("Canvas_UI(10)보다 낮게 두면, 사망 대사가 이 화면 위에 나옵니다.")]
    [SerializeField] private int sortingOrder = 9;

    // UI (위쪽 띠 + 안내 문구)
    private GameObject canvasObject;
    private Image blackImage;
    private RawImage strip;
    private RenderTexture stageTexture;
    private Text hintText;
    private AudioSource audioSource;

    // 3D 무대
    private GameObject stageRoot;
    private Camera stageCamera;
    private Transform[] pivots;        // 칸마다 하나. 크기(호버)는 여기에 건다.
    private Transform[] spinners;      // 칸마다 하나. 제자리 회전은 여기에 건다.
    private float[] currentScale;

    private int newIndex = -1;
    private int visibleCount = 0;
    private bool interactive = false;
    private bool clicked = false;
    private bool showing = false;
    private int hovered = -1;

    private CursorLockMode prevLockState;
    private bool prevCursorVisible;
    private bool cursorOverridden = false;

    public bool IsShowing => showing;

    /// <summary>대본 파일(GameSceneStoryDialogues)에서 안내 문구를 채울 때 씁니다.</summary>
    public void SetClickHint(string hint) => clickHint = hint;

    /// <summary>이번에 띄운 새 파편을 눌렀는지 여부. (누르는 순간 기억 재생을 시작하는 데 씀)</summary>
    public bool WasClicked => clicked;

    /// <summary>DeathItemSpawner가 아이템 프리팹 목록을 넘겨줄 때 사용합니다. (직접 지정한 목록이 있으면 무시)</summary>
    public void SetDefaultPrefabs(List<GameObject> prefabs)
    {
        if (fragmentPrefabs != null && fragmentPrefabs.Count > 0) return;
        fragmentPrefabs = prefabs != null ? new List<GameObject>(prefabs) : new List<GameObject>();
    }

    /// <summary>
    /// 어두운 화면을 깔고, 이미 모은 파편(obtainedBefore개)을 띄운 뒤 newFragmentIndex번 파편을 새로 떠오르게 합니다.
    /// 클릭은 아직 받지 않습니다. (사망 대사가 끝난 뒤 WaitForClickAndHide에서 받습니다)
    /// </summary>
    public IEnumerator Show(int obtainedBefore, int newFragmentIndex)
    {
        BuildUI();

        newIndex = newFragmentIndex;
        visibleCount = Mathf.Clamp(newFragmentIndex + 1, 0, slotCount);
        interactive = false;
        clicked = false;
        hovered = -1;
        showing = true;

        prevLockState = Cursor.lockState;
        prevCursorVisible = Cursor.visible;

        hintText.gameObject.SetActive(false);
        SetBlack(0f);
        canvasObject.SetActive(true);

        // 1) 무대를 세우고 위쪽 띠를 서서히 띄운다. (검은 화면 없음)
        BuildStage(visibleCount);
        yield return FadeStrip(0f, 1f, blackFadeInDuration * 0.5f);

        // 3) 지금까지 모은 파편이 먼저 보인다.
        if (obtainedBefore > 0)
        {
            float t = 0f;
            while (t < oldFragmentsAppearDuration)
            {
                t += Time.deltaTime;
                float k = Ease(t / oldFragmentsAppearDuration);
                for (int i = 0; i < obtainedBefore && i < slotCount; i++)
                    SetSlotScale(i, k);
                yield return null;
            }
            for (int i = 0; i < obtainedBefore && i < slotCount; i++)
                SetSlotScale(i, 1f);

            yield return new WaitForSeconds(0.3f);
        }

        // 4) 이번 파편이 천천히 떠오른다.
        if (newIndex >= 0 && newIndex < slotCount)
        {
            if (appearSound != null) audioSource.PlayOneShot(appearSound);

            float t = 0f;
            while (t < newFragmentAppearDuration)
            {
                t += Time.deltaTime;
                SetSlotScale(newIndex, Ease(t / newFragmentAppearDuration));
                yield return null;
            }
            SetSlotScale(newIndex, 1f);
        }

        Debug.Log($"[MemoryFragmentScreen] 기억 파편 {newIndex + 1}/{slotCount} 표시.");
    }

    /// <summary>
    /// 새 파편을 클릭할 때까지 기다렸다가, 화면을 걷어냅니다.
    /// </summary>
    public IEnumerator WaitForClickAndHide()
    {
        if (!showing) yield break;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorOverridden = true;

        interactive = true;
        clicked = false;

        if (!string.IsNullOrEmpty(clickHint))
        {
            hintText.text = clickHint;
            hintText.gameObject.SetActive(true);
        }

        while (!clicked)
            yield return null;

        interactive = false;
        hintText.gameObject.SetActive(false);

        // 커서는 누른 즉시 돌려놓는다. 화면이 걷히는 동안 기억(탐색 구간)이 커서를 잠글 수 있어서,
        // 다 걷힌 뒤에 되돌리면 그 잠금을 풀어버린다.
        RestoreCursor();

        if (clickSound != null) audioSource.PlayOneShot(clickSound);
        Debug.Log($"[MemoryFragmentScreen] 기억 파편 {newIndex + 1} 선택. 기억으로 들어갑니다.");

        // 클릭한 파편이 한 번 크게 번지고, 검은 막이 덮은 뒤 무대를 치우고 막을 걷는다.
        float from = currentScale[newIndex];
        float t = 0f;
        const float punch = 0.35f;
        while (t < punch)
        {
            t += Time.deltaTime;
            SetSlotScale(newIndex, Mathf.Lerp(from, hoverScale * 1.35f, t / punch));
            yield return null;
        }

        yield return FadeStrip(1f, 0f, fadeOutDuration * 0.5f);
        DestroyStage();

        HideImmediate();
    }

    public void HideImmediate()
    {
        interactive = false;
        showing = false;
        DestroyStage();

        if (canvasObject != null)
            canvasObject.SetActive(false);

        RestoreCursor();
    }

    private void RestoreCursor()
    {
        if (!cursorOverridden) return;
        cursorOverridden = false;
        Cursor.lockState = prevLockState;
        Cursor.visible = prevCursorVisible;
    }

    private void OnDestroy()
    {
        DestroyStage();
    }

    private void Update()
    {
        if (!showing || pivots == null || stageCamera == null) return;

        // 아이템은 제자리에서 천천히 돈다.
        if (spinSpeed != 0f)
        {
            for (int i = 0; i < visibleCount; i++)
                if (spinners[i] != null)
                    spinners[i].Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
        }

        // 떠 있는 파편만 마우스에 반응한다. (화면에 비친 크기 기준 원 판정)
        // 무대 카메라는 위쪽 띠 텍스처에 그리므로, 마우스 위치를 텍스처 좌표로 바꿔서 비교한다.
        int over = -1;
        Vector2 mouse = MouseToStage();
        for (int i = 0; i < visibleCount; i++)
        {
            if (pivots[i] == null || currentScale[i] < 0.05f) continue;

            Vector3 center = pivots[i].position;
            Vector3 c = stageCamera.WorldToScreenPoint(center);
            Vector3 e = stageCamera.WorldToScreenPoint(center + stageCamera.transform.right * (itemSize * 0.6f * currentScale[i]));
            float radius = Mathf.Abs(e.x - c.x);

            if (((Vector2)c - mouse).sqrMagnitude <= radius * radius)
            {
                over = i;
                break;
            }
        }

        if (over != hovered)
        {
            hovered = over;
            if (hovered >= 0 && interactive && hoverSound != null)
                audioSource.PlayOneShot(hoverSound, 0.6f);
        }

        if (clicked) return; // 클릭 뒤의 크기는 연출이 맡는다.

        for (int i = 0; i < visibleCount; i++)
        {
            // 떠오르는 연출 중에는 그 파편의 크기를 연출이 맡는다.
            if (!interactive && (i == newIndex || currentScale[i] < 0.99f)) continue;

            float target = (i == hovered) ? hoverScale : 1f;

            // 새 파편은 클릭을 기다리는 동안 살짝 숨 쉬듯 뛴다.
            if (interactive && i == newIndex && i != hovered)
                target = 1f + Mathf.Sin(Time.time * 3f) * 0.04f;

            SetSlotScale(i, Mathf.Lerp(currentScale[i], target, 1f - Mathf.Exp(-scaleSpeed * Time.deltaTime)));
        }

        if (interactive && hovered == newIndex && Input.GetMouseButtonDown(0))
            clicked = true;
    }

    // ─────────────────────────────────────────────────────────────
    // 3D 무대
    // ─────────────────────────────────────────────────────────────

    // 화면의 마우스 위치 → 무대 텍스처 픽셀 좌표 (띠 밖이면 아주 먼 곳)
    private Vector2 MouseToStage()
    {
        if (strip == null || stageTexture == null) return new Vector2(-9999f, -9999f);
        RectTransform rt = strip.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, null, out Vector2 local))
            return new Vector2(-9999f, -9999f);
        Rect r = rt.rect;
        float u = (local.x - r.xMin) / r.width;
        float v = (local.y - r.yMin) / r.height;
        if (u < 0f || u > 1f || v < 0f || v > 1f) return new Vector2(-9999f, -9999f);
        return new Vector2(u * stageTexture.width, v * stageTexture.height);
    }

    private void BuildStage(int count)
    {
        DestroyStage();

        stageRoot = new GameObject("MemoryFragmentStage");
        stageRoot.transform.position = stageOrigin;

        // 카메라: 아이템 줄 전체가 화면 가로에 들어오도록 거리를 잡는다.
        var camGo = new GameObject("FragmentCamera");
        camGo.transform.SetParent(stageRoot.transform, false);
        stageCamera = camGo.AddComponent<Camera>();
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        stageCamera.allowHDR = false;
        stageCamera.allowMSAA = false;

        int texW = 1920, texH = Mathf.RoundToInt(1080f * stripHeight);
        stageTexture = new RenderTexture(texW, texH, 24, RenderTextureFormat.ARGB32) { name = "MemoryFragmentStrip" };
        stageTexture.Create();
        stageCamera.targetTexture = stageTexture;
        if (strip != null) strip.texture = stageTexture;
        stageCamera.fieldOfView = cameraFov;
        stageCamera.nearClipPlane = 0.05f;
        stageCamera.farClipPlane = 50f;
        stageCamera.depth = 100f; // 메인 카메라보다 나중에 그려서 화면을 덮는다.

        var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
        urp.renderPostProcessing = false;
        urp.renderType = CameraRenderType.Base;

        float rowWidth = (slotCount - 1) * itemSpacing + itemSize + screenMargin * 2f;
        float aspect = (float)stageTexture.width / stageTexture.height;
        float halfFovTan = Mathf.Tan(cameraFov * 0.5f * Mathf.Deg2Rad);
        float distance = Mathf.Max((rowWidth * 0.5f) / (halfFovTan * aspect), itemSize * 3f);
        camGo.transform.localPosition = new Vector3(0f, 0f, -distance);
        camGo.transform.localRotation = Quaternion.identity;

        // 조명: 카메라 쪽 위에서 비추는 불빛
        var lightGo = new GameObject("FragmentLight");
        lightGo.transform.SetParent(stageRoot.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, distance * 0.35f, -distance * 0.6f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = distance * 3f;
        light.color = lightColor;
        light.intensity = lightIntensity;
        light.shadows = LightShadows.None;

        pivots = new Transform[slotCount];
        spinners = new Transform[slotCount];
        currentScale = new float[slotCount];

        float startX = -(slotCount - 1) * itemSpacing * 0.5f;
        for (int i = 0; i < slotCount; i++)
        {
            var pivot = new GameObject($"Slot_{i + 1}").transform;
            pivot.SetParent(stageRoot.transform, false);
            pivot.localPosition = new Vector3(startX + i * itemSpacing, 0f, 0f);
            pivot.localRotation = Quaternion.Euler(displayTilt);
            pivots[i] = pivot;

            var spinner = new GameObject("Spin").transform;
            spinner.SetParent(pivot, false);
            spinners[i] = spinner;

            SetSlotScale(i, 0f);

            if (i < count)
                SpawnDisplayItem(i, spinner);
        }
    }

    // 프리팹을 "보여주기용"으로만 복제한다. 스크립트/콜라이더는 떼고, 크기와 중심을 맞춘다.
    private void SpawnDisplayItem(int index, Transform spinner)
    {
        GameObject prefab = (fragmentPrefabs != null && index < fragmentPrefabs.Count) ? fragmentPrefabs[index] : null;
        if (prefab == null)
        {
            Debug.LogWarning($"[MemoryFragmentScreen] {index + 1}번 파편에 쓸 아이템 프리팹이 없습니다.");
            return;
        }

        // 꺼진 부모 아래에서 복제하면 스크립트의 Awake/OnEnable이 돌지 않는다.
        var holder = new GameObject($"Item_{index + 1}");
        holder.SetActive(false);
        holder.transform.SetParent(spinner, false);

        GameObject item = Instantiate(prefab, holder.transform);
        item.transform.localPosition = Vector3.zero;
        item.SetActive(true);

        foreach (var mb in item.GetComponentsInChildren<MonoBehaviour>(true))
            if (mb != null) DestroyImmediate(mb);
        foreach (var col in item.GetComponentsInChildren<Collider>(true))
            if (col != null) DestroyImmediate(col);
        foreach (var rb in item.GetComponentsInChildren<Rigidbody>(true))
            if (rb != null) DestroyImmediate(rb);

        holder.SetActive(true);

        // 크기를 재는 동안은 칸 배율(떠오르기 전 0)의 영향을 받지 않게 잠깐 1로 둔다.
        Transform pivot = spinner.parent;
        Vector3 savedScale = pivot.localScale;
        pivot.localScale = Vector3.one;

        // 가장 긴 변이 itemSize가 되도록 맞추고, 중심을 칸 한가운데로 옮긴다.
        Bounds b = GetBounds(item);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f)
            item.transform.localScale *= itemSize / longest;

        b = GetBounds(item);
        item.transform.position += spinner.position - b.center;

        pivot.localScale = savedScale;
    }

    private static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void SetSlotScale(int i, float s)
    {
        if (pivots == null || i < 0 || i >= pivots.Length || pivots[i] == null) return;
        currentScale[i] = s;
        pivots[i].localScale = Vector3.one * Mathf.Max(0.0001f, s);
    }

    private void DestroyStage()
    {
        if (strip != null) strip.texture = null;
        if (stageCamera != null) stageCamera.targetTexture = null;
        if (stageTexture != null)
        {
            stageTexture.Release();
            Destroy(stageTexture);
            stageTexture = null;
        }
        if (stageRoot != null)
            Destroy(stageRoot);
        stageRoot = null;
        stageCamera = null;
        pivots = null;
        spinners = null;
    }

    private static float Ease(float k)
    {
        k = Mathf.Clamp01(k);
        return 1f - Mathf.Pow(1f - k, 3f);
    }

    // ─────────────────────────────────────────────────────────────
    // UI
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FadeStrip(float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetStripAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetStripAlpha(to);
    }

    private void SetStripAlpha(float a)
    {
        if (strip != null) strip.color = new Color(1f, 1f, 1f, a);
    }

    private IEnumerator FadeBlack(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetBlack(to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            SetBlack(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetBlack(to);
    }

    private void SetBlack(float a)
    {
        if (blackImage != null)
            blackImage.color = new Color(0f, 0f, 0f, a);
    }

    private void BuildUI()
    {
        if (canvasObject != null) return;

        canvasObject = new GameObject("MemoryFragmentCanvas");
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

        // 검은 막 (눈 감기 / 장면 전환용)
        var bg = new GameObject("Black", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasObject.transform, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        blackImage = bg.GetComponent<Image>();
        blackImage.raycastTarget = false;
        SetBlack(0f);

        // 위쪽 띠: 무대 카메라가 찍은 파편들
        var stripGo = new GameObject("FragmentStrip", typeof(RectTransform), typeof(RawImage));
        stripGo.transform.SetParent(canvasObject.transform, false);
        var stripRt = (RectTransform)stripGo.transform;
        stripRt.anchorMin = new Vector2(0f, 1f - stripHeight);
        stripRt.anchorMax = new Vector2(1f, 1f);
        stripRt.offsetMin = Vector2.zero;
        stripRt.offsetMax = Vector2.zero;
        strip = stripGo.GetComponent<RawImage>();
        strip.raycastTarget = false;
        SetStripAlpha(0f);

        // 안내 문구
        var hint = new GameObject("Hint", typeof(RectTransform), typeof(Text));
        hint.transform.SetParent(canvasObject.transform, false);
        var hintRt = (RectTransform)hint.transform;
        hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 1f - stripHeight);
        hintRt.sizeDelta = new Vector2(1200f, 60f);
        hintRt.anchoredPosition = new Vector2(0f, -20f);
        hintText = hint.GetComponent<Text>();
        hintText.font = UIFontUtil.Resolve(font);
        hintText.fontSize = 26;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(1f, 1f, 1f, 0.75f);
        hintText.raycastTarget = false;
        hint.SetActive(false);

        canvasObject.SetActive(false);
    }
}
