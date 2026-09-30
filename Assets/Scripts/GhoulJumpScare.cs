using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.UI;

// 회상 탐색 구간에서 한 번 튀어나오는 구울 점프 스퀘어.
//
// 플레이어가 triggerPoint 근처에 들어오면:
//  1) 눈앞에 구울이 나타나고 비명 소리
//  2) 시선이 구울 얼굴로 확 꺾이고 화면이 흔들림 (공격 모션)
//  3) 순간 암전 → 구울이 사라지고 다시 밝아짐 → 조작 복귀
// 탐색 구간(FreeRoamMovement 조작)이 새로 시작될 때마다 다시 한 번 나올 수 있습니다.
public class GhoulJumpScare : MonoBehaviour
{
    [Header("구울")]
    [Tooltip("튀어나올 구울 모델 (SK_Ghoul.fbx)")]
    [SerializeField] private GameObject ghoulPrefab;

    [Tooltip("튀어나올 때 재생할 공격 모션")]
    [SerializeField] private AnimationClip attackClip;

    [Tooltip("구울 키(m). 모델 크기를 여기에 맞춥니다.")]
    [SerializeField] private float ghoulHeight = 2.0f;

    [Tooltip("켜면 구울 얼굴이 플레이어 눈높이에 오도록 키를 맞춥니다. (ghoulHeight 대신)")]
    [SerializeField] private bool matchPlayerEyeLevel = true;

    [Tooltip("키에서 얼굴(눈)이 있는 높이 비율")]
    [Range(0.5f, 1f)] [SerializeField] private float faceHeightRatio = 0.9f;

    [Tooltip("모델 앞쪽이 플레이어를 보도록 돌리는 보정 각도")]
    [SerializeField] private float modelYawOffset = 0f;

    [Tooltip("플레이어 눈앞 몇 m에 나타날지")]
    [SerializeField] private float spawnDistance = 1.3f;

    [Tooltip("나타난 뒤 플레이어 쪽으로 달려드는 거리(m)")]
    [SerializeField] private float lungeDistance = 0.45f;

    [Header("발동 위치")]
    [SerializeField] private Transform triggerPoint;
    [SerializeField] private float triggerRadius = 3f;

    [Header("연출")]
    [SerializeField] private AudioClip scareSound;
    [Range(0f, 1f)] [SerializeField] private float soundVolume = 1f;

    [Tooltip("소리를 이 시간(초)만 쓰고 줄여서 끕니다. 0이면 끝까지")]
    [SerializeField] private float soundCutAfter = 2.2f;

    [Tooltip("시선이 구울 쪽으로 꺾이는 시간(초)")]
    [SerializeField] private float lookSnapDuration = 0.12f;

    [Tooltip("구울이 눈앞에 머무는 시간(초)")]
    [SerializeField] private float holdDuration = 1.3f;

    [Tooltip("화면 흔들림 세기(도)")]
    [SerializeField] private float shakeStrength = 2.5f;

    [SerializeField] private Color faceLightColor = new Color(1f, 0.55f, 0.45f);
    [SerializeField] private float faceLightIntensity = 4f;

    [Tooltip("암전 후 다시 밝아지는 시간(초)")]
    [SerializeField] private float recoverFadeDuration = 0.6f;

    [Header("미리 경고 (설정에서 켰을 때만)")]
    [SerializeField] private string warningText = "[주의] 곧 깜짝 놀랄 수 있는 장면이 나옵니다";
    [Tooltip("경고가 뜬 뒤 구울이 나오기까지의 시간(초)")]
    [SerializeField] private float warningLead = 1.5f;

    private FreeRoamMovement player;
    private bool armed = true;
    private bool playing = false;

    private GameObject blackCanvas;
    private Image blackImage;
    private Text warningLabel;
    private AudioSource audioSource;

    private void Update()
    {
        if (playing) return;

        if (player == null)
            player = FindFirstObjectByType<FreeRoamMovement>(FindObjectsInactive.Include);
        if (player == null) return;

        // 탐색 구간이 끝나면 다시 장전. 다음 회차에 또 한 번 나온다.
        if (!player.isActiveAndEnabled || !player.IsControlling)
        {
            armed = true;
            return;
        }

        if (!armed || player.InputPaused || triggerPoint == null) return;

        Vector3 a = player.transform.position, b = triggerPoint.position;
        a.y = 0f; b.y = 0f;
        if (Vector3.Distance(a, b) <= triggerRadius)
        {
            armed = false;
            StartCoroutine(ScareRoutine());
        }
    }

    private IEnumerator ScareRoutine()
    {
        playing = true;

        // 설정에서 경고를 켰으면, 나오기 전에 잠깐 알려준다. (그동안은 계속 움직일 수 있다)
        if (SettingsManager.WarnBeforeJumpScare && warningLead > 0f)
        {
            ShowWarning(true);
            yield return new WaitForSeconds(warningLead);
            ShowWarning(false);
        }

        player.InputPaused = true;
        Debug.Log("[GhoulJumpScare] 구울 등장!");

        Transform head = player.Head;
        Vector3 fwd = head.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = player.transform.forward;
        fwd.Normalize();

        // ── 1) 눈앞에 구울 ──
        Vector3 spawn = head.position + fwd * spawnDistance;
        float floorY = player.transform.position.y;
        if (Physics.Raycast(spawn + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            floorY = hit.point.y;
        spawn.y = floorY;

        // 얼굴이 플레이어 눈높이에 오도록 키를 정한다.
        float wantHeight = ghoulHeight;
        if (matchPlayerEyeLevel)
            wantHeight = Mathf.Max(1.2f, (head.position.y - floorY) / faceHeightRatio);

        GameObject ghoul = SpawnGhoul(spawn, -fwd, wantHeight, out PlayableGraph graph, out float height);

        var lightGo = new GameObject("GhoulFaceLight");
        lightGo.transform.SetParent(ghoul.transform, false);
        lightGo.transform.position = spawn + Vector3.up * height * faceHeightRatio - fwd * 0.6f;
        var faceLight = lightGo.AddComponent<Light>();
        faceLight.type = LightType.Point;
        faceLight.range = 4f;
        faceLight.color = faceLightColor;
        faceLight.intensity = faceLightIntensity;
        faceLight.shadows = LightShadows.None;

        AudioSource src = EnsureAudio();
        if (scareSound != null) { src.volume = soundVolume; src.clip = scareSound; src.Play(); }

        // ── 2) 시선이 얼굴로 꺾이고, 흔들리며 달려든다 ──
        Vector3 face = spawn + Vector3.up * height * faceHeightRatio;
        player.GetLook(out float fromYaw, out float fromPitch);
        Vector3 dir = face - head.position;
        float toYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float toPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
        toYaw = fromYaw + Mathf.DeltaAngle(fromYaw, toYaw);

        float t = 0f;
        while (t < lookSnapDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / lookSnapDuration);
            player.SetLook(Mathf.Lerp(fromYaw, toYaw, k), Mathf.Lerp(fromPitch, toPitch, k));
            yield return null;
        }
        player.SetLook(toYaw, toPitch);

        Vector3 start = ghoul.transform.position;
        Vector3 end = start - fwd * lungeDistance;
        t = 0f;
        while (t < holdDuration)
        {
            t += Time.deltaTime;
            float k = t / holdDuration;
            ghoul.transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 1.5f)));
            float s = shakeStrength * (1f - k * 0.5f);
            player.CameraShakeEuler = new Vector3(Random.Range(-s, s), Random.Range(-s, s), Random.Range(-s, s) * 0.5f);
            faceLight.intensity = faceLightIntensity * (0.6f + Random.value * 0.6f);
            yield return null;
        }

        // ── 3) 순간 암전 → 사라짐 → 다시 밝아짐 ──
        SetBlack(1f);
        player.CameraShakeEuler = Vector3.zero;
        if (graph.IsValid()) graph.Destroy();
        Destroy(ghoul);

        StartCoroutine(FadeOutSound(src));
        yield return new WaitForSeconds(0.45f);

        t = 0f;
        while (t < recoverFadeDuration)
        {
            t += Time.deltaTime;
            SetBlack(1f - t / recoverFadeDuration);
            yield return null;
        }
        SetBlack(0f);

        player.InputPaused = false;
        playing = false;
    }

    private GameObject SpawnGhoul(Vector3 feet, Vector3 facing, float wantHeight, out PlayableGraph graph, out float height)
    {
        graph = default;
        GameObject ghoul = ghoulPrefab != null
            ? Instantiate(ghoulPrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ghoul.name = "JumpScareGhoul";
        foreach (var c in ghoul.GetComponentsInChildren<Collider>()) Destroy(c);

        ghoul.transform.rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(0f, modelYawOffset, 0f);
        ghoul.transform.position = feet;

        // 공격 모션의 첫 프레임 자세로 크기를 잰다.
        var animator = ghoul.GetComponentInChildren<Animator>();
        if (animator == null && attackClip != null) animator = ghoul.AddComponent<Animator>();
        if (animator != null && attackClip != null)
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            AnimationPlayableUtilities.PlayClip(animator, attackClip, out graph);
            graph.Evaluate(0f);
        }

        foreach (var smr in ghoul.GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;

        Bounds b = GetBounds(ghoul);
        if (b.size.y > 0.01f)
            ghoul.transform.localScale *= wantHeight / b.size.y;

        b = GetBounds(ghoul);
        ghoul.transform.position += new Vector3(feet.x - b.center.x, feet.y - b.min.y, feet.z - b.center.z);

        height = wantHeight;
        return ghoul;
    }

    private static Bounds GetBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    private IEnumerator FadeOutSound(AudioSource src)
    {
        if (src == null || !src.isPlaying || soundCutAfter <= 0f) yield break;

        float wait = soundCutAfter - (lookSnapDuration + holdDuration);
        if (wait > 0f) yield return new WaitForSeconds(wait);

        float from = src.volume;
        float t = 0f;
        while (t < 0.4f && src.isPlaying)
        {
            t += Time.deltaTime;
            src.volume = Mathf.Lerp(from, 0f, t / 0.4f);
            yield return null;
        }
        src.Stop();
        src.volume = soundVolume;
    }

    private AudioSource EnsureAudio()
    {
        if (audioSource != null) return audioSource;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        return audioSource;
    }

    private void ShowWarning(bool show)
    {
        SetBlack(0f); // 캔버스 준비
        if (warningLabel == null)
        {
            var go = new GameObject("Warning", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(blackCanvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.78f);
            rt.anchorMax = new Vector2(1f, 0.9f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            warningLabel = go.GetComponent<Text>();
            warningLabel.font = UIFontUtil.Resolve(null);
            warningLabel.fontSize = 30;
            warningLabel.alignment = TextAnchor.MiddleCenter;
            warningLabel.color = new Color(1f, 0.35f, 0.3f, 1f);
            warningLabel.raycastTarget = false;
        }

        warningLabel.text = warningText;
        warningLabel.gameObject.SetActive(show);
        blackCanvas.SetActive(show || blackImage.color.a > 0f);
    }

    private void SetBlack(float a)
    {
        if (blackImage == null)
        {
            blackCanvas = new GameObject("JumpScareBlack", typeof(Canvas));
            blackCanvas.transform.SetParent(transform, false);
            var canvas = blackCanvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 490;

            var img = new GameObject("Black", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(blackCanvas.transform, false);
            var rt = (RectTransform)img.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            blackImage = img.GetComponent<Image>();
            blackImage.raycastTarget = false;
        }

        blackImage.color = new Color(0f, 0f, 0f, a);
        bool warningShown = warningLabel != null && warningLabel.gameObject.activeSelf;
        blackCanvas.SetActive(a > 0f || warningShown);
    }
}
