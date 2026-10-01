using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 목표 위치 표시 (화면 위 노란 마름모 + 거리).
//  - 목표가 화면 안에 있으면 그 위에, 화면 밖/뒤에 있으면 화면 가장자리에 붙어서 방향을 알려 준다.
//  - 아래에 "580 m"처럼 남은 거리를 띄운다. 아주 가까워지면 흐려진다.
// 탐색 구간(FlashbackFreeRoamSegment)이 매 프레임 Show(목표들)로 갱신하고, 끝나면 Hide().
public class WaypointHUD : MonoBehaviour
{
    [SerializeField] private Color color = new Color(1f, 0.84f, 0.15f, 1f);
    [SerializeField] private float iconSize = 46f;
    [Tooltip("월드 몇 유닛을 1m로 볼지 (플레이어 눈높이 2.2 ≒ 1.7m)")]
    [SerializeField] private float unitsPerMeter = 1.3f;
    [Tooltip("목표 지점보다 이만큼 위에 띄운다 (유닛)")]
    [SerializeField] private float heightOffset = 1.4f;
    [SerializeField] private float edgeMargin = 70f;
    [Tooltip("이 거리(유닛)보다 가까우면 흐려지며 사라진다")]
    [SerializeField] private float fadeNear = 2.5f;

    private static WaypointHUD instance;
    private static Sprite diamondSprite;

    private RectTransform canvasRect;
    private readonly List<Marker> markers = new List<Marker>();
    private int usedThisFrame;
    private int shownFrame = -1;

    private class Marker
    {
        public RectTransform root;
        public Image icon;
        public Text distance;
        public CanvasGroup group;
    }

    public static WaypointHUD Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("WaypointHUD").AddComponent<WaypointHUD>();
            return instance;
        }
    }

    /// <summary>이번 프레임에 보여 줄 목표들. (null인 것은 무시)</summary>
    public static void Show(IEnumerable<Transform> targets)
    {
        var hud = Instance;
        hud.BeginFrame();
        if (targets != null)
            foreach (var t in targets)
                if (t != null && t.gameObject.activeInHierarchy) hud.Place(t.position);
        hud.EndFrame();
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.BeginFrame();
        instance.EndFrame();
    }

    private void Awake()
    {
        instance = this;
        var canvasObject = UIBuild.OverlayCanvas("WaypointCanvas", transform, 300);
        canvasRect = (RectTransform)canvasObject.transform;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void BeginFrame()
    {
        // 같은 프레임에 여러 번 불려도 겹치지 않게
        if (shownFrame != Time.frameCount) { usedThisFrame = 0; shownFrame = Time.frameCount; }
    }

    private void EndFrame()
    {
        for (int i = usedThisFrame; i < markers.Count; i++)
            markers[i].root.gameObject.SetActive(false);
    }

    private void Place(Vector3 worldTarget)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 p = worldTarget + Vector3.up * heightOffset;
        Vector3 sp = cam.WorldToScreenPoint(p);
        float dist = Vector3.Distance(cam.transform.position, worldTarget);

        bool behind = sp.z < 0f;
        if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }

        float w = Screen.width, h = Screen.height;
        bool offScreen = behind || sp.x < edgeMargin || sp.x > w - edgeMargin || sp.y < edgeMargin || sp.y > h - edgeMargin;
        if (offScreen)
        {
            // 화면 가운데에서 목표 쪽으로 뻗은 선이 가장자리에 닿는 곳
            Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
            Vector2 d = new Vector2(sp.x, sp.y) - center;
            if (behind && d.sqrMagnitude < 1f) d = Vector2.down;
            float sx = (w * 0.5f - edgeMargin) / Mathf.Max(0.001f, Mathf.Abs(d.x));
            float sy = (h * 0.5f - edgeMargin) / Mathf.Max(0.001f, Mathf.Abs(d.y));
            float s = Mathf.Min(sx, sy);
            if (!behind) s = Mathf.Min(s, 1f);
            Vector2 e = center + d * s;
            sp.x = e.x; sp.y = e.y;
        }

        Marker m = GetMarker(usedThisFrame++);
        m.root.gameObject.SetActive(true);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, new Vector2(sp.x, sp.y), null, out Vector2 local);
        m.root.anchoredPosition = local;

        int meters = Mathf.Max(0, Mathf.RoundToInt(dist / Mathf.Max(0.01f, unitsPerMeter)));
        m.distance.text = meters + " m";
        m.group.alpha = Mathf.Clamp01((dist - fadeNear * 0.5f) / Mathf.Max(0.01f, fadeNear * 0.5f));

        // 화면 밖일 때는 조금 작게
        m.root.localScale = Vector3.one * (offScreen ? 0.8f : 1f);
    }

    private Marker GetMarker(int i)
    {
        while (markers.Count <= i)
        {
            var go = new GameObject("Waypoint", typeof(RectTransform));
            go.transform.SetParent(canvasRect, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(iconSize, iconSize);
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var icon = UIBuild.Image("Icon", go.transform, color);
            icon.sprite = DiamondSprite();
            icon.raycastTarget = false;
            var irt = icon.rectTransform;
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.sizeDelta = new Vector2(iconSize, iconSize);
            irt.anchoredPosition = Vector2.zero;
            var shadow = icon.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
            shadow.effectDistance = new Vector2(2f, -2f);

            var text = UIBuild.Text("Distance", go.transform, UIFontUtil.Resolve(null), 28, Color.white, TextAnchor.UpperCenter);
            text.raycastTarget = false;
            var trt = text.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(160f, 30f);
            trt.anchoredPosition = new Vector2(0f, -iconSize * 0.5f - 18f);
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            markers.Add(new Marker { root = rt, icon = icon, distance = text, group = group });
        }
        return markers[i];
    }

    // 마름모 테두리 + 가운데 점 (흰색, Image 색으로 칠한다)
    private static Sprite DiamondSprite()
    {
        if (diamondSprite != null) return diamondSprite;
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // 마름모 거리 (|dx|+|dy|)
            float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
            float outer = c - 1f, inner = c - 8f;
            float ring = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
            float dot = Mathf.Clamp01(9f - d + 0.5f);   // 가운데 작은 마름모 점
            float a = Mathf.Max(ring, dot);
            px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        diamondSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return diamondSprite;
    }
}
