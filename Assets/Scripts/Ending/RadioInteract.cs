using UnityEngine;

// 라디오 오브젝트 상호작용 스크립트
// - DoorCrosshairInteractor가 크로스헤어로 라디오를 감지하면 TryInteract()를 호출해서 E키로 재생/정지를 토글합니다.
// - 재생 중일 때는 매 프레임 리스너(플레이어/카메라)와의 거리를 계산해서 AudioSource 볼륨을 자동으로 조절합니다.
//   (재생 상태는 크로스헤어가 라디오를 벗어나도 유지되고, 거리 계산도 계속 돌아갑니다)
// 이 스크립트는 라디오 오브젝트 자신에 붙입니다. Collider(Is Trigger 체크 X, 실제 충돌용)만 있으면 됩니다.
[RequireComponent(typeof(AudioSource))]
public class RadioInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Audio")]
    [Tooltip("라디오에서 재생할 음악 클립")]
    [SerializeField] private AudioClip radioClip;
    [Tooltip("재생에 사용할 AudioSource. 비워두면 이 오브젝트의 AudioSource를 자동으로 사용")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool loop = true;

    [Header("Distance Volume Settings")]
    [Tooltip("거리 계산 기준이 될 리스너(보통 플레이어 카메라). 비워두면 Camera.main을 자동 사용")]
    [SerializeField] private Transform listener;
    [Tooltip("이 거리 이내에서는 최대 볼륨으로 들림")]
    [SerializeField] private float minDistance = 1.5f;
    [Tooltip("이 거리를 넘어가면 소리가 0이 됨")]
    [SerializeField] private float maxDistance = 15f;
    [Tooltip("최대로 낼 수 있는 볼륨 (0~1)")]
    [Range(0f, 1f)][SerializeField] private float maxVolume = 1f;
    [Tooltip("거리에 따른 감쇠 곡선. X: 0(가까움)~1(멀음) 정규화된 거리, Y: 볼륨 배율(0~1). " +
             "기본값(선형) 대신 커스텀 커브로 자연스러운 감쇠를 줄 수 있습니다.")]
    [SerializeField] private AnimationCurve falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

    private bool isPlaying = false;

    void Awake()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        // 거리 기반 볼륨은 우리가 직접 계산할 것이므로,
        // Unity의 기본 3D 스페이셜 감쇠(spatialBlend/rolloff)는 꺼서 중복 계산을 방지합니다.
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.spatialBlend = 0f; // 우리가 볼륨을 직접 계산하므로 2D로 두고 재생
        audioSource.volume = 0f;

        if (radioClip != null)
            audioSource.clip = radioClip;
    }

    void Start()
    {
        if (listener == null && Camera.main != null)
            listener = Camera.main.transform;

        if (listener == null)
            Debug.LogWarning("[RadioInteract] listener가 지정되어 있지 않고 Camera.main도 찾을 수 없습니다. 인스펙터에서 지정해주세요.");

        if (radioClip == null)
            Debug.LogWarning("[RadioInteract] radioClip이 할당되어 있지 않습니다!");
    }

    void Update()
    {
        // 재생 중일 때만 매 프레임 거리 기반 볼륨을 갱신
        if (isPlaying)
            UpdateDistanceVolume();
    }

    // 크로스헤어(레이캐스트)가 이 라디오를 가리키고 있을 때, DoorCrosshairInteractor가 호출합니다.
    public void TryInteract()
    {
        if (Input.GetKeyDown(interactKey))
        {
            ToggleRadio();
        }
    }

    private void ToggleRadio()
    {
        if (audioSource.clip == null)
        {
            Debug.LogWarning("[RadioInteract] 재생할 클립이 없어 라디오를 켤 수 없습니다.");
            return;
        }

        isPlaying = !isPlaying;

        if (isPlaying)
        {
            audioSource.Play();
            UpdateDistanceVolume(); // 켜자마자 현재 거리 기준으로 볼륨 즉시 반영
            Debug.Log("[RadioInteract] 라디오 재생 시작");
        }
        else
        {
            audioSource.Pause();
            Debug.Log("[RadioInteract] 라디오 정지");
        }
    }

    private void UpdateDistanceVolume()
    {
        if (listener == null)
        {
            audioSource.volume = maxVolume;
            return;
        }

        float distance = Vector3.Distance(transform.position, listener.position);

        // minDistance 이내 = 최대 볼륨, maxDistance 이상 = 0
        float t = Mathf.InverseLerp(minDistance, maxDistance, distance);
        t = Mathf.Clamp01(t);

        float curveMultiplier = falloffCurve.Evaluate(t);
        audioSource.volume = maxVolume * curveMultiplier;
    }

    // 외부(다른 스크립트)에서 강제로 켜고 끄고 싶을 때 사용할 수 있는 public 메서드들
    public void PlayRadio()
    {
        if (!isPlaying) ToggleRadio();
    }

    public void StopRadio()
    {
        if (isPlaying) ToggleRadio();
    }
}