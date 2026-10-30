#if UNITY_EDITOR
// Farm Chaos - plants a little grove of Simple Nature trees (+ a few bushes/flowers) around the dog kennel.
// Leaves the side facing HQ open (that's where the path comes in) and skips spots that would hit anything.
// Menu: Tools > Farm Chaos > 17. Add Trees Around Dog Kennel   (re-run = new random layout)
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmKennelTrees
{
    const string FarmPath = "Assets/Scenes/Farm_Main.unity";
    const string Pack = "Assets/SimpleNaturePack/Prefabs/";

    [MenuItem("Tools/Farm Chaos/17. Add Trees Around Dog Kennel")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.GetActiveScene().path == FarmPath ? EditorSceneManager.GetActiveScene() : EditorSceneManager.OpenScene(FarmPath, OpenSceneMode.Single);

        var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
        var kennel = all.FirstOrDefault(t => t.name.ToLowerInvariant().Replace("_", "").StartsWith("dogkennel"));
        if (kennel == null) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't find the dog kennel area in Farm_Main.", "OK"); return; }

        // remove an earlier grove
        foreach (var old in all.Where(t => t != null && t.name == "Kennel_Grove").ToList()) Undo.DestroyObjectImmediate(old.gameObject);

        // where things go + how big the existing new-pack trees are
        var treesParent = all.FirstOrDefault(t => t != null && t.name == "Trees");
        var grove = new GameObject("Kennel_Grove").transform;
        Undo.RegisterCreatedObjectUndo(grove.gameObject, "Kennel trees");
        if (treesParent != null) grove.SetParent(treesParent, false);
        var existing = all.Where(t => t != null && t.name.StartsWith("Tree_0") && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).ToList();
        float baseScale = existing.Count > 0 ? existing.Average(t => t.lossyScale.y) : 1f;

        Vector3 c = kennel.position;
        float groundY = c.y;
        var kr = kennel.GetComponent<Renderer>();
        float half = kr != null ? Mathf.Max(kr.bounds.extents.x, kr.bounds.extents.z) : 4f;
        Vector3 toHQ = (Vector3.zero - c); toHQ.y = 0; toHQ.Normalize();

        // map edge (the invisible floor)
        var land = all.FirstOrDefault(t => t != null && (t.name == "land" || t.name == "Zoo_Ground"));
        Bounds mapB = land != null && land.GetComponent<Renderer>() != null ? land.GetComponent<Renderer>().bounds : new Bounds(Vector3.zero, new Vector3(100, 1, 100));

        Physics.SyncTransforms();
        var rng = new System.Random(System.Environment.TickCount);
        string[] trees = { "Tree_01", "Tree_02", "Tree_03", "Tree_04", "Tree_05" };
        string[] small = { "Bush_01", "Bush_02", "Bush_03", "Flowers_01", "Flowers_02", "Mushroom_01", "Stump_01" };
        int placedTrees = 0, placedSmall = 0;

        for (int attempt = 0; attempt < 400 && (placedTrees < 16 || placedSmall < 14); attempt++)
        {
            bool isTree = placedTrees < 16 && (placedSmall >= 14 || rng.NextDouble() < 0.55);
            float ang = (float)rng.NextDouble() * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
            if (Vector3.Angle(dir, toHQ) < 40f) continue;   // keep the way in open
            float r = half + (isTree ? 3.5f + (float)rng.NextDouble() * 7f : 1.5f + (float)rng.NextDouble() * 6f);
            Vector3 p = c + dir * r; p.y = groundY;
            if (p.x < mapB.min.x + 2f || p.x > mapB.max.x - 2f || p.z < mapB.min.z + 2f || p.z > mapB.max.z - 2f) continue;

            // anything already here? (fences, kennel, paths with colliders, other trees)
            float clear = isTree ? 1.6f : 0.6f;
            var hits = Physics.OverlapSphere(p + Vector3.up * (clear + 0.15f), clear, ~0, QueryTriggerInteraction.Ignore);
            if (hits.Length > 0) continue;
            if (grove.Cast<Transform>().Any(o => Vector3.Distance(o.position, p) < (isTree ? 3.2f : 1.2f))) continue;
            if (OnPath(all, p)) continue;

            string name = isTree ? trees[rng.Next(trees.Length)] : small[rng.Next(small.Length)];
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + name + ".prefab");
            if (prefab == null) continue;
            var g = (GameObject)PrefabUtility.InstantiatePrefab(prefab, grove);
            g.name = name;
            g.transform.position = p;
            g.transform.rotation = Quaternion.Euler(0, (float)rng.NextDouble() * 360f, 0);
            float s = baseScale * (isTree ? 0.85f + (float)rng.NextDouble() * 0.5f : 1.2f + (float)rng.NextDouble() * 0.6f);
            g.transform.localScale = Vector3.one * s / Mathf.Max(0.0001f, grove.lossyScale.y);
            g.isStatic = true;
            if (!isTree) foreach (var col in g.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            Physics.SyncTransforms();
            if (isTree) placedTrees++; else placedSmall++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = grove.gameObject;
        Debug.Log($"[Farm Chaos] Planted {placedTrees} trees and {placedSmall} bushes/flowers around the dog kennel (Kennel_Grove). Run again for a new random layout.");
    }

    // paths are flat meshes without colliders, so check them by their footprint
    static bool OnPath(System.Collections.Generic.List<Transform> all, Vector3 p)
    {
        foreach (var t in all)
        {
            if (t == null) continue;
            var n = t.name.ToLowerInvariant();
            if (!(n.Contains("path") || n.Contains("walk") || n.Contains("trail"))) continue;
            var r = t.GetComponent<Renderer>(); if (r == null) continue;
            var b = r.bounds; b.Expand(new Vector3(2.5f, 10f, 2.5f));
            if (b.Contains(p)) return true;
        }
        return false;
    }
}
#endif
