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
        Debug.Log($"[SlumpableBody] {name} 쓰러짐: 허리 {target:0}도까지 꺾음");

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
            // 위에서 아래로 쏴서 "허리보다 낮은" 면 중 가장 높은 것 = 책상. (딜러 조준용 큰 콜라이더 등은 무시)
            float surface = float.NegativeInfinity;
            foreach (RaycastHit hit in Physics.RaycastAll(head + Vector3.up * height, Vector3.down, height * 3f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (hit.point.y >= hipWorld.y) continue;
                if (hit.point.y > surface) surface = hit.point.y;
            }
            if (head.y - headRadius <= surface)
            {
                slumpedAngle = a;
                return a;
            }
        }
        slumpedAngle = maxBendAngle;
        return slumpedAngle;
    }

    // ── 팔 (관절 인형 모델은 어깨/위팔/팔꿈치/아래팔/손이 따로 떨어진 조각이라 관절째로 움직일 수 있다) ──
    public class ArmChain
    {
        public Transform shoulder;     // 어깨 관절 (위팔이 매달림)
        public Transform elbow;        // 팔꿈치 관절 (아래팔 + 손이 매달림)
        public Vector3 restUpperDir;   // 쉬는 자세에서 어깨→팔꿈치 방향 (어깨 부모 공간)
        public Vector3 restForeDir;    // 쉬는 자세에서 팔꿈치→손 방향 (팔꿈치 부모 공간)
        public float upperLength;      // 메쉬 단위
        public float foreLength;
        public float side;             // 루트 기준 +1 = 오른쪽(root.right), -1 = 왼쪽
    }

    public ArmChain ArmRight { get; private set; }
    public ArmChain ArmLeft { get; private set; }

    /// <summary>팔/허리 조각 나누기를 지금 해 둔다. (한 번만)</summary>
    public void EnsureBuilt() => Build();

    // 메쉬를 허리 높이에서 둘로 나누고, 팔은 따로 떼어 관절에 매단다. (한 번만)
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

        // 붙어 있는 조각(섬)끼리 묶는다. 같은 자리의 정점은 하나로 본다.
        int[] island = FindIslands(src, v);
        var islandBounds = new System.Collections.Generic.Dictionary<int, Bounds>();
        for (int i = 0; i < v.Length; i++)
        {
            int k = island[i];
            if (islandBounds.TryGetValue(k, out Bounds ib)) { ib.Encapsulate(v[i]); islandBounds[k] = ib; }
            else islandBounds[k] = new Bounds(v[i], Vector3.zero);
        }

        // 팔 조각: 몸통 바깥(|x| > 0.36)에 있고 다리보다 위에 있는 섬
        float meshHeight = src.bounds.size.y;
        float sideX = src.bounds.extents.x * 0.65f;
        float armBottom = src.bounds.min.y + meshHeight * 0.42f;
        float elbowSplit = src.bounds.min.y + meshHeight * 0.66f;
        int ArmGroup(int isl)   // 0 = 팔 아님, 1/2 = +x 위팔/아래팔, 3/4 = -x 위팔/아래팔
        {
            Bounds ib = islandBounds[isl];
            if (Mathf.Abs(ib.center.x) < sideX || ib.center.y < armBottom || ib.max.y > src.bounds.max.y - meshHeight * 0.12f) return 0;
            bool upperArm = ib.center.y > elbowSplit;
            return ib.center.x > 0 ? (upperArm ? 1 : 2) : (upperArm ? 3 : 4);
        }

        var upperTris = new System.Collections.Generic.List<int>();
        var lowerTris = new System.Collections.Generic.List<int>();
        var armTris = new System.Collections.Generic.List<int>[5];
        for (int g = 1; g < 5; g++) armTris[g] = new System.Collections.Generic.List<int>();
        var groupBounds = new Bounds[5];
        var groupHas = new bool[5];

        for (int s = 0; s < src.subMeshCount; s++)
        {
            int[] tris = src.GetTriangles(s);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int g = ArmGroup(island[tris[i]]);
                if (g > 0)
                {
                    armTris[g].Add(tris[i]); armTris[g].Add(tris[i + 1]); armTris[g].Add(tris[i + 2]);
                    for (int k = 0; k < 3; k++)
                    {
                        if (!groupHas[g]) { groupBounds[g] = new Bounds(v[tris[i + k]], Vector3.zero); groupHas[g] = true; }
                        else groupBounds[g].Encapsulate(v[tris[i + k]]);
                    }
                    continue;
                }
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

        // 팔: 상체 아래 어깨 → 팔꿈치 관절로 매단다 (메쉬 좌표 그대로)
        ArmChain MakeArm(int upperGroup, int foreGroup, string sideName)
        {
            if (!groupHas[upperGroup] || !groupHas[foreGroup]) return null;
            Bounds ub = groupBounds[upperGroup], fb = groupBounds[foreGroup];
            Vector3 shoulderPos = new Vector3(ub.center.x, ub.max.y - (ub.size.x * 0.5f), ub.center.z);
            Vector3 elbowPos = new Vector3(fb.center.x, fb.max.y - (fb.size.x * 0.35f), fb.center.z);
            Vector3 handPos = new Vector3(fb.center.x, fb.min.y + fb.size.y * 0.08f, fb.center.z);

            var shoulder = new GameObject("Shoulder_" + sideName).transform;
            shoulder.SetParent(upper, false);
            shoulder.localPosition = shoulderPos;
            var upperMesh = new GameObject("UpperArm_" + sideName);
            upperMesh.transform.SetParent(shoulder, false);
            upperMesh.transform.localPosition = -shoulderPos;
            upperMesh.AddComponent<MeshFilter>().sharedMesh = MakePart(armTris[upperGroup], "UpperArm_" + sideName);
            upperMesh.AddComponent<MeshRenderer>().sharedMaterial = mat;

            var elbow = new GameObject("Elbow_" + sideName).transform;
            elbow.SetParent(shoulder, false);
            elbow.localPosition = elbowPos - shoulderPos;
            var foreMesh = new GameObject("ForeArm_" + sideName);
            foreMesh.transform.SetParent(elbow, false);
            foreMesh.transform.localPosition = -elbowPos;
            foreMesh.AddComponent<MeshFilter>().sharedMesh = MakePart(armTris[foreGroup], "ForeArm_" + sideName);
            foreMesh.AddComponent<MeshRenderer>().sharedMaterial = mat;

            float side = Mathf.Sign(Vector3.Dot(upper.TransformPoint(shoulderPos) - transform.position, transform.right));
            return new ArmChain
            {
                shoulder = shoulder,
                elbow = elbow,
                restUpperDir = (elbowPos - shoulderPos).normalized,
                restForeDir = (handPos - elbowPos).normalized,
                upperLength = (elbowPos - shoulderPos).magnitude,
                foreLength = (handPos - elbowPos).magnitude,
                side = side
            };
        }

        ArmChain a = MakeArm(1, 2, "A");
        ArmChain c = MakeArm(3, 4, "B");
        if (a != null && c != null)
        {
            ArmRight = a.side > 0 ? a : c;
            ArmLeft = a.side > 0 ? c : a;
        }
    }

    // 같은 위치의 정점을 하나로 보고, 삼각형으로 이어진 정점끼리 같은 번호를 준다.
    private static int[] FindIslands(Mesh mesh, Vector3[] v)
    {
        var weld = new System.Collections.Generic.Dictionary<Vector3Int, int>();
        int[] id = new int[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            var key = new Vector3Int(Mathf.RoundToInt(v[i].x * 1000f), Mathf.RoundToInt(v[i].y * 1000f), Mathf.RoundToInt(v[i].z * 1000f));
            if (!weld.TryGetValue(key, out int w)) { w = weld.Count; weld[key] = w; }
            id[i] = w;
        }
        int[] parent = new int[weld.Count];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;
        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            int[] t = mesh.GetTriangles(s);
            for (int i = 0; i < t.Length; i += 3)
            {
                int r0 = Find(id[t[i]]);
                parent[Find(id[t[i + 1]])] = r0;
                parent[Find(id[t[i + 2]])] = r0;
            }
        }
        int[] result = new int[v.Length];
        for (int i = 0; i < v.Length; i++) result[i] = Find(id[i]);
        return result;
    }

    private static void SetWorldScale(Transform t, Vector3 world)
    {
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(world.x / parent.x, world.y / parent.y, world.z / parent.z);
    }
}
