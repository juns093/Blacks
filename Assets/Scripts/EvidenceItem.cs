using UnityEngine;

// 회상 탐색 구간에서 찾아서 "조작"하는 증거.
//
// 가까이 가서 바라보면 [E] 살펴보기 → 문서가 크게 펼쳐지고, [F]로 조작합니다.
//  - Rewrite : 원래 내용을 검게 지우고 조작한 내용으로 다시 적습니다. (물건은 남음)
//  - Destroy : 찢어서 없애버립니다. (물건이 사라짐)
// 조작까지 끝내야 "처리한 증거"로 칩니다. 진행은 FlashbackFreeRoamSegment가 맡습니다.
//
// 모델은 아무거나 붙여도 됩니다. 이 스크립트는 거리/시선으로만 판정합니다. (콜라이더 불필요)
public class EvidenceItem : MonoBehaviour
{
    public enum TamperStyle
    {
        Rewrite,
        Destroy
    }

    [Header("문서 내용")]
    [SerializeField] private string title = "증거";

    [TextArea(4, 12)]
    [SerializeField] private string originalText = "원래 기록";

    [Header("조작")]
    [SerializeField] private TamperStyle tamperStyle = TamperStyle.Rewrite;

    [Tooltip("Rewrite: 조작한 뒤의 내용 / Destroy: 없앤 뒤 남길 한 줄")]
    [TextArea(4, 12)]
    [SerializeField] private string tamperedText = "조작한 기록";

    [Tooltip("F키 안내에 쓸 행동 이름. 비워두면 방식에 맞게 자동으로 정합니다.")]
    [SerializeField] private string tamperActionLabel = "";

    [Tooltip("조작한 직후 주인공이 속으로 하는 말 (선택)")]
    [TextArea] [SerializeField] private string afterTamperThought = "";

    [Header("판정")]
    [Tooltip("이 거리(수평, m) 안에서 바라보면 살펴볼 수 있습니다.")]
    [SerializeField] private float interactRadius = 2.2f;

    [Tooltip("이 각도 안으로 바라봐야 합니다. (아주 가까우면 각도 무시)")]
    [SerializeField] private float lookAngle = 40f;

    [Header("눈에 띄게 하기")]
    [Tooltip("아직 조작하지 않았을 때만 켜 둘 불빛 등 (선택). 비워두면 자식 Light를 자동으로 씁니다.")]
    [SerializeField] private GameObject highlight;

    public string Title => title;
    public string OriginalText => originalText;
    public string TamperedText => tamperedText;
    public TamperStyle Style => tamperStyle;
    public string AfterTamperThought => afterTamperThought;
    public bool IsTampered { get; private set; }

    public string ActionLabel
    {
        get
        {
            if (!string.IsNullOrEmpty(tamperActionLabel)) return tamperActionLabel;
            return tamperStyle == TamperStyle.Rewrite ? "기록을 고친다" : "없애버린다";
        }
    }

    private void Awake()
    {
        if (highlight == null)
        {
            var light = GetComponentInChildren<Light>(true);
            if (light != null) highlight = light.gameObject;
        }
    }

    /// <summary>대본 파일(GameSceneStoryDialogues)에서 문서 내용을 채울 때 씁니다. null이면 그대로 둡니다.</summary>
    public void SetTexts(string newTitle, string original, string tampered, string thought)
    {
        if (newTitle != null) title = newTitle;
        if (original != null) originalText = original;
        if (tampered != null) tamperedText = tampered;
        if (thought != null) afterTamperThought = thought;
    }

    /// <summary>탐색 구간이 시작될 때 호출됩니다. 다시 보이게 하고 조작 전 상태로 되돌립니다.</summary>
    public void Arm()
    {
        IsTampered = false;
        gameObject.SetActive(true);
        if (highlight != null) highlight.SetActive(true);
    }

    public void Disarm()
    {
        gameObject.SetActive(false);
    }

    /// <summary>조작 연출이 끝났을 때 EvidenceViewer가 호출합니다.</summary>
    public void MarkTampered()
    {
        IsTampered = true;
        if (highlight != null) highlight.SetActive(false);
        Debug.Log($"[EvidenceItem] '{title}' 조작 완료 ({tamperStyle}).");
    }

    /// <summary>문서를 닫은 뒤 호출됩니다. 없애는 방식이면 여기서 사라집니다.</summary>
    public void OnViewerClosed()
    {
        if (IsTampered && tamperStyle == TamperStyle.Destroy)
            gameObject.SetActive(false);
    }

    /// <summary>플레이어 머리 기준으로 지금 살펴볼 수 있는지. 가까울수록 점수가 낮습니다(음수면 불가).</summary>
    public float GetFocusScore(Transform head)
    {
        if (!gameObject.activeInHierarchy || head == null) return -1f;

        Vector3 target = GetCenter();
        Vector3 flat = target - head.position;
        flat.y = 0f;
        float dist = flat.magnitude;
        if (dist > interactRadius) return -1f;

        if (dist > 0.9f)
        {
            Vector3 dir = (target - head.position).normalized;
            if (Vector3.Angle(head.forward, dir) > lookAngle) return -1f;
        }

        return dist;
    }

    private Vector3 GetCenter()
    {
        var r = GetComponentInChildren<Renderer>();
        return r != null ? r.bounds.center : transform.position;
    }
}
