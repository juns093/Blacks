using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메인 메뉴 화면 전용 스크립트
// - 샷건이 위아래로 둥둥 떠다니는 대기 연출
// - BGM 재생
// - Play 버튼을 누르면 렌즈가 광각으로 벌어지고, 총이 플레이어(카메라)를 조준해 발사한 뒤 게임 씬으로 전환
// - 발사 시: 총구 라이트 플래시 + 카메라 반동(위로 젖혀짐)
// - Settings 버튼을 누르면 카메라가 장전하는 시점(총 가까이)으로 이동해서 설정 패널을 띄우고,
//   확인을 누르면 장전 소리와 함께 원래 자리로 돌아온다.
public class SceneChange : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("Play 연출이 끝난 뒤 이동할 씬 이름 (Build Settings에 등록되어 있어야 함)")]
    public string sceneName;

    [Header("Shotgun Float 연출")]
    [Tooltip("메인 화면에서 둥둥 떠다닐 샷건 오브젝트의 Transform (조준 시 이 오브젝트 전체가 회전함)")]
    [SerializeField] private Transform shotgunTransform;
    [Tooltip("위아래로 움직이는 폭(진폭)")]
    [SerializeField] private float floatHeight = 0.1f;
    [Tooltip("위아래로 움직이는 속도")]
    [SerializeField] private float floatSpeed = 1.5f;
    [Tooltip("살짝 좌우로 기울이며 흔들고 싶을 때 각도(도). 필요 없으면 0")]
    [SerializeField] private float rotateSway = 0f;

    private Vector3 shotgunOriginalLocalPos;
    private Quaternion shotgunOriginalLocalRot;

    [Header("BGM")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip bgmClip;
    [Range(0f, 1f)][SerializeField] private float bgmVolume = 0.6f;
    [SerializeField] private bool bgmLoop = true;

    [Header("Play 연출 - 조준")]
    [Tooltip("Play 버튼을 눌렀을 때 총구가 바라볼 목표 지점 (플레이어/카메라 위치의 Transform)")]
    [SerializeField] private Transform aimAtPlayerTarget;
    [Tooltip("조준까지 걸리는 시간(초)")]
    [SerializeField] private float aimDuration = 0.5f;
    [Tooltip("조준한 채로 대기하는 시간(초). 지나면 발사 연출로 넘어감")]
    [SerializeField] private float aimHoldDuration = 0.6f;
    [Tooltip("모델의 forward축(+Z)이 실제 총구 방향과 다를 때 보정하는 회전값. " +
             "예: 모델이 X축을 정면으로 만들어져 있으면 (0, -90, 0) 등을 넣어서 맞추세요. " +
             "GunObject.cs의 aimRotationOffsetPlayer/Who와 같은 방식입니다.")]
    [SerializeField] private Vector3 muzzleAimOffset = Vector3.zero;

    [Header("Play 연출 - 발사 (총구 위치)")]
    [Tooltip("총구 끝 위치 (뮤즐플래시/라이트/사운드가 여기서 재생됨). 비워두면 shotgunTransform 위치 사용")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private ParticleSystem muzzleFlash;
    [Tooltip("발사 순간 켜지는 실제 Light 컴포넌트 (Point Light 또는 Spot Light). " +
             "총구 위치에 배치하고 평소엔 꺼두거나 intensity를 0으로 둔 상태로 연결하세요.")]
    [SerializeField] private Light muzzleLight;
    [Tooltip("라이트가 켜졌을 때의 최대 intensity")]
    [SerializeField] private float muzzleLightIntensity = 8f;
    [Tooltip("라이트가 최대 밝기까지 도달하는 시간(초). 아주 짧게(퍽 하고 터지는 느낌)")]
    [SerializeField] private float muzzleLightRiseTime = 0.02f;
    [Tooltip("라이트가 최대 밝기에서 꺼질 때까지 걸리는 시간(초)")]
    [SerializeField] private float muzzleLightFallTime = 0.15f;
    [SerializeField] private AudioClip shootSound;
    [Tooltip("발사 사운드용 AudioSource (없으면 PlayClipAtPoint로 대체 재생)")]
    [SerializeField] private AudioSource sfxSource;
    [Tooltip("발사 순간 화면이 번쩍이는 연출용 풀스크린 UI Image (선택, 없어도 동작함)")]
    [SerializeField] private Image flashOverlay;
    [SerializeField] private float flashDuration = 0.15f;
    [Tooltip("발사 연출이 끝난 뒤 씬 전환까지 추가로 대기하는 시간(초)")]
    [SerializeField] private float delayBeforeSceneChange = 0.5f;

    [Header("Play 연출 - 피격 리액션 (카메라)")]
    [Tooltip("총에 맞아 젖혀질 카메라(또는 카메라 리그) Transform. 비워두면 Camera.main 사용")]
    [SerializeField] private Transform hitReactionTransform;
    [Tooltip("위로 젖혀지는 각도(도). 클수록 더 세게 맞은 느낌")]
    [SerializeField] private float hitKickAngle = 35f;
    [Tooltip("옆으로도 살짝 틀어지는 각도(도). 정면으로만 젖혀지면 부자연스러우니 약간의 흔들림 추가")]
    [SerializeField] private float hitSideJitter = 6f;
    [Tooltip("젖혀지는(맞는 순간) 데 걸리는 시간(초). 아주 짧고 급격하게")]
    [SerializeField] private float hitKickUpDuration = 0.06f;
    [Tooltip("젖혀진 채로 멍하게 정지해 있는 시간(초)")]
    [SerializeField] private float hitHoldDuration = 0.4f;
    [Tooltip("천천히 원래 각도로 정신 차리며 복귀하는 시간(초)")]
    [SerializeField] private float hitReturnDuration = 0.6f;

    [Header("Play 연출 - 광각")]
    [Tooltip("Play를 누르면 렌즈(FOV)를 이 값까지 넓힌 뒤 맞는다. 0이면 광각 연출을 하지 않습니다.")]
    [SerializeField] private float playWideFov = 100f;
    [Tooltip("광각으로 벌어지는 시간(초). 조준하는 동안 함께 진행됩니다.")]
    [SerializeField] private float playWidenDuration = 1.1f;

    [Header("UI")]
    [SerializeField] private Button playButton;

    [Header("Settings 연출 (장전 시점)")]
    [SerializeField] private Button settingsButton;
    [SerializeField] private SettingsPanelUI settingsPanel;
    [Tooltip("설정할 때 카메라가 옮겨갈 위치/방향 (총을 장전하는 시점). 비워두면 총 위에서 내려다보는 자리로 자동 계산합니다.")]
    [SerializeField] private Transform settingsViewPoint;
    [Tooltip("설정 화면에서 숨길 메뉴 UI (타이틀, 버튼들)")]
    [SerializeField] private GameObject[] hideDuringSettings;
    [SerializeField] private float settingsCameraMoveDuration = 0.9f;
    [Tooltip("설정 화면에서 총이 돌아갈 회전값 (총의 로컬 회전, 인스펙터 Rotation과 같은 값)")]
    [SerializeField] private bool rotateGunInSettings = true;
    [SerializeField] private Vector3 settingsGunEuler = new Vector3(323.375458f, 59.5954285f, 332.327576f);
    [Tooltip("설정을 끝내고 확인을 누르면 나는 장전 소리")]
    [SerializeField] private AudioClip reloadSound;
    [Range(0f, 1f)][SerializeField] private float reloadVolume = 1f;

    [Header("버튼 클릭 효과음 (발사음과 별개)")]
    [Tooltip("Play 버튼을 누르는 순간 재생할 클릭 효과음. shootSound(발사음)와는 다른 클립을 넣으세요.")]
    [SerializeField] private AudioClip buttonClickSound;
    [Range(0f, 1f)][SerializeField] private float buttonClickVolume = 0.7f;
    [Tooltip("버튼을 누르는 순간 메뉴 BGM 볼륨을 줄일지 여부")]
    [SerializeField] private bool duckBgmOnClick = true;
    [Tooltip("BGM이 줄어든 볼륨(원래 bgmVolume에 곱해질 배율)")]
    [Range(0f, 1f)][SerializeField] private float bgmDuckMultiplier = 0.3f;
    [Tooltip("BGM 볼륨이 줄어들거나 복구되는 데 걸리는 시간(초)")]
    [SerializeField] private float bgmDuckFadeDuration = 0.3f;

    // Play 연출이 이미 진행 중인지 (중복 클릭 방지, 이 동안은 둥둥 뜨는 연출도 멈춤)
    private bool isPlaying = false;
    private Coroutine bgmFadeRoutine;

    void Awake()
    {
        if (shotgunTransform != null)
        {
            shotgunOriginalLocalPos = shotgunTransform.localPosition;
            shotgunOriginalLocalRot = shotgunTransform.localRotation;
        }

        if (flashOverlay != null)
        {
            Color c = flashOverlay.color;
            c.a = 0f;
            flashOverlay.color = c;
            flashOverlay.gameObject.SetActive(true);
        }

        // 평소엔 꺼진 상태로 시작 (인스펙터에서 켜둔 채로 배치돼 있어도 강제로 끔)
        if (muzzleLight != null)
        {
            muzzleLight.gameObject.SetActive(true); // GameObject가 꺼져 있으면 intensity를 바꿔도 빛이 안 나옴
            muzzleLight.intensity = 0f;
            muzzleLight.enabled = true; // enabled는 켜두고 intensity로만 제어 (끄고 켜는 텀 없이 즉각 반응)
        }
    }

    void Start()
    {
        if (bgmSource != null && bgmClip != null)
        {
            bgmSource.clip = bgmClip;
            bgmSource.volume = bgmVolume;
            bgmSource.loop = bgmLoop;
            bgmSource.Play();
        }
        else
        {
            Debug.LogWarning("[SceneChange] bgmSource 또는 bgmClip이 연결되어 있지 않아 BGM을 재생할 수 없습니다.");
        }

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayButtonClicked);
        else
            Debug.LogWarning("[SceneChange] playButton이 연결되어 있지 않습니다!");

        // Settings 버튼은 카메라 이동을 먼저 해야 하므로, 인스펙터에서 패널을 바로 여는 연결은 걷어내고 여기서 처리한다.
        if (settingsButton != null)
        {
            settingsButton.onClick = new Button.ButtonClickedEvent();
            settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        }

        if (settingsPanel != null)
        {
            settingsPanel.OnConfirm += OnSettingsConfirmed;
            settingsPanel.Close();
        }

        Camera cam = MenuCamera;
        if (cam != null)
        {
            menuCamPos = cam.transform.position;
            menuCamRot = cam.transform.rotation;
            menuFov = cam.fieldOfView;
        }
    }

    private void OnDestroy()
    {
        if (settingsPanel != null)
            settingsPanel.OnConfirm -= OnSettingsConfirmed;
    }

    // ─────────────────────────────────────────────────────────────
    // Settings: 장전 시점으로 이동 → 설정 → 확인하면 장전 소리와 함께 복귀
    // ─────────────────────────────────────────────────────────────

    private Vector3 menuCamPos;
    private Quaternion menuCamRot;
    private float menuFov;
    private bool settingsBusy = false;

    private Camera MenuCamera => hitReactionTransform != null && hitReactionTransform.GetComponent<Camera>() != null
        ? hitReactionTransform.GetComponent<Camera>()
        : Camera.main;

    public void OnSettingsButtonClicked()
    {
        if (isPlaying || settingsBusy) return;
        StartCoroutine(OpenSettingsRoutine());
    }

    private void OnSettingsConfirmed()
    {
        if (settingsBusy) return;
        StartCoroutine(CloseSettingsRoutine());
    }

    private IEnumerator OpenSettingsRoutine()
    {
        settingsBusy = true;
        PlayButtonClickSfxOnly();
        SetMenuUiVisible(false);

        GetSettingsView(out Vector3 pos, out Quaternion rot);
        if (rotateGunInSettings)
            StartCoroutine(RotateGun(Quaternion.Euler(settingsGunEuler), settingsCameraMoveDuration));
        yield return MoveCamera(pos, rot, settingsCameraMoveDuration);

        if (settingsPanel != null) settingsPanel.Open();
        settingsBusy = false;
    }

    private IEnumerator CloseSettingsRoutine()
    {
        settingsBusy = true;
        if (settingsPanel != null) settingsPanel.Close();

        // 철컥 - 장전 소리
        if (reloadSound != null)
        {
            if (sfxSource != null) sfxSource.PlayOneShot(reloadSound, reloadVolume);
            else AudioSource.PlayClipAtPoint(reloadSound, MenuCamera != null ? MenuCamera.transform.position : transform.position, reloadVolume);
        }

        if (rotateGunInSettings)
            StartCoroutine(RotateGun(shotgunOriginalLocalRot, settingsCameraMoveDuration));
        yield return MoveCamera(menuCamPos, menuCamRot, settingsCameraMoveDuration);

        SetMenuUiVisible(true);
        settingsBusy = false;
    }

    private void SetMenuUiVisible(bool visible)
    {
        if (hideDuringSettings == null) return;
        foreach (var go in hideDuringSettings)
            if (go != null) go.SetActive(visible);
    }

    // 장전 시점: 지정한 자리가 있으면 그곳, 없으면 총 위에서 비스듬히 내려다보는 자리.
    private void GetSettingsView(out Vector3 pos, out Quaternion rot)
    {
        if (settingsViewPoint != null)
        {
            pos = settingsViewPoint.position;
            rot = settingsViewPoint.rotation;
            return;
        }

        Vector3 target = shotgunTransform != null ? shotgunTransform.position : menuCamPos + menuCamRot * Vector3.forward * 2f;
        Vector3 fromCam = menuCamPos - target;
        fromCam.y = 0f;
        Vector3 back = fromCam.sqrMagnitude > 0.001f ? fromCam.normalized : Vector3.back;
        pos = target + Vector3.up * 1.1f + back * 0.6f;
        rot = Quaternion.LookRotation(target - pos, Vector3.up);
    }

    // 총을 부드럽게 돌린다. (둥둥 뜨는 연출은 위치만 건드리므로 회전과 겹치지 않는다)
    private IEnumerator RotateGun(Quaternion toLocalRot, float duration)
    {
        if (shotgunTransform == null) yield break;

        Quaternion from = shotgunTransform.localRotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            shotgunTransform.localRotation = Quaternion.Slerp(from, toLocalRot, k);
            yield return null;
        }
        shotgunTransform.localRotation = toLocalRot;
    }

    private IEnumerator MoveCamera(Vector3 toPos, Quaternion toRot, float duration)
    {
        Camera cam = MenuCamera;
        if (cam == null) yield break;

        Transform t = cam.transform;
        Vector3 fromPos = t.position;
        Quaternion fromRot = t.rotation;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration)));
            t.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, k), Quaternion.Slerp(fromRot, toRot, k));
            yield return null;
        }
        t.SetPositionAndRotation(toPos, toRot);
    }

    private void PlayButtonClickSfxOnly()
    {
        if (buttonClickSound == null) return;
        if (sfxSource != null) sfxSource.PlayOneShot(buttonClickSound, buttonClickVolume);
        else AudioSource.PlayClipAtPoint(buttonClickSound, MenuCamera != null ? MenuCamera.transform.position : transform.position, buttonClickVolume);
    }

    // 조준하는 동안 렌즈가 광각으로 서서히 벌어진다.
    private IEnumerator WidenLensRoutine()
    {
        Camera cam = MenuCamera;
        if (cam == null || playWideFov <= 0f) yield break;

        float from = cam.fieldOfView;
        float elapsed = 0f;
        while (elapsed < playWidenDuration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, playWidenDuration)));
            cam.fieldOfView = Mathf.Lerp(from, playWideFov, k);
            yield return null;
        }
        cam.fieldOfView = playWideFov;
    }

    void Update()
    {
        // Play 연출 중에는 총이 조준/발사 회전을 해야 하므로 둥둥 뜨는 연출은 멈춘다
        if (isPlaying || shotgunTransform == null) return;

        float offsetY = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        shotgunTransform.localPosition = shotgunOriginalLocalPos + Vector3.up * offsetY;

        if (rotateSway > 0f)
        {
            float sway = Mathf.Sin(Time.time * floatSpeed * 0.5f) * rotateSway;
            shotgunTransform.localRotation = shotgunOriginalLocalRot * Quaternion.Euler(0f, 0f, sway);
        }
    }

    // Play 버튼 OnClick()에 연결하거나, 인스펙터에서 자동으로 연결됨 (Start에서 addListener)
    public void OnPlayButtonClicked()
    {
        if (isPlaying || settingsBusy) return;
        isPlaying = true;

        if (playButton != null)
            playButton.interactable = false;

        // 발사음과는 별개로, 버튼을 누르는 순간의 클릭 효과음을 재생하고 BGM을 살짝 줄인다.
        PlayButtonClickSfx();

        StartCoroutine(PlayIntroRoutine());
    }

    // Play 버튼 클릭 효과음 재생 + BGM 덕킹 (발사 사운드(shootSound)와는 별개의 연출)
    private void PlayButtonClickSfx()
    {
        if (buttonClickSound != null)
        {
            if (sfxSource != null)
                sfxSource.PlayOneShot(buttonClickSound, buttonClickVolume);
            else
                AudioSource.PlayClipAtPoint(buttonClickSound, Camera.main != null ? Camera.main.transform.position : transform.position, buttonClickVolume);
        }

        if (duckBgmOnClick && bgmSource != null)
        {
            if (bgmFadeRoutine != null)
                StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = StartCoroutine(FadeBgmVolume(bgmVolume * bgmDuckMultiplier, bgmDuckFadeDuration));
        }
    }

    private IEnumerator FadeBgmVolume(float target, float duration)
    {
        float start = bgmSource.volume;
        float safeDuration = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            bgmSource.volume = Mathf.Lerp(start, target, elapsed / safeDuration);
            yield return null;
        }

        bgmSource.volume = target;
        bgmFadeRoutine = null;
    }

    // 총이 플레이어(카메라)를 조준 -> 발사 연출(라이트+반동) -> 씬 전환까지의 전체 흐름
    private IEnumerator PlayIntroRoutine()
    {
        // 렌즈가 광각으로 벌어지는 것과 조준을 동시에 진행하고, 둘 다 끝나면 쏜다.
        StartCoroutine(WidenLensRoutine());
        float widenEndTime = Time.time + (playWideFov > 0f ? playWidenDuration : 0f);

        if (shotgunTransform != null && aimAtPlayerTarget != null)
        {
            // 총구 기준 위치에서 타겟까지의 방향으로 회전 계산
            Vector3 muzzlePos = muzzleTransform != null ? muzzleTransform.position : shotgunTransform.position;
            Vector3 dir = (aimAtPlayerTarget.position - muzzlePos).normalized;

            Quaternion startRot = shotgunTransform.rotation;
            // LookRotation은 모델의 +Z가 정면이라고 가정하므로, 실제 총구축이 다르면
            // muzzleAimOffset으로 보정해서 진짜 총구가 타겟을 향하도록 맞춘다.
            Quaternion targetRot = dir != Vector3.zero
                ? Quaternion.LookRotation(dir) * Quaternion.Euler(muzzleAimOffset)
                : startRot;

            float elapsed = 0f;
            while (elapsed < aimDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / aimDuration));
                shotgunTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            shotgunTransform.rotation = targetRot;
        }
        else
        {
            Debug.LogWarning("[SceneChange] shotgunTransform 또는 aimAtPlayerTarget이 연결되어 있지 않아 조준 연출을 스킵합니다.");
        }

        // 조준한 채로 잠깐 대기 (긴장감 연출)
        if (aimHoldDuration > 0f)
            yield return new WaitForSeconds(aimHoldDuration);

        // 광각이 다 벌어질 때까지 기다린 뒤 쏜다.
        while (Time.time < widenEndTime)
            yield return null;

        // 발사 연출(라이트, 사운드, 파티클, 카메라 반동)은 서로 기다릴 필요 없이 동시에 진행
        // 발사와 동시에 라이트/사운드/파티클이 터지고,
        // 그 총알에 맞아서 카메라(플레이어)가 위로 젖혀지는 리액션이 함께 진행됨
        StartCoroutine(HitReactionRoutine());
        yield return StartCoroutine(FireRoutine());

        if (delayBeforeSceneChange > 0f)
            yield return new WaitForSeconds(delayBeforeSceneChange);

        LoadGameplayScene();
    }

    private IEnumerator FireRoutine()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        if (shootSound != null)
        {
            Vector3 soundPos = muzzleTransform != null ? muzzleTransform.position
                              : (shotgunTransform != null ? shotgunTransform.position : Camera.main.transform.position);

            if (sfxSource != null)
                sfxSource.PlayOneShot(shootSound);
            else
                AudioSource.PlayClipAtPoint(shootSound, soundPos);
        }

        // 실제 Light 컴포넌트 플래시: 순간적으로 확 밝아졌다가 빠르게 꺼짐
        if (muzzleLight != null)
            StartCoroutine(MuzzleLightFlashRoutine());
        else
            Debug.LogWarning("[SceneChange] muzzleLight가 연결되어 있지 않습니다. 총구 위치에 Light(Point/Spot)를 배치하고 연결하세요.");

        // 화면 전체가 번쩍이는 UI 플래시 (없으면 그냥 대기만 함)
        if (flashOverlay != null)
        {
            Color c = flashOverlay.color;
            float elapsed = 0f;

            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(0f, 1f, elapsed / flashDuration);
                flashOverlay.color = c;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Lerp(1f, 0f, elapsed / flashDuration);
                flashOverlay.color = c;
                yield return null;
            }

            c.a = 0f;
            flashOverlay.color = c;
        }
        else
        {
            yield return new WaitForSeconds(flashDuration);
        }
    }

    // 총구 Light의 intensity를 순간적으로 확 올렸다가 빠르게 0으로 떨어뜨림
    private IEnumerator MuzzleLightFlashRoutine()
    {
        float elapsed = 0f;
        while (elapsed < muzzleLightRiseTime)
        {
            elapsed += Time.deltaTime;
            muzzleLight.intensity = Mathf.Lerp(0f, muzzleLightIntensity, elapsed / muzzleLightRiseTime);
            yield return null;
        }
        muzzleLight.intensity = muzzleLightIntensity;

        elapsed = 0f;
        while (elapsed < muzzleLightFallTime)
        {
            elapsed += Time.deltaTime;
            muzzleLight.intensity = Mathf.Lerp(muzzleLightIntensity, 0f, elapsed / muzzleLightFallTime);
            yield return null;
        }
        muzzleLight.intensity = 0f;
    }

    // 발사 순간 카메라가 위로 확 젖혀졌다가 서서히 원래 각도로 복귀
    // 총에 맞는 순간 카메라(플레이어 머리)가 위로 홱 젖혀졌다가,
    // 잠깐 멍하게 그 상태로 있다가, 천천히 정신 차리며 원래 각도로 돌아옴
    private IEnumerator HitReactionRoutine()
    {
        Transform cam = hitReactionTransform != null ? hitReactionTransform
                        : (Camera.main != null ? Camera.main.transform : null);

        if (cam == null)
        {
            Debug.LogWarning("[SceneChange] hitReactionTransform이 없고 Camera.main도 찾을 수 없어 피격 리액션을 스킵합니다.");
            yield break;
        }

        Quaternion originalRot = cam.localRotation;

        // 위로 젖혀짐(X축 음수 = 위를 올려다보는 방향) + 좌우 약간의 랜덤 틀어짐
        float side = Random.Range(-hitSideJitter, hitSideJitter);
        Quaternion kickedRot = originalRot * Quaternion.Euler(-hitKickAngle, 0f, side);

        // 1) 맞는 순간: 매우 빠르게 위로 튕겨 올라감
        float elapsed = 0f;
        while (elapsed < hitKickUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / hitKickUpDuration);
            cam.localRotation = Quaternion.Slerp(originalRot, kickedRot, t);
            yield return null;
        }
        cam.localRotation = kickedRot;

        // 2) 젖혀진 채로 멍하게 정지 (충격으로 잠깐 못 움직이는 느낌)
        if (hitHoldDuration > 0f)
            yield return new WaitForSeconds(hitHoldDuration);

        // 3) 천천히 정신 차리며 원래 각도로 복귀
        elapsed = 0f;
        while (elapsed < hitReturnDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / hitReturnDuration));
            cam.localRotation = Quaternion.Slerp(kickedRot, originalRot, t);
            yield return null;
        }
        cam.localRotation = originalRot;
    }

    // sceneName으로 게임플레이 씬으로 이동.
    // SceneTransition(페이드/와이프 연출)이 씬에 있으면 그걸 사용하고, 없으면 바로 SceneManager로 로드.
    private void LoadGameplayScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[SceneChange] sceneName이 비어있습니다! 인스펙터에서 이동할 씬 이름을 지정하세요.");
            return;
        }

        if (SceneTransition.Instance != null)
        {
            int buildIndex = GetBuildIndexByName(sceneName);
            if (buildIndex >= 0)
            {
                SceneTransition.Instance.LoadScene(buildIndex);
                return;
            }

            Debug.LogWarning($"[SceneChange] '{sceneName}' 씬을 Build Settings에서 찾지 못해 SceneTransition을 사용하지 못했습니다. 이름으로 직접 로드합니다.");
        }

        SceneManager.LoadScene(sceneName);
    }

    // Build Settings에 등록된 씬 이름으로 build index를 찾는 헬퍼
    private int GetBuildIndexByName(string name)
    {
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneNameAtIndex = Path.GetFileNameWithoutExtension(path);
            if (sceneNameAtIndex == name)
                return i;
        }
        return -1;
    }
}