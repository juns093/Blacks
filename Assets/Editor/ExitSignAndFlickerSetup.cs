using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 지하철 출구 표시(EXIT) + 깜빡이는 조명들을 GameScene에 한 번에 세팅한다.
// 메뉴: Tools/BuckShot/Setup Exit Sign + Flicker Lights
// (플레이 중에 예약해 두면 플레이가 끝나는 순간 한 번 실행된다)
[InitializeOnLoad]
public static class ExitSignAndFlickerSetup
{
    private const string PendingKey = "BuckShot_ExitFlickerSetupPending";
    private const string NeonPath = "Assets/Audio/SoundEffect/500693__bolkmar__neon-tube-light-flickering.wav";

    static ExitSignAndFlickerSetup()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && EditorPrefs.GetBool(PendingKey, false))
            {
                EditorPrefs.SetBool(PendingKey, false);
                EditorApplication.delayCall += Run;
            }
        };
    }

    public static void Schedule()
    {
        if (EditorApplication.isPlaying) EditorPrefs.SetBool(PendingKey, true);
        else Run();
    }

    [MenuItem("Tools/BuckShot/Setup Exit Sign + Flicker Lights")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { Debug.LogWarning("[Setup] 플레이 중에는 실행할 수 없습니다."); return; }

        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "GameScene") { Debug.LogWarning("[Setup] GameScene을 열고 실행하세요."); return; }

        BuildExitSign();
        AddFlickers();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Setup] 출구 표시 + 깜빡이는 조명 세팅 완료.");
    }

    // ── 출구 표시 ──
    private static void BuildExitSign()
    {
        var metro = GameObject.Find("FirstDieTime/Metro");
        if (metro == null) return;
        var root = metro.transform.parent; // FirstDieTime

        var old = root.Find("StationExit");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var exit = new GameObject("StationExit").transform;
        exit.SetParent(root, false);

        Material green = LoadOrMakeMat("Assets/Materials/Restroom/ExitSignGreen.mat", new Color(0.05f, 0.45f, 0.15f), new Color(0.1f, 1.2f, 0.35f));
        Font font = UIFontUtil.Resolve(null);

        // 계단 입구 위 (승강장에서 보이게)
        MakeSign(exit, "ExitSign_StairBottom", new Vector3(-86f, 4.6f, 33.2f), 0f, green, font, "출구 EXIT");
        // 계단 꼭대기 (나가는 곳)
        MakeSign(exit, "ExitSign_StairTop", new Vector3(-86f, 8.2f, 47.5f), 0f, green, font, "EXIT");

        // 나가야 할 때만 켜지는 표시: 계단 꼭대기의 초록 불빛 (천천히 맥박치듯)
        var marker = new GameObject("ExitMarker");
        marker.transform.SetParent(exit, false);
        marker.transform.position = new Vector3(-86f, 7f, 46f);
        var l = marker.AddComponent<Light>();
        l.type = LightType.Point; l.range = 10f; l.intensity = 25f; l.color = new Color(0.3f, 1f, 0.45f);
        // (길 안내는 화면 위 목표 표시(WaypointHUD)가 맡는다)
        marker.SetActive(false);

        // 탐색 구간에 연결
        var seg = GameObject.Find("FreeRoamSegment_1");
        if (seg != null)
        {
            var so = new SerializedObject(seg.GetComponent<FlashbackFreeRoamSegment>());
            var p = so.FindProperty("afterCallExitMarker");
            if (p != null) { p.objectReferenceValue = marker; so.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    private static void MakeSign(Transform parent, string name, Vector3 pos, float yaw, Material mat, Font font, string text)
    {
        var sign = new GameObject(name).transform;
        sign.SetParent(parent, false);
        sign.position = pos;
        sign.rotation = Quaternion.Euler(0f, yaw, 0f);

        var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        Object.DestroyImmediate(board.GetComponent<Collider>());
        board.transform.SetParent(sign, false);
        board.transform.localScale = new Vector3(2.2f, 0.6f, 0.08f);
        board.GetComponent<Renderer>().sharedMaterial = mat;

        var tg = new GameObject("Text");
        tg.transform.SetParent(sign, false);
        tg.transform.localPosition = new Vector3(0f, 0f, -0.06f);
        var tm = tg.AddComponent<TextMesh>();
        tm.font = font;
        // 기본 글자 재질은 벽 너머에서도 보이므로, 벽에 가려지는 재질을 쓴다.
        var textMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/WorldText_" + font.name + ".mat");
        tg.GetComponent<MeshRenderer>().sharedMaterial = textMat != null ? textMat : font.material;
        tm.text = text; tm.fontSize = 64; tm.characterSize = 0.045f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.color = Color.white;

        var lg = new GameObject("Glow");
        lg.transform.SetParent(sign, false);
        lg.transform.localPosition = new Vector3(0f, -0.4f, -0.5f);
        var l = lg.AddComponent<Light>();
        l.type = LightType.Point; l.range = 5f; l.intensity = 6f; l.color = new Color(0.35f, 1f, 0.5f);
    }

    private static Material LoadOrMakeMat(string path, Color baseColor, Color emission)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.SetColor("_BaseColor", baseColor);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", emission);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ── 깜빡이는 조명 ──
    private static void AddFlickers()
    {
        var neon = AssetDatabase.LoadAssetAtPath<AudioClip>(NeonPath);

        // 형광등 (지지직 소리)
        Flicker("FirstDieTime/Metro/Lights/light (2)", neon, 0.18f, 0.06f);
        Flicker("FirstDieTime/Metro/Lights/light (5)", neon, 0.18f, 0.06f);
        Flicker("FirstDieTime/OutsideRestroom/Restroom/RestroomLights/RestroomLight2", neon, 0.3f, 0.1f);
        Flicker("SecondDieTime/hospital/EastWing/Lights/CorridorLight_2", neon, 0.25f, 0.08f);
        Flicker("SecondDieTime/hospital/EastWing/Lights/RoomLight_S1", neon, 0.2f, 0.08f);
        Flicker("SecondDieTime/hospital/Map2/Point Light (20)", neon, 0.2f, 0.06f);
        Flicker("ThridDieTime/Lights/Point Light (3)", neon, 0.2f, 0.06f);

        // 소리 없는 불빛 (가로등, 현관등)
        Flicker("FirstDieTime/OutsideRestroom/PorchLight", null, 0f, 0.05f);
        Flicker("ForthDieTime/Lights/street_light_clean (8)", null, 0f, 0.04f);
        Flicker("ForthDieTime/Lights/street_light_clean (11)", null, 0f, 0.04f);
    }

    private static void Flicker(string path, AudioClip buzz, float volume, float jitter)
    {
        var go = GameObject.Find(path);
        if (go == null) { Debug.LogWarning("[Setup] 조명을 찾지 못했습니다: " + path); return; }
        var f = go.GetComponent<FlickerLight>();
        if (f == null) f = go.AddComponent<FlickerLight>();
        f.Setup(buzz, volume, jitter);
        EditorUtility.SetDirty(f);
    }
}
