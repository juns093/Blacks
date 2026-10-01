using UnityEngine;

// 맞은편 사람(??? / 트레일)이 총을 직접 들고 쏘는 동작.
//
//  1) 총이 테이블에서 미끄러지듯 손으로 온다 → 두 손으로 받아 든다
//  2) 펌프를 당겨 장전한다 (철컥-철컥)
//  3) 플레이어를 겨누거나, 개머리판을 테이블에 대고 총구를 자기 턱 밑에 댄다
//  4) 발사 반동 → 총을 내려놓으면(GunObject가 테이블로 되돌릴 때) 팔을 내린다
//
// 팔은 SlumpableBody가 나눠 둔 어깨/팔꿈치 관절을 2관절 IK로 움직인다.
// 총의 위치는 이 차례 동안만 LateUpdate에서 덮어쓴다. (타임라인의 총 애니메이션보다 나중에 적용됨)
[RequireComponent(typeof(SlumpableBody))]
public class OpponentGunHandler : MonoBehaviour
{
    [Header("동작 시간 (차례 전체 길이에 맞춰 줄어듦)")]
    [SerializeField] private float glideTime = 1.1f;
    [SerializeField] private float rackTime = 0.7f;
    [SerializeField] private float aimTime = 0.9f;

    [Header("소리")]
    [SerializeField] private AudioClip rackSound;
    [Range(0f, 1f)] [SerializeField] private float rackVolume = 0.9f;

    private enum Phase { Idle, Glide, Rack, Aim, Hold, Released }

    private SlumpableBody body;
    private GunObject gun;
    private Transform gunT;
    private bool targetSelf;
    private Phase phase = Phase.Idle;
    private float phaseTime;
    private float timeScaleFactor = 1f;
    private Vector3 startPos;
    private Quaternion startRot;
    private float muzzleDist = 4.9f;
    private float stockDist = 2.3f;
    private float armWeight;
    private float recoil;          // 0~1, 발사 직후 1에서 빠르게 줄어듦
    private float recoilStrength;
    private bool rackPlayed1, rackPlayed2;
    private AudioSource audioSource;
    private static AudioClip proceduralRack;

    /// <summary>지금 맞은편에 앉은 사람의 손. 없으면 null.</summary>
    public static OpponentGunHandler Current
    {
        get
        {
            var presenter = OpponentPresenter.Instance;
            if (presenter == null) return null;
            var b = presenter.CurrentBody;
            return b != null ? b.GetComponent<OpponentGunHandler>() : null;
        }
    }

    public bool IsHolding => phase == Phase.Glide || phase == Phase.Rack || phase == Phase.Aim || phase == Phase.Hold;

    private void Awake()
    {
        body = GetComponent<SlumpableBody>();
        GunObject.WhoReturnStarted += Release;
    }

    private void OnDestroy()
    {
        GunObject.WhoReturnStarted -= Release;
    }

    /// <summary>
    /// 이 사람 차례가 시작될 때. totalTime = 방아쇠를 당기기까지의 시간(초).
    /// </summary>
    public void BeginTurn(GunObject theGun, bool shootSelf, float totalTime)
    {
        if (theGun == null) return;
        body.EnsureBuilt();
        if (body.ArmRight == null || body.ArmLeft == null)
        {
            Debug.LogWarning($"[OpponentGunHandler] '{name}' 팔 관절이 없어 동작을 건너뜁니다.");
            return;
        }

        gun = theGun;
        gunT = gun.transform;
        targetSelf = shootSelf;
        startPos = gunT.position;
        startRot = gunT.rotation;
        MeasureGun();

        float needed = glideTime + rackTime + aimTime + 0.3f;
        timeScaleFactor = Mathf.Clamp(totalTime / needed, 0.35f, 1f);
        rackPlayed1 = rackPlayed2 = false;
        recoil = 0f;
        SetPhase(Phase.Glide);
    }

    /// <summary>방아쇠를 당긴 직후.</summary>
    public void OnFired(bool live, bool shotSelf)
    {
        if (!IsHolding) return;
        if (live && shotSelf)
        {
            // 자기 머리에 실탄 → 그 자리에서 힘이 빠진다 (책상에 머리를 박는 건 GameStateManager가)
            ApplyArms(Vector3.zero, Vector3.zero, 0f);
            armWeight = 0f;
            SetPhase(Phase.Idle);
            return;
        }
        recoil = 1f;
        recoilStrength = live ? 1f : 0.25f;
        SetPhase(Phase.Hold);
    }

    /// <summary>총을 놓는다. 팔은 천천히 내려간다.</summary>
    public void Release()
    {
        if (phase == Phase.Idle || phase == Phase.Released) return;
        SetPhase(Phase.Released);
    }

    private void SetPhase(Phase p)
    {
        phase = p;
        phaseTime = 0f;
    }

    private void MeasureGun()
    {
        // 총구까지의 거리: 총구 불빛 위치, 개머리판까지의 거리: 메쉬 외곽
        var lightT = gunT.GetComponentInChildren<Light>(true);
        if (lightT != null)
            muzzleDist = Mathf.Max(1f, Vector3.Dot(lightT.transform.position - gunT.position, gunT.forward));

        float minProj = 0f;
        foreach (var mf in gunT.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = new Vector3(i % 2 == 0 ? mb.min.x : mb.max.x, (i / 2) % 2 == 0 ? mb.min.y : mb.max.y, i < 4 ? mb.min.z : mb.max.z);
                float proj = Vector3.Dot(mf.transform.TransformPoint(c) - gunT.position, gunT.forward);
                minProj = Mathf.Min(minProj, proj);
            }
        }
        stockDist = Mathf.Max(0.5f, -minProj);
    }

    private void LateUpdate()
    {
        if (phase == Phase.Idle) { armWeight = 0f; return; }

        float dt = Time.deltaTime;
        phaseTime += dt;
        recoil = Mathf.MoveTowards(recoil, 0f, dt / 0.35f);

        if (phase == Phase.Released)
        {
            armWeight = Mathf.MoveTowards(armWeight, 0f, dt / 0.5f);
            if (armWeight <= 0f)
            {
                ApplyArms(Vector3.zero, Vector3.zero, 0f);
                phase = Phase.Idle;
                return;
            }
            // 놓은 총 대신 마지막 손 위치에서 그대로 내린다
            ApplyArms(lastRight, lastLeft, armWeight);
            return;
        }

        if (gunT == null) { SetPhase(Phase.Released); return; }

        float glide = glideTime * timeScaleFactor, rack = rackTime * timeScaleFactor, aim = aimTime * timeScaleFactor;
        Pose ready = ReadyPose();
        Pose final = targetSelf ? SelfPose() : AimPose();
        Pose pose;
        float rackSlide = 0f;

        switch (phase)
        {
            case Phase.Glide:
            {
                float k = Smooth(phaseTime / glide);
                Vector3 p = Vector3.Lerp(startPos, ready.position, k);
                p += Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.2f;
                pose = new Pose(p, Quaternion.Slerp(startRot, ready.rotation, k));
                if (phaseTime >= glide) SetPhase(Phase.Rack);
                break;
            }
            case Phase.Rack:
            {
                float k = Mathf.Clamp01(phaseTime / rack);
                // 두 번 당긴다: 0~0.5 당기고 밀고, 소리는 끝에서 철컥
                rackSlide = Mathf.Sin(Mathf.Clamp01(k / 0.6f) * Mathf.PI);
                if (!rackPlayed1 && k > 0.22f) { rackPlayed1 = true; PlayRack(); }
                pose = ready;
                pose.position -= (ready.rotation * Vector3.forward) * rackSlide * 0.15f;
                if (phaseTime >= rack) SetPhase(Phase.Aim);
                break;
            }
            case Phase.Aim:
            {
                float k = Smooth(phaseTime / aim);
                pose = new Pose(Vector3.Lerp(ready.position, final.position, k), Quaternion.Slerp(ready.rotation, final.rotation, k));
                if (phaseTime >= aim) SetPhase(Phase.Hold);
                break;
            }
            default: // Hold
            {
                pose = final;
                // 숨 쉬듯 미세하게 흔들린다
                float t = Time.time;
                pose.position += Vector3.up * Mathf.Sin(t * 1.3f) * 0.03f + transform.right * Mathf.Sin(t * 0.9f) * 0.02f;
                break;
            }
        }

        // 발사 반동: 뒤로 밀리고 총구가 들린다
        if (recoil > 0f)
        {
            float r = recoil * recoil * recoilStrength;
            Vector3 dirNow = pose.rotation * Vector3.forward;
            Vector3 rightNow = pose.rotation * Vector3.right;
            pose.position -= dirNow * 0.8f * r;
            pose.rotation = Quaternion.AngleAxis(-14f * r, rightNow) * pose.rotation;
        }

        gunT.SetPositionAndRotation(pose.position, pose.rotation);

        // 손 위치
        Vector3 dir = pose.rotation * Vector3.forward;
        Vector3 rightHand, leftHand;
        if (targetSelf && (phase == Phase.Aim || phase == Phase.Hold))
        {
            // 총구 쪽 총열을 두 손으로 붙잡아 턱에 댄다
            float k = phase == Phase.Hold ? 1f : Smooth(phaseTime / aim);
            Vector3 rReady = pose.position + dir * 0.15f, lReady = pose.position + dir * 2.0f;
            Vector3 rSelf = pose.position + dir * (muzzleDist * 0.62f), lSelf = pose.position + dir * (muzzleDist * 0.78f);
            rightHand = Vector3.Lerp(rReady, rSelf, k);
            leftHand = Vector3.Lerp(lReady, lSelf, k);
        }
        else
        {
            float forend = (phase == Phase.Aim || phase == Phase.Hold) && !targetSelf ? 1.35f : 2.0f;
            rightHand = pose.position + dir * 0.15f;
            leftHand = pose.position + dir * (forend - rackSlide * 0.6f);
        }

        // 총이 손에 닿을 만큼 가까워질수록 팔이 따라 붙는다
        float reach = ArmReach(body.ArmRight);
        float distR = Vector3.Distance(body.ArmRight.shoulder.position, rightHand);
        float want = phase == Phase.Glide ? Mathf.Clamp01((reach * 1.6f - distR) / (reach * 0.6f)) : 1f;
        armWeight = Mathf.MoveTowards(armWeight, want, dt / 0.25f);

        lastRight = rightHand;
        lastLeft = leftHand;
        ApplyArms(rightHand, leftHand, armWeight);
    }

    private Vector3 lastRight, lastLeft;

    // ── 자세 (루트 기준: x 오른쪽, y 위, z 앞 = 플레이어 쪽) ──
    private Pose ReadyPose()
    {
        Vector3 piv = transform.TransformPoint(new Vector3(0.3f, 4.6f, 1.5f));
        Vector3 dir = transform.TransformDirection(new Vector3(-0.8f, 0.45f, 0.35f).normalized);
        return new Pose(piv, Quaternion.LookRotation(dir, transform.forward));
    }

    private Pose AimPose()
    {
        Vector3 piv = transform.TransformPoint(new Vector3(0.2f, 5.35f, 1.05f));
        Camera cam = Camera.main;
        Vector3 target = cam != null ? cam.transform.position : piv + transform.forward * 10f;
        Vector3 dir = (target - piv).normalized;
        return new Pose(piv, Quaternion.LookRotation(dir, Vector3.up));
    }

    private Pose SelfPose()
    {
        // 개머리판을 테이블 위에 대고, 총구를 턱 밑에
        Vector3 stock = transform.TransformPoint(new Vector3(0.1f, 2.28f, 6.3f));
        Vector3 muzzle = transform.TransformPoint(new Vector3(0f, 6.75f, 0.85f));
        Vector3 dir = (muzzle - stock).normalized;
        Vector3 piv = muzzle - dir * muzzleDist;
        return new Pose(piv, Quaternion.LookRotation(dir, transform.forward));
    }

    // ── 2관절 IK ──
    private void ApplyArms(Vector3 rightTarget, Vector3 leftTarget, float w)
    {
        SolveArm(body.ArmRight, rightTarget, w);
        SolveArm(body.ArmLeft, leftTarget, w);
    }

    private float ArmReach(SlumpableBody.ArmChain arm)
    {
        float scale = arm.shoulder.lossyScale.x;
        return (arm.upperLength + arm.foreLength) * scale;
    }

    private void SolveArm(SlumpableBody.ArmChain arm, Vector3 target, float w)
    {
        if (arm == null) return;
        Transform sh = arm.shoulder, el = arm.elbow;
        sh.localRotation = Quaternion.identity;
        el.localRotation = Quaternion.identity;
        if (w <= 0.001f) return;

        float scale = sh.lossyScale.x;
        float l1 = arm.upperLength * scale, l2 = arm.foreLength * scale;
        Vector3 s = sh.position;
        Vector3 toT = target - s;
        float d = toT.magnitude;
        if (d < 0.0001f) return;
        Vector3 dirT = toT / d;

        float dc = Mathf.Clamp(d, Mathf.Abs(l1 - l2) + 0.01f, l1 + l2 - 0.001f);
        float a = (l1 * l1 + dc * dc - l2 * l2) / (2f * dc);
        float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));

        // 팔꿈치는 아래 + 바깥쪽으로 굽는다
        Vector3 pole = -transform.up + transform.right * arm.side * 0.7f - transform.forward * 0.2f;
        Vector3 poleOrtho = Vector3.ProjectOnPlane(pole, dirT);
        if (poleOrtho.sqrMagnitude < 0.0001f) poleOrtho = Vector3.ProjectOnPlane(-transform.up, dirT);
        poleOrtho.Normalize();
        Vector3 elbowPos = s + dirT * a + poleOrtho * h;

        // 어깨: 쉬는 자세의 위팔 방향을 팔꿈치 목표 쪽으로
        Quaternion shRest = sh.rotation;
        Vector3 restUpper = sh.parent.TransformDirection(arm.restUpperDir);
        Quaternion shAim = Quaternion.FromToRotation(restUpper, (elbowPos - s).normalized) * shRest;
        sh.rotation = Quaternion.Slerp(shRest, shAim, w);

        // 팔꿈치: 아래팔을 손 목표 쪽으로
        Quaternion elRest = el.rotation;
        Vector3 restFore = sh.TransformDirection(arm.restForeDir);
        Quaternion elAim = Quaternion.FromToRotation(restFore, (target - el.position).normalized) * elRest;
        el.rotation = Quaternion.Slerp(elRest, elAim, w);
    }

    private static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // ── 펌프 장전 소리 (철컥) ──
    private void PlayRack()
    {
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0.4f;
        }
        AudioClip clip = rackSound != null ? rackSound : ProceduralRack();
        audioSource.PlayOneShot(clip, rackVolume);
    }

    private static AudioClip ProceduralRack()
    {
        if (proceduralRack != null) return proceduralRack;
        const int rate = 44100;
        int n = (int)(rate * 0.55f);
        var data = new float[n];
        var rng = new System.Random(7);
        void Click(float at, float pitch, float gain)
        {
            int start = (int)(at * rate);
            for (int i = 0; i < (int)(rate * 0.12f) && start + i < n; i++)
            {
                float t = (float)i / rate;
                float env = Mathf.Exp(-t * 45f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 160f);
                float ring = Mathf.Sin(2f * Mathf.PI * pitch * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * pitch * 1.73f * t) * 0.3f;
                float thump = Mathf.Sin(2f * Mathf.PI * 110f * t) * Mathf.Exp(-t * 30f);
                data[start + i] += (noise * 0.9f + ring * env * 0.5f + thump * 0.6f) * gain;
            }
        }
        Click(0.00f, 1900f, 0.8f);   // 철 (당김)
        Click(0.22f, 1500f, 1.0f);   // 컥 (밀어 넣음)
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
        proceduralRack = AudioClip.Create("ShotgunRack", n, 1, rate, false);
        proceduralRack.SetData(data, 0);
        return proceduralRack;
    }
}
