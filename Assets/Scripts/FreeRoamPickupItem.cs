using UnityEngine;

// 자유 이동 구간에서 걸어가서 줍는 아이템.
// 콜라이더/태그 없이 거리로 판정합니다. (CharacterController와 트리거 조합은 설정을 타서 빗나가기 쉽다)
// 눈에 띄도록 제자리에서 천천히 돌고 위아래로 살짝 떠 있습니다.
public class FreeRoamPickupItem : MonoBehaviour
{
    [Tooltip("이 거리(수평) 안으로 들어오면 줍습니다.")]
    [SerializeField] private float pickupRadius = 1.3f;

    [Tooltip("주울 때 재생할 효과음 (선택)")]
    [SerializeField] private AudioClip pickupSound;

    [Tooltip("주웠을 때 화면에 잠깐 띄울 문구 (단서 내용 등). 비워두면 띄우지 않습니다.")]
    [TextArea] [SerializeField] private string pickupMessage;

    public string PickupMessage => pickupMessage;

    [Header("기억 노트에 적을 단서 (선택)")]
    [SerializeField] private string clueId = "";
    [SerializeField] private string clueTitle = "";
    [TextArea(3, 8)] [SerializeField] private string clueText = "";
    [SerializeField] private int memoryIndex = 0;

    public string ClueId => string.IsNullOrEmpty(clueId) ? clueTitle : clueId;

    public void SetClue(string id, string title, string text, int memory)
    {
        clueId = id; clueTitle = title; clueText = text; memoryIndex = memory;
    }

    /// <summary>대본 파일(GameSceneStoryDialogues)에서 주웠을 때 문구를 채울 때 씁니다.</summary>
    public void SetPickupMessage(string message) => pickupMessage = message;

    [Header("눈에 띄게 하기")]
    [SerializeField] private float spinSpeed = 70f;
    [SerializeField] private float bobHeight = 0.06f;
    [SerializeField] private float bobSpeed = 2.2f;

    /// <summary>주웠을 때 호출됩니다. FlashbackFreeRoamSegment가 구독합니다.</summary>
    public event System.Action OnPicked;

    private Transform player;
    private bool picked = false;
    private Vector3 basePosition;
    private bool hasBase = false;

    public bool IsPicked => picked;

    // 씬에는 꺼진 채로 배치해 두세요. Arm()이 켜줍니다.
    // (여기서 SetActive(false)를 하면, 처음 켜지는 순간 Awake가 돌면서 도로 꺼져 버린다)
    private void Awake()
    {
        CacheBase();
    }

    private void CacheBase()
    {
        if (hasBase) return;
        basePosition = transform.position;
        hasBase = true;
    }

    /// <summary>
    /// 탐색 구간이 시작될 때 호출합니다. 다시 보이게 하고 주울 수 있는 상태로 되돌립니다.
    /// (루프를 돌아 다시 오면 또 주울 수 있어야 한다)
    /// </summary>
    public void Arm(Transform playerRoot)
    {
        CacheBase();
        player = playerRoot;
        picked = false;
        transform.position = basePosition;
        gameObject.SetActive(true);
    }

    public void Disarm()
    {
        player = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.position = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);

        if (picked || player == null) return;

        Vector3 a = player.position, b = basePosition;
        float vertical = Mathf.Abs(a.y - b.y);
        a.y = 0f; b.y = 0f;

        if (vertical < 2.5f && Vector3.Distance(a, b) <= pickupRadius)
            Pick();
    }

    private void Pick()
    {
        picked = true;

        if (pickupSound != null)
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);

        Debug.Log($"[FreeRoamPickupItem] '{gameObject.name}' 획득.");
        if (!string.IsNullOrEmpty(clueTitle))
            MemoryNotebook.Add(clueId, clueTitle, clueText, memoryIndex);
        OnPicked?.Invoke();

        gameObject.SetActive(false);
    }
}
