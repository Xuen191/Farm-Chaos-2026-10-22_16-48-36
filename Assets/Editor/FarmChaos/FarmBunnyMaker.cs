#if UNITY_EDITOR
// Farm Chaos - Tools > Farm Chaos > 25. Make Bunny (from Pig)
// Builds Assets/Prefabs/Animals/Bunny.prefab in the same simple-shapes style as your friend's animals.
// It starts from a copy of Pig.prefab (so the AnimalController, legs, eyes and collider all keep working),
// then: round head, long upright ears with pink insides, a tiny pink nose, a puffy white tail, rounder body,
// white fur, a bit smaller. The Pig prefab itself is not changed. Safe to run again (rebuilds the bunny).
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FarmBunnyMaker
{
    const string Dir = "Assets/Prefabs/Animals";
    const string Pig = Dir + "/Pig.prefab";
    const string Bunny = Dir + "/Bunny.prefab";
    const string Mats = "Assets/Materials/Animals";

    [MenuItem("Tools/Farm Chaos/25. Make Bunny (from Pig)")]
    public static void Make()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Pig) == null) { EditorUtility.DisplayDialog("Farm Chaos", "Pig.prefab not found in " + Dir, "OK"); return; }
        AssetDatabase.DeleteAsset(Bunny);
        if (!AssetDatabase.CopyAsset(Pig, Bunny)) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't copy the pig.", "OK"); return; }
        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(Bunny);
        root.name = "Bunny";
        Material fur = Mat("White"), pink = Mat("Pink"), nose = Mat("Dark Pink") ?? pink;

        var baseA = Find(root, "BaseAnimal");
        baseA.localScale = Vector3.one * 0.6f;                       // smaller than the pig
        baseA.localPosition = new Vector3(0f, 0.39f, 0f);            // short legs -> sits low, feet on the ground

        // body: rounder and shorter, white
        var torso = Find(root, "Torso");
        torso.localScale = new Vector3(0.95f, 0.62f, 0.95f);
        Paint(torso, fur);
        foreach (var leg in new[] { "EPA_FL", "EPA_FR", "EPA_RL", "EPA_RR" })
        {
            var l = Find(root, leg); if (l == null) continue;
            bool rear = leg.Contains("R") && leg[4] == 'R';
            l.localScale = rear ? new Vector3(0.3f, 0.3f, 0.4f) : new Vector3(0.17f, 0.3f, 0.17f);   // short legs, big back feet
            l.localPosition = new Vector3(Mathf.Sign(l.localPosition.x) * (rear ? 0.25f : 0.15f), -0.42f, rear ? 0.3f : -0.38f);
            foreach (var r in l.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = fur;   // paws, not trotters
        }

        // head: a round ball at the front, a bit higher than the body
        var head = Find(root, "Head");
        var headBall = Shape(PrimitiveType.Sphere, "HeadSphere", head, new Vector3(0f, 0.32f, -0.62f), Quaternion.identity, Vector3.one * 0.66f, fur);

        // eyes move onto the head
        var eyes = Find(root, "Eyes");
        eyes.localPosition = new Vector3(0f, 0.22f, -0.33f);
        eyes.localScale = Vector3.one * 0.85f;

        // pig snout -> tiny pink bunny nose; no nostrils
        var snout = Find(root, "Snout");
        if (snout != null)
        {
            snout.localPosition = new Vector3(0f, 0.3f, -0.95f);
            snout.localScale = new Vector3(0.11f, 0.035f, 0.08f);
            Paint(snout, nose);
            foreach (Transform c in snout) c.gameObject.SetActive(false);
        }
        foreach (var t in All(root).Where(t => t.name.StartsWith("Nostril"))) t.gameObject.SetActive(false);
        var mouth = Find(root, "MouthOpening");
        if (mouth != null) { mouth.localPosition = new Vector3(0f, 0.16f, -0.9f); mouth.localScale = new Vector3(0.12f, 0.12f, 0.04f); }

        // ears: long, upright, slightly apart, pink inside
        var ears = Find(root, "Ears");
        foreach (var t in ears.Cast<Transform>().ToList()) t.gameObject.SetActive(false);   // pig ears off
        for (int s = -1; s <= 1; s += 2)
        {
            string side = s > 0 ? "L" : "R";
            var rot = Quaternion.Euler(-8f, 0f, -s * 9f);
            Shape(PrimitiveType.Capsule, "BunnyEarOuter" + side, ears, new Vector3(s * 0.15f, 0.92f, -0.58f), rot, new Vector3(0.17f, 0.36f, 0.08f), fur);
            Shape(PrimitiveType.Capsule, "BunnyEarInner" + side, ears, new Vector3(s * 0.15f, 0.9f, -0.625f), rot, new Vector3(0.09f, 0.28f, 0.02f), pink);
        }

        // tail: one puffy white ball instead of the curly pig tail
        var tail = Find(root, "Tail");
        foreach (var t in tail.Cast<Transform>().ToList()) t.gameObject.SetActive(false);
        Shape(PrimitiveType.Sphere, "PuffTail", tail, new Vector3(0f, 0.28f, 0.62f), Quaternion.identity, Vector3.one * 0.3f, fur);

        // root collider a bit smaller to match
        var cap = root.GetComponent<CapsuleCollider>();
        if (cap != null) { cap.radius *= 0.8f; cap.height *= 0.8f; cap.center *= 0.8f; }

        PrefabUtility.SaveAsPrefabAsset(root, Bunny);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();
        var made = AssetDatabase.LoadAssetAtPath<GameObject>(Bunny);
        Selection.activeObject = made;
        EditorGUIUtility.PingObject(made);
        Snapshots(made);
        AssetDatabase.OpenAsset(made);                                   // show it in Prefab Mode
        Debug.Log("[Farm Chaos] Bunny.prefab made in " + Dir + " (from a copy of the pig, same AnimalController).");
    }

    // front / side / back pictures -> Art/renders/bunny_*.png (handy for checking without the editor camera)
    static void Snapshots(GameObject prefab)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            var inst = (GameObject)Object.Instantiate(prefab);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inst, scene);
            foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled = false;
            var lightGo = new GameObject("Light", typeof(Light)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            var camGo = new GameObject("Cam", typeof(Camera)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.GetComponent<Camera>(); cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.74f, 0.84f, 0.95f); cam.fieldOfView = 30f;
            var b = inst.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((x, y) => { x.Encapsulate(y); return x; });
            var rt = new RenderTexture(512, 512, 24); cam.targetTexture = rt;
            var tex = new Texture2D(512, 512, TextureFormat.RGB24, false);
            System.IO.Directory.CreateDirectory("Art/renders");
            foreach (var (tag, yaw) in new[] { ("back", 0f), ("3q", 145f), ("side", 90f), ("front", 180f) })
            {
                // the animals face +Z in the world, so "front" looks from +Z
                Vector3 dir = Quaternion.Euler(12f, yaw, 0f) * Vector3.back;
                cam.transform.position = b.center + dir * (b.extents.magnitude * 3.6f);
                cam.transform.LookAt(b.center);
                cam.Render();
                RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); tex.Apply(); RenderTexture.active = null;
                System.IO.File.WriteAllBytes($"Art/renders/bunny_{tag}.png", tex.EncodeToPNG());
            }
            cam.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Mats}/{name}.mat");

    static System.Collections.Generic.IEnumerable<Transform> All(GameObject root) => root.GetComponentsInChildren<Transform>(true);
    static Transform Find(GameObject root, string name) => All(root).FirstOrDefault(t => t.name == name);

    static void Paint(Transform t, Material m)
    {
        if (m == null) return;
        var r = t.GetComponent<Renderer>(); if (r != null) r.sharedMaterial = m;
    }

    static Transform Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localRotation = rot; go.transform.localScale = scale;
        if (m != null) go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }
}
#endif
