#if UNITY_EDITOR
// Farm Chaos - swap Pandazole nature for the Simple Nature Pack.
// Menu: Tools > Farm Chaos > 16. Swap To Simple Nature Pack
//  - converts the pack's material from the old Standard shader to URP Lit (otherwise it renders pink)
//  - Farm_Main + Lobby: Pandazole trees / bushes / grass plants -> Simple Nature trees / bushes / grass + flowers
//    (same spot, same turn, similar size). Buildings, fences and farm props are kept (the new pack has none).
//  - lays the flattest Simple Nature ground (Ground_03) over the floor, squashed so bumps are < 10 cm.
//    The old flat floor stays as the invisible thing the ball rolls on, so rolling stays smooth.
//  - backs both scenes up to Backups/ first.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmNatureSwap
{
    const string Pack = "Assets/SimpleNaturePack";
    static readonly string[] Scenes = { "Assets/Scenes/Farm_Main.unity", "Assets/Scenes/Lobby.unity" };
    const float MaxBump = 0.1f;

    [MenuItem("Tools/Farm Chaos/16. Swap To Simple Nature Pack")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!AssetDatabase.IsValidFolder(Pack)) { EditorUtility.DisplayDialog("Farm Chaos", "Simple Nature Pack not found in Assets/SimpleNaturePack.", "OK"); return; }
        if (!EditorUtility.DisplayDialog("Farm Chaos", "Swap Pandazole trees, bushes and grass for the Simple Nature Pack in Farm_Main and Lobby, and lay the new ground?\n\nBoth scenes are backed up to the Backups folder first.", "Swap", "Cancel")) return;

        FixMaterials();
        EditorSceneManager.SaveOpenScenes();
        string returnTo = SceneManager.GetActiveScene().path;

        // backup
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Directory.CreateDirectory("Backups");
        foreach (var s in Scenes) if (File.Exists(s)) File.Copy(s, $"Backups/{Path.GetFileNameWithoutExtension(s)}_before_nature_{stamp}.unity", true);

        var log = new List<string>();
        foreach (var sp in Scenes)
        {
            if (!File.Exists(sp)) continue;
            var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
            int trees = 0, bushes = 0, grass = 0;
            var rng = new System.Random(sp.Length * 31);
            foreach (var inst in AllPrefabRoots(scene))
            {
                string src = SourceName(inst);
                if (src == null) continue;
                if (src.StartsWith("Env_Tree_") || src == "Tree") { if (Replace(inst, TreeFor(src), true, false)) trees++; }
                else if (src.StartsWith("Env_Bush_")) { if (Replace(inst, rng.Next(3) switch { 0 => "Bush_01", 1 => "Bush_02", _ => "Bush_03" }, false, false)) bushes++; }
                else if (src.StartsWith("Env_GrassPlant_"))
                {
                    int r = rng.Next(10);
                    string pick = r < 4 ? "Grass_01" : r < 7 ? "Grass_02" : r < 9 ? "Flowers_01" : "Flowers_02";
                    if (r == 9 && rng.Next(3) == 0) pick = "Mushroom_02";
                    if (Replace(inst, pick, true, true)) grass++;
                }
            }
            string ground = LayGround(scene);
            // the 3D intro uses a tree + bush too
            foreach (var root in scene.GetRootGameObjects())
                foreach (var cut in root.GetComponentsInChildren<IntroCutscene3D>(true))
                {
                    Undo.RecordObject(cut, "Nature swap");
                    cut.tree = Load("Tree_01") ?? cut.tree; cut.bush = Load("Bush_01") ?? cut.bush;
                    EditorUtility.SetDirty(cut);
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.Add($"{Path.GetFileNameWithoutExtension(sp)}: {trees} trees, {bushes} bushes, {grass} grass/flowers swapped; ground: {ground}");
        }
        if (!string.IsNullOrEmpty(returnTo) && File.Exists(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[Farm Chaos] Nature swap done.\n" + string.Join("\n", log) + $"\nBackups saved as Backups/*_before_nature_{stamp}.unity");
    }

    // ---------------------------------------------------------------- materials
    static void FixMaterials()
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) return;
        foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Pack + "/Materials" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.shader == urp) continue;
            var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            m.shader = urp;
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- swapping
    static IEnumerable<GameObject> AllPrefabRoots(Scene scene)
    {
        var list = new List<GameObject>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) list.Add(t.gameObject);
        return list;
    }

    static string SourceName(GameObject inst)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(inst);
        if (src == null) return null;
        var p = AssetDatabase.GetAssetPath(src);
        return p.Contains("Pandazole") || Path.GetFileNameWithoutExtension(p) == "Tree" ? Path.GetFileNameWithoutExtension(p) : null;
    }

    static string TreeFor(string src)
    {
        switch (src)
        {
            case "Env_Tree_01": return "Tree_01";
            case "Env_Tree_02": return "Tree_02";
            case "Env_Tree_03": return "Tree_03";
            case "Env_Tree_04": return "Tree_04";
            case "Env_Tree_05": return "Tree_05";
            case "Env_Tree_06": return "Tree_03";
            case "Env_Tree_07": return "Tree_02";
            default: return "Tree_01";
        }
    }

    static GameObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/Prefabs/{name}.prefab");

    // replace one Pandazole instance, matching height (or footprint for bushes)
    static bool Replace(GameObject old, string newName, bool matchHeight, bool noCollider)
    {
        var prefab = Load(newName);
        if (prefab == null) return false;
        var oldB = Bounds(old);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
        Undo.RegisterCreatedObjectUndo(go, "Nature swap");
        go.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
        go.transform.position = old.transform.position;
        go.transform.rotation = Quaternion.Euler(0f, old.transform.eulerAngles.y, 0f);
        go.transform.localScale = Vector3.one;
        go.layer = old.layer;
        go.SetActive(old.activeSelf);
        go.isStatic = old.isStatic;
        var newB = Bounds(go);
        if (oldB.size.sqrMagnitude > 0.0001f && newB.size.sqrMagnitude > 0.0001f)
        {
            float k = matchHeight
                ? oldB.size.y / Mathf.Max(0.01f, newB.size.y)
                : Mathf.Max(oldB.size.x, oldB.size.z) / Mathf.Max(0.01f, Mathf.Max(newB.size.x, newB.size.z));
            k = Mathf.Clamp(k, 0.2f, 8f);
            SetWorldScale(go.transform, k);
            newB = Bounds(go);
            go.transform.position += new Vector3(0f, oldB.min.y - newB.min.y, 0f);   // same ground contact
        }
        if (noCollider) foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        go.name = newName;
        Undo.DestroyObjectImmediate(old);
        return true;
    }

    static void SetWorldScale(Transform t, float uniform)
    {
        var ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(uniform / Mathf.Max(0.0001f, ps.x), uniform / Mathf.Max(0.0001f, ps.y), uniform / Mathf.Max(0.0001f, ps.z));
    }

    // ---------------------------------------------------------------- ground
    static string LayGround(Scene scene)
    {
        var tile = Load("Ground_03");
        if (tile == null) return "Ground_03 not found";
        // the existing floor: "land" in Farm_Main, "Ground" in the Lobby (or the biggest flat thing)
        Renderer floor = null;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.gameObject.name.ToLowerInvariant();
                if (n != "land" && n != "ground" && n != "zoo_ground") continue;
                if (floor == null || Area(r.bounds) > Area(floor.bounds)) floor = r;
            }
        if (floor == null) return "no floor named land/Ground found - skipped";
        // re-running: replace the old tiles
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Nature_Ground").ToList())
                Undo.DestroyObjectImmediate(t.gameObject);
        var groundMat = GroundMaterial();

        var fb = floor.bounds;
        var parent = new GameObject("Nature_Ground").transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Nature ground");
        parent.SetParent(floor.transform.parent, true);
        parent.SetSiblingIndex(floor.transform.GetSiblingIndex() + 1);

        // measure the tile once
        var probe = (GameObject)PrefabUtility.InstantiatePrefab(tile);
        var tb = Bounds(probe);
        Object.DestroyImmediate(probe);
        float squash = MaxBump / Mathf.Max(0.01f, tb.size.y);
        int nx = Mathf.Max(1, Mathf.CeilToInt(fb.size.x / 29f)), nz = Mathf.Max(1, Mathf.CeilToInt(fb.size.z / 29f));
        float sx = fb.size.x / nx / tb.size.x * 1.06f, sz = fb.size.z / nz / tb.size.z * 1.06f;   // slight overlap hides seams
        float topY = fb.max.y;
        var rng = new System.Random(7);
        for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(tile, parent);
                g.name = $"Ground_{ix}_{iz}";
                g.transform.rotation = Quaternion.Euler(0f, rng.Next(2) * 180f, 0f);
                SetWorldScaleXYZ(g.transform, new Vector3(sx, squash, sz));
                var b = Bounds(g);
                Vector3 centre = new Vector3(fb.min.x + (ix + 0.5f) * fb.size.x / nx, 0f, fb.min.z + (iz + 0.5f) * fb.size.z / nz);
                g.transform.position += new Vector3(centre.x - b.center.x, topY - 0.01f - b.max.y, centre.z - b.center.z);
                foreach (var c in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);   // visuals only
                foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; if (groundMat != null) r.sharedMaterial = groundMat; }
                g.isStatic = true;
            }

        // grass-coloured sheet just under the tiles so seams between them never show the sky
        var under = GameObject.CreatePrimitive(PrimitiveType.Plane);
        under.name = "Ground_Underlay";
        Object.DestroyImmediate(under.GetComponent<Collider>());
        under.transform.SetParent(parent, true);
        under.transform.position = new Vector3(fb.center.x, topY - 0.15f, fb.center.z);
        SetWorldScaleXYZ(under.transform, new Vector3(fb.size.x / 10f * 1.04f, 1f, fb.size.z / 10f * 1.04f));
        var ur = under.GetComponent<Renderer>();
        if (groundMat != null) ur.sharedMaterial = groundMat;
        ur.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        under.isStatic = true;

        // keep the flat floor for physics, hide its look
        Undo.RecordObject(floor, "Nature ground");
        floor.enabled = false;
        EditorUtility.SetDirty(floor);
        return $"{nx * nz} Ground_03 tiles over '{floor.gameObject.name}' ({fb.size.x:F0}x{fb.size.z:F0} m), bumps < {MaxBump * 100:F0} cm, sunk just under paths; old floor kept invisible for smooth rolling";
    }

    static void SetWorldScaleXYZ(Transform t, Vector3 s)
    {
        var ps = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(s.x / Mathf.Max(0.0001f, Mathf.Abs(ps.x)), s.y / Mathf.Max(0.0001f, Mathf.Abs(ps.y)), s.z / Mathf.Max(0.0001f, Mathf.Abs(ps.z)));
    }

    // soft pastel grass for the ground tiles (the pack's palette green is quite dark)
    static Material GroundMaterial()
    {
        const string path = "Assets/Materials/Nature_Ground.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) return null;
        m = new Material(sh) { name = "Nature_Ground" };
        m.SetColor("_BaseColor", new Color32(0xA7, 0xD1, 0x8A, 255));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static float Area(Bounds b) => b.size.x * b.size.z;

    static Bounds Bounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(g.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }
}
#endif
