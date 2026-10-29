#if UNITY_EDITOR
// Farm Chaos
//  19. Make Fences + Buildings Solid: the Farm Ranch (Pandazole) prefabs only have "convex" mesh colliders,
//      which break on detailed models (the 256-face limit) so you roll straight through. This switches every
//      Pandazole fence / building / prop in Farm_Main + Lobby to simple box colliders (one per part).
//  20. Electric Gates: Switch + Cozy Look: thin normal wires instead of glowing laser bars, and a switch box
//      next to each gate (USE / E to open-close while powered, lamp shows power, little sparks on live wires).
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmSolidAndGates
{
    static readonly string[] Scenes = { "Assets/Scenes/Farm_Main.unity", "Assets/Scenes/Lobby.unity" };
    const string GateMats = "Assets/Materials/ElectricGate";

    // ================================================================ solid colliders
    [MenuItem("Tools/Farm Chaos/19. Make Fences + Buildings Solid")]
    public static void MakeSolid()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        string returnTo = SceneManager.GetActiveScene().path;
        var report = new System.Collections.Generic.List<string>();
        foreach (var sp in Scenes)
        {
            if (!System.IO.File.Exists(sp)) continue;
            var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
            int objs = 0, boxes = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    var path = src != null ? AssetDatabase.GetAssetPath(src) : "";
                    if (!path.Contains("Pandazole")) continue;
                    string n = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!(n.StartsWith("Env_WoodFence") || n.StartsWith("Env_MetalFence") || n.StartsWith("Bld_") || n.StartsWith("Prop_") || n.StartsWith("Env_Well"))) continue;
                    objs++;
                    foreach (var mc in t.GetComponentsInChildren<MeshCollider>(true)) { Undo.RecordObject(mc, "Solid"); mc.enabled = false; }
                    foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var bc = mf.GetComponent<BoxCollider>();
                        if (bc == null) { bc = Undo.AddComponent<BoxCollider>(mf.gameObject); boxes++; }
                        var b = mf.sharedMesh.bounds;
                        bc.center = b.center; bc.size = b.size; bc.enabled = true;
                    }
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.Add($"{System.IO.Path.GetFileNameWithoutExtension(sp)}: {objs} fences/buildings/props, {boxes} box colliders added");
        }
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
        Debug.Log("[Farm Chaos] Solid colliders done.\n" + string.Join("\n", report));
    }

    // ================================================================ gates
    [MenuItem("Tools/Farm Chaos/20. Electric Gates: Switch + Cozy Look")]
    public static void GateSwitches()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = SceneManager.GetActiveScene().path == Scenes[0] ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(Scenes[0], OpenSceneMode.Single);
        var gates = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ElectricGate>(true)).ToList();
        if (gates.Count == 0) { EditorUtility.DisplayDialog("Farm Chaos", "No electric gates in Farm_Main (run step 3 first).", "OK"); return; }

        // calmer wire look: normal metal wire, no glow
        var wireOn = AssetDatabase.LoadAssetAtPath<Material>(GateMats + "/Mat_gate_wire_on.mat");
        var wireOff = AssetDatabase.LoadAssetAtPath<Material>(GateMats + "/Mat_gate_wire_off.mat");
        Calm(wireOn, new Color32(0xA9, 0xB1, 0xBC, 255));
        Calm(wireOff, new Color32(0x7C, 0x78, 0x70, 255));

        var lampOn = Mat("Mat_switch_lamp_on", new Color32(0x7C, 0xE0, 0x8C, 255), new Color(0.35f, 1f, 0.45f) * 2.2f);
        var lampOff = Mat("Mat_switch_lamp_off", new Color32(0xE0, 0x4C, 0x5C, 255), new Color(1f, 0.25f, 0.3f) * 1.6f);
        var box = Mat("Mat_switch_box", new Color32(0xC8, 0xE6, 0xD0, 255), null);
        var post = Mat("Mat_switch_post", new Color32(0xD9, 0xB9, 0x9B, 255), null);
        var sign = AssetDatabase.LoadAssetAtPath<Material>(GateMats + "/Mat_gate_sign.mat") ?? Mat("Mat_gate_sign", new Color32(0xFF, 0xD2, 0x3F, 255), null);
        var dark = Mat("Mat_switch_dark", new Color32(0x3A, 0x33, 0x46, 255), null);
        var cream = Mat("Mat_switch_cream", new Color32(0xFF, 0xF0, 0xD9, 255), null);
        var spark = SparkMat();

        int n = 0;
        foreach (var g in gates)
        {
            // thinner wires
            foreach (var w in g.wires.Where(w => w != null))
            {
                Undo.RecordObject(w.transform, "Gate look");
                var s = w.transform.localScale; w.transform.localScale = new Vector3(s.x, 0.022f, 0.022f);
            }
            foreach (var old in g.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Gate_Switch").ToList()) Undo.DestroyObjectImmediate(old.gameObject);

            var sw = new GameObject("Gate_Switch").transform;
            Undo.RegisterCreatedObjectUndo(sw.gameObject, "Gate switch");
            sw.SetParent(g.transform, false);
            sw.localPosition = new Vector3(-0.75f, 0f, 0.55f);   // just outside the pen, beside the first post
            sw.localRotation = Quaternion.identity;               // faces out of the pen (+z)
            Prim(PrimitiveType.Cube, sw, "Post", new Vector3(0, 0.55f, 0), new Vector3(0.12f, 1.1f, 0.12f), post, true);
            Prim(PrimitiveType.Cube, sw, "Box", new Vector3(0, 1.05f, 0.04f), new Vector3(0.42f, 0.5f, 0.2f), box, true);
            Prim(PrimitiveType.Cube, sw, "Roof", new Vector3(0, 1.33f, 0.04f), new Vector3(0.5f, 0.06f, 0.26f), cream, false);
            Prim(PrimitiveType.Cube, sw, "Bolt_Plate", new Vector3(0, 1.1f, 0.145f), new Vector3(0.24f, 0.24f, 0.02f), sign, false);
            var bolt = Prim(PrimitiveType.Cube, sw, "Bolt", new Vector3(0, 1.1f, 0.158f), new Vector3(0.05f, 0.18f, 0.01f), dark, false);
            bolt.localRotation = Quaternion.Euler(0, 0, 25f);
            var lamp = Prim(PrimitiveType.Sphere, sw, "Lamp", new Vector3(0, 1.42f, 0.04f), Vector3.one * 0.16f, lampOn, false);
            var leverPivot = new GameObject("Lever").transform; leverPivot.SetParent(sw, false); leverPivot.localPosition = new Vector3(0.24f, 1.0f, 0.04f);
            Prim(PrimitiveType.Cylinder, leverPivot, "Stick", new Vector3(0, 0.1f, 0), new Vector3(0.04f, 0.1f, 0.04f), dark, false);
            Prim(PrimitiveType.Sphere, leverPivot, "Knob", new Vector3(0, 0.21f, 0), Vector3.one * 0.08f, lampOff, false);

            var gs = Undo.AddComponent<GateSwitch>(sw.gameObject);
            gs.gate = g; gs.penName = string.IsNullOrEmpty(g.areaName) ? "pen" : g.areaName.ToLowerInvariant();
            gs.lamp = lamp.GetComponent<Renderer>(); gs.lampOn = lampOn; gs.lampOff = lampOff;
            gs.lever = leverPivot; gs.sparkMat = spark;
            n++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Farm Chaos] {n} gates: calm metal wires + a switch box each (USE / E to open-close while powered).");
    }

    // ---------------------------------------------------------------- helpers
    static void Calm(Material m, Color c)
    {
        if (m == null) return;
        m.SetColor("_BaseColor", c); m.SetColor("_Color", c);
        m.DisableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", Color.black);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.5f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.4f);
        EditorUtility.SetDirty(m);
    }

    static Material Mat(string name, Color c, Color? emission)
    {
        string p = GateMats + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SparkMat()
    {
        string p = GateMats + "/Mat_spark.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Mat_spark" }; AssetDatabase.CreateAsset(m, p); }
        m.SetColor("_BaseColor", new Color(1f, 0.95f, 0.55f));
        EditorUtility.SetDirty(m);
        return m;
    }

    static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
        return g.transform;
    }
}
#endif
