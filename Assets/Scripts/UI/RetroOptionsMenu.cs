using System;
using UnityEngine;
using UnityEngine.UI;

// 설정 화면을 옛날 게임 옵션 메뉴처럼 세로 목록으로 다룬다.
//  - 선택된 줄 왼쪽에 초록 삼각형 커서가 까딱거린다.
//  - ↑↓ / W·S : 줄 이동,  ←→ / A·D : 슬라이더 값 조절,  Enter / Space : 버튼 누르기
//  - 마우스를 줄(글자/슬라이더/버튼)에 올려도 선택된다.
// 실제 값 저장은 슬라이더/버튼에 연결된 SettingsPanelUI가 그대로 처리한다.
public class RetroOptionsMenu : MonoBehaviour
{
    [Serializable]
    public class Row
    {
        [Tooltip("커서가 붙을 글자")]
        public Text label;
        [Tooltip("←→로 조절할 슬라이더 (없으면 비워두기)")]
        public Slider slider;
        [Tooltip("←→ 한 번에 바뀌는 양")]
        public float step = 0.1f;
        [Tooltip("Enter로 누를 버튼 (없으면 비워두기)")]
        public Button button;
    }

    [SerializeField] private Row[] rows;

    [Header("커서")]
    [SerializeField] private Color cursorColor = new Color(0.679f, 0.679f, 0.679f, 1f);
    [SerializeField] private Vector2 cursorSize = new Vector2(24f, 28f);
    [SerializeField] private float cursorGap = 12f;
    [SerializeField] private float selectedScale = 1.06f;

    [SerializeField] private AudioClip moveSound;
    [Range(0f, 1f)][SerializeField] private float moveVolume = 0.6f;

    private RectTransform cursor;
    private int selected = 0;
    private AudioSource audioSource;
    private bool hoverSetup = false;

    private void OnEnable()
    {
        if (!hoverSetup) SetupHover();
        Select(0, false);
    }

    private void SetupHover()
    {
        hoverSetup = true;
        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;
            AddHover(rows[i].label != null ? rows[i].label.gameObject : null, index);
            AddHover(rows[i].slider != null ? rows[i].slider.gameObject : null, index);
            AddHover(rows[i].button != null ? rows[i].button.gameObject : null, index);
        }
    }

    private void AddHover(GameObject go, int index)
    {
        if (go == null) return;
        var hover = go.GetComponent<MenuCursorHover>();
        if (hover == null) hover = go.AddComponent<MenuCursorHover>();
        hover.Setup(i => Select(i), index);
    }

    private void Update()
    {
        if (rows == null || rows.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Select((selected + 1) % rows.Length);
        else if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Select((selected - 1 + rows.Length) % rows.Length);

        Row row = rows[selected];
        if (row.slider != null)
        {
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Nudge(row, 1);
            else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Nudge(row, -1);
        }

        if (row.button != null && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            row.button.onClick.Invoke();

        PlaceCursor();
    }

    private void Nudge(Row row, int dir)
    {
        // 빈칸 미터가 붙어 있으면 반 칸씩, 아니면 step만큼.
        // 슬라이더 값을 바꾸면 onValueChanged가 불려서 SettingsPanelUI가 저장/틱 소리를 처리한다.
        var meter = row.slider.GetComponent<SegmentMeter>();
        float step = meter != null ? meter.StepValue : row.step;
        row.slider.value = Mathf.Clamp(row.slider.value + step * dir, row.slider.minValue, row.slider.maxValue);
    }

    public void Select(int index, bool playSound = true)
    {
        if (rows == null || index < 0 || index >= rows.Length) return;
        if (index != selected && playSound) PlayMove();
        selected = index;

        for (int i = 0; i < rows.Length; i++)
            if (rows[i].label != null)
                rows[i].label.transform.localScale = Vector3.one * (i == selected ? selectedScale : 1f);

        PlaceCursor();
    }

    private void PlaceCursor()
    {
        if (cursor == null) cursor = CreateCursor();
        Text label = rows[selected].label;
        if (label == null) { cursor.gameObject.SetActive(false); return; }
        cursor.gameObject.SetActive(true);

        // 글자가 실제로 시작하는 왼쪽 끝
        var lrt = label.rectTransform;
        float textWidth = Mathf.Min(label.preferredWidth, lrt.rect.width);
        bool leftAligned = label.alignment == TextAnchor.MiddleLeft || label.alignment == TextAnchor.UpperLeft || label.alignment == TextAnchor.LowerLeft;
        float alignOffset = leftAligned ? 0f : (lrt.rect.width - textWidth) * 0.5f;
        Vector3 leftWorld = lrt.TransformPoint(new Vector3(lrt.rect.xMin + alignOffset, lrt.rect.center.y, 0f));
        Vector3 local = cursor.parent.InverseTransformPoint(leftWorld);

        float bob = Mathf.Sin(Time.unscaledTime * 6f) * 3f;
        cursor.localPosition = new Vector3(local.x - cursorGap - cursorSize.x * 0.5f + bob, local.y, 0f);
    }

    private RectTransform CreateCursor()
    {
        var go = new GameObject("OptionsCursor", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = cursorSize;
        var img = go.GetComponent<Image>();
        img.sprite = MakeTriangleSprite();
        img.color = cursorColor;
        img.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);
        return rt;
    }

    // 오른쪽을 가리키는 삼각형
    private static Sprite MakeTriangleSprite()
    {
        const int w = 32, h = 36;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = Mathf.Abs((y + 0.5f) - h * 0.5f) / (h * 0.5f);
            int maxX = Mathf.RoundToInt((1f - t) * w);
            for (int x = 0; x < w; x++)
                px[y * w + x] = x < maxX ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
    }

    private void PlayMove()
    {
        if (moveSound == null) return;
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(moveSound, moveVolume);
    }
}
