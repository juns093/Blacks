using UnityEngine;

// 회상 탐색 구간에서 쓰는 손전등.
//
//  - 플레이어 머리(Head)에 스포트라이트를 붙이고, 시선을 살짝 늦게 따라가서 손에 든 느낌을 냅니다.
//  - F키로 켜고 끕니다. 가끔 아주 짧게 깜빡입니다.
//  - FlashbackFreeRoamSegment의 "손전등 사용"이 켜진 구간에서만 쓸 수 있습니다.
[RequireComponent(typeof(FreeRoamMovement))]
public class FreeRoamFlashlight : MonoBehaviour
{
    [Header("빛")]
    [SerializeField] private Color color = new Color(1f, 0.95f, 0.82f);
    [Tooltip("화면 보정이 어두운 편이라 꽤 세게 잡아야 보입니다.")]
    [SerializeField] private float intensity = 40f;
    [SerializeField] private float range = 25f;
    [SerializeField] private float spotAngle = 55f;
    [SerializeField] private float innerSpotAngle = 25f;
    [SerializeField] private LightShadows shadows = LightShadows.Soft;

    [Tooltip("머리 기준 위치 (오른손에 든 느낌)")]
    [SerializeField] private Vector3 holdOffset = new Vector3(0.18f, -0.15f, 0.1f);

    [Tooltip("시선을 따라가는 속도. 낮을수록 늦게 따라옵니다.")]
    [SerializeField] private float followSharpness = 14f;

    [Header("조작")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F;
    [SerializeField] private AudioClip clickSound;

    [Header("깜빡임")]
    [Tooltip("평균 몇 초에 한 번 깜빡일지 (0이면 안 깜빡임)")]
    [SerializeField] private float flickerEvery = 9f;

    private FreeRoamMovement movement;
    private Light spot;
    private AudioSource audioSource;
    private bool available = false;
    private bool isOn = true;
    private float flickerUntil;
    private float nextFlicker;

    public bool IsOn => available && isOn;

    private void Awake()
    {
        movement = GetComponent<FreeRoamMovement>();
    }

    /// <summary>탐색 구간이 시작/끝날 때 부릅니다. 켜면 손전등을 켠 채로 시작합니다.</summary>
    public void SetAvailable(bool value)
    {
        available = value;
        isOn = true;
        EnsureLight();
        spot.enabled = available && isOn;
        if (available) SnapToHead();
        ScheduleFlicker();
    }

    private void Update()
    {
        if (!available || spot == null) return;

        if (!movement.InputPaused && Input.GetKeyDown(toggleKey))
        {
            isOn = !isOn;
            PlayClick();
        }

        // 가끔 짧게 깜빡인다.
        bool flickering = Time.time < flickerUntil;
        if (flickerEvery > 0f && Time.time >= nextFlicker)
        {
            flickerUntil = Time.time + Random.Range(0.08f, 0.25f);
            ScheduleFlicker();
        }

        spot.enabled = isOn && !(flickering && Random.value < 0.5f);
        spot.intensity = flickering ? intensity * Random.Range(0.3f, 0.9f) : intensity;
    }

    private void LateUpdate()
    {
        if (!available || spot == null) return;

        Transform head = movement.Head;
        Transform t = spot.transform;
        Vector3 targetPos = head.TransformPoint(holdOffset);
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        t.position = Vector3.Lerp(t.position, targetPos, k * 2f);
        t.rotation = Quaternion.Slerp(t.rotation, head.rotation, k);
    }

    private void SnapToHead()
    {
        Transform head = movement.Head;
        spot.transform.SetPositionAndRotation(head.TransformPoint(holdOffset), head.rotation);
    }

    private void ScheduleFlicker()
    {
        nextFlicker = flickerEvery > 0f ? Time.time + Random.Range(flickerEvery * 0.5f, flickerEvery * 1.5f) : float.MaxValue;
    }

    private void EnsureLight()
    {
        if (spot != null) return;

        // 머리의 자식으로 두지 않는다. 따로 두고 LateUpdate에서 늦게 따라가게 해야 흔들림이 생긴다.
        var go = new GameObject("Flashlight");
        go.transform.SetParent(transform.parent, false);
        spot = go.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = color;
        spot.intensity = intensity;
        spot.range = range;
        spot.spotAngle = spotAngle;
        spot.innerSpotAngle = innerSpotAngle;
        spot.shadows = shadows;
    }

    private void PlayClick()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
        audioSource.PlayOneShot(clickSound != null ? clickSound : GetClickClip(), 0.7f);
    }

    private void OnDisable()
    {
        if (spot != null) spot.enabled = false;
    }

    private void OnDestroy()
    {
        if (spot != null) Destroy(spot.gameObject);
    }

    // 스위치 딸깍 소리
    private static AudioClip cachedClick;
    private static AudioClip GetClickClip()
    {
        if (cachedClick != null) return cachedClick;
        const int rate = 44100;
        int n = (int)(0.05f * rate);
        var data = new float[n];
        var rng = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float env = Mathf.Exp(-t * 180f);
            float tone = Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.5f;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;
            data[i] = (tone + noise) * env * 0.8f;
        }
        cachedClick = AudioClip.Create("FlashlightClick", n, 1, rate, false);
        cachedClick.SetData(data, 0);
        return cachedClick;
    }
}
