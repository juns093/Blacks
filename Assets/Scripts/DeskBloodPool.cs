using System.Collections;
using UnityEngine;

// Who가 죽었을 때 책상 위로 피가 번져 나가는 연출.
//
// 프로젝트에 피 텍스처가 없어서 런타임에 직접 그립니다.
// 가장자리가 울퉁불퉁한 웅덩이 모양을 만든 뒤, SpriteRenderer를 책상에 눕혀 놓고
// 크기를 0에서부터 키우면서 알파를 올려 "번져 나가는" 것처럼 보이게 합니다.
//
// SpriteRenderer를 쓰는 이유는 셰이더를 직접 찾지 않아도 되기 때문입니다.
// (URP에서 런타임에 투명 머티리얼을 만들면 설정이 까다로워서 깨지기 쉽습니다)
[RequireComponent(typeof(SpriteRenderer))]
public class DeskBloodPool : MonoBehaviour
{
    [Header("웅덩이 모양")]
    [Tooltip("웅덩이 텍스처 한 변의 픽셀 수")]
    [Range(128, 1024)]
    [SerializeField] private int resolution = 512;

    [Tooltip("웅덩이 색")]
    [SerializeField] private Color bloodColor = new Color(0.32f, 0.01f, 0.02f, 1f);

    [Tooltip("가장자리가 얼마나 울퉁불퉁할지 (0이면 정확한 원)")]
    [Range(0f, 0.6f)]
    [SerializeField] private float edgeWobble = 0.28f;

    [Tooltip("웅덩이 주변에 튀어 있을 작은 방울 개수")]
    [SerializeField] private Vector2Int dropletCount = new Vector2Int(10, 22);

    [Header("번지는 크기")]
    [Tooltip("다 번졌을 때의 크기 (월드 단위, 지름). 책상 크기에 맞춰 조절하세요.")]
    [SerializeField] private float finalSize = 2.2f;

    [Tooltip("번지기 시작할 때의 크기 비율 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float startSizeRatio = 0.08f;

    [Header("타이밍")]
    [Tooltip("피가 다 번지는 데 걸리는 시간(초)")]
    [SerializeField] private float spreadDuration = 3.5f;

    [Tooltip("번지는 속도 곡선. 처음에 훅 퍼졌다가 점점 느려지면 자연스럽습니다.")]
    [SerializeField]
    private AnimationCurve spreadCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 3f, 3f),
        new Keyframe(1f, 1f, 0f, 0f));

    [Tooltip("불투명해지는 데 걸리는 시간(초). 크기보다 빨리 진해지면 더 걸쭉해 보입니다.")]
    [SerializeField] private float alphaInDuration = 0.6f;

    [Tooltip("웅덩이의 최대 불투명도")]
    [Range(0f, 1f)]
    [SerializeField] private float maxAlpha = 1f;

    [Header("시작 상태")]
    [Tooltip("켜면 씬이 시작될 때 자동으로 숨깁니다. (평소에는 안 보여야 하므로 켜두세요)")]
    [SerializeField] private bool hideOnStart = true;

    private SpriteRenderer sr;
    private Coroutine routine;

    public bool IsPlaying => routine != null;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();

        if (sr.sprite == null)
            sr.sprite = GeneratePoolSprite();

        if (hideOnStart)
        {
            SetAlpha(0f);
            transform.localScale = Vector3.one * (finalSize * startSizeRatio);
            sr.enabled = false;
        }
    }

    private Sprite GeneratePoolSprite()
    {
        int res = resolution;
        var px = new Color32[res * res];

        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.36f;

        byte r = (byte)(bloodColor.r * 255f);
        byte g = (byte)(bloodColor.g * 255f);
        byte b = (byte)(bloodColor.b * 255f);

        // 가장자리를 흔들기 위한 위상값
        float phaseA = Random.Range(0f, Mathf.PI * 2f);
        float phaseB = Random.Range(0f, Mathf.PI * 2f);
        float phaseC = Random.Range(0f, Mathf.PI * 2f);

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);

                // 각도에 따라 반지름을 흔들어서 자연스러운 웅덩이 윤곽을 만든다
                float wobble = 1f
                             + edgeWobble * 0.6f * Mathf.Sin(angle * 2f + phaseA)
                             + edgeWobble * 0.3f * Mathf.Sin(angle * 5f + phaseB)
                             + edgeWobble * 0.15f * Mathf.Sin(angle * 9f + phaseC);
                float edge = radius * Mathf.Max(0.2f, wobble);

                if (dist > edge) continue;

                // 가운데는 진하고 가장자리로 갈수록 살짝 옅어지게
                float t = 1f - (dist / edge);
                float a = Mathf.Clamp01(t * 4f);

                px[y * res + x] = new Color32(r, g, b, (byte)(a * 255f));
            }
        }

        // 웅덩이 주변에 튄 방울들
        int drops = Random.Range(dropletCount.x, dropletCount.y + 1);
        for (int i = 0; i < drops; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float d = radius * Random.Range(1.02f, 1.35f);
            Vector2 dc = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * d;
            float dr = Random.Range(res * 0.006f, res * 0.024f);
            DrawDot(px, res, dc, dr, r, g, b);
        }

        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels32(px);
        tex.Apply(false);

        // pixelsPerUnit을 해상도와 같게 두면 스프라이트가 1x1 월드 단위가 되어
        // localScale이 곧 지름이 된다. (크기 계산이 단순해짐)
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
    }

    private void DrawDot(Color32[] px, int res, Vector2 c, float radius, byte r, byte g, byte b)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(c.x - radius));
        int maxX = Mathf.Min(res - 1, Mathf.CeilToInt(c.x + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(c.y - radius));
        int maxY = Mathf.Min(res - 1, Mathf.CeilToInt(c.y + radius));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), c);
                if (dist > radius) continue;

                float a = Mathf.Clamp01((1f - dist / radius) * 3f);
                int idx = y * res + x;
                byte na = (byte)(a * 255f);
                if (na > px[idx].a)
                    px[idx] = new Color32(r, g, b, na);
            }
        }
    }

    /// <summary>
    /// 피가 번지기 시작합니다. Who가 죽는 순간 호출하세요.
    /// </summary>
    public void Play()
    {
        if (sr == null) return;

        if (routine != null)
            StopCoroutine(routine);

        routine = StartCoroutine(SpreadRoutine());
    }

    /// <summary>
    /// 웅덩이를 즉시 지웁니다.
    /// </summary>
    public void ClearImmediately()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (sr != null)
        {
            SetAlpha(0f);
            sr.enabled = false;
        }

        transform.localScale = Vector3.one * (finalSize * startSizeRatio);
    }

    private IEnumerator SpreadRoutine()
    {
        Debug.Log($"[DeskBloodPool] 책상에 피가 번지기 시작합니다 ({spreadDuration:0.00}초)");

        sr.enabled = true;

        float startSize = finalSize * startSizeRatio;
        float elapsed = 0f;

        while (elapsed < spreadDuration)
        {
            float t = Mathf.Clamp01(elapsed / spreadDuration);

            float size = Mathf.Lerp(startSize, finalSize, spreadCurve.Evaluate(t));
            transform.localScale = new Vector3(size, size, size);

            float a = alphaInDuration <= 0f
                ? maxAlpha
                : Mathf.Lerp(0f, maxAlpha, Mathf.Clamp01(elapsed / alphaInDuration));
            SetAlpha(a);

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localScale = Vector3.one * finalSize;
        SetAlpha(maxAlpha);

        routine = null;
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
