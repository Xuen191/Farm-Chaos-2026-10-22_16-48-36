#if UNITY_EDITOR
// Farm Chaos - builds the Lobby scene: a cozy pastel farmhouse front yard at dusk with 4 party circles.
// Menu: Tools > Farm Chaos > 11. Build Lobby Scene
// Creates/overwrites Assets/Scenes/Lobby.unity and puts Lobby (first) + Farm_Main in the build's scene list.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmLobbyBuilder
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string FarmPath = "Assets/Scenes/Farm_Main.unity";
    const string MatFolder = "Assets/Materials/Lobby";
    const string Pack = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";
    static readonly Vector3 Spawn = new Vector3(0f, 1.2f, -13f);

    static Dictionary<string, Material> M;

    [MenuItem("Tools/Farm Chaos/11. Build Lobby Scene")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (System.IO.File.Exists(LobbyPath) &&
            !EditorUtility.DisplayDialog("Farm Chaos", "Rebuild the Lobby scene from scratch? (Any manual changes in Lobby.unity will be replaced.)", "Rebuild", "Cancel")) return;

        // save whatever is open, then start a fresh scene
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, LobbyPath);
        LoadMaterials();

        // ---- lighting: soft pastel dusk
        var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            sun.color = new Color(1f, 0.82f, 0.72f); sun.intensity = 1.05f;
            sun.transform.rotation = Quaternion.Euler(28f, -35f, 0f); sun.shadowStrength = 0.6f;
        }
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.86f, 0.78f, 0.92f);
        RenderSettings.ambientEquatorColor = new Color(0.82f, 0.72f, 0.76f);
        RenderSettings.ambientGroundColor = new Color(0.45f, 0.42f, 0.48f);
        var cam = Camera.main;
        if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(0xE8, 0xC9, 0xDE, 255); }

        // ---- yard
        var world = new GameObject("Lobby_World").transform;
        var ground = Prim(PrimitiveType.Plane, "Ground", world, Vector3.zero, new Vector3(5f, 1f, 5f), M["grass"], true);
        Prim(PrimitiveType.Cube, "Path", world, new Vector3(0, 0.02f, 1f), new Vector3(3.2f, 0.04f, 30f), M["path"], false);
        Prim(PrimitiveType.Cylinder, "Plaza", world, new Vector3(0, 0.03f, -1f), new Vector3(16f, 0.02f, 9f), M["path"], false);

        var house = Pack_("Bld_FarmerHouse", world, new Vector3(0, 0, 17f), 8f, 180f, true);
        if (house == null) Debug.LogWarning("[Farm Chaos] Farmer house prefab not found - lobby will have no house.");

        Fence(world, 21f);
        Decorate(world);
        Lamps(world);

        // ---- party circles
        var circles = new GameObject("Party_Circles").transform;
        Vector3[] spots = { new Vector3(-10f, 0, 2f), new Vector3(-3.6f, 0, -0.5f), new Vector3(3.6f, 0, -0.5f), new Vector3(10f, 0, 2f) };
        for (int i = 0; i < spots.Length; i++) Circle(circles, i + 1, spots[i]);

        // ---- UI + EventSystem
        BuildUI();
        if (Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 0)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem));
            var t = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (t != null) es.AddComponent(t); else es.AddComponent<StandaloneInputModule>();
        }

        EditorSceneManager.SaveScene(scene);

        // ---- player sphere, joystick & follow camera (same as the farm)
        FarmPlayerBuilder.Build();
        var ball = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == "Player_Sphere");
        if (ball != null)
        {
            ball.position = Spawn;
            if (cam != null) { cam.transform.position = Spawn + new Vector3(0, 11, -9); cam.transform.LookAt(Spawn); }
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // ---- build scene list: Lobby first (the phone starts here), then the farm
        var list = EditorBuildSettings.scenes.Where(s => s.path != LobbyPath && s.path != FarmPath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(FarmPath, true));
        list.Insert(0, new EditorBuildSettingsScene(LobbyPath, true));
        EditorBuildSettings.scenes = list.ToArray();

        Debug.Log("[Farm Chaos] Lobby scene built and saved (Assets/Scenes/Lobby.unity). Press Play and roll into a circle!");
    }

    // ===================================================================== pieces
    static void Circle(Transform parent, int number, Vector3 pos)
    {
        var root = new GameObject("Circle_" + number); root.transform.SetParent(parent, false); root.transform.position = pos;
        var ring = Prim(PrimitiveType.Cylinder, "Ring", root.transform, new Vector3(0, 0.06f, 0), new Vector3(4.6f, 0.02f, 4.6f), M["circle_idle"], false);
        Prim(PrimitiveType.Cylinder, "Inner", root.transform, new Vector3(0, 0.08f, 0), new Vector3(3.8f, 0.02f, 3.8f), M["cream"], false);
        Prim(PrimitiveType.Cylinder, "Dot", root.transform, new Vector3(0, 0.1f, 0), new Vector3(1.0f, 0.02f, 1.0f), M["pink"], false);
        var trig = root.AddComponent<BoxCollider>(); trig.isTrigger = true; trig.center = new Vector3(0, 1.5f, 0); trig.size = new Vector3(3.8f, 3f, 3.8f);

        // sign post behind the circle
        var post = Prim(PrimitiveType.Cylinder, "Sign_Post", root.transform, new Vector3(0, 1.1f, 2.7f), new Vector3(0.14f, 1.1f, 0.14f), M["wood"], true);
        Prim(PrimitiveType.Cube, "Sign_Board", root.transform, new Vector3(0, 2.4f, 2.7f), new Vector3(2.4f, 1.1f, 0.12f), M["mint"], false);
        var tg = new GameObject("Sign_Text"); tg.transform.SetParent(root.transform, false);
        tg.transform.localPosition = new Vector3(0, 2.4f, 2.62f);
        var tmp = tg.AddComponent<TextMeshPro>();
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.1f; tmp.fontSizeMax = 12f;
        tmp.color = new Color32(0x6B, 0x4A, 0x3A, 255);
        tmp.rectTransform.sizeDelta = new Vector2(2.2f, 0.95f);

        var pc = root.AddComponent<PartyCircle>();
        pc.circleNumber = number; pc.ring = ring.GetComponent<Renderer>();
        pc.idleMaterial = M["circle_idle"]; pc.activeMaterial = M["circle_active"]; pc.sign = tmp;
        pc.maxPlayers = 4;
        tmp.text = $"Circle {number}\n<size=70%>0/4 players</size>";
    }

    static void Fence(Transform parent, float half)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Env_WoodFence_04.prefab");
        var holder = new GameObject("Fence").transform; holder.SetParent(parent, false);
        if (pf == null) return;
        // measure one piece
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder);
        var b = Bounds(probe); float len = Mathf.Max(b.size.x, b.size.z); bool alongX = b.size.x >= b.size.z;
        Object.DestroyImmediate(probe);
        if (len < 0.1f) return;
        for (int side = 0; side < 4; side++)
        {
            int n = Mathf.CeilToInt(half * 2f / len);
            for (int i = 0; i < n; i++)
            {
                float t = -half + len * (i + 0.5f);
                Vector3 p; float yaw;
                switch (side)
                {
                    case 0: p = new Vector3(t, 0, half); yaw = 0; break;
                    case 1: p = new Vector3(t, 0, -half); yaw = 0; break;
                    case 2: p = new Vector3(half, 0, t); yaw = 90; break;
                    default: p = new Vector3(-half, 0, t); yaw = 90; break;
                }
                var f = (GameObject)PrefabUtility.InstantiatePrefab(pf, holder);
                f.transform.rotation = Quaternion.Euler(0, yaw + (alongX ? 0 : 90), 0) * pf.transform.rotation;
                f.transform.position = p;
                var fb = Bounds(f); f.transform.position += new Vector3(p.x - fb.center.x, -fb.min.y, p.z - fb.center.z);
            }
        }
    }

    static void Decorate(Transform parent)
    {
        var holder = new GameObject("Decorations").transform; holder.SetParent(parent, false);
        var rng = new System.Random(11);
        string[] trees = { "Env_Tree_01", "Env_Tree_02", "Env_Tree_04", "Env_Tree_05", "Env_Tree_07" };
        string[] bushes = { "Env_Bush_01", "Env_Bush_02" };
        // trees: a row behind the house (leaving the house clear) and down both sides
        for (float x = -18.5f; x <= 18.6f; x += 2.6f)
        {
            if (Mathf.Abs(x) < 7.5f) continue;
            Pack_(trees[rng.Next(trees.Length)], holder, new Vector3(x + (float)rng.NextDouble() - 0.5f, 0, 18.6f), 5.5f + (float)rng.NextDouble() * 2f, rng.Next(360), true);
        }
        foreach (var sx in new[] { -1, 1 })
            for (float z = -17f; z <= 14f; z += 4.5f)
                Pack_(trees[rng.Next(trees.Length)], holder, new Vector3(sx * 18.6f, 0, z + (float)rng.NextDouble()), 5.5f + (float)rng.NextDouble() * 1.5f, rng.Next(360), true);
        // bushes & flowers-ish grass around the plaza
        for (int i = 0; i < 18; i++)
        {
            float a = i / 18f * Mathf.PI * 2f;
            var p = new Vector3(Mathf.Cos(a) * 13.5f, 0, -1f + Mathf.Sin(a) * 9f);
            if (Mathf.Abs(p.x) < 4.5f) continue;   // keep the path and lamps clear
            Pack_(bushes[rng.Next(bushes.Length)], holder, p, 1.1f + (float)rng.NextDouble() * 0.5f, rng.Next(360), false);
        }
        // a few cozy props by the house
        Pack_("Prop_Haystack_02", holder, new Vector3(-6.5f, 0, 12f), 1.8f, 20, true);
        Pack_("Prop_Berrel_03", holder, new Vector3(6.5f, 0, 12.5f), 1.1f, 0, true);
        Pack_("Prop_WoodenCrates_01", holder, new Vector3(7.6f, 0, 11.2f), 1.0f, 35, true);
        Pack_("Prop_Wheelbarrow", holder, new Vector3(-8f, 0, 9.5f), 1.0f, 60, true);
    }

    static void Lamps(Transform parent)
    {
        var holder = new GameObject("Lamp_Posts").transform; holder.SetParent(parent, false);
        Vector3[] spots = { new Vector3(-2.6f, 0, -8f), new Vector3(2.6f, 0, -8f), new Vector3(-2.6f, 0, 7f), new Vector3(2.6f, 0, 7f) };
        foreach (var p in spots)
        {
            var lamp = new GameObject("Lamp_Post").transform; lamp.SetParent(holder, false); lamp.position = p;
            Prim(PrimitiveType.Cylinder, "Base", lamp, new Vector3(0, 0.1f, 0), new Vector3(0.5f, 0.1f, 0.5f), M["mint"], false);
            Prim(PrimitiveType.Cylinder, "Pole", lamp, new Vector3(0, 1.3f, 0), new Vector3(0.13f, 1.2f, 0.13f), M["pole"], true);
            Prim(PrimitiveType.Cube, "Lantern", lamp, new Vector3(0, 2.78f, 0), new Vector3(0.38f, 0.48f, 0.38f), M["glow"], false).transform.localRotation = Quaternion.Euler(0, 45, 0);
            Prim(PrimitiveType.Cylinder, "Lantern_Roof", lamp, new Vector3(0, 3.07f, 0), new Vector3(0.62f, 0.07f, 0.62f), M["pink"], false);
            Prim(PrimitiveType.Sphere, "Topper", lamp, new Vector3(0, 3.25f, 0), Vector3.one * 0.16f, M["mint"], false);
        }
    }

    // ===================================================================== UI
    static void BuildUI()
    {
        var go = new GameObject("Lobby_UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        var ui = go.AddComponent<LobbyUI>();
        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var intro = go.AddComponent<IntroStory>();
        intro.circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"); intro.rounded = rounded;
        Color navy = new Color(0.08f, 0.09f, 0.2f, 0.72f), cream = new Color32(0xFF, 0xF0, 0xD9, 255), brown = new Color32(0x6B, 0x4A, 0x3A, 255);

        // title + hint
        var title = Txt("Title", go.transform, "FARM CHAOS", 76, new Vector2(900, 100), new Vector2(0.5f, 1f), new Vector2(0, -80), Color.white);
        title.outlineWidth = 0.2f; title.outlineColor = new Color32(0xE0, 0x6C, 0x8C, 255);
        var hint = Txt("Hint", go.transform, "Roll into a circle to start a party!", 40, new Vector2(1000, 60), new Vector2(0.5f, 1f), new Vector2(0, -160), Color.white);
        hint.outlineWidth = 0.18f; hint.outlineColor = new Color32(0x2A, 0x23, 0x40, 255);
        ui.hintBanner = hint;

        // party panel (right side)
        var panel = Box("Party_Panel", go.transform, rounded, new Color(1f, 0.96f, 0.9f, 0.96f), new Vector2(560, 620), new Vector2(1f, 0.5f), new Vector2(-330, 30), true);
        ui.partyPanel = panel.gameObject;
        ui.partyTitle = Txt("Party_Title", panel, "Circle 1", 56, new Vector2(500, 70), new Vector2(0.5f, 1f), new Vector2(0, -55), new Color32(0xE0, 0x6C, 0x4C, 255));
        Txt("Size_Label", panel, "How many players?", 32, new Vector2(500, 40), new Vector2(0.5f, 1f), new Vector2(0, -125), brown);
        for (int i = 0; i < 5; i++)
        {
            var b = Box("Size_" + (i + 1), panel, rounded, cream, new Vector2(84, 84), new Vector2(0.5f, 1f), new Vector2(-188 + i * 94, -200), true);
            b.GetComponent<Image>().raycastTarget = true;
            ui.sizeButtons[i] = b.gameObject.AddComponent<Button>();
            Txt("Label", b, (i + 1).ToString(), 44, new Vector2(80, 80), new Vector2(0.5f, 0.5f), Vector2.zero, brown);
        }
        Txt("Code_Label", panel, "Join code", 30, new Vector2(500, 40), new Vector2(0.5f, 1f), new Vector2(0, -280), brown);
        var codeBox = Box("Code_Box", panel, rounded, new Color32(0xC8, 0xE6, 0xD0, 255), new Vector2(340, 90), new Vector2(0.5f, 1f), new Vector2(0, -345), true);
        ui.codeText = Txt("Code", codeBox, "ABCDE", 56, new Vector2(330, 86), new Vector2(0.5f, 0.5f), Vector2.zero, brown);
        ui.codeText.characterSpacing = 18f;
        ui.partyStatus = Txt("Status", panel, "1/4 players", 26, new Vector2(520, 40), new Vector2(0.5f, 1f), new Vector2(0, -415), brown);
        ui.partyStatus.fontStyle = FontStyles.Normal;
        ui.startButton = Btn("Start_Button", panel, rounded, "START", new Color32(0x8C, 0xCF, 0xA4, 255), new Vector2(300, 100), new Vector2(0.5f, 0f), new Vector2(0, 150));
        ui.leaveButton = Btn("Leave_Button", panel, rounded, "Leave", new Color32(0xF4, 0xB8, 0xC1, 255), new Vector2(200, 64), new Vector2(0.5f, 0f), new Vector2(0, 60));

        // join-by-code button + panel (top-left)
        ui.openJoinButton = Btn("Join_Code_Button", go.transform, rounded, "Join with code", new Color32(0xC9, 0xDD, 0xF5, 255), new Vector2(320, 84), new Vector2(0f, 1f), new Vector2(200, -70));
        var jp = Box("Join_Panel", go.transform, rounded, new Color(1f, 0.96f, 0.9f, 0.97f), new Vector2(560, 340), new Vector2(0.5f, 0.5f), new Vector2(0, 40), true);
        ui.joinPanel = jp.gameObject;
        Txt("Join_Title", jp, "Join a friend's party", 40, new Vector2(520, 60), new Vector2(0.5f, 1f), new Vector2(0, -45), new Color32(0xE0, 0x6C, 0x4C, 255));
        var input = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources { inputField = rounded, background = rounded });
        input.name = "Code_Input"; input.transform.SetParent(jp, false);
        var irt = input.GetComponent<RectTransform>(); irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 1f); irt.sizeDelta = new Vector2(340, 80); irt.anchoredPosition = new Vector2(0, -130);
        var field = input.GetComponent<TMP_InputField>();
        field.characterLimit = 5; field.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        field.pointSize = 46; field.textComponent.alignment = TextAlignmentOptions.Center;
        if (field.placeholder is TMP_Text ph) { ph.text = "CODE"; ph.alignment = TextAlignmentOptions.Center; }
        ui.codeInput = field;
        ui.joinMessage = Txt("Join_Message", jp, "", 26, new Vector2(520, 40), new Vector2(0.5f, 1f), new Vector2(0, -195), brown);
        ui.joinMessage.fontStyle = FontStyles.Normal;
        ui.joinButton = Btn("Join_Button", jp, rounded, "Join", new Color32(0x8C, 0xCF, 0xA4, 255), new Vector2(200, 76), new Vector2(0.5f, 0f), new Vector2(-115, 55));
        ui.closeJoinButton = Btn("Close_Button", jp, rounded, "Close", new Color32(0xF4, 0xB8, 0xC1, 255), new Vector2(200, 76), new Vector2(0.5f, 0f), new Vector2(115, 55));

        // fade + countdown (on top)
        var fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        fadeGo.transform.SetParent(go.transform, false);
        var frt = fadeGo.GetComponent<RectTransform>(); frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
        fadeGo.GetComponent<Image>().color = new Color32(0x1B, 0x1D, 0x38, 255);
        ui.fade = fadeGo.GetComponent<CanvasGroup>();
        var cd = Txt("Countdown", go.transform, "", 220, new Vector2(400, 300), new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
        cd.outlineWidth = 0.2f; cd.outlineColor = new Color32(0xE0, 0x6C, 0x8C, 255);
        ui.countdown = cd;
        FarmLobbyPolish.ApplyTo(ui);
    }

    static RectTransform Box(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 anchor, Vector2 pos, bool sliced)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var rt = g.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.sprite = sprite; img.color = color; if (sliced) img.type = Image.Type.Sliced;
        return rt;
    }

    static Button Btn(string name, Transform parent, Sprite sprite, string label, Color color, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var rt = Box(name, parent, sprite, color, size, anchor, pos, true);
        var t = Txt("Label", rt, label, size.y * 0.42f, size, new Vector2(0.5f, 0.5f), Vector2.zero, Color.white);
        t.outlineWidth = 0.12f; t.outlineColor = new Color32(0x6B, 0x4A, 0x3A, 255);
        return rt.gameObject.AddComponent<Button>();
    }

    static TextMeshProUGUI Txt(string name, Transform parent, string text, float size, Vector2 box, Vector2 anchor, Vector2 pos, Color color)
    {
        var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = color; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = box; rt.anchoredPosition = pos;
        return t;
    }

    // ===================================================================== helpers
    static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 lp, Vector3 scale, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(t); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = lp; g.transform.localScale = scale;
        if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    static GameObject Pack_(string prefab, Transform parent, Vector3 pos, float height, float yaw, bool addCollider)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefab + ".prefab"); if (pf == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0) * pf.transform.rotation;
        go.transform.position = pos;
        var b = Bounds(go); if (b.size.y < 1e-4f) return go;
        go.transform.localScale = pf.transform.localScale * (height / b.size.y);
        b = Bounds(go);
        go.transform.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
        if (addCollider && go.GetComponentInChildren<Collider>() == null)
        {
            var bc = go.AddComponent<BoxCollider>();
            b = Bounds(go);
            bc.center = go.transform.InverseTransformPoint(b.center);
            var s = go.transform.lossyScale; bc.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
        }
        return go;
    }

    static void LoadMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Lobby");
        Material Find(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
        M = new Dictionary<string, Material>
        {
            ["grass"] = Find("Assets/Materials/Mat_grass.mat") ?? Mat("Mat_lobby_grass", "#B9D99A"),
            ["path"] = Find("Assets/Materials/Mat_walkways.mat") ?? Mat("Mat_lobby_path", "#E8D7B9"),
            ["cream"] = Mat("Mat_lobby_cream", "#FFF0D9"),
            ["pink"] = Mat("Mat_lobby_pink", "#F4B8C1"),
            ["mint"] = Mat("Mat_lobby_mint", "#C8E6D0"),
            ["wood"] = Mat("Mat_lobby_wood", "#9C7656"),
            ["pole"] = Find("Assets/Lighting/Mat_lamp_pole.mat") ?? Mat("Mat_lobby_pole", "#6C5B7B"),
            ["glow"] = Find("Assets/Lighting/Mat_lamp_glow.mat") ?? Mat("Mat_lobby_glow", "#FFE2A8", new Color(1f, 0.72f, 0.38f) * 3f),
            ["circle_idle"] = Mat("Mat_circle_idle", "#A8DCC0", new Color(0.35f, 0.75f, 0.55f) * 0.6f),
            ["circle_active"] = Mat("Mat_circle_active", "#F7A8C4", new Color(1f, 0.45f, 0.65f) * 1.4f),
        };
    }

    static Material Mat(string name, string hex, Color? emission = null)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (m != null) return m;
        ColorUtility.TryParseHtmlString(hex, out var c);
        m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c); m.SetFloat("_Smoothness", 0.3f);
        if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
