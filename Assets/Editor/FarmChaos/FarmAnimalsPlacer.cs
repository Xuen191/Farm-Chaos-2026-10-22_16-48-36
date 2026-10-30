#if UNITY_EDITOR
// Farm Chaos - Tools > Farm Chaos > 26. Put Animals In Pens
// Clones the animals into their pens in Farm_Main under "Farm_Animals" and gives each one an AnimalBrain
// (graze in the pen, escape when the gate pops open, follow you when caught, coins when returned).
// Safe to run again: it replaces the old Farm_Animals. The animal prefabs themselves are not changed.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmAnimalsPlacer
{
    const string Scene = "Assets/Scenes/Farm_Main.unity";
    const string Dir = "Assets/Prefabs/Animals/";

    //            prefab       kind                     how many  pen           gate area  walk  run   coins
    static readonly (string prefab, AnimalBrain.Kind kind, int count, string pen, string area, float walk, float run, int coins)[] Herd =
    {
        ("Chicken", AnimalBrain.Kind.Chicken, 6, "Chicken_Pen", "Chicken", 1.0f, 4.2f, 3),
        ("Bunny",   AnimalBrain.Kind.Bunny,   5, "Rabbit_Pen",  "Rabbit",  0.9f, 4.8f, 4),
        ("Sheep",   AnimalBrain.Kind.Sheep,   4, "Sheep_Pen",   "Sheep",   0.8f, 3.4f, 5),
        ("Cow",     AnimalBrain.Kind.Cow,     3, "Cow_Pen",     "Cow",     0.6f, 2.6f, 8),
    };

    [MenuItem("Tools/Farm Chaos/26. Put Animals In Pens")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        string back = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);

        var old = GameObject.Find("Farm_Animals"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("Farm_Animals");
        var gates = Object.FindObjectsByType<ElectricGate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var pens = GameObject.Find("Animal_Pens");
        Physics.SyncTransforms();
        var rng = new System.Random(7);
        int made = 0; string report = "";

        foreach (var h in Herd)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + h.prefab + ".prefab");
            var pen = pens != null ? pens.transform.Find(h.pen) : null;
            var gate = gates.FirstOrDefault(g => g.areaName == h.area);
            if (prefab == null || pen == null) { report += $"\n  {h.prefab}: skipped (prefab or {h.pen} missing)"; continue; }

            var rs = pen.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) { report += $"\n  {h.prefab}: {h.pen} has nothing to measure"; continue; }
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            Vector3 centre = new Vector3(b.center.x, b.min.y, b.center.z);
            float radius = Mathf.Max(1.5f, Mathf.Min(b.extents.x, b.extents.z) - 1.6f);   // safely inside the fence

            var group = new GameObject(h.prefab + "s").transform; group.SetParent(root.transform, false);
            for (int i = 0; i < h.count; i++)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                inst.name = $"{h.prefab}_{i + 1}";
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = Mathf.Sqrt((float)rng.NextDouble()) * radius * 0.8f;
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                p.y = Ground(p, centre.y);
                inst.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));

                var steer = inst.GetComponent<EasyPrimitiveAnimals.AnimalController>(); if (steer != null) steer.enabled = false;
                var rb = inst.GetComponent<Rigidbody>(); if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
                var brain = inst.AddComponent<AnimalBrain>();
                brain.kind = h.kind; brain.coins = h.coins; brain.walkSpeed = h.walk; brain.runSpeed = h.run;
                brain.homeCentre = centre; brain.homeRadius = radius; brain.gate = gate;
                made++;
            }
            report += $"\n  {h.count} x {h.prefab} in {h.pen} (gate: {(gate != null ? gate.name : "none!")})";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Farm Chaos] Put {made} animals in their pens:{report}\nCatch escaped animals and walk them home for coins.");
        if (!string.IsNullOrEmpty(back) && back != Scene && System.IO.File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);
    }

    static float Ground(Vector3 p, float fallback)
    {
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore))
            if (h.normal.y > 0.7f && h.point.y < fallback + 1.5f && h.point.y > best) best = h.point.y;
        return best > float.MinValue ? best : fallback;
    }
}
#endif
