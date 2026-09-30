using System;
using UnityEngine;

// 거리를 순찰하는 목격자. 플레이어가 시야에 오래 들어가 있으면 "들킨다".
//  - 정해진 지점들을 차례로 걸어 다니고, 지점마다 잠깐 두리번거린다.
//  - 앞쪽을 비추는 불빛으로 시야 방향이 보인다.
//  - 시야(거리/각도/가림막) 안에 있으면 의심이 차오르고, 다 차면 OnCaught.
//  - 머리 위에 "?" → "!" 표시가 뜬다.
// 탐색 구간(FlashbackFreeRoamSegment)이 Arm/Disarm하고, 들키면 처음부터 다시 하게 만든다.
public class WitnessPatrol : MonoBehaviour
{
    public static event Action<WitnessPatrol> OnCaught;

    [Header("순찰")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float walkSpeed = 1.5f;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float waitAtPoint = 1.5f;

    [Header("시야")]
    [Tooltip("눈 높이(발에서)")]
    [SerializeField] private float eyeHeight = 1.7f;
    [SerializeField] private float viewDistance = 14f;
    [SerializeField] private float viewAngle = 50f;
    [Tooltip("이 시간(초) 동안 계속 보이면 들킨다")]
    [SerializeField] private float timeToCatch = 1.2f;
    [Tooltip("아주 가까우면 이 배율로 빨리 들킨다")]
    [SerializeField] private float closeRange = 3f;

    [Header("표시")]
    [SerializeField] private bool showViewLight = true;
    [SerializeField] private Color lightColor = new Color(1f, 0.9f, 0.6f);
    [SerializeField] private float lightIntensity = 45f;
    [Tooltip("어두운 곳에서도 몸이 보이도록 주변을 은은하게 비추는 불빛 세기 (0이면 없음)")]
    [SerializeField] private float bodyGlowIntensity = 3f;

    public float Suspicion { get; private set; }

    private bool armed;
    private int targetIndex;
    private float waitTimer;
    private Vector3 startPos;
    private Quaternion startRot;
    private bool hasStart;
    private Light viewLight;
    private Light bodyLight;
    private TextMesh marker;
    private float lookAround;

    public void Arm()
    {
        if (!hasStart) { startPos = transform.position; startRot = transform.rotation; hasStart = true; }
        transform.SetPositionAndRotation(startPos, startRot);
        targetIndex = 0;
        waitTimer = 0f;
        Suspicion = 0f;
        armed = true;
        gameObject.SetActive(true);
        EnsureVisuals();
    }

    public void Disarm()
    {
        armed = false;
        Suspicion = 0f;
        if (marker != null) marker.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!armed) return;

        Patrol();
        Watch();
        UpdateMarker();
    }

    private void Patrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (waitTimer > 0f)
        {
            // 멈춰서 좌우로 두리번
            waitTimer -= Time.deltaTime;
            lookAround += Time.deltaTime;
            transform.Rotate(Vector3.up, Mathf.Sin(lookAround * 2f) * 60f * Time.deltaTime, Space.World);
            return;
        }

        Transform target = waypoints[targetIndex];
        if (target == null) { targetIndex = (targetIndex + 1) % waypoints.Length; return; }

        Vector3 to = target.position - transform.position;
        to.y = 0f;
        if (to.magnitude < 0.3f)
        {
            targetIndex = (targetIndex + 1) % waypoints.Length;
            waitTimer = waitAtPoint;
            lookAround = 0f;
            return;
        }

        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        transform.position += to.normalized * Mathf.Min(walkSpeed * Time.deltaTime, to.magnitude);
    }

    private void Watch()
    {
        var player = FindFirstObjectByType<FreeRoamMovement>();
        bool seen = player != null && player.IsControlling && !player.InputPaused && CanSee(player);

        if (seen)
        {
            float dist = Vector3.Distance(Eye, player.Head.position);
            float rate = dist < closeRange ? 3f : 1f;
            Suspicion = Mathf.Min(1f, Suspicion + Time.deltaTime / Mathf.Max(0.05f, timeToCatch) * rate);
            if (Suspicion >= 1f)
            {
                Debug.Log($"[WitnessPatrol] '{name}'에게 들켰다!");
                Suspicion = 0f;
                OnCaught?.Invoke(this);
            }
        }
        else
        {
            Suspicion = Mathf.Max(0f, Suspicion - Time.deltaTime * 0.5f);
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

    private void UpdateMarker()
    {
        if (marker == null) return;
        bool show = Suspicion > 0.02f;
        marker.gameObject.SetActive(show);
        if (!show) return;

        marker.text = Suspicion > 0.6f ? "!" : "?";
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
