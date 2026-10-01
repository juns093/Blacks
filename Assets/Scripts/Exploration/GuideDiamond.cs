using UnityEngine;

// 길을 알려 주는 떠 있는 마름모(팔면체).
//  - 천천히 돌면서 위아래로 둥실거린다.
//  - 플레이어가 가까이 올수록 점점 작아지다가 바로 앞에서는 사라진다.
//    (지나온 마름모는 사라지고, 앞쪽 마름모만 남아 길을 이어 준다)
// 메쉬는 코드로 만들기 때문에 빈 오브젝트에 붙이기만 하면 된다.
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GuideDiamond : MonoBehaviour
{
    [SerializeField] private float size = 0.45f;
    [Tooltip("이 거리보다 멀면 원래 크기")]
    [SerializeField] private float fullSizeDistance = 9f;
    [Tooltip("이 거리 안으로 들어오면 완전히 사라짐")]
    [SerializeField] private float vanishDistance = 1.6f;
    [SerializeField] private float spinSpeed = 70f;
    [SerializeField] private float bobHeight = 0.12f;
    [SerializeField] private float bobSpeed = 2f;

    private static Mesh mesh;
    private Vector3 basePos;
    private float current = 1f;

    private void Awake()
    {
        GetComponent<MeshFilter>().sharedMesh = DiamondMesh();
        basePos = transform.localPosition;
    }

    private void OnEnable()
    {
        current = 1f;
    }

    private void Update()
    {
        float target = 1f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 d = transform.position - cam.transform.position;
            float dist = d.magnitude;
            target = Mathf.Clamp01((dist - vanishDistance) / Mathf.Max(0.01f, fullSizeDistance - vanishDistance));
        }
        current = Mathf.MoveTowards(current, target, Time.deltaTime * 3f);

        float bob = Mathf.Sin(Time.time * bobSpeed + basePos.z) * bobHeight;
        transform.localPosition = basePos + Vector3.up * bob;
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        transform.localScale = new Vector3(size, size * 1.6f, size) * current;
    }

    // 위아래로 뾰족한 팔면체 (높이 1, 폭 1)
    private static Mesh DiamondMesh()
    {
        if (mesh != null) return mesh;

        Vector3 top = new Vector3(0f, 0.5f, 0f), bottom = new Vector3(0f, -0.5f, 0f);
        Vector3[] ring =
        {
            new Vector3(0.5f, 0f, 0f), new Vector3(0f, 0f, 0.5f),
            new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, -0.5f)
        };

        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        for (int i = 0; i < 4; i++)
        {
            Vector3 a = ring[i], b = ring[(i + 1) % 4];
            int v = verts.Count;
            verts.Add(top); verts.Add(b); verts.Add(a);           // 위쪽 면 (바깥을 보게)
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            v = verts.Count;
            verts.Add(bottom); verts.Add(a); verts.Add(b);        // 아래쪽 면
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
        }

        mesh = new Mesh { name = "GuideDiamond" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
