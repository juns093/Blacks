using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

// 거리(기억 4)의 행인이 걸어 다닐 NavMesh.
// 씬에 NavMeshSurface를 붙여 두면, 처음 행인이 나올 때 한 번만 굽는다. (미리 구워 둔 데이터가 있으면 그걸 씀)
[RequireComponent(typeof(NavMeshSurface))]
public class StreetNavMesh : MonoBehaviour
{
    private static bool built;

    public static void EnsureBuilt()
    {
        if (built) return;
        built = true;

        var street = FindFirstObjectByType<StreetNavMesh>(FindObjectsInactive.Include);
        if (street == null) return;

        var surface = street.GetComponent<NavMeshSurface>();
        if (surface.navMeshData == null)
        {
            float t = Time.realtimeSinceStartup;
            surface.BuildNavMesh();
            Debug.Log($"[StreetNavMesh] 거리 NavMesh를 구웠습니다. ({(Time.realtimeSinceStartup - t) * 1000f:0}ms)");
        }
    }

    public static bool HasNavMeshNear(Vector3 p) => NavMesh.SamplePosition(p, out _, 3f, NavMesh.AllAreas);

    private void OnDestroy() => built = false;
}
