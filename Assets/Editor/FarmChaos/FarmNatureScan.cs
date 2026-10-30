#if UNITY_EDITOR
// Farm Chaos - read-only scan: how bumpy are the Simple Nature Pack grounds, and which Pandazole
// nature pieces are used in Farm_Main / Lobby. Writes the report to Logs/FarmNatureScan.txt.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmNatureScan
{
    [MenuItem("Tools/Farm Chaos/Scan Nature Pack (read only)")]
    public static void Scan()
    {
        var sb = new StringBuilder();
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/SimpleNaturePack/Prefabs" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var mfs = go.GetComponentsInChildren<MeshFilter>(true);
            var b = new Bounds(); bool first = true; int verts = 0, tris = 0;
            var tops = new List<float>();
            foreach (var mf in mfs)
            {
                var m = mf.sharedMesh; if (m == null) continue;
                verts += m.vertexCount; tris += m.triangles.Length / 3;
                var mtx = go.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                var vs = m.vertices; var ns = m.normals;
                for (int i = 0; i < vs.Length; i++)
                {
                    var p = mtx.MultiplyPoint3x4(vs[i]);
                    if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
                    if (ns.Length == vs.Length && mtx.MultiplyVector(ns[i]).normalized.y > 0.5f) tops.Add(p.y);
                }
            }
            string bump = "";
            if (tops.Count > 0)
            {
                float avg = tops.Average(), sd = Mathf.Sqrt(tops.Select(y => (y - avg) * (y - avg)).Average());
                bump = $" topY min {tops.Min():F2} max {tops.Max():F2} avg {avg:F2} sd {sd:F3}";
            }
            var cols = string.Join(",", go.GetComponentsInChildren<Collider>(true).Select(c => c.GetType().Name + (c is MeshCollider mc && mc.convex ? "(convex)" : "")));
            var mats = string.Join(",", go.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name + "/" + (m.shader != null ? m.shader.name : "?")).Distinct());
            sb.AppendLine($"{go.name}: size {b.size.x:F2}x{b.size.y:F2}x{b.size.z:F2} centre {b.center} verts {verts} tris {tris}{bump} colliders [{cols}] mats [{mats}]");
        }

        sb.AppendLine();
        string returnTo = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        foreach (var sp in new[] { "Assets/Scenes/Farm_Main.unity", "Assets/Scenes/Lobby.unity" })
        {
            var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
            var counts = new Dictionary<string, int>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    var p = src != null ? AssetDatabase.GetAssetPath(src) : "";
                    string key = Path.GetFileNameWithoutExtension(p) + "  <" + (t.parent != null ? t.parent.name : "root") + ">";
                    counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
                }
            sb.AppendLine("== " + sp);
            foreach (var kv in counts.OrderBy(k => k.Key)) sb.AppendLine($"  {kv.Value} x {kv.Key}");
            foreach (var root in scene.GetRootGameObjects())
            {
                var g = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Zoo_Ground");
                if (g != null) sb.AppendLine($"  Zoo_Ground pos {g.position} scale {g.lossyScale} mesh {g.GetComponent<MeshFilter>()?.sharedMesh?.name} mat {g.GetComponent<Renderer>()?.sharedMaterial?.name}");
                var terr = root.GetComponentsInChildren<Terrain>(true);
                foreach (var tr in terr) sb.AppendLine($"  Terrain {tr.name} at {tr.transform.position} size {tr.terrainData?.size}");
            }
            sb.AppendLine("  roots: " + string.Join(", ", scene.GetRootGameObjects().Select(r => r.name)));
        }
        if (!string.IsNullOrEmpty(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/FarmNatureScan.txt", sb.ToString());
        Debug.Log("[Farm Chaos] Nature scan written to Logs/FarmNatureScan.txt");
    }
}
#endif
