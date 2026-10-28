#if UNITY_EDITOR
// Farm Chaos - adds a test sphere player, on-screen phone controls and a follow camera.
// Menu: Tools > Farm Chaos > 4. Add Sphere Player
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmPlayerBuilder
{
    const string Root = "_Player";

    [MenuItem("Tools/Farm Chaos/4. Add Sphere Player")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        foreach (var r in scene.GetRootGameObjects()) if (r.name == Root) Undo.DestroyObjectImmediate(r);

        var root = new GameObject(Root);
        Undo.RegisterCreatedObjectUndo(root, "Add Sphere Player");

        // ---- spawn point: on the HQ pad if we can find it
        Vector3 spawn = new Vector3(0, 1.5f, 0);
        var hq = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                      .FirstOrDefault(t => t.name.Replace("_", "").ToLowerInvariant() == "hqarea");
        if (hq != null && hq.GetComponent<Renderer>() != null) { var b = hq.GetComponent<Renderer>().bounds; spawn = new Vector3(b.center.x, b.max.y + 0.8f, b.center.z); }

        // ---- the sphere
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Player_Sphere";
        ball.transform.SetParent(root.transform, false);
        ball.transform.position = spawn;
        ball.transform.localScale = Vector3.one * 1.2f;
        ball.GetComponent<Renderer>().sharedMaterial = PlayerMaterial();
        try { ball.tag = "Player"; } catch { }
        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = 1f;
        rb.linearDamping = 0.2f;
        rb.angularDamping = 0.6f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        ball.AddComponent<SpherePlayer>();

        // ---- follow camera on the scene's camera
        var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        var cam = cams.FirstOrDefault(c => c.CompareTag("MainCamera"));
        if (cam == null && cams.Length > 0) cam = cams[0];
        if (cam != null)
        {
            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = Undo.AddComponent<CameraFollow>(cam.gameObject);
            Undo.RecordObject(follow, "Add Sphere Player");
            follow.target = ball.transform;
            ball.GetComponent<SpherePlayer>().cameraTransform = cam.transform;
            Undo.RecordObject(cam.transform, "Add Sphere Player");
            cam.transform.position = spawn + follow.offset;
            cam.transform.LookAt(spawn);
        }
        else Debug.LogWarning("[Farm Chaos] No camera found in the scene.");

        // ---- on-screen controls
        var canvasGo = new GameObject("Mobile_Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var bg = Img("Joystick_Background", canvasGo.transform, knob, new Color(1, 1, 1, 0.3f), 260, new Vector2(0.5f, 0.5f));
        var canvasSize = scaler.referenceResolution;
        bg.anchoredPosition = new Vector2(-canvasSize.x / 2f + 260, -canvasSize.y / 2f + 240); // bottom-left resting spot
        var handle = Img("Joystick_Handle", bg, knob, new Color(1, 1, 1, 0.85f), 115, new Vector2(0.5f, 0.5f));
        handle.anchoredPosition = Vector2.zero;
        var jump = Img("Jump_Button", canvasGo.transform, knob, new Color(0.96f, 0.72f, 0.76f, 0.75f), 180, new Vector2(1, 0));
        jump.anchoredPosition = new Vector2(-220, 220);
        var arrow = Img("Jump_Arrow", jump, null, new Color(1, 1, 1, 0.9f), 40, new Vector2(0.5f, 0.5f));
        arrow.sizeDelta = new Vector2(46, 46); arrow.localRotation = Quaternion.Euler(0, 0, 45); arrow.anchoredPosition = new Vector2(0, -6);

        var joy = canvasGo.AddComponent<MobileJoystick>();
        joy.background = bg; joy.handle = handle; joy.jumpButton = jump;

        root.transform.SetSiblingIndex(1);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = ball;
        Debug.Log("[Farm Chaos] Sphere player added and scene saved. Press Play: WASD/arrows + Space, or drag on the left half of the Game view and click the right half to jump.");
    }

    static RectTransform Img(string name, Transform parent, Sprite sprite, Color color, float size, Vector2 anchor)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite; img.color = color; img.raycastTarget = false;
        return rt;
    }

    static Material PlayerMaterial()
    {
        const string path = "Assets/Materials/Mat_player_sphere.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        var c = new Color32(0x6F, 0xB7, 0xF2, 255); // bright baby blue so it stands out on the grass
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", 0.6f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
