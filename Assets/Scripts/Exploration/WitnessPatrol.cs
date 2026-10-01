using System;
using UnityEngine;
using UnityEngine.AI;

// 밤길을 걸어 집에 돌아가는 행인. 플레이어가 시야에 오래 들어가 있으면 "들킨다".
//  - waypoints[0]에서 출발해 마지막 지점(집)까지 NavMesh 길찾기로 걸어간다. (중간 지점은 들르는 곳)
//  - 가다가 가끔 멈춰 휴대폰을 보거나 두리번거린다.
//  - 집에 도착하면 사라졌다가, 잠시 뒤 출발 지점에서 다시 나타나 걸어온다. (다른 사람의 퇴근길)
//  - 플레이어가 가까이서 뛰면 발소리를 듣고 그쪽을 돌아본다.
//  - 시야(거리/각도/가림막) 안에 있으면 의심이 차오르고, 다 차면 OnCaught.
//    탐색 구간이 "뭐해요?" 대화(변명 50%)를 띄우고, 결과에 따라 Release() 또는 처음부터 다시.
//  - 머리 위에 "?" → "!" 표시가 뜬다.
// NavMesh가 없으면 지점 사이를 직선으로 걷는다.
public class WitnessPatrol : MonoBehaviour
{
    public static event Action<WitnessPatrol> OnCaught;

    /// <summary>대화 중에는 모든 행인이 의심을 멈춘다.</summary>
    public static bool GlobalPause { get; set; }

    [Header("귀갓길")]
    [Tooltip("0번 = 출발 지점, 마지막 = 집. 중간 지점은 차례로 들른다.")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 180f;
    [Tooltip("집에 들어간 뒤 다시 출발 지점에 나타나기까지(초)")]
    [SerializeField] private Vector2 respawnDelay = new Vector2(3f, 6f);
    [Tooltip("걷다가 멈춰 설 확률 (초당)")]
    [SerializeField] private float pauseChancePerSecond = 0.06f;
    [SerializeField] private Vector2 pauseDuration = new Vector2(1.5f, 3f);

    [Header("시야")]
    [Tooltip("눈 높이(발에서)")]
    [SerializeField] private float eyeHeight = 1.7f;
    [SerializeField] private float viewDistance = 14f;
    [SerializeField] private float viewAngle = 50f;
    [Tooltip("이 시간(초) 동안 계속 보이면 들킨다")]
    [SerializeField] private float timeToCatch = 1.2f;
    [Tooltip("아주 가까우면 이 배율로 빨리 들킨다")]
    [SerializeField] private float closeRange = 3f;

    [Header("소리")]
    [Tooltip("플레이어가 뛰면 이 거리 안에서 발소리를 듣고 돌아본다")]
    [SerializeField] private float hearRunRadius = 9f;

    [Header("표시")]
    [SerializeField] private bool showViewLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private float lightIntensity = 45f;
    [Tooltip("어두운 곳에서도 몸이 보이도록 주변을 은은하게 비추는 불빛 세기 (0이면 없음)")]
    [SerializeField] private float bodyGlowIntensity = 3f;

    public float Suspicion { get; private set; }

    private enum Mode { Walking, Paused, Home, Listening, Confronting }

    private bool armed;
    private Mode mode;
    private int targetIndex;
    private float timer;
    private float graceUntil;
    private Vector3 listenTarget;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool hasStart;
    private Light viewLight;
    private Light bodyLight;
    private TextMesh marker;
    private float lookAround;
    private NavMeshAgent agent;
    private Renderer[] renderers;
    private FreeRoamMovement cachedPlayer;

    public void Arm()
    {
        if (!hasStart) { startPos = transform.position; startRot = transform.rotation; hasStart = true; }
        gameObject.SetActive(true);
        EnsureVisuals();
        EnsureAgent();
        Suspicion = 0f;
        graceUntil = 0f;
        GlobalPause = false;
        armed = true;
        Respawn();
    }

    public void Disarm()
    {
        armed = false;
        Suspicion = 0f;
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
        if (marker != null) marker.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>들킨 뒤 대화하는 동안: 멈춰서 플레이어를 바라본다.</summary>
    public void Confront(Transform player)
    {
        mode = Mode.Confronting;
        StopAgent();
        listenTarget = player.position;
        Suspicion = 1f;
    }

    /// <summary>변명이 먹혔을 때: 의심을 풀고 가던 길을 간다. grace초 동안은 다시 의심하지 않는다.</summary>
    public void Release(float grace)
    {
        Suspicion = 0f;
        graceUntil = Time.time + grace;
        mode = Mode.Walking;
        SetDestination();
    }

    private void Respawn()
    {
        Vector3 p = waypoints != null && waypoints.Length > 0 && waypoints[0] != null ? waypoints[0].position : startPos;
        Warp(p, startRot);
        SetVisible(true);
        targetIndex = waypoints != null && waypoints.Length > 1 ? 1 : 0;
        mode = Mode.Walking;
        SetDestination();
    }

    private void Update()
    {
        if (!armed) return;

        switch (mode)
        {
            case Mode.Walking: Walk(); break;
            case Mode.Paused: LookAround(); break;
            case Mode.Home: WaitAtHome(); break;
            case Mode.Listening: Listen(); break;
            case Mode.Confronting: Face(listenTarget); break;
        }

        if (mode != Mode.Home && mode != Mode.Confronting && !GlobalPause)
        {
            Hear();
            Watch();
        }
        UpdateMarker();
    }

    // ── 걷기 ──
    private void Walk()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // 가끔 멈춰 선다 (휴대폰을 보거나 두리번)
        if (UnityEngine.Random.value < pauseChancePerSecond * Time.deltaTime)
        {
            mode = Mode.Paused;
            timer = UnityEngine.Random.Range(pauseDuration.x, pauseDuration.y);
            lookAround = 0f;
            StopAgent();
            return;
        }

        Vector3 target = CurrentTarget;
        Vector3 to = target - transform.position;
        to.y = 0f;

        bool arrived = UsingAgent
            ? (!agent.pathPending && agent.remainingDistance <= Mathf.Max(0.4f, agent.stoppingDistance + 0.1f))
            : to.magnitude < 0.3f;

        if (arrived)
        {
            if (targetIndex >= waypoints.Length - 1)
            {
                // 집에 도착 → 들어가서 사라진다
                mode = Mode.Home;
                timer = UnityEngine.Random.Range(respawnDelay.x, respawnDelay.y);
                StopAgent();
                SetVisible(false);
                Suspicion = 0f;
                return;
            }
            targetIndex++;
            SetDestination();
            return;
        }

        if (!UsingAgent)
        {
            Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            transform.position += to.normalized * Mathf.Min(walkSpeed * Time.deltaTime, to.magnitude);
        }
    }

    private void LookAround()
    {
        timer -= Time.deltaTime;
        lookAround += Time.deltaTime;
        transform.Rotate(Vector3.up, Mathf.Sin(lookAround * 1.6f) * 50f * Time.deltaTime, Space.World);
        if (timer <= 0f)
        {
            mode = Mode.Walking;
            SetDestination();
        }
    }

    private void WaitAtHome()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f) Respawn();
    }

    // 뛰는 발소리를 듣고 돌아본다
    private void Hear()
    {
        if (mode == Mode.Listening) return;
        var player = Player;
        if (player == null || !player.IsControlling || !player.IsSprinting) return;
        if (Time.time < graceUntil) return;
        if (Vector3.Distance(transform.position, player.transform.position) > hearRunRadius) return;

        mode = Mode.Listening;
        timer = 1.6f;
        listenTarget = player.transform.position;
        StopAgent();
    }

    private void Listen()
    {
        Face(listenTarget);
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            mode = Mode.Walking;
            SetDestination();
        }
    }

    private void Face(Vector3 point)
    {
        Vector3 to = point - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized), turnSpeed * 1.5f * Time.deltaTime);
    }

    // ── 보기 ──
    private void Watch()
    {
        var player = Player;
        bool seen = player != null && player.IsControlling && !player.InputPaused && Time.time >= graceUntil && CanSee(player);

        if (seen)
        {
            float dist = Vector3.Distance(Eye, player.Head.position);
            float rate = dist < closeRange ? 3f : 1f;
            if (player.IsSprinting) rate *= 1.5f; // 뛰는 사람은 더 수상하다
            Suspicion = Mathf.Min(1f, Suspicion + Time.deltaTime / Mathf.Max(0.05f, timeToCatch) * rate);
            if (Suspicion >= 1f)
            {
                Debug.Log($"[WitnessPatrol] '{name}'에게 들켰다!");
                OnCaught?.Invoke(this);
            }
        }
        else
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime * 0.5f);
        }
    }

    private FreeRoamMovement Player
    {
        get
        {
            if (cachedPlayer == null) cachedPlayer = FindFirstObjectByType<FreeRoamMovement>();
            return cachedPlayer;
        }
    }

    private Vector3 Eye => transform.position + Vector3.up * eyeHeight;

    private bool CanSee(FreeRoamMovement player)
    {
        Vector3 target = player.Head.position;
        Vector3 dir = target - Eye;
        float dist = dir.magnitude;
        if (dist > viewDistance) return false;
        if (dist > closeRange * 0.5f && Vector3.Angle(transform.forward, dir) > viewAngle) return false;

        // 가려져 있는지 (자기 자신과 플레이어는 무시)
        var hits = Physics.RaycastAll(Eye, dir.normalized, dist, ~0, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.transform.IsChildOf(player.transform)) continue;
            return false;
        }
        return true;
    }

    // ── NavMesh ──
    private bool UsingAgent => agent != null && agent.enabled && agent.isOnNavMesh;

    private Vector3 CurrentTarget =>
        waypoints != null && targetIndex < waypoints.Length && waypoints[targetIndex] != null
            ? waypoints[targetIndex].position
            : transform.position;

    private void EnsureAgent()
    {
        StreetNavMesh.EnsureBuilt();
        if (!StreetNavMesh.HasNavMeshNear(transform.position)) return;

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed * 2f;
        agent.acceleration = 6f;
        agent.stoppingDistance = 0.2f;
        agent.radius = 0.35f;
        agent.height = eyeHeight + 0.3f;
        agent.autoBraking = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    }

    private void Warp(Vector3 p, Quaternion rot)
    {
        if (agent != null && agent.enabled && NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            agent.Warp(hit.position);
            transform.rotation = rot;
        }
        else
        {
            transform.SetPositionAndRotation(p, rot);
        }
    }

    private void SetDestination()
    {
        if (!UsingAgent) return;
        agent.isStopped = false;
        Vector3 target = CurrentTarget;
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas)) target = hit.position;
        agent.SetDestination(target);
    }

    private void StopAgent()
    {
        if (UsingAgent)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private void SetVisible(bool visible)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            if (r != null && r.GetComponent<TextMesh>() == null) r.enabled = visible;
        if (viewLight != null) viewLight.enabled = visible;
        if (bodyLight != null) bodyLight.enabled = visible;
    }

    private void UpdateMarker()
    {
        if (marker == null) return;
        bool show = mode == Mode.Confronting || mode == Mode.Listening || Suspicion > 0.02f;
        marker.gameObject.SetActive(show && mode != Mode.Home);
        if (!show) return;

        float s = mode == Mode.Confronting ? 1f : Mathf.Max(Suspicion, mode == Mode.Listening ? 0.3f : 0f);
        marker.text = s > 0.6f ? "!" : "?";
        marker.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.15f, 0.1f), s);
        var cam = Camera.main;
        if (cam != null) marker.transform.rotation = Quaternion.LookRotation(marker.transform.position - cam.transform.position);
    }

    private void EnsureVisuals()
    {
        if (showViewLight && viewLight == null)
        {
            var go = new GameObject("ViewLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, eyeHeight, 0.2f);
            go.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            viewLight = go.AddComponent<Light>();
            viewLight.type = LightType.Spot;
            viewLight.spotAngle = viewAngle * 2f;
            viewLight.range = viewDistance;
            viewLight.intensity = lightIntensity;
            viewLight.color = lightColor;
            viewLight.shadows = LightShadows.None;
        }

        if (bodyGlowIntensity > 0f && bodyLight == null)
        {
            var go = new GameObject("BodyGlow");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, eyeHeight * 0.75f, 0.6f);
            bodyLight = go.AddComponent<Light>();
            bodyLight.type = LightType.Point;
            bodyLight.range = 2.5f;
            bodyLight.intensity = bodyGlowIntensity;
            bodyLight.color = lightColor;
            bodyLight.shadows = LightShadows.None;
        }

        if (marker == null)
        {
            var go = new GameObject("SuspicionMarker");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, eyeHeight + 0.6f, 0f);
            marker = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            marker.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            marker.fontSize = 64;
            marker.characterSize = 0.05f;
            marker.anchor = TextAnchor.MiddleCenter;
            marker.alignment = TextAlignment.Center;
            marker.fontStyle = FontStyle.Bold;
            go.SetActive(false);
        }
    }
}
