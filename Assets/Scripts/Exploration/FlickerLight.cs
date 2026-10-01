using System.Collections;
using UnityEngine;

// 고장 난 형광등처럼 깜빡이는 불빛.
//  - 평소에는 미세하게 떨리다가, 가끔 "타닥타닥" 몇 번 꺼졌다 켜진다.
//  - deadChance만큼은 한동안 완전히 꺼져 있다가 다시 들어온다.
//  - 지지직 소리(buzzClip)를 붙이면 불빛 위치에서 작게 들리고, 꺼질 때는 소리도 끊긴다.
// 같은 오브젝트(또는 자식)의 Light에 붙인다. 발광 재질(emissiveRenderer)이 있으면 같이 꺼진다.
public class FlickerLight : MonoBehaviour
{
    [SerializeField] private Light[] lights;

    [Header("평소 떨림")]
    [Range(0f, 0.5f)] [SerializeField] private float jitter = 0.08f;

    [Header("깜빡임")]
    [Tooltip("깜빡임이 오는 간격(초) 최소/최대")]
    [SerializeField] private Vector2 burstInterval = new Vector2(1.5f, 6f);
    [Tooltip("한 번 깜빡일 때 꺼졌다 켜지는 횟수")]
    [SerializeField] private Vector2Int blinksPerBurst = new Vector2Int(2, 7);
    [SerializeField] private Vector2 blinkDuration = new Vector2(0.03f, 0.12f);
    [Tooltip("꺼졌을 때 남는 밝기 비율 (0 = 완전히 꺼짐)")]
    [Range(0f, 1f)] [SerializeField] private float offLevel = 0.05f;
    [Tooltip("깜빡임 대신 한동안 꺼져 있을 확률")]
    [Range(0f, 1f)] [SerializeField] private float deadChance = 0.15f;
    [SerializeField] private Vector2 deadDuration = new Vector2(0.6f, 2.2f);

    [Header("발광 재질 (선택)")]
    [SerializeField] private Renderer emissiveRenderer;

    [Header("소리 (선택)")]
    [SerializeField] private AudioClip buzzClip;
    [Range(0f, 1f)] [SerializeField] private float buzzVolume = 0.25f;
    [SerializeField] private float buzzMaxDistance = 14f;

    private float[] baseIntensity;
    private float level = 1f;
    private AudioSource buzz;
    private MaterialPropertyBlock block;
    private Color baseEmission;
    private bool hasEmission;

    /// <summary>코드에서 붙일 때 소리/세기를 지정한다.</summary>
    public void Setup(AudioClip clip, float volume, float flickerJitter)
    {
        buzzClip = clip;
        buzzVolume = volume;
        jitter = flickerJitter;
    }

    private void Awake()
    {
        if (lights == null || lights.Length == 0)
            lights = GetComponentsInChildren<Light>(true);

        baseIntensity = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;

        if (emissiveRenderer != null && emissiveRenderer.sharedMaterial != null &&
            emissiveRenderer.sharedMaterial.HasProperty("_EmissionColor"))
        {
            hasEmission = true;
            baseEmission = emissiveRenderer.sharedMaterial.GetColor("_EmissionColor");
            block = new MaterialPropertyBlock();
        }
    }

    private void OnEnable()
    {
        if (buzzClip != null && buzz == null)
        {
            buzz = gameObject.AddComponent<AudioSource>();
            buzz.clip = buzzClip;
            buzz.loop = true;
            buzz.playOnAwake = false;
            buzz.spatialBlend = 1f;
            buzz.rolloffMode = AudioRolloffMode.Linear;
            buzz.minDistance = 1.5f;
            buzz.maxDistance = buzzMaxDistance;
            buzz.volume = buzzVolume;
        }
        if (buzz != null)
        {
            buzz.time = Random.Range(0f, Mathf.Max(0f, buzzClip.length - 0.1f));
            buzz.Play();
        }
        StartCoroutine(Run());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        level = 1f;
        Apply(1f);
        if (buzz != null) buzz.Stop();
    }

    private IEnumerator Run()
    {
        // 여러 등이 같은 박자로 깜빡이지 않게 시작을 어긋나게 한다.
        yield return new WaitForSeconds(Random.Range(0f, burstInterval.y));

        while (true)
        {
            if (Random.value < deadChance)
            {
                level = offLevel;
                yield return new WaitForSeconds(Random.Range(deadDuration.x, deadDuration.y));
                // 다시 켜질 때 한두 번 튄다
                for (int i = 0; i < 2; i++)
                {
                    level = 1f; yield return new WaitForSeconds(0.05f);
                    level = offLevel; yield return new WaitForSeconds(0.08f);
                }
                level = 1f;
            }
            else
            {
                int blinks = Random.Range(blinksPerBurst.x, blinksPerBurst.y + 1);
                for (int i = 0; i < blinks; i++)
                {
                    level = Random.value < 0.7f ? offLevel : Random.Range(0.3f, 0.6f);
                    yield return new WaitForSeconds(Random.Range(blinkDuration.x, blinkDuration.y));
                    level = 1f;
                    yield return new WaitForSeconds(Random.Range(blinkDuration.x, blinkDuration.y * 1.5f));
                }
            }

            yield return new WaitForSeconds(Random.Range(burstInterval.x, burstInterval.y));
        }
    }

    private void Update()
    {
        float noise = 1f - jitter + jitter * Mathf.PerlinNoise(Time.time * 12f, GetInstanceID() * 0.01f) * 2f;
        Apply(level * noise);
    }

    private void Apply(float k)
    {
        if (lights != null)
            for (int i = 0; i < lights.Length; i++)
                if (lights[i] != null) lights[i].intensity = baseIntensity[i] * k;

        if (hasEmission)
        {
            emissiveRenderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", baseEmission * k);
            emissiveRenderer.SetPropertyBlock(block);
        }

        if (buzz != null)
            buzz.volume = buzzVolume * (level > 0.5f ? 1f : 0.15f);
    }
}
