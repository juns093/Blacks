using System.Collections;
using UnityEngine;

// 뼈대가 없는 통짜 모델(LowPolyHuman2 등)을 "책상에 머리를 박으며 쓰러지게" 만든다.
//
// 처음 쓸 때 메쉬를 허리 높이에서 상체/하체 두 조각으로 나눈다.
//  - 하체는 그대로 서 있고, 상체만 허리를 축으로 앞으로 꺾인다.
//  - 앞으로 꺾이다가 머리가 책상(콜라이더)에 닿는 각도에서 멈추며 쿵 소리를 낸다.
// 이 컴포넌트는 모델의 루트(발밑, 앞을 보는 방향 = forward)에 붙인다.
public class SlumpableBody : MonoBehaviour
{
    [Tooltip("키에서 허리(꺾이는 축)가 있는 높이 비율")]
    [Range(0.3f, 0.7f)] [SerializeField] private float hipRatio = 0.5f;

    [Tooltip("머리가 책상을 못 찾았을 때 꺾을 최대 각도")]
    [SerializeField] private float maxBendAngle = 105f;

    [Tooltip("머리 위치 보정: 키 대비 얼굴이 앞으로 나온 정도")]
    [SerializeField] private float faceForwardRatio = 0.06f;

    [SerializeField] private float slamDuration = 0.32f;
    [SerializeField] private AudioClip slamSound;
    [Range(0f, 1f)] [SerializeField] private float slamVolume = 1f;

    private Transform hipPivot;
    private Transform upper;
    private float height;
    private float hipHeight;
    private bool built;
    private float slumpedAngle = -1f;

    public bool IsSlumped { get; private set; }

    /// <summary>똑바로 선 자세로 되돌린다.</summary>
    public void ResetPose()
    {
        Build();
        if (hipPivot != null) hipPivot.localRotation = Quaternion.identity;
        slumpedAngle = -1f; // 자리가 바뀌었을 수 있으니 책상 높이를 다시 잰다
        IsSlumped = false;
    }

    /// <summary>연출 없이 바로 엎어진 자세로. (시체)</summary>
    public void SetSlumpedInstant()
    {
        Build();
        if (hipPivot == null) return;
        hipPivot.localRotation = Quaternion.identity;
        slumpedAngle = -1f;
        hipPivot.localRotation = Quaternion.Euler(FindSlamAngle(), 0f, 0f);
        IsSlumped = true;
    }

    /// <summary>앞으로 확 꺾이며 책상에 머리를 박는다.</summary>
    public IEnumerator Slam()
    {
        Build();
        if (hipPivot == null) yield break;

        float target = FindSlamAngle();

        // 1) 힘이 빠지며 살짝 뒤로 젖혀졌다가
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            hipPivot.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -6f, Mathf.SmoothStep(0f, 1f, t / 0.18f)), 0f, 0f);
            yield return null;
        }

        // 2) 앞으로 떨어지듯 가속하며 꺾인다 (쿵)
        t = 0f;
        while (t < slamDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / slamDuration);
            hipPivot.localRotation = Quaternion.Euler(Mathf.Lerp(-6f, target, k * k), 0f, 0f);
            yield return null;
        }
        hipPivot.localRotation = Quaternion.Euler(target, 0f, 0f);
        PlaySlam();

        // 3) 부딪힌 반동으로 살짝 튀었다가 가라앉는다
        t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float bounce = Mathf.Sin(Mathf.Clamp01(t / 0.35f) * Mathf.PI) * 5f;
            hipPivot.localRotation = Quaternion.Euler(target - bounce, 0f, 0f);
            yield return null;
        }
        hipPivot.localRotation = Quaternion.Euler(target, 0f, 0f);
        IsSlumped = true;
    }

    private void PlaySlam()
    {
        var src = GetComponent<AudioSource>();
        if (src == null)
        {
            src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0.6f;
        }
        src.PlayOneShot(slamSound != null ? slamSound : ProceduralSfx.DoorThud(), slamVolume);
    }

    // 머리가 책상에 닿는 각도를 찾는다. (허리 기준으로 1도씩 돌려 보며 아래의 콜라이더와 비교)
    private float FindSlamAngle()
    {
        if (slumpedAngle > 0f) return slumpedAngle;

        float headDist = height - hipHeight;
        Vector3 hipWorld = hipPivot.position;
        float headRadius = height * 0.07f;

        for (float a = 20f; a <= maxBendAngle; a += 1f)
        {
            Quaternion q = transform.rotation * Quaternion.Euler(a, 0f, 0f);
            Vector3 head = hipWorld + q * new Vector3(0f, headDist * 0.92f, height * faceForwardRatio);
            if (Physics.Raycast(head + Vector3.up * height, Vector3.down, out RaycastHit hit, height * 3f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(transform)
                && head.y - headRadius <= hit.point.y)
            {
                slumpedAngle = a;
                return a;
            }
        }
        slumpedAngle = maxBendAngle;
        return slumpedAngle;
    }

    // 메쉬를 허리 높이에서 둘로 나눈다. (한 번만)
    private void Build()
    {
        if (built) return;
        built = true;

        var filter = GetComponentInChildren<MeshFilter>();
        var renderer = filter != null ? filter.GetComponent<MeshRenderer>() : null;
        if (filter == null || renderer == null || filter.sharedMesh == null)
        {
            Debug.LogWarning($"[SlumpableBody] '{name}'에 나눌 메쉬가 없습니다.");
            return;
        }

        // 루트 기준 높이(발 = 0)
        Bounds b = renderer.bounds;
        float feetY = transform.position.y;
        height = b.max.y - feetY;
        hipHeight = height * hipRatio;
        float hipWorldY = feetY + hipHeight;

        hipPivot = new GameObject("HipPivot").transform;
        hipPivot.SetParent(transform, false);
        hipPivot.localPosition = new Vector3(0f, hipHeight / Mathf.Max(0.0001f, transform.lossyScale.y), 0f);
        hipPivot.localRotation = Quaternion.identity;

        Mesh src = filter.sharedMesh;
        Vector3[] v = src.vertices;
        Vector3[] n = src.normals;
        Vector2[] uv = src.uv;
        Transform ft = filter.transform;

        var upperTris = new System.Collections.Generic.List<int>();
        var lowerTris = new System.Collections.Generic.List<int>();
        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tris = src.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                float cy = (ft.TransformPoint(v[tris[i]]).y + ft.TransformPoint(v[tris[i + 1]]).y + ft.TransformPoint(v[tris[i + 2]]).y) / 3f;
                var list = cy > hipWorldY ? upperTris : lowerTris;
                list.Add(tris[i]); list.Add(tris[i + 1]); list.Add(tris[i + 2]);
            }
        }

        Mesh MakePart(System.Collections.Generic.List<int> tris, string partName)
        {
            var m = new Mesh { name = src.name + "_" + partName };
            m.indexFormat = src.indexFormat;
            m.vertices = v;
            if (n != null && n.Length == v.Length) m.normals = n;
            if (uv != null && uv.Length == v.Length) m.uv = uv;
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            if (n == null || n.Length != v.Length) m.RecalculateNormals();
            return m;
        }

        // 하체: 원래 오브젝트의 메쉬를 교체
        Material mat = renderer.sharedMaterial;
        filter.sharedMesh = MakePart(lowerTris, "Lower");

        // 상체: 허리 축 아래에 같은 자리/회전/크기로 복제
        var up = new GameObject("Upper");
        upper = up.transform;
        upper.SetParent(hipPivot, true);
        upper.SetPositionAndRotation(ft.position, ft.rotation);
        upper.localScale = Vector3.one;
        SetWorldScale(upper, ft.lossyScale);
        up.AddComponent<MeshFilter>().sharedMesh = MakePart(upperTris, "Upper");
        up.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void SetWorldScale(Transform t, Vector3 world)
    {
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
    }
}
