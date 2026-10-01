using System.Collections;
using Cinemachine;
using UnityEngine;

// 대사 중간에 넣는 연출 신호. 대사 줄이 '@'로 시작하면 글자로 보여 주지 않고 여기서 처리한다.
//
//   "@focus gun"       총 쪽으로 고개를 돌리고 렌즈를 당긴다. (@back이 나올 때까지 그대로)
//   "@focus opponent"  지금 맞은편에 앉은 사람
//   "@focus who"       ??? (트레일과 할 때는 옆에 서 있다)
//   "@focus trail"     트레일
//   "@focus corpse"    트레일의 시체
//   "@back"            원래 자리로 돌아온다.
//
// 그룹이 끝날 때까지 @back이 없으면 TypeWriter가 마지막에 알아서 되돌린다.
public class DialogueCues : MonoBehaviour
{
    private const float MoveDuration = 0.8f;

    private static DialogueCues instance;

    private Quaternion savedRotation;
    private float savedFov;
    private CinemachineVirtualCamera lensCam;
    private bool focused;
    private Coroutine moving;

    public static bool IsCue(string line) => !string.IsNullOrEmpty(line) && line[0] == '@';

    public static bool IsFocused => instance != null && instance.focused;

    private static DialogueCues Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("DialogueCues").AddComponent<DialogueCues>();
            return instance;
        }
    }

    /// <summary>신호 한 줄을 처리한다. 고개를 돌리는 건 기다리지 않고, 되돌리는 건 끝까지 기다린다.</summary>
    public static IEnumerator Run(string line)
    {
        string[] parts = line.Substring(1).Trim().Split(' ');
        string command = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].ToLowerInvariant() : "";

        switch (command)
        {
            case "focus":
                Instance.Focus(arg);
                yield return new WaitForSeconds(0.25f);
                break;
            case "back":
                yield return Release();
                break;
            default:
                Debug.LogWarning($"[DialogueCues] 알 수 없는 신호: {line}");
                break;
        }
    }

    public static IEnumerator Release()
    {
        if (instance == null || !instance.focused) yield break;
        yield return instance.StartCoroutine(instance.ReturnRoutine());
    }

    private void Focus(string target)
    {
        CamMove rig = CamMove.Instance;
        Camera cam = Camera.main;
        if (rig == null || cam == null) return;

        if (!FindTarget(target, out Vector3 point, out float zoom))
        {
            Debug.LogWarning($"[DialogueCues] '{target}' 대상을 찾지 못했습니다.");
            return;
        }

        if (!focused)
        {
            savedRotation = rig.transform.rotation;
            lensCam = rig.GetComponent<CinemachineVirtualCamera>();
            savedFov = lensCam != null ? lensCam.m_Lens.FieldOfView : cam.fieldOfView;
            focused = true;
        }

        Quaternion to = Quaternion.LookRotation(point - cam.transform.position, Vector3.up);
        if (moving != null) StopCoroutine(moving);
        moving = StartCoroutine(MoveRoutine(to, savedFov - zoom));
    }

    private IEnumerator ReturnRoutine()
    {
        if (moving != null) StopCoroutine(moving);
        yield return MoveRoutine(savedRotation, savedFov);
        focused = false;
        if (CamMove.Instance != null) CamMove.Instance.SyncRotation();
    }

    private IEnumerator MoveRoutine(Quaternion to, float fov)
    {
        CamMove rig = CamMove.Instance;
        if (rig == null) yield break;

        Transform t = rig.transform;
        Quaternion from = t.rotation;
        float fromFov = lensCam != null ? lensCam.m_Lens.FieldOfView : fov;

        float e = 0f;
        while (e < MoveDuration)
        {
            e += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / MoveDuration));
            CamMove.blockLook = true;
            t.rotation = Quaternion.Slerp(from, to, k);
            SetFov(Mathf.Lerp(fromFov, fov, k));
            yield return null;
        }
        t.rotation = to;
        SetFov(fov);
        moving = null;
    }

    private void SetFov(float fov)
    {
        if (lensCam == null) return;
        var lens = lensCam.m_Lens;
        lens.FieldOfView = fov;
        lensCam.m_Lens = lens;
    }

    // 바라볼 곳과 렌즈를 당길 정도(시야각을 몇 도 줄일지)
    private static bool FindTarget(string target, out Vector3 point, out float zoom)
    {
        point = Vector3.zero;
        zoom = 0f;
        OpponentPresenter presenter = OpponentPresenter.Instance;

        switch (target)
        {
            case "gun":
                GunObject gun = FindFirstObjectByType<GunObject>();
                if (gun == null) return false;
                point = BoundsOf(gun.gameObject).center;
                zoom = 22f;
                return true;

            case "opponent":
                return Head(presenter != null && presenter.CurrentBody != null ? presenter.CurrentBody.gameObject : null, 16f, out point, out zoom);
            case "who":
                return Head(presenter != null && presenter.WhoBody != null ? presenter.WhoBody.gameObject : null, 16f, out point, out zoom);
            case "trail":
                return Head(presenter != null && presenter.TrailBody != null ? presenter.TrailBody.gameObject : null, 16f, out point, out zoom);

            case "corpse":
                return Head(presenter != null && presenter.TrailBody != null ? presenter.TrailBody.gameObject : null, 14f, out point, out zoom);
        }
        return false;
    }

    private static bool Head(GameObject body, float zoomAmount, out Vector3 point, out float zoom)
    {
        point = Vector3.zero;
        zoom = zoomAmount;
        if (body == null || !body.activeInHierarchy) return false;
        Bounds b = BoundsOf(body);
        point = new Vector3(b.center.x, b.max.y - b.size.y * 0.12f, b.center.z);

        // 책상에 엎어진 몸: 머리가 책상 위에 있다. (책상 높이 = 총이 놓인 높이)
        SlumpableBody slump = body.GetComponent<SlumpableBody>();
        if (slump != null && slump.IsSlumped)
        {
            GunObject gun = FindFirstObjectByType<GunObject>();
            float tableY = gun != null ? BoundsOf(gun.gameObject).center.y : b.center.y;
            Vector3 head = body.transform.position + body.transform.forward * 1.2f;
            point = new Vector3(head.x, tableY + 0.6f, head.z);
        }
        return true;
    }

    private static Bounds BoundsOf(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
