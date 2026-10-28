#if UNITY_EDITOR
// Farm Chaos - builds an enterable Zoo HQ and Power Station on their pads, with automatic doors.
// Menu: Tools > Farm Chaos > 5. Build Zoo HQ + Power Station
// Re-running replaces only "Zoo_HQ_Building" and "PowerStation_Building". Everything supports Ctrl+Z.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmBuildingBuilder
{
    const string HQName = "Zoo_HQ_Building";
    const string PSName = "PowerStation_Building";
    const string MatFolder = "Assets/Materials/Buildings";
    const string Pack = "Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/";

    static Dictionary<string, Material> M;

    [MenuItem("Tools/Farm Chaos/5. Build Zoo HQ + Power Station")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        foreach (var t in all.Where(t => t != null && (t.name == HQName || t.name == PSName)).ToList())
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();

        LoadMaterials();
        Transform parent = scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "Buildings");
        if (parent == null) { parent = new GameObject("Buildings").transform; Undo.RegisterCreatedObjectUndo(parent.gameObject, "Buildings"); }

        var hqPad = all.FirstOrDefault(t => Norm(t.name) == "hqarea");
        var psPad = all.FirstOrDefault(t => Norm(t.name) == "powerstationarea");

        if (hqPad != null)
        {
            var b = hqPad.GetComponent<Renderer>().bounds;
            float yaw = hqPad.eulerAngles.y;
            // building sits toward the back (north) of the pad, front door faces south toward the power station
            Vector3 pos = new Vector3(b.center.x, b.max.y, b.center.z) + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, 1f);
            BuildHQ(parent, pos, yaw + 180f);
        }
        else Debug.LogWarning("[Farm Chaos] hq_area not found.");

        if (psPad != null)
        {
            var b = psPad.GetComponent<Renderer>().bounds;
            float yaw = psPad.eulerAngles.y;
            // building sits toward the back (south) of the pad, door faces north toward HQ
            Vector3 pos = new Vector3(b.center.x, b.max.y, b.center.z) + Quaternion.Euler(0, yaw, 0) * new Vector3(-0.6f, 0, -0.8f);
            BuildPowerStation(parent, pos, yaw);
        }
        else Debug.LogWarning("[Farm Chaos] powerstation_area not found.");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Farm Chaos] Zoo HQ and Power Station built (scene saved). Roll the sphere up to a door to open it.");
    }

    static string Norm(string s) => s.ToLowerInvariant().Replace("_", "").Replace(" ", "");

    // =====================================================================  ZOO HQ
    static void BuildHQ(Transform parent, Vector3 pos, float yaw)
    {
        float w = 11f, d = 8.5f, H = 3.4f, t = 0.25f, dw = 2.6f, dh = 2.5f;
        var root = Root(HQName, parent, pos, yaw);

        Box("Floor", root, new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), M["hq_floor"]);
        var walls = Shell(root, w, d, H, t, dw, dh, M["hq_wall"], M["trim"]);

        // windows: front (either side of the door), sides, back
        Window(walls, new Vector3(-3.3f, 1.7f, d / 2 - t / 2), 0, t);
        Window(walls, new Vector3(3.3f, 1.7f, d / 2 - t / 2), 0, t);
        Window(walls, new Vector3(-w / 2 + t / 2, 1.7f, 0), 90, t);
        Window(walls, new Vector3(w / 2 - t / 2, 1.7f, 0), 90, t);
        Window(walls, new Vector3(-2.5f, 1.7f, -d / 2 + t / 2), 0, t);
        Window(walls, new Vector3(2.5f, 1.7f, -d / 2 + t / 2), 0, t);

        // corner trims
        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
            Box("Corner_Trim", walls, new Vector3(sx * (w / 2 - 0.05f), H / 2, sz * (d / 2 - 0.05f)), new Vector3(0.36f, H, 0.36f), M["trim"]);

        // gable roof (ridge runs left-right)
        var roof = new GameObject("Roof").transform; roof.SetParent(root, false);
        float slope = 22f, ov = 0.6f, tr = 0.2f, tan = Mathf.Tan(slope * Mathf.Deg2Rad);
        float s = d / 2 + ov, Ls = s / Mathf.Cos(slope * Mathf.Deg2Rad), c = s / 2f;
        float hr = (d / 2) * tan;
        foreach (var side in new[] { 1, -1 })
        {
            float y = H + (d / 2 - c) * tan + tr / 2f / Mathf.Cos(slope * Mathf.Deg2Rad);
            var slab = Box(side > 0 ? "Roof_Front" : "Roof_Back", roof, new Vector3(0, y, side * c), new Vector3(w + ov * 2, tr, Ls + 0.05f), M["hq_roof"]);
            slab.transform.localRotation = Quaternion.Euler(side * slope, 0, 0);
        }
        Box("Ridge", roof, new Vector3(0, H + hr + tr * 1.1f, 0), new Vector3(w + ov * 2 + 0.1f, 0.18f, 0.4f), M["trim"]);
        foreach (var sx in new[] { -1, 1 })
            Prism("Gable", roof, new Vector3(sx * (w / 2 - t / 2), H, 0), d, hr, t, M["hq_wall"]);
        Box("Chimney", roof, new Vector3(3f, H + hr * 0.6f + 0.6f, -1.2f), new Vector3(0.7f, 1.4f, 0.7f), M["trim"]);

        // sign over the door (outside)
        Box("Sign_Board", root, new Vector3(0, 2.98f, d / 2 + 0.08f), new Vector3(3.8f, 0.75f, 0.1f), M["trim"], false);
        Text("Sign_Text", root, "ZOO HQ", new Vector3(0, 2.98f, d / 2 + 0.14f), 180, new Vector2(3.5f, 0.6f), new Color32(0x6B, 0x4A, 0x3A, 255));

        // porch: little step + lamp posts
        Box("Porch", root, new Vector3(0, 0.05f, d / 2 + 0.9f), new Vector3(dw + 1.6f, 0.1f, 1.8f), M["wood"]);
        foreach (var sx in new[] { -1, 1 })
        {
            Box("Lamp_Post", root, new Vector3(sx * (dw / 2 + 1.1f), 1.0f, d / 2 + 1.5f), new Vector3(0.14f, 2f, 0.14f), M["dark"]);
            Box("Lamp", root, new Vector3(sx * (dw / 2 + 1.1f), 2.1f, d / 2 + 1.5f), new Vector3(0.35f, 0.35f, 0.35f), M["lamp"], false);
        }

        // ---- interior
        var inside = new GameObject("Interior").transform; inside.SetParent(root, false);
        Box("Rug", inside, new Vector3(0, 0.11f, 0.8f), new Vector3(4.2f, 0.02f, 3f), M["hq_roof"], false);
        // shop counter
        Box("Counter", inside, new Vector3(0, 0.6f, -d / 2 + 1.9f), new Vector3(4f, 1.0f, 0.9f), M["trim"]);
        Box("Counter_Top", inside, new Vector3(0, 1.14f, -d / 2 + 1.9f), new Vector3(4.2f, 0.08f, 1.05f), M["wood"]);
        Box("Cash_Register", inside, new Vector3(1.3f, 1.35f, -d / 2 + 1.9f), new Vector3(0.6f, 0.35f, 0.45f), M["dark"], false);
        Box("Coin_Pile", inside, new Vector3(-1.2f, 1.22f, -d / 2 + 1.9f), new Vector3(0.35f, 0.08f, 0.35f), M["ps_yellow"], false);
        Box("Shop_Board", inside, new Vector3(0, 2.4f, -d / 2 + t + 0.06f), new Vector3(3f, 0.8f, 0.08f), M["hq_roof"], false);
        Text("Shop_Text", inside, "SHOP", new Vector3(0, 2.4f, -d / 2 + t + 0.11f), 180, new Vector2(2.7f, 0.6f), Color.white);
        // supplies (bait/tools) from the pack along the left wall
        string[] stock = { "Prop_FoodSack_01", "Prop_FoodSack_02", "Prop_WoodenBox_01", "Prop_WoodenCrates_01", "Prop_Bucket_01", "Prop_FoodSack_03", "Prop_Berrel_03" };
        float[] heights = { 0.9f, 0.9f, 0.8f, 1.0f, 0.5f, 0.9f, 1.0f };
        for (int i = 0; i < stock.Length; i++)
            PackProp(stock[i], inside, new Vector3(-w / 2 + 0.8f, 0.1f, -d / 2 + 1.1f + i * 1.0f), heights[i], i * 37f);
        // task board on the right wall
        Box("Task_Board", inside, new Vector3(w / 2 - t - 0.05f, 1.8f, 0.6f), new Vector3(0.08f, 1.3f, 2.4f), M["wood"], false);
        for (int i = 0; i < 6; i++)
            Box("Task_Note", inside, new Vector3(w / 2 - t - 0.1f, 1.55f + (i / 3) * 0.55f, -0.15f + (i % 3) * 0.75f), new Vector3(0.02f, 0.4f, 0.5f), M["hq_wall"], false);
        Text("Tasks_Text", inside, "TASKS", new Vector3(w / 2 - t - 0.11f, 2.65f, 0.6f), 90, new Vector2(2f, 0.4f), new Color32(0x6B, 0x4A, 0x3A, 255));

        Door(root, new Vector3(0, 0, d / 2 - t / 2), dw, dh, AutoDoor.Mode.Swing, M["door"], M["glass"]);
        RoofTrigger(root, roof, w, d, H, t);
    }

    // =====================================================================  POWER STATION
    static void BuildPowerStation(Transform parent, Vector3 pos, float yaw)
    {
        float w = 8f, d = 5.6f, H = 3.6f, t = 0.25f, dw = 2.4f, dh = 2.6f;
        var root = Root(PSName, parent, pos, yaw);

        Box("Floor", root, new Vector3(0, 0.05f, 0), new Vector3(w, 0.1f, d), M["ps_floor"]);
        var walls = Shell(root, w, d, H, t, dw, dh, M["ps_wall"], M["ps_yellow"]);
        // hazard band near the top of the walls (outside)
        Box("Band_Front", walls, new Vector3(0, H - 0.35f, d / 2 + 0.01f), new Vector3(w + 0.04f, 0.3f, 0.04f), M["ps_yellow"], false);
        Box("Band_Back", walls, new Vector3(0, H - 0.35f, -d / 2 - 0.01f), new Vector3(w + 0.04f, 0.3f, 0.04f), M["ps_yellow"], false);
        Box("Band_Left", walls, new Vector3(-w / 2 - 0.01f, H - 0.35f, 0), new Vector3(0.04f, 0.3f, d + 0.04f), M["ps_yellow"], false);
        Box("Band_Right", walls, new Vector3(w / 2 + 0.01f, H - 0.35f, 0), new Vector3(0.04f, 0.3f, d + 0.04f), M["ps_yellow"], false);
        Window(walls, new Vector3(-w / 2 + t / 2, 2.1f, 0), 90, t, 1.2f, 0.7f);
        Window(walls, new Vector3(w / 2 - t / 2, 2.1f, 0), 90, t, 1.2f, 0.7f);

        // flat roof with vents
        var roof = new GameObject("Roof").transform; roof.SetParent(root, false);
        Box("Roof_Slab", roof, new Vector3(0, H + 0.13f, 0), new Vector3(w + 0.5f, 0.26f, d + 0.5f), M["ps_roof"]);
        Box("Roof_Edge", roof, new Vector3(0, H + 0.32f, d / 2 + 0.2f), new Vector3(w + 0.5f, 0.14f, 0.1f), M["ps_yellow"], false);
        for (int i = 0; i < 2; i++)
        {
            var v = Cyl("Vent", roof, new Vector3(-2f + i * 1.6f, H + 0.7f, -0.8f), new Vector3(0.7f, 0.45f, 0.7f), M["metal"]);
            Cyl("Vent_Cap", roof, v.transform.localPosition + Vector3.up * 0.5f, new Vector3(0.9f, 0.06f, 0.9f), M["dark"]);
        }

        // sign over the door
        Box("Sign_Board", root, new Vector3(0, 3.05f, d / 2 + 0.08f), new Vector3(3f, 0.7f, 0.1f), M["ps_yellow"], false);
        Text("Sign_Text", root, "POWER STATION", new Vector3(0, 3.05f, d / 2 + 0.14f), 180, new Vector2(2.8f, 0.5f), new Color32(0x2E, 0x32, 0x38, 255));

        // outside: transformers on the right, a power pole on the left
        for (int i = 0; i < 2; i++)
        {
            float z = -1.2f + i * 2.2f;
            Box("Transformer", root, new Vector3(w / 2 + 1.3f, 0.9f, z), new Vector3(1.3f, 1.8f, 1.6f), M["metal"]);
            Box("Transformer_Plate", root, new Vector3(w / 2 + 1.96f, 1.1f, z), new Vector3(0.02f, 0.45f, 0.45f), M["ps_yellow"], false);
            for (int k = -1; k <= 1; k++)
                Cyl("Insulator", root, new Vector3(w / 2 + 1.3f, 2.0f, z + k * 0.45f), new Vector3(0.18f, 0.2f, 0.18f), M["hq_roof"], false);
        }
        Cyl("Power_Pole", root, new Vector3(-w / 2 - 1.2f, 2.5f, 1.0f), new Vector3(0.28f, 2.5f, 0.28f), M["wood_dark"]);
        Box("Pole_Crossbar", root, new Vector3(-w / 2 - 1.2f, 4.6f, 1.0f), new Vector3(0.15f, 0.15f, 1.8f), M["wood_dark"], false);
        foreach (var k in new[] { -0.75f, 0f, 0.75f })
            Cyl("Pole_Insulator", root, new Vector3(-w / 2 - 1.2f, 4.78f, 1.0f + k), new Vector3(0.14f, 0.12f, 0.14f), M["ps_yellow"], false);

        // ---- interior
        var inside = new GameObject("Interior").transform; inside.SetParent(root, false);
        // generator
        Box("Generator_Base", inside, new Vector3(-1.9f, 0.3f, -0.9f), new Vector3(2.8f, 0.4f, 1.8f), M["dark"]);
        var gen = Cyl("Generator", inside, new Vector3(-1.9f, 1.25f, -0.9f), new Vector3(1.5f, 1.2f, 1.5f), M["ps_yellow"]);
        gen.transform.localRotation = Quaternion.Euler(0, 0, 90);
        Cyl("Generator_End", inside, new Vector3(-0.65f, 1.25f, -0.9f), new Vector3(1.0f, 0.06f, 1.0f), M["metal"], false)
            .transform.localRotation = Quaternion.Euler(0, 0, 90);
        // control panel on the back wall
        Box("Control_Panel", inside, new Vector3(1.9f, 1.0f, -d / 2 + t + 0.35f), new Vector3(2.6f, 1.8f, 0.6f), M["metal"]);
        Box("Panel_Screen", inside, new Vector3(1.9f, 1.45f, -d / 2 + t + 0.66f), new Vector3(1.4f, 0.6f, 0.02f), M["screen"], false);
        Material[] lights = { M["light_green"], M["light_red"], M["light_yellow"] };
        for (int i = 0; i < 6; i++)
            Box("Panel_Light", inside, new Vector3(1.25f + i * 0.26f, 0.85f, -d / 2 + t + 0.66f), new Vector3(0.14f, 0.14f, 0.03f), lights[i % 3], false);
        // cable from generator to panel
        var cable = Cyl("Cable", inside, new Vector3(0.2f, 0.14f, -1.6f), new Vector3(0.12f, 1.3f, 0.12f), M["dark"], false);
        cable.transform.localRotation = Quaternion.Euler(0, 0, 90);
        // reserved spot for the electrical box (friend's part)
        var spot = new GameObject("ElectricalBox_Spot").transform; spot.SetParent(inside, false);
        spot.localPosition = new Vector3(w / 2 - t - 0.05f, 1.3f, 0.2f); spot.localRotation = Quaternion.Euler(0, 90, 0);
        Box("Spot_Frame_Top", spot, new Vector3(0, 0.75f, 0), new Vector3(1.4f, 0.06f, 0.02f), M["ps_yellow"], false);
        Box("Spot_Frame_Bottom", spot, new Vector3(0, -0.75f, 0), new Vector3(1.4f, 0.06f, 0.02f), M["ps_yellow"], false);
        Box("Spot_Frame_L", spot, new Vector3(-0.7f, 0, 0), new Vector3(0.06f, 1.5f, 0.02f), M["ps_yellow"], false);
        Box("Spot_Frame_R", spot, new Vector3(0.7f, 0, 0), new Vector3(0.06f, 1.5f, 0.02f), M["ps_yellow"], false);
        Text("Spot_Text", spot, "ELECTRICAL BOX", new Vector3(0, 0, -0.02f), 0, new Vector2(1.2f, 0.3f), new Color32(0x2E, 0x32, 0x38, 255));

        Door(root, new Vector3(0, 0, d / 2 - t / 2), dw, dh, AutoDoor.Mode.Slide, M["ps_yellow"], M["glass"]);
        RoofTrigger(root, roof, w, d, H, t);
    }

    // =====================================================================  pieces
    static Transform Root(string name, Transform parent, Vector3 pos, float yaw)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Build Buildings");
        go.transform.SetParent(parent, true);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        return go.transform;
    }

    // four walls with a door gap in the front (+Z) wall
    static Transform Shell(Transform root, float w, float d, float H, float t, float dw, float dh, Material wall, Material trim)
    {
        var walls = new GameObject("Walls").transform; walls.SetParent(root, false);
        Box("Wall_Back", walls, new Vector3(0, H / 2, -d / 2 + t / 2), new Vector3(w, H, t), wall);
        Box("Wall_Left", walls, new Vector3(-w / 2 + t / 2, H / 2, 0), new Vector3(t, H, d - 2 * t), wall);
        Box("Wall_Right", walls, new Vector3(w / 2 - t / 2, H / 2, 0), new Vector3(t, H, d - 2 * t), wall);
        float seg = (w - dw) / 2f;
        Box("Wall_Front_L", walls, new Vector3(-(dw / 2 + seg / 2), H / 2, d / 2 - t / 2), new Vector3(seg, H, t), wall);
        Box("Wall_Front_R", walls, new Vector3(dw / 2 + seg / 2, H / 2, d / 2 - t / 2), new Vector3(seg, H, t), wall);
        Box("Wall_Front_Top", walls, new Vector3(0, dh + (H - dh) / 2, d / 2 - t / 2), new Vector3(dw, H - dh, t), wall);
        // door frame
        Box("Frame_L", walls, new Vector3(-dw / 2 - 0.06f, dh / 2, d / 2 - t / 2), new Vector3(0.14f, dh, t + 0.08f), trim);
        Box("Frame_R", walls, new Vector3(dw / 2 + 0.06f, dh / 2, d / 2 - t / 2), new Vector3(0.14f, dh, t + 0.08f), trim);
        Box("Frame_Top", walls, new Vector3(0, dh + 0.07f, d / 2 - t / 2), new Vector3(dw + 0.26f, 0.14f, t + 0.08f), trim);
        // skirting along the bottom outside
        Box("Base_Trim", walls, new Vector3(0, 0.15f, -d / 2 - 0.02f), new Vector3(w + 0.06f, 0.3f, 0.06f), trim, false);
        return walls;
    }

    static void Window(Transform parent, Vector3 pos, float yRot, float t, float ww = 1.4f, float wh = 1.0f)
    {
        var g = new GameObject("Window").transform; g.SetParent(parent, false);
        g.localPosition = pos; g.localRotation = Quaternion.Euler(0, yRot, 0);
        Box("Frame", g, Vector3.zero, new Vector3(ww + 0.2f, wh + 0.2f, t + 0.04f), M["window_frame"], false);
        Box("Glass", g, Vector3.zero, new Vector3(ww, wh, t + 0.08f), M["glass"], false);
        Box("Bar", g, Vector3.zero, new Vector3(0.06f, wh, t + 0.1f), M["window_frame"], false);
    }

    static void Door(Transform root, Vector3 pos, float dw, float dh, AutoDoor.Mode mode, Material panel, Material glass)
    {
        var door = new GameObject("Door").transform; door.SetParent(root, false);
        door.localPosition = pos;
        var ad = door.gameObject.AddComponent<AutoDoor>();
        ad.mode = mode;
        var trig = door.gameObject.AddComponent<BoxCollider>();
        trig.isTrigger = true; trig.center = new Vector3(0, 1.5f, 0); trig.size = new Vector3(dw + 1.5f, 3f, 5.5f);

        if (mode == AutoDoor.Mode.Swing)
        {
            var leaves = new List<Transform>(); var signs = new List<float>();
            foreach (var side in new[] { 1f, -1f })   // +1 = left leaf (hinge on the left, extends +X)
            {
                var hinge = new GameObject(side > 0 ? "Hinge_L" : "Hinge_R").transform; hinge.SetParent(door, false);
                hinge.localPosition = new Vector3(-side * dw / 2f, 0, 0);
                float L = dw / 2f - 0.03f;
                Box("Leaf", hinge, new Vector3(side * L / 2f, dh / 2f, 0), new Vector3(L, dh - 0.04f, 0.1f), panel);
                Box("Leaf_Window", hinge, new Vector3(side * L / 2f, dh * 0.7f, 0), new Vector3(L * 0.55f, dh * 0.25f, 0.13f), glass, false);
                Box("Knob", hinge, new Vector3(side * (L - 0.15f), dh * 0.45f, 0), new Vector3(0.08f, 0.08f, 0.2f), M["ps_yellow"], false);
                var rb = hinge.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
                leaves.Add(hinge); signs.Add(side);
            }
            ad.leaves = leaves.ToArray(); ad.leafSigns = signs.ToArray();
            ad.openAngle = 95f;
        }
        else
        {
            // one sliding panel that slides along the inside of the wall
            var p = new GameObject("Slide_Panel").transform; p.SetParent(door, false);
            p.localPosition = new Vector3(0, 0, -0.22f);
            Box("Panel", p, new Vector3(0, dh / 2f, 0), new Vector3(dw + 0.2f, dh, 0.1f), panel);
            for (int i = 0; i < 4; i++)
                Box("Stripe", p, new Vector3(0, 0.35f + i * 0.6f, 0), new Vector3(dw + 0.21f, 0.18f, 0.12f), M["dark"], false);
            Box("Handle", p, new Vector3(-dw / 2f + 0.25f, dh * 0.45f, 0.08f), new Vector3(0.08f, 0.5f, 0.08f), M["metal"], false);
            var rb = p.gameObject.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            ad.leaves = new[] { p }; ad.leafSigns = new[] { 1f };
            ad.slideDistance = dw + 0.15f;
        }
    }

    static void RoofTrigger(Transform root, Transform roof, float w, float d, float H, float t)
    {
        var g = new GameObject("Inside_Trigger"); g.transform.SetParent(root, false);
        var bc = g.AddComponent<BoxCollider>(); bc.isTrigger = true;
        bc.center = new Vector3(0, H / 2, 0); bc.size = new Vector3(w - 2 * t - 0.2f, H, d - 2 * t - 0.2f);
        g.AddComponent<RoofHider>().roof = roof;
    }

    static GameObject Box(string name, Transform parent, Vector3 lp, Vector3 size, Material mat, bool collider = true)
        => Prim(PrimitiveType.Cube, name, parent, lp, size, mat, collider);

    static GameObject Cyl(string name, Transform parent, Vector3 lp, Vector3 size, Material mat, bool collider = true)
        => Prim(PrimitiveType.Cylinder, name, parent, lp, size, mat, collider);

    static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 lp, Vector3 size, Material mat, bool collider)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = lp;
        g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g;
    }

    // triangular gable end: base d along Z, height h, thickness t along X
    static void Prism(string name, Transform parent, Vector3 lp, float d, float h, float t, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = lp;
        float x = t / 2;
        Vector3 a0 = new Vector3(-x, 0, -d / 2), b0 = new Vector3(-x, 0, d / 2), c0 = new Vector3(-x, h, 0);
        Vector3 a1 = new Vector3(x, 0, -d / 2), b1 = new Vector3(x, 0, d / 2), c1 = new Vector3(x, h, 0);
        var v = new List<Vector3>(); var tri = new List<int>();
        void Tri(Vector3 p, Vector3 q, Vector3 r) { int i = v.Count; v.Add(p); v.Add(q); v.Add(r); v.Add(p); v.Add(r); v.Add(q); tri.AddRange(new[] { i, i + 1, i + 2, i + 3, i + 4, i + 5 }); } // both sides
        void Quad(Vector3 p, Vector3 q, Vector3 r, Vector3 s) { Tri(p, q, r); Tri(p, r, s); }
        Tri(a0, c0, b0);              // -X face
        Tri(a1, b1, c1);              // +X face
        Quad(a0, a1, c1, c0);         // back slope
        Quad(b0, c0, c1, b1);         // front slope
        Quad(a0, b0, b1, a1);         // bottom
        var m = new Mesh { name = name + "_Mesh" };
        m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
        g.AddComponent<MeshFilter>().sharedMesh = m;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void Text(string name, Transform parent, string text, Vector3 lp, float yRot, Vector2 size, Color color)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = Quaternion.Euler(0, yRot, 0);
        var tmp = g.AddComponent<TextMeshPro>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.1f; tmp.fontSizeMax = 30f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = color;
        tmp.rectTransform.sizeDelta = size;
    }

    static void PackProp(string prefab, Transform parent, Vector3 lp, float height, float yRot)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + prefab + ".prefab");
        if (pf == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent);
        go.transform.localPosition = lp;
        go.transform.localRotation = Quaternion.Euler(0, yRot, 0) * pf.transform.localRotation;
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        if (b.size.y < 1e-4f) return;
        go.transform.localScale = pf.transform.localScale * (height / b.size.y);
        rs = go.GetComponentsInChildren<Renderer>();
        b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var floorY = parent.TransformPoint(lp).y;
        go.transform.position += new Vector3(0, floorY - b.min.y, 0);
    }

    // =====================================================================  materials
    static void LoadMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Buildings");
        M = new Dictionary<string, Material>
        {
            ["hq_wall"] = Mat("Mat_hq_wall", "#FFF0D9"),        // vanilla cream
            ["hq_roof"] = Mat("Mat_hq_roof", "#F4B8C1"),        // pastel pink
            ["hq_floor"] = Mat("Mat_hq_floor", "#E8D3B9"),
            ["trim"] = Mat("Mat_building_trim", "#C8E6D0"),     // mint green
            ["wood"] = Mat("Mat_building_wood", "#D9B99B"),     // soft wood brown
            ["wood_dark"] = Mat("Mat_building_wood_dark", "#9C7656"),
            ["door"] = Mat("Mat_hq_door", "#B98A63"),
            ["glass"] = Mat("Mat_window_glass", "#C9DDF5", 0.9f), // baby blue
            ["window_frame"] = Mat("Mat_window_frame", "#FFFFFF"),
            ["lamp"] = Mat("Mat_lamp", "#FFF3C4", 0.5f, new Color(1f, 0.9f, 0.6f) * 1.5f),
            ["dark"] = Mat("Mat_building_dark", "#3A3F47"),
            ["metal"] = Mat("Mat_ps_metal", "#8D96A3", 0.5f),
            ["ps_wall"] = Mat("Mat_ps_wall", "#D9E1EA"),
            ["ps_floor"] = Mat("Mat_ps_floor", "#9AA2AC"),
            ["ps_roof"] = Mat("Mat_ps_roof", "#5A606B"),
            ["ps_yellow"] = Mat("Mat_ps_yellow", "#FFD23F"),
            ["screen"] = Mat("Mat_ps_screen", "#9FD8F5", 0.8f, new Color(0.45f, 0.8f, 1f) * 1.2f),
            ["light_green"] = Mat("Mat_light_green", "#6BE38A", 0.5f, new Color(0.3f, 1f, 0.45f) * 2f),
            ["light_red"] = Mat("Mat_light_red", "#F26B6B", 0.5f, new Color(1f, 0.25f, 0.25f) * 2f),
            ["light_yellow"] = Mat("Mat_light_yellow", "#FFE066", 0.5f, new Color(1f, 0.85f, 0.2f) * 2f),
        };
    }

    static Material Mat(string name, string hex, float smooth = 0.3f, Color? emission = null)
    {
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        ColorUtility.TryParseHtmlString(hex, out var c);
        m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.SetFloat("_Smoothness", smooth);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
