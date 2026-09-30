using System;
using UnityEngine;
using UnityEngine.AI;

// 밤거리를 지나가는 행인(목격자). 플레이어가 시야에 오래 들어가 있으면 "들킨다".
//  - 순찰을 반복하지 않고 waypoints를 따라 "집"(마지막 지점)으로 걸어간다.
//    집에 들어가면 잠시 사라졌다가 처음 자리에서 다시 나온다. (거리가 텅 비지 않게)
//    routeMode를 Loop로 두면 예전처럼 지점을 계속 돈다.
//  - 씬에 NavMesh가 구워져 있으면 NavMeshAgent로 길을 찾아 걷는다. 없으면 지점까지 곧장 걷는다.
//  - 앞쪽을 비추는 불빛으로 시야 방향이 보인다.
//  - 시야(거리/각도/가림막) 안에 있으면 의심이 차오르고, 다 차면 멈춰 서서 플레이어를 부른다. (OnSpotted)
//    탐색 구간이 "뭐해요?" → 변명(50%)을 처리하고, 결과에 따라 Forgive() 또는 들킨 것으로 처리한다.
//  - 가까이서 뛰면 발소리를 듣고 뒤돌아본다.
//  - 머리 위에 "?" → "!" 표시가 뜬다.
// 탐색 구간(FlashbackFreeRoamSegment)이 Arm/Disarm한다.
public class WitnessPatrol : MonoBehaviour
{
    public enum RouteMode { GoHome, Loop }

    /// <summary>의심이 다 차서 플레이어를 불러 세웠을 때. 받는 쪽이 없으면 바로 OnCaught로 처리한다.</summary>
    public static event Action<WitnessPatrol> OnSpotted;

    /// <summary>들켰을 때 (변명이 없거나 실패했을 때)</summary>
    public static event Action<WitnessPatrol> OnCaught;

    [Header("이동")]
    [SerializeField] private Transform[] waypoints;
    [Tooltip("GoHome: 지점을 따라 마지막 지점(집)까지 가서 들어간다 / Loop: 지점을 계속 돈다")]
    [SerializeField] private RouteMode routeMode = RouteMode.GoHome;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Tooltip("집에 들어간 뒤 처음 자리에서 다시 나오기까지의 시간(초)")]
    [SerializeField] private float stayHomeDuration = 6f;

    [Header("길 찾기 (NavMesh)")]
    [Tooltip("켜면 NavMesh가 있을 때 NavMeshAgent로 걷는다")]
    [SerializeField] private bool useNavMesh = true;
    [Tooltip("시작 위치 근처 이 반경 안에서 NavMesh를 찾는다")]
    [SerializeField] private float navMeshSearchRadius = 2f;

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
    [Tooltip("플레이어가 이 거리(m) 안에서 뛰면 발소리를 듣고 뒤돌아본다")]
    [SerializeField] private float hearRunDistance = 7f;
    [Tooltip("발소리를 듣고 멈춰서 두리번거리는 시간(초)")]
    [SerializeField] private float alertDuration = 1.8f;

    [Header("변명이 통한 뒤")]
    [Tooltip("변명이 통하면 이 시간(초) 동안은 플레이어를 신경 쓰지 않는다")]
    [SerializeField] private float forgiveDuration = 8f;

    [Header("표시")]
    [SerializeField] private bool showViewLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private float lightIntensity = 45f;
    [Tooltip("어두운 곳에서도 몸이 보이도록 주변을 은은하게 비추는 불빛 세기 (0이면 없음)")]
    [SerializeField] private float bodyGlowIntensity = 3f;

    public float Suspicion { get; private set; }

    /// <summary>이번 탐색에서 이 사람에게 변명을 이미 한 번 했는지 (같은 사람에게 두 번은 안 통한다)</summary>
    public bool ExcuseUsed { get; private set; }

    /// <summary>지금 플레이어를 불러 세워 이야기하는 중인지</summary>
    public bool IsConfronting => state == State.Confronting;

    /// <summary>머리(눈) 위치. 불러 세울 때 플레이어가 이쪽을 바라본다.</summary>
    public Vector3 Eye => transform.position + Vector3.up * eyeHeight;

    private enum State { Walking, Waiting, Alert, Confronting, Home }

    private State state;
    private bool armed;
    private int targetIndex;
    private float stateTimer;
    private float forgivenUntil;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool hasStart;
    private Light viewLight;
    private Light bodyLight;
    private TextMesh marker;
    private float lookAround;
    private NavMeshAgent agent;
    private bool useAgent;
    private Vector3 agentStartPos; // 시작 위치에서 가장 가까운 NavMesh 위의 점
    private FreeRoamMovement cachedPlayer;

    public void Arm()
    {
        if (!hasStart) { startPos = transform.position; startRot = transform.rotation; hasStart = true; }

        gameObject.SetActive(true);
        EnsureVisuals();
        SetupAgent();
        PlaceAtStart();

        Suspicion = 0f;
        ExcuseUsed = false;
        forgivenUntil = 0f;
        armed = true;
    }

    public void Disarm()
    {
        armed = false;
        Suspicion = 0f;
        if (agent != null && agent.enabled) agent.enabled = false;
        if (marker != null) marker.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    /// <summary>변명이 통했다. 잠시 플레이어를 신경 쓰지 않고 가던 길을 간다.</summary>
    public void Forgive()
    {
        ExcuseUsed = true;
        Suspicion = 0f;
        forgivenUntil = Time.time + forgiveDuration;
        ResumeWalking();
    }

    /// <summary>변명을 했지만 통하지 않았다. (들킨 것으로 처리할 때 탐색 구간이 부른다)</summary>
    public void MarkExcuseUsed() => ExcuseUsed = true;

    /// <summary>다른 사람이 먼저 불러 세운 경우 등, 이번 발견을 없던 일로 하고 가던 길을 간다.</summary>
    public void CancelSpot()
    {
        Suspicion = 0.5f;
        ResumeWalking();
    }

    private void Update()
    {
        if (!armed) return;

        switch (state)
        {
            case State.Walking: Walk(); break;
            case State.Waiting: WaitAtPoint(); break;
            case State.Alert: Alert(); break;
            case State.Confronting: FacePlayer(); break;
            case State.Home: StayHome(); break;
        }

        if (state != State.Home && state != State.Confronting)
            Watch();

        UpdateMarker();
    }

    // ─────────────────────────────────────────────────────────────
    // 이동
    // ─────────────────────────────────────────────────────────────

    private void SetupAgent()
    {
        useAgent = false;
        if (!useNavMesh) return;

        Vector3 from = hasStart ? startPos : transform.position;
        if (!NavMesh.SamplePosition(from, out NavMeshHit hit, navMeshSearchRadius, NavMesh.AllAreas))
        {
            if (agent != null) agent.enabled = false;
            return;
        }

        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();

        agent.enabled = true;
        agent.speed = walkSpeed;
        agent.angularSpeed = turnSpeed * 2f;
        agent.acceleration = 8f;
        agent.stoppingDistance = 0.2f;
        agent.autoBraking = true;
        agentStartPos = hit.position;
        agent.Warp(agentStartPos);
        useAgent = agent.isOnNavMesh;
        if (!useAgent) agent.enabled = false;
    }

    private void PlaceAtStart()
    {
        if (useAgent)
        {
            agent.Warp(agentStartPos);
            transform.rotation = startRot;
        }
        else
        {
            transform.SetPositionAndRotation(startPos, startRot);
        }

        SetVisible(true);
        targetIndex = 0;
        stateTimer = 0f;
        state = State.Walking;
        GoToCurrentTarget();
    }

    private bool HasRoute => waypoints != null && waypoints.Length > 0;

    private void GoToCurrentTarget()
    {
        if (!useAgent || !HasRoute) return;
        Transform target = waypoints[targetIndex];
        if (target == null) return;
        agent.isStopped = false;
        agent.SetDestination(target.position);
    }

    private void ResumeWalking()
    {
        state = State.Walking;
        if (useAgent) agent.isStopped = false;
        GoToCurrentTarget();
    }

    private void StopMoving()
    {
        if (useAgent && agent.enabled) agent.isStopped = true;
    }

    private void Walk()
    {
        if (!HasRoute) return;

        Transform target = waypoints[targetIndex];
        if (target == null) { AdvanceTarget(); return; }

        bool arrived;
        if (useAgent)
        {
            // 길이 없으면(집이 NavMesh 밖 등) 곧장 걷기로 바꾼다.
            if (!agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"[WitnessPatrol] '{name}': '{target.name}'까지 길을 찾지 못해 곧장 걷습니다.");
                agent.enabled = false;
                useAgent = false;
                return;
            }
            arrived = !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.15f;
        }
        else
        {
            Vector3 to = target.position - transform.position;
            to.y = 0f;
            arrived = to.magnitude < 0.3f;
            if (!arrived)
            {
                Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
                transform.position += to.normalized * Mathf.Min(walkSpeed * Time.deltaTime, to.magnitude);
            }
        }

        if (!arrived) return;

        bool reachedHome = routeMode == RouteMode.GoHome && targetIndex >= waypoints.Length - 1;
        if (reachedHome)
        {
            GoInsideHome();
            return;
        }

        AdvanceTarget();
        StopMoving();
        state = State.Waiting;
        stateTimer = waitAtPoint;
        lookAround = 0f;
    }

    private void AdvanceTarget()
    {
        targetIndex = routeMode == RouteMode.Loop
            ? (targetIndex + 1) % waypoints.Length
            : Mathf.Min(targetIndex + 1, waypoints.Length - 1);
    }

    private void WaitAtPoint()
    {
        // 멈춰서 좌우로 두리번
        stateTimer -= Time.deltaTime;
        lookAround += Time.deltaTime;
        transform.Rotate(Vector3.up, Mathf.Sin(lookAround * 2f) * 60f * Time.deltaTime, Space.World);
        if (stateTimer <= 0f)
            ResumeWalking();
    }

    // 발소리를 듣고 멈춰서 소리 난 쪽을 본다.
    private void Alert()
    {
        stateTimer -= Time.deltaTime;
        FacePlayer();
        if (stateTimer <= 0f)
            ResumeWalking();
    }

    private void GoInsideHome()
    {
        Debug.Log($"[WitnessPatrol] '{name}'이(가) 집에 들어갔습니다. {stayHomeDuration:0.#}초 뒤 다시 나옵니다.");
        StopMoving();
        Suspicion = 0f;
        SetVisible(false);
        state = State.Home;
        stateTimer = stayHomeDuration;
    }

    private void StayHome()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            PlaceAtStart();
    }

    private void FacePlayer()
    {
        var player = GetPlayer();
        if (player == null) return;
        Vector3 to = player.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * 1.5f * Time.deltaTime);
    }

    // 집에 들어가 있는 동안에는 몸과 불빛을 끈다. (오브젝트를 끄면 Update가 멈춘다)
    private void SetVisible(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (marker == null || r.gameObject != marker.gameObject)
                r.enabled = visible;
        foreach (var l in GetComponentsInChildren<Light>(true))
            l.enabled = visible;
        foreach (var c in GetComponentsInChildren<Collider>(true))
            c.enabled = visible;
    }

    // ─────────────────────────────────────────────────────────────
    // 감시
    // ─────────────────────────────────────────────────────────────

    private FreeRoamMovement GetPlayer()
    {
        if (cachedPlayer == null || !cachedPlayer.isActiveAndEnabled)
            cachedPlayer = FindFirstObjectByType<FreeRoamMovement>();
        return cachedPlayer;
    }

    private void Watch()
    {
        var player = GetPlayer();
        bool active = player != null && player.IsControlling && !player.InputPaused;
        bool forgiven = Time.time < forgivenUntil;

        if (!active || forgiven)
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime * 0.5f);
            return;
        }

        bool seen = CanSee(player);

        // 가까이서 뛰면 발소리를 듣고 돌아본다. (돌아본 뒤 보이면 시야로 잡힌다)
        if (!seen && player.IsRunning && state != State.Alert &&
            Vector3.Distance(transform.position, player.transform.position) <= hearRunDistance)
        {
            Debug.Log($"[WitnessPatrol] '{name}'이(가) 뛰는 발소리를 들었다.");
            StopMoving();
            state = State.Alert;
            stateTimer = alertDuration;
            Suspicion = Mathf.Max(Suspicion, 0.25f);
        }

        if (seen)
        {
            float dist = Vector3.Distance(Eye, player.Head.position);
            float rate = dist < closeRange ? 3f : 1f;
            Suspicion = Mathf.Min(1f, Suspicion + Time.deltaTime / Mathf.Max(0.05f, timeToCatch) * rate);
            if (Suspicion >= 1f)
                Spot();
        }
        else if (state != State.Alert)
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime * 0.5f);
        }
    }

    // 의심이 다 찼다. 멈춰 서서 플레이어를 부른다.
    private void Spot()
    {
        Debug.Log($"[WitnessPatrol] '{name}'에게 들켰다!");
        Suspicion = 1f;

        if (OnSpotted == null)
        {
            // 변명을 받아 줄 쪽이 없으면 예전처럼 바로 들킨 것으로 처리한다.
            Suspicion = 0f;
            OnCaught?.Invoke(this);
            return;
        }

        StopMoving();
        state = State.Confronting;
        OnSpotted.Invoke(this);
    }

    /// <summary>탐색 구간이 변명 실패 등으로 들킨 것으로 처리할 때 부른다.</summary>
    public void ReportCaught()
    {
        Suspicion = 0f;
        OnCaught?.Invoke(this);
    }

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

    private void UpdateMarker()
    {
        if (marker == null) return;
        bool show = state != State.Home && (Suspicion > 0.02f || state == State.Confronting);
        marker.gameObject.SetActive(show);
        if (!show) return;

        marker.text = Suspicion > 0.6f || state == State.Confronting ? "!" : "?";
        marker.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.15f, 0.1f), Suspicion);
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
