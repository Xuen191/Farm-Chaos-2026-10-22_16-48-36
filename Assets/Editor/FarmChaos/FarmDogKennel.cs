#if UNITY_EDITOR
// Farm Chaos - builds a cute pastel doghouse in the dog kennel area (door faces the path / HQ).
// Real arched opening (the dog can walk in and out), straw bed, WOOF sign, food bowl and a bone.
// Menu: Tools > Farm Chaos > 18. Build Dog Kennel   (re-run = rebuild)
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmDogKennel
{
    const string FarmPath = "Assets/Scenes/Farm_Main.unity";
    const string MatDir = "Assets/Materials/Kennel";

    [MenuItem("Tools/Farm Chaos/18. Build Dog Kennel")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.GetActiveScene().path == FarmPath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(FarmPath, OpenSceneMode.Single);
        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var area = all.FirstOrDefault(t => t.name.ToLowerInvariant().Replace("_", "").StartsWith("dogkennel"));
        if (area == null) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't find the dog kennel area in Farm_Main.", "OK"); return; }
        foreach (var old in all.Where(t => t != null && t.name == "Dog_Kennel").ToList()) Undo.DestroyObjectImmediate(old.gameObject);

        // materials
        var wall = Mat("Kennel_Wall", new Color32(0xC9, 0xDD, 0xF5, 255));
        var roof = Mat("Kennel_Roof", new Color32(0xF4, 0xB8, 0xC1, 255));
        var trim = Mat("Kennel_Trim", new Color32(0xFF, 0xF0, 0xD9, 255));
        var dark = Mat("Kennel_Dark", new Color32(0x3A, 0x33, 0x46, 255));
        var straw = Mat("Kennel_Straw", new Color32(0xD6, 0xB7, 0x5A, 255));
        var bowl = Mat("Kennel_Bowl", new Color32(0xE0, 0x6C, 0x8C, 255));
        var food = Mat("Kennel_Food", new Color32(0xA8, 0x7B, 0x5C, 255));
        var wood = Mat("Kennel_Wood", new Color32(0xD9, 0xB9, 0x9B, 255));

        // place: centre of the area, a little toward the back, door facing HQ
        var ar = area.GetComponent<Renderer>();
        Vector3 c = ar != null ? ar.bounds.center : area.position;
        float top = ar != null ? ar.bounds.max.y : area.position.y;
        Vector3 toHQ = -c; toHQ.y = 0; toHQ = toHQ.sqrMagnitude > 0.01f ? toHQ.normalized : Vector3.forward;

        var root = new GameObject("Dog_Kennel").transform;
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Dog kennel");
        root.SetParent(area.parent, true);
        root.position = new Vector3(c.x, top, c.z) - toHQ * 1.0f;
        root.rotation = Quaternion.LookRotation(toHQ);   // local +z = door side
        root.localScale = Vector3.one;

        const float W = 2.4f, D = 2.2f, H = 1.5f, T = 0.12f;   // width, depth, wall height, thickness
        const float doorW = 0.95f, doorH = 0.95f;               // straight part of the doorway (arch adds ~0.47 on top)
        var house = new GameObject("House").transform; house.SetParent(root, false);
        house.localScale = Vector3.one * 1.15f;

        // base + floor + straw bed
        Box(house, "Base", new Vector3(0, 0.05f, 0), new Vector3(W + 0.25f, 0.1f, D + 0.25f), wood, true);
        Box(house, "Straw_Bed", new Vector3(0, 0.13f, -0.25f), new Vector3(W - 0.5f, 0.06f, D - 0.8f), straw, false);
        // back + sides
        Box(house, "Wall_Back", new Vector3(0, H / 2 + 0.1f, -D / 2 + T / 2), new Vector3(W, H, T), wall, true);
        Box(house, "Wall_Left", new Vector3(-W / 2 + T / 2, H / 2 + 0.1f, 0), new Vector3(T, H, D), wall, true);
        Box(house, "Wall_Right", new Vector3(W / 2 - T / 2, H / 2 + 0.1f, 0), new Vector3(T, H, D), wall, true);
        // front with a real opening
        float side = (W - doorW) / 2f, fz = D / 2 - T / 2;
        Box(house, "Wall_Front_L", new Vector3(-W / 2 + side / 2, H / 2 + 0.1f, fz), new Vector3(side, H, T), wall, true);
        Box(house, "Wall_Front_R", new Vector3(W / 2 - side / 2, H / 2 + 0.1f, fz), new Vector3(side, H, T), wall, true);
        Box(house, "Wall_Front_Top", new Vector3(0, (doorH + H) / 2 + 0.1f, fz), new Vector3(doorW, H - doorH, T), wall, true);
        // arch: dark disc over the top of the opening makes it look rounded
        Semi(house, "Arch_Trim", new Vector3(0, doorH + 0.1f, fz + T / 2 + 0.006f), doorW + 0.18f, trim);
        Semi(house, "Arch_Dark", new Vector3(0, doorH + 0.1f, fz + T / 2 + 0.012f), doorW, dark);
        Box(house, "Door_Post_L", new Vector3(-doorW / 2 - 0.045f, doorH / 2 + 0.1f, fz + T / 2 + 0.005f), new Vector3(0.09f, doorH, 0.02f), trim, false);
        Box(house, "Door_Post_R", new Vector3(doorW / 2 + 0.045f, doorH / 2 + 0.1f, fz + T / 2 + 0.005f), new Vector3(0.09f, doorH, 0.02f), trim, false);
        Box(house, "Inside_Shade", new Vector3(0, H / 2 + 0.1f, -D / 2 + T + 0.005f), new Vector3(W - 2 * T, H, 0.01f), dark, false);   // dark inside
        // corner trims
        foreach (var x in new[] { -W / 2, W / 2 }) foreach (var z in new[] { -D / 2, D / 2 })
            Box(house, "Corner", new Vector3(x, H / 2 + 0.1f, z), new Vector3(0.16f, H + 0.02f, 0.16f), trim, false);

        // gables (triangles) front + back
        float rise = 0.95f;
        var gable = GableMesh();
        foreach (var z in new[] { D / 2 - T / 2, -D / 2 + T / 2 })
        {
            var g = new GameObject("Gable"); g.transform.SetParent(house, false);
            g.transform.localPosition = new Vector3(0, H + 0.1f, z);
            g.transform.localScale = new Vector3(W, rise, T);
            g.AddComponent<MeshFilter>().sharedMesh = gable;
            g.AddComponent<MeshRenderer>().sharedMaterial = wall;
        }
        // roof: two slabs + ridge
        float halfSpan = W / 2 + 0.22f, slope = Mathf.Atan2(rise, W / 2) * Mathf.Rad2Deg, len = halfSpan / Mathf.Cos(slope * Mathf.Deg2Rad);
        for (int s = -1; s <= 1; s += 2)
        {
            var slab = Box(house, s < 0 ? "Roof_L" : "Roof_R", Vector3.zero, new Vector3(len, 0.12f, D + 0.45f), roof, true);
            slab.localRotation = Quaternion.Euler(0, 0, -s * slope);
            slab.localPosition = new Vector3(s * (len / 2) * Mathf.Cos(slope * Mathf.Deg2Rad) - s * 0.01f, H + 0.1f + rise - (len / 2) * Mathf.Sin(slope * Mathf.Deg2Rad) + 0.06f, 0);
        }
        var ridge = Cyl(house, "Ridge", new Vector3(0, H + 0.1f + rise + 0.1f, 0), new Vector3(0.16f, (D + 0.5f) / 2f, 0.16f), trim);
        ridge.localRotation = Quaternion.Euler(90, 0, 0);

        // WOOF sign above the door
        var sign = Box(house, "Sign", new Vector3(0, H + 0.42f, D / 2 + 0.02f), new Vector3(0.95f, 0.32f, 0.05f), trim, false);
        var tg = new GameObject("Sign_Text"); tg.transform.SetParent(house, false);
        tg.transform.localPosition = new Vector3(0, H + 0.42f, D / 2 + 0.05f);
        tg.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var tmp = tg.AddComponent<TextMeshPro>();
        tmp.text = "WOOF"; tmp.fontSize = 2.4f; tmp.fontStyle = FontStyles.Bold; tmp.color = new Color32(0x6B, 0x4A, 0x3A, 255);
        tmp.alignment = TextAlignmentOptions.Center; tmp.rectTransform.sizeDelta = new Vector2(0.9f, 0.3f); tmp.textWrappingMode = TextWrappingModes.NoWrap;

        // food bowl + bone in front
        var bowlT = new GameObject("Food_Bowl").transform; bowlT.SetParent(root, false); bowlT.localPosition = new Vector3(1.0f, 0, 2.2f);
        Cyl(bowlT, "Bowl", new Vector3(0, 0.08f, 0), new Vector3(0.55f, 0.08f, 0.55f), bowl);
        Cyl(bowlT, "Food", new Vector3(0, 0.155f, 0), new Vector3(0.44f, 0.01f, 0.44f), food);
        var bone = new GameObject("Bone").transform; bone.SetParent(root, false); bone.localPosition = new Vector3(-0.9f, 0.08f, 2.0f); bone.localRotation = Quaternion.Euler(0, 35, 0);
        var shaft = Cyl(bone, "Shaft", Vector3.zero, new Vector3(0.09f, 0.22f, 0.09f), trim); shaft.localRotation = Quaternion.Euler(0, 0, 90);
        foreach (var x in new[] { -0.24f, 0.24f }) foreach (var z in new[] { -0.05f, 0.05f })
            Sph(bone, "Knob", new Vector3(x, 0, z), Vector3.one * 0.12f, trim);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[Farm Chaos] Dog kennel built (Dog_Kennel) with its door facing the path. Re-run to rebuild.");
    }

    // ---------------------------------------------------------------- helpers
    static Material Mat(string name, Color c)
    {
        if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Kennel");
        string p = MatDir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            AssetDatabase.CreateAsset(m, p);
        }
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh GableMesh()
    {
        string p = MatDir + "/Kennel_Gable.asset";
        var m = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (m != null) return m;
        // unit triangular prism: base from x -0.5..0.5 at y 0, peak at y 1, z -0.5..0.5 (two-sided so winding can't hide it)
        Vector3 a = new Vector3(-0.5f, 0, 0), b = new Vector3(0.5f, 0, 0), t = new Vector3(0, 1, 0), f = new Vector3(0, 0, 0.5f), k = new Vector3(0, 0, -0.5f);
        var tris = new System.Collections.Generic.List<Vector3[]>
        {
            new[] { a + f, b + f, t + f }, new[] { a + k, b + k, t + k },
            new[] { a + f, t + f, t + k }, new[] { a + f, t + k, a + k },
            new[] { b + f, t + f, t + k }, new[] { b + f, t + k, b + k },
            new[] { a + f, b + f, b + k }, new[] { a + f, b + k, a + k },
        };
        var verts = new System.Collections.Generic.List<Vector3>(); var idx = new System.Collections.Generic.List<int>();
        foreach (var tr in tris)
        {
            int i0 = verts.Count; verts.AddRange(tr); idx.AddRange(new[] { i0, i0 + 1, i0 + 2 });
            int i1 = verts.Count; verts.AddRange(tr); idx.AddRange(new[] { i1, i1 + 2, i1 + 1 });
        }
        m = new Mesh { name = "Kennel_Gable" };
        m.SetVertices(verts); m.SetTriangles(idx, 0);
        m.RecalculateNormals(); m.RecalculateBounds();
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    // flat half-disc (arch top), facing +z, flat edge at the bottom
    static Transform Semi(Transform parent, string name, Vector3 pos, float diameter, Material m)
    {
        string p = MatDir + "/Kennel_SemiDisc.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
        if (mesh == null)
        {
            var v = new System.Collections.Generic.List<Vector3> { Vector3.zero };
            const int seg = 24;
            for (int i = 0; i <= seg; i++) { float a = Mathf.PI * i / seg; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, Mathf.Sin(a) * 0.5f, 0)); }
            int n = v.Count; v.AddRange(v.ToArray());   // second copy for the back face
            var tri = new System.Collections.Generic.List<int>();
            for (int i = 1; i <= seg; i++) { tri.AddRange(new[] { 0, i, i + 1 }); tri.AddRange(new[] { n, n + i + 1, n + i }); }
            mesh = new Mesh { name = "Kennel_SemiDisc" };
            mesh.SetVertices(v); mesh.SetTriangles(tri, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, p);
        }
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = pos; g.transform.localScale = new Vector3(diameter, diameter, 1f);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        g.isStatic = true;
        return g.transform;
    }

    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        g.isStatic = true;
        return g.transform;
    }

    static Transform Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        if (name.StartsWith("Arch")) g.transform.localRotation = Quaternion.Euler(90, 0, 0);
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.isStatic = true;
        return g.transform;
    }

    static Transform Sph(Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        g.isStatic = true;
        return g.transform;
    }
}
#endif
