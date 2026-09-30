using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 슬라이더를 "빈칸 5개" 모양으로 바꿔 보여준다. 한 번 누를 때마다 반 칸씩 찬다. (0 ~ 10단계)
//  - 칸의 왼쪽 절반을 누르면 반 칸, 오른쪽 절반을 누르면 한 칸까지 채운다.
//  - 첫 칸 반만 찬 상태에서 첫 칸 왼쪽을 다시 누르면 0으로 비운다.
//  - 값 자체는 원래 Slider에 넣는다. 그래서 저장/틱 소리 등 기존 연결(onValueChanged)이 그대로 동작한다.
[RequireComponent(typeof(Slider))]
public class SegmentMeter : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private int boxCount = 5;
    [SerializeField] private Vector2 boxSize = new Vector2(64f, 30f);
    [SerializeField] private float boxGap = 14f;
    [SerializeField] private float frameThickness = 3f;
    [SerializeField] private float fillInset = 5f;

    [SerializeField] private Color frameColor = new Color(0.679f, 0.679f, 0.679f, 1f);
    [SerializeField] private Color emptyColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    [SerializeField] private Color fillColor = new Color(0.679f, 0.679f, 0.679f, 1f);

    private Slider slider;
    private RectTransform[] fills;
    private int shownSteps = -1;

    /// <summary>반 칸 = 한 단계. 전체 단계 수.</summary>
    public int MaxSteps => boxCount * 2;

    /// <summary>한 단계에 해당하는 슬라이더 값 (←→ 키 조절용)</summary>
    public float StepValue => slider != null ? (slider.maxValue - slider.minValue) / MaxSteps : 0.1f;

    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.interactable = false; // 끌어서 조절하는 대신 칸을 눌러서 조절한다.

        // 원래 슬라이더 그림(배경/채움/손잡이)은 숨긴다. 배경은 투명하게 남겨 클릭을 받는다.
        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject == gameObject) { g.color = new Color(0f, 0f, 0f, 0f); g.raycastTarget = true; }
            else g.enabled = false;
        }

        Build();
        Refresh(true);
    }

    private void OnEnable()
    {
        if (slider != null) Refresh(true);
    }

    private void Update()
    {
        Refresh(false);
    }

    private int CurrentSteps()
    {
        float t = Mathf.InverseLerp(slider.minValue, slider.maxValue, slider.value);
        return Mathf.Clamp(Mathf.RoundToInt(t * MaxSteps), 0, MaxSteps);
    }

    private void Refresh(bool force)
    {
        int steps = CurrentSteps();
        if (!force && steps == shownSteps) return;
        shownSteps = steps;

        float innerWidth = boxSize.x - fillInset * 2f;
        for (int i = 0; i < fills.Length; i++)
        {
            int filledHalves = Mathf.Clamp(steps - i * 2, 0, 2);
            fills[i].sizeDelta = new Vector2(innerWidth * filledHalves * 0.5f, boxSize.y - fillInset * 2f);
        }
    }

    /// <summary>단계(0 ~ MaxSteps)로 값을 정한다.</summary>
    public void SetSteps(int steps)
    {
        steps = Mathf.Clamp(steps, 0, MaxSteps);
        slider.value = Mathf.Lerp(slider.minValue, slider.maxValue, (float)steps / MaxSteps);
        Refresh(true);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        var rt = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        // 왼쪽 끝 기준 x
        float x = local.x - rt.rect.xMin;
        float pitch = boxSize.x + boxGap;
        int box = Mathf.Clamp(Mathf.FloorToInt(x / pitch), 0, boxCount - 1);
        float inBox = x - box * pitch;
        int target = box * 2 + (inBox < boxSize.x * 0.5f ? 1 : 2);

        // 첫 칸 반만 찬 상태에서 같은 자리를 또 누르면 비운다.
        if (target == 1 && CurrentSteps() == 1) target = 0;

        SetSteps(target);
    }

    private void Build()
    {
        var rt = (RectTransform)transform;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, boxSize.y);

        fills = new RectTransform[boxCount];
        for (int i = 0; i < boxCount; i++)
        {
            // 테두리
            var frame = MakeImage($"Box_{i + 1}", transform, frameColor);
            frame.rectTransform.anchorMin = frame.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0f, 0.5f);
            frame.rectTransform.sizeDelta = boxSize;
            frame.rectTransform.anchoredPosition = new Vector2(i * (boxSize.x + boxGap), 0f);

            // 빈 안쪽
            var inner = MakeImage("Empty", frame.transform, emptyColor);
            inner.rectTransform.anchorMin = Vector2.zero;
            inner.rectTransform.anchorMax = Vector2.one;
            inner.rectTransform.offsetMin = new Vector2(frameThickness, frameThickness);
            inner.rectTransform.offsetMax = new Vector2(-frameThickness, -frameThickness);

            // 채움 (왼쪽부터 반 칸씩)
            var fill = MakeImage("Fill", frame.transform, fillColor);
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = new Vector2(fillInset, 0f);
            fills[i] = fill.rectTransform;
        }
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }
}
