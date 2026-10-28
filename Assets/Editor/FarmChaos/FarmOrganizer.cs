#if UNITY_EDITOR
// Farm Chaos - tidy the Farm_Main hierarchy and move your own assets into proper folders.
// Menu: Tools > Farm Chaos > Organize Hierarchy + Folders
// Hierarchy changes support Ctrl+Z. World positions never change. Friend's power-box files are not moved.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmOrganizer
{
    [MenuItem("Tools/Farm Chaos/Organize Hierarchy + Folders")]
    public static void Organize()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        OrganizeHierarchy();
        OrganizeFolders();
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Farm Chaos] Hierarchy organized, folders tidied, scene saved.");
    }

    // ------------------------------------------------------------------ hierarchy
    static Scene S => SceneManager.GetActiveScene();

    static List<Transform> AllTransforms() =>
        S.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

    static Transform FindAny(string name) =>
        AllTransforms().FirstOrDefault(t => t.name.Replace("_", "").ToLowerInvariant() == name.Replace("_", "").ToLowerInvariant());

    static Transform Group(string name, Transform parent = null)
    {
        Transform t = parent == null
            ? S.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(x => x.name == name)
            : parent.Find(name);
        if (t != null) return t;
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Organize");
        if (parent != null) Undo.SetTransformParent(go.transform, parent, "Organize");
        go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.identity; go.transform.localScale = Vector3.one;
        return go.transform;
    }

    static void Move(Transform t, Transform parent)
    {
        if (t == null || t == parent || t.parent == parent) return;
        Undo.SetTransformParent(t, parent, "Organize"); // keeps world position/rotation/scale
    }

    static void Rename(Transform t, string name)
    {
        if (t == null || t.name == name) return;
        Undo.RecordObject(t.gameObject, "Organize"); t.name = name;
    }

    static Vector3 Center(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return t.position;
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        return b.center;
    }

    static void OrganizeHierarchy()
    {
        Undo.SetCurrentGroupName("Organize Farm Hierarchy");
        int undoGroup = Undo.GetCurrentGroup();

        // 1) camera + light
        var setup = Group("_Scene");
        foreach (var r in S.GetRootGameObjects())
            if (r.GetComponent<Camera>() != null || r.GetComponent<Light>() != null) Move(r.transform, setup);

        // 2) environment: ground + borders
        var oldEnv = FindAny("enviroment");
        var env = Group("Environment");
        var borders = Group("Borders", env);
        foreach (var n in new[] { "border_north", "border_south", "border_east", "border_west", "corner_ne", "corner_nw", "corner_se", "corner_sw" })
            Move(FindAny(n), borders);
        Move(FindAny("land"), env);

        // 3) zones
        var zones = Group("Zones");
        foreach (var n in new[] { "hq_area", "powerstation_area", "dogkennelarea" }) Move(FindAny(n), zones);

        // 4) animal pens: pad + fence + (chicken coops)
        var pens = Group("Animal_Pens");
        var penInfo = new List<(string label, Transform area, Transform group)>();
        foreach (var (key, label) in new[] { ("chicken_area", "Chicken"), ("sheep_area", "Sheep"), ("cow_area", "Cow"), ("rabbit_area", "Rabbit") })
        {
            var area = FindAny(key);
            if (area == null) continue;
            var g = Group(label + "_Pen", pens);
            Move(area, g);
            penInfo.Add((label, area, g));
        }
        // fences: any root object whose name contains "fence" -> nearest pen
        foreach (var r in S.GetRootGameObjects().Select(x => x.transform).ToList())
        {
            if (!r.name.ToLowerInvariant().Contains("fence") || penInfo.Count == 0) continue;
            var c = Center(r);
            var best = penInfo.OrderBy(p => Vector2.Distance(new Vector2(c.x, c.z), new Vector2(Center(p.area).x, Center(p.area).z))).First();
            Move(r, best.group);
            Rename(r, best.label + "_Fence");
        }
        // chicken coops (hand-built ones)
        var chickenPen = penInfo.FirstOrDefault(p => p.label == "Chicken").group;
        if (chickenPen != null)
        {
            var coops = S.GetRootGameObjects().Select(x => x.transform).Where(x => x.name.ToLowerInvariant().StartsWith("chickencoop")).ToList();
            if (coops.Count > 0)
            {
                var cg = Group("Chicken_Coops", chickenPen);
                foreach (var c in coops) Move(c, cg);
            }
        }

        // 5) buildings (pack prefabs named Bld_*)
        var bld = S.GetRootGameObjects().Select(x => x.transform).Where(x => x.name.StartsWith("Bld_")).ToList();
        if (bld.Count > 0) { var b = Group("Buildings"); foreach (var x in bld) Move(x, b); }

        // old empty "enviroment" parent
        if (oldEnv != null && oldEnv.childCount == 0 && oldEnv.GetComponents<Component>().Length == 1)
            Undo.DestroyObjectImmediate(oldEnv.gameObject);
        else if (oldEnv != null) Move(oldEnv, env);

        // 6) order at the root
        string[] order = { "_Scene", "Environment", "Zones", "Animal_Pens", "Buildings", "Farm_Paths_Rounded", "Farm_ElectricGates", "Farm_Decorations" };
        int idx = 0;
        foreach (var n in order)
        {
            var t = S.GetRootGameObjects().Select(x => x.transform).FirstOrDefault(x => x.name == n);
            if (t != null) { Undo.RecordObject(t, "Organize"); t.SetSiblingIndex(idx++); }
        }
        var leftovers = S.GetRootGameObjects().Select(x => x.name).Where(n => !order.Contains(n)).ToList();
        if (leftovers.Count > 0) Debug.Log("[Farm Chaos] Left at the top level (not sure where they belong): " + string.Join(", ", leftovers));

        Undo.CollapseUndoOperations(undoGroup);
    }

    // ------------------------------------------------------------------ folders
    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void MoveAsset(string from, string to)
    {
        if (!System.IO.File.Exists(from) && !AssetDatabase.IsValidFolder(from)) return;
        if (System.IO.File.Exists(to) || AssetDatabase.IsValidFolder(to)) { Debug.LogWarning("[Farm Chaos] Skipped move, already exists: " + to); return; }
        EnsureFolder(System.IO.Path.GetDirectoryName(to).Replace('\\', '/'));
        var err = AssetDatabase.MoveAsset(from, to);
        if (!string.IsNullOrEmpty(err)) Debug.LogWarning("[Farm Chaos] Move failed " + from + " -> " + to + ": " + err);
        else Debug.Log("[Farm Chaos] Moved " + from + " -> " + to);
    }

    static void OrganizeFolders()
    {
        // your farm materials (incl. ElectricGate) -> Assets/Materials
        MoveAsset("Assets/Scenes/materials", "Assets/Materials");
        // loose tree prefab + textures
        MoveAsset("Assets/Tree.prefab", "Assets/Prefabs/Environment/Tree.prefab");
        MoveAsset("Assets/Tree_Textures", "Assets/Textures/Tree_Textures");
        // terrains not used by any scene ("New Terrain 1" is used by Game.unity, so it stays)
        MoveAsset("Assets/New Terrain.asset", "Assets/Terrain/New Terrain.asset");
        MoveAsset("Assets/New Terrain 2.asset", "Assets/Terrain/New Terrain 2.asset");
        // your chicken coop door script
        MoveAsset("Assets/Scripts/chickencoopdoor.cs", "Assets/Scripts/Farm/chickencoopdoor.cs");
        AssetDatabase.Refresh();
    }
}
#endif
