using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 노크 소리로 시작하는 점프 스퀘어. (구울 없음)
//
// Arm()으로 준비된 뒤 플레이어가 triggerPoint 근처에 오면:
//  1) 닫힌 칸막이 문 안에서 "똑 똑" → 잠시 뒤 더 크게 "쾅 쾅 쾅"
//  2) 몸이 굳고 시선이 천천히 그 문으로 돌아간다. 불이 깜빡인다.
//  3) 정적 → 문이 확 열리며 비명 + 불이 나가고 화면이 흔들림 (그림자가 순간 보였다 사라짐)
//  4) 암전 → 다시 밝아지면 칸은 비어 있다 → 조작 복귀
public class KnockJumpScare : MonoBehaviour
{
    [Header("발동")]
    [SerializeField] private Transform triggerPoint;
    [SerializeField] private float triggerRadius = 1.8f;

    [Header("무대")]
    [Tooltip("노크가 들리고 확 열릴 문")]
    [SerializeField] private SwingDoor door;
    [Tooltip("깜빡이고 꺼질 조명")]
    [SerializeField] private Light[] roomLights;
    [Tooltip("문이 열릴 때 순간 보이는 그림자 (없어도 됨)")]
    [SerializeField] private GameObject figure;

    [Header("소리")]
    [SerializeField] private AudioClip knockClip;
    [SerializeField] private AudioClip flickerClip;
    [SerializeField] private AudioClip scareClip;
    [Range(0f, 1f)] [SerializeField] private float scareVolume = 1f;
    [SerializeField] private float scareSoundLength = 2.2f;

    [Header("연출")]
    [SerializeField] private float lookTurnDuration = 1.4f;
    [SerializeField] private float shakeStrength = 3f;

    [Header("미리 경고 (설정에서 켰을 때만)")]
    [SerializeField] private string warningText = "[주의] 곧 깜짝 놀랄 수 있는 장면이 나옵니다";
    [SerializeField] private float warningLead = 1.5f;

    public bool Done { get; private set; }
    public bool Armed { get; private set; }

    private FreeRoamMovement player;
    private bool playing;
    private float[] baseIntensity;
    private AudioSource knockSource;
    private AudioSource screenSource;
    private GameObject overlay;
    private Image black;
    private Text warningLabel;

    /// <summary>탐색이 시작될 때 처음 상태로.</summary>
    public void ResetState()
    {
        StopAllCoroutines();
        Armed = false;
        Done = false;
        playing = false;
        if (door != null) door.ResetState();
        if (figure != null) figure.SetActive(false);
        RestoreLights();
        SetBlack(0f, false);
    }

    public void Arm()
    {
        if (Done) return;
        Armed = true;
    }

    private void Awake()
    {
        if (figure != null) figure.SetActive(false);
        if (roomLights != null)
        {
            baseIntensity = new float[roomLights.Length];
            for (int i = 0; i < roomLights.Length; i++)
                baseIntensity[i] = roomLights[i] != null ? roomLights[i].intensity : 0f;
        }
    }

    private void Update()
    {
        if (!Armed || playing || Done || triggerPoint == null) return;

        if (player == null) player = FindFirstObjectByType<FreeRoamMovement>(FindObjectsInactive.Include);
        if (player == null || !player.IsControlling || player.InputPaused) return;

        Vector3 a = player.transform.position, b = triggerPoint.position;
        a.y = 0f; b.y = 0f;
        if (Vector3.Distance(a, b) <= triggerRadius)
            StartCoroutine(ScareRoutine());
    }

    private IEnumerator ScareRoutine()
    {
        playing = true;
        Armed = false;
        Debug.Log("[KnockJumpScare] 노크 시작");

        // 1) 가볍게 "똑 똑" (아직 움직일 수 있다)
        Knock(0.55f, 1f);
        yield return new WaitForSeconds(1.7f);

        if (SettingsManager.WarnBeforeJumpScare && warningLead > 0f)
        {
            ShowWarning(true);
            yield return new WaitForSeconds(warningLead);
            ShowWarning(false);
        }

        // 2) 몸이 굳는다. 더 세게 "쾅 쾅 쾅" 하면서 시선이 문으로 돌아간다.
        player.InputPaused = true;
        Coroutine turn = StartCoroutine(TurnToDoor());
        for (int i = 0; i < 3; i++)
        {
            Knock(1f, 0.82f + i * 0.04f);
            yield return new WaitForSeconds(0.42f);
        }
        yield return turn;

        // 불이 깜빡이다가...
        PlayScreen(flickerClip, 0.8f);
        float t = 0f;
        while (t < 1.3f)
        {
            t += Time.deltaTime;
            SetLights(Random.value < 0.35f ? 0.05f : Random.Range(0.4f, 1f));
            yield return null;
        }
        SetLights(1f);

        // 정적
        if (screenSource != null) screenSource.Stop();
        yield return new WaitForSeconds(0.9f);

        // 3) 문이 확 열리며 비명 + 불이 나감
        Debug.Log("[KnockJumpScare] 문이 열린다!");
        if (door != null) door.SlamOpen(0.12f);
        PlayScreen(scareClip, scareVolume);
        SetLights(0f);

        Light red = null;
        if (figure != null)
        {
            figure.SetActive(true);
            var lightGo = new GameObject("ScareRedLight");
            lightGo.transform.SetParent(figure.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.9f, 0.9f);
            red = lightGo.AddComponent<Light>();
            red.type = LightType.Point;
            red.color = new Color(1f, 0.15f, 0.1f);
            red.range = 5f;
            red.intensity = 6f;
            red.shadows = LightShadows.None;
        }

        t = 0f;
        Vector3 figureStart = figure != null ? figure.transform.position : Vector3.zero;
        Vector3 toPlayer = figure != null ? (player.Head.position - figureStart) : Vector3.zero;
        toPlayer.y = 0f;
        while (t < 0.75f)
        {
            t += Time.deltaTime;
            float s = shakeStrength * (1f - t / 0.75f * 0.4f);
            player.CameraShakeEuler = new Vector3(Random.Range(-s, s), Random.Range(-s, s), Random.Range(-s, s) * 0.5f);
            if (red != null) red.intensity = Random.value < 0.3f ? 0f : 6f;
            if (figure != null)
                figure.transform.position = figureStart + toPlayer.normalized * Mathf.Min(toPlayer.magnitude - 0.9f, 1.6f) * Mathf.SmoothStep(0f, 1f, t / 0.75f);
            yield return null;
        }

        // 4) 암전 → 칸은 비어 있다
        SetBlack(1f, true);
        player.CameraShakeEuler = Vector3.zero;
        if (red != null) Destroy(red.gameObject);
        if (figure != null) { figure.transform.position = figureStart; figure.SetActive(false); }
        StartCoroutine(FadeOutScreen(scareSoundLength - 0.75f));
        yield return new WaitForSeconds(0.8f);

        RestoreLights();
        t = 0f;
        while (t < 0.7f)
        {
            t += Time.deltaTime;
            SetBlack(1f - t / 0.7f, true);
            yield return null;
        }
        SetBlack(0f, false);

        player.InputPaused = false;
        playing = false;
        Done = true;
    }

    private IEnumerator TurnToDoor()
    {
        if (door == null) yield break;
        Vector3 target = door.transform.position;
        var r = door.GetComponentInChildren<Renderer>();
        if (r != null) target = r.bounds.center;

        Vector3 dir = target - player.Head.position;
        player.GetLook(out float fromYaw, out float fromPitch);
        float toYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        float toPitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
        toYaw = fromYaw + Mathf.DeltaAngle(fromYaw, toYaw);

        float t = 0f;
        while (t < lookTurnDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / lookTurnDuration);
            player.SetLook(Mathf.Lerp(fromYaw, toYaw, k), Mathf.Lerp(fromPitch, toPitch, k));
            yield return null;
        }
    }

    private void Knock(float volume, float pitch)
    {
        if (knockClip == null) return;
        if (knockSource == null)
        {
            knockSource = (door != null ? door.gameObject : gameObject).AddComponent<AudioSource>();
            knockSource.playOnAwake = false;
            knockSource.spatialBlend = 0.9f;
            knockSource.minDistance = 2f;
            knockSource.maxDistance = 20f;
            knockSource.rolloffMode = AudioRolloffMode.Linear;
        }
        knockSource.pitch = pitch;
        knockSource.PlayOneShot(knockClip, volume);
    }

    private void PlayScreen(AudioClip clip, float volume)
    {
        if (clip == null) return;
        if (screenSource == null)
        {
            screenSource = gameObject.AddComponent<AudioSource>();
            screenSource.playOnAwake = false;
            screenSource.spatialBlend = 0f;
        }
        screenSource.Stop();
        screenSource.clip = clip;
        screenSource.volume = volume;
        screenSource.Play();
    }

    private IEnumerator FadeOutScreen(float wait)
    {
        if (screenSource == null) yield break;
        if (wait > 0f) yield return new WaitForSeconds(wait);
        float from = screenSource.volume, t = 0f;
        while (t < 0.5f && screenSource.isPlaying)
        {
            t += Time.deltaTime;
            screenSource.volume = Mathf.Lerp(from, 0f, t / 0.5f);
            yield return null;
        }
        screenSource.Stop();
    }

    private void SetLights(float k)
    {
        if (roomLights == null || baseIntensity == null) return;
        for (int i = 0; i < roomLights.Length; i++)
            if (roomLights[i] != null) roomLights[i].intensity = baseIntensity[i] * k;
    }

    private void RestoreLights() => SetLights(1f);

    private void EnsureOverlay()
    {
        if (overlay != null) return;
        overlay = UIBuild.OverlayCanvas("KnockScareOverlay", transform, 490);
        black = UIBuild.Image("Black", overlay.transform, Color.black);
        UIBuild.Stretch(black.rectTransform);

        warningLabel = UIBuild.Text("Warning", overlay.transform, UIFontUtil.Resolve(null), 30, new Color(1f, 0.35f, 0.3f, 1f), TextAnchor.MiddleCenter);
        var rt = warningLabel.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.78f); rt.anchorMax = new Vector2(1f, 0.9f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        warningLabel.gameObject.SetActive(false);
    }

    private void SetBlack(float a, bool show)
    {
        if (overlay == null && !show) return;
        EnsureOverlay();
        black.color = new Color(0f, 0f, 0f, a);
        overlay.SetActive(show || warningLabel.gameObject.activeSelf);
    }

    private void ShowWarning(bool show)
    {
        EnsureOverlay();
        warningLabel.text = warningText;
        warningLabel.gameObject.SetActive(show);
        black.color = new Color(0f, 0f, 0f, 0f);
        overlay.SetActive(show);
    }
}
