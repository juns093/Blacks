using UnityEngine;

// "차 소리로 위치 찾기".
// E키를 누르면 차에서 경적이 울립니다. 가까울수록 크게, 멀수록 작게 들립니다.
// 소리는 차 위치에서 나기 때문에 왼쪽/오른쪽 방향도 구분됩니다.
// 차 위치는 BeginSearch()가 불릴 때마다(= 매 판마다) spawnPoints 중 하나로 무작위로 바뀝니다.
public class CarSoundFinder : MonoBehaviour
{
    [Header("탐색 대상")]
    [Tooltip("찾아야 할 자동차 오브젝트. 평소엔 꺼져 있다가 탐색이 시작될 때 무작위 지점에 나타납니다.")]
    [SerializeField] private Transform car;

    [Tooltip("매 판마다 이 중 하나로 차를 무작위 이동시킵니다. 회전값도 그대로 씁니다.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("플레이어와 이 거리보다 가까운 지점에는 차를 놓지 않습니다. (너무 쉽게 찾지 않도록)")]
    [SerializeField] private float minSpawnDistanceFromPlayer = 18f;

    [Header("기준")]
    [Tooltip("거리를 재는 기준점(플레이어). 비워두면 Camera.main을 씁니다. FlashbackFreeRoamSegment가 채워줍니다.")]
    [SerializeField] private Transform listener;

    [Header("경적")]
    [SerializeField] private KeyCode pingKey = KeyCode.E;

    [Tooltip("경적 소리. 비워두면 코드로 만든 '빵빵' 경적을 씁니다.")]
    [SerializeField] private AudioClip pingSound;

    [Tooltip("차에 붙은 AudioSource. 비워두면 차에 자동으로 만듭니다.")]
    [SerializeField] private AudioSource carAudio;

    [Tooltip("E를 연타해도 소리가 겹치지 않도록 두는 최소 간격(초)")]
    [SerializeField] private float pingCooldown = 0.8f;

    [Header("거리에 따른 볼륨")]
    [Tooltip("이 거리 이하로 가까워지면 최대 볼륨이 됩니다.")]
    [SerializeField] private float minFullVolumeDistance = 3f;

    [Tooltip("이 거리 이상 멀어지면 가장 작은 볼륨이 됩니다.")]
    [SerializeField] private float maxHearDistance = 60f;

    [Tooltip("거리에 따른 볼륨 곡선. x=0(가까움)~1(멀어짐), y=볼륨(0~1).")]
    [SerializeField]
    private AnimationCurve volumeByDistance = new AnimationCurve(
        new Keyframe(0f, 1f, 0f, -2f),
        new Keyframe(1f, 0.06f, 0f, 0f));

    [Header("발견 판정")]
    [Tooltip("차 몸체에서 이 거리 안으로 들어오면 '찾았다'고 판정합니다.")]
    [SerializeField] private float foundDistance = 1.3f;

    /// <summary>차를 찾았을 때 한 번만 호출됩니다.</summary>
    public event System.Action OnCarFound;

    private bool searching = false;
    private bool found = false;
    private float lastPingTime = -999f;

    // 차 몸체의 월드 외곽. 콜라이더 대신 렌더러로 잰다.
    // (차 모델의 MeshCollider는 메쉬가 비어 있거나 볼록이 아니면 ClosestPoint가 입력 좌표를
    //  그대로 돌려줘서, 멀리 있어도 거리가 0으로 나와 곧바로 '찾았다'가 되어 버린다)
    private Bounds carBounds;
    private bool hasCarBounds = false;
    private static AudioClip proceduralHorn;

    public bool IsSearching => searching;

    /// <summary>찾아야 할 차. 탑승 연출에서 차 쪽을 바라볼 때 씁니다.</summary>
    public Transform Car => car;

    /// <summary>차 몸체의 월드 외곽 (탐색이 시작된 뒤에 유효)</summary>
    public Bounds CarBounds => hasCarBounds ? carBounds : new Bounds(car != null ? car.position : Vector3.zero, Vector3.one);

    /// <summary>플레이어 머리에서 차 중심까지의 거리.</summary>
    public float CurrentDistance =>
        (car == null || Listener == null) ? Mathf.Infinity : Vector3.Distance(Listener.position, car.position);

    private Transform Listener
    {
        get
        {
            if (listener == null && Camera.main != null)
                listener = Camera.main.transform;
            return listener;
        }
    }

    private void Awake()
    {
        // 차는 탐색이 시작되기 전까지 숨겨 둔다. (회상 컷신에 미리 보이지 않도록)
        if (car != null)
            car.gameObject.SetActive(false);
    }

    public void SetListener(Transform t)
    {
        listener = t;
    }

    /// <summary>
    /// 이번 판에서 찾을 차 위치를 무작위로 정하고 탐색을 시작합니다.
    /// </summary>
    public void BeginSearch()
    {
        found = false;
        searching = true;

        if (car == null || spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning("[CarSoundFinder] car 또는 spawnPoints가 비어 있습니다! 인스펙터에서 연결하세요.");
            return;
        }

        Transform chosen = PickSpawnPoint();
        car.SetPositionAndRotation(chosen.position, chosen.rotation);
        car.gameObject.SetActive(true);

        // 모델마다 피벗 높이가 달라서, 스폰 지점 높이를 믿지 않고 바퀴를 바닥에 직접 붙인다.
        SnapCarToGround();
        EnsureCarAudio();

        Debug.Log($"[CarSoundFinder] 차를 '{chosen.name}'에 무작위 배치했습니다. " +
                  $"(플레이어와 {CurrentDistance:0.0}m)");
    }

    public void StopSearch()
    {
        searching = false;
    }

    // 플레이어와 너무 가까운 지점은 빼고 고른다. 전부 가깝다면 그냥 아무거나.
    private Transform PickSpawnPoint()
    {
        var candidates = new System.Collections.Generic.List<Transform>();
        foreach (var p in spawnPoints)
        {
            if (p == null) continue;
            if (Listener == null || FlatDistance(Listener.position, p.position) >= minSpawnDistanceFromPlayer)
                candidates.Add(p);
        }

        if (candidates.Count == 0)
            foreach (var p in spawnPoints) if (p != null) candidates.Add(p);

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void Update()
    {
        if (!searching || found) return;

        if (Input.GetKeyDown(pingKey) && Time.time - lastPingTime >= pingCooldown)
            Ping();

        if (car != null && Listener != null && DistanceToCarBody() <= foundDistance)
            HandleFound();
    }

    private void Ping()
    {
        if (car == null || Listener == null)
        {
            Debug.LogWarning("[CarSoundFinder] car 또는 listener가 비어 있어 경적을 울릴 수 없습니다!");
            return;
        }

        // 차가 아직 배치되지 않았으면(꺼진 상태) 소리를 낼 수 없다.
        if (!car.gameObject.activeInHierarchy)
            return;

        EnsureCarAudio();
        lastPingTime = Time.time;

        float distance = CurrentDistance;
        float t = Mathf.InverseLerp(minFullVolumeDistance, maxHearDistance, distance);
        float volume = Mathf.Clamp01(volumeByDistance.Evaluate(t));

        AudioClip clip = pingSound != null ? pingSound : GetProceduralHorn();
        carAudio.PlayOneShot(clip, volume);

        Debug.Log($"[CarSoundFinder] 경적 - 거리 {distance:0.0}m -> 볼륨 {volume:0.00}");
    }

    // 차 중심이 아니라 차 몸체 외곽까지의 수평 거리. 차가 길쭉해서 중심 기준이면 옆에 붙어도 판정이 안 된다.
    private float DistanceToCarBody()
    {
        if (!hasCarBounds) RecalculateCarBounds();
        if (!hasCarBounds) return FlatDistance(Listener.position, car.position);

        Vector3 p = Listener.position;
        p.y = carBounds.center.y;   // 높이 차이는 무시하고 수평으로만 잰다
        return FlatDistance(p, carBounds.ClosestPoint(p));
    }

    private void RecalculateCarBounds()
    {
        hasCarBounds = false;
        if (car == null) return;

        foreach (var r in car.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            if (!hasCarBounds) { carBounds = r.bounds; hasCarBounds = true; }
            else carBounds.Encapsulate(r.bounds);
        }
    }

    // 스폰 지점 아래 바닥을 찾아 차 외곽의 가장 낮은 곳이 바닥에 닿게 올리거나 내린다.
    private void SnapCarToGround()
    {
        RecalculateCarBounds();
        if (!hasCarBounds) return;

        Vector3 origin = new Vector3(carBounds.center.x, carBounds.max.y + 1f, carBounds.center.z);
        float groundY = float.NaN;
        float best = float.MaxValue;

        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 10f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(car)) continue;   // 차 자기 자신은 무시
            if (hit.distance < best) { best = hit.distance; groundY = hit.point.y; }
        }

        if (float.IsNaN(groundY))
        {
            Debug.LogWarning("[CarSoundFinder] 차 아래에서 바닥을 찾지 못했습니다. 스폰 지점 높이를 그대로 씁니다.");
            return;
        }

        float offset = groundY - carBounds.min.y;
        car.position += Vector3.up * offset;
        RecalculateCarBounds();
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void HandleFound()
    {
        found = true;
        searching = false;
        Debug.Log("[CarSoundFinder] 차를 찾았습니다!");
        OnCarFound?.Invoke();
    }

    // 소리는 차에서 나야 좌우 방향이 들린다.
    // 다만 볼륨은 위 곡선으로 직접 정하므로, Unity의 거리 감쇠는 평평하게 꺼둔다. (이중 감쇠 방지)
    private void EnsureCarAudio()
    {
        if (car == null) return;

        if (carAudio == null)
        {
            carAudio = car.GetComponent<AudioSource>();
            if (carAudio == null)
                carAudio = car.gameObject.AddComponent<AudioSource>();
        }

        carAudio.playOnAwake = false;
        carAudio.loop = false;
        carAudio.spatialBlend = 1f;
        carAudio.dopplerLevel = 0f;
        carAudio.spread = 0f;
        carAudio.minDistance = 1f;
        carAudio.maxDistance = 1000f;
        carAudio.rolloffMode = AudioRolloffMode.Custom;
        carAudio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Constant(0f, 1f, 1f));
    }

    // 프로젝트에 경적 소리가 없어서 직접 만든다. 두 음을 겹친 '빵-빵'.
    private static AudioClip GetProceduralHorn()
    {
        if (proceduralHorn != null) return proceduralHorn;

        const int rate = 44100;
        const float honk = 0.26f;
        const float gap = 0.11f;
        int total = Mathf.CeilToInt(rate * (honk * 2f + gap));
        var data = new float[total];

        for (int i = 0; i < total; i++)
        {
            float t = i / (float)rate;
            float local;
            if (t < honk) local = t;
            else if (t < honk + gap) continue;
            else local = t - honk - gap;
            if (local >= honk) continue;

            // 앞뒤를 부드럽게 끊어서 '딱' 소리가 나지 않게 한다.
            float env = Mathf.Clamp01(local / 0.012f) * Mathf.Clamp01((honk - local) / 0.035f);

            // 자동차 경적은 보통 약 400Hz / 500Hz 두 음을 같이 낸다.
            float a = Mathf.Sin(2f * Mathf.PI * 405f * t);
            float b = Mathf.Sin(2f * Mathf.PI * 507f * t);
            float square = (Mathf.Sign(a) + Mathf.Sign(b)) * 0.5f; // 경적 특유의 거친 느낌
            float s = square * 0.55f + (a + b) * 0.25f;

            data[i] = s * env * 0.6f;
        }

        proceduralHorn = AudioClip.Create("ProceduralCarHorn", total, 1, rate, false);
        proceduralHorn.SetData(data, 0);
        return proceduralHorn;
    }
}
