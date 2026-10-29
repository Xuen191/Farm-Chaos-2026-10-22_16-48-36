#if UNITY_EDITOR
// Farm Chaos - checks which way the chibi's face points and fixes the prefab's facing if needed.
// Menu: Tools > Farm Chaos > 23. Fix Chibi Facing
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FarmChibiCheck
{
    [MenuItem("Tools/Farm Chaos/23. Fix Chibi Facing")]
    public static void Run()
    {
        const string path = "Assets/Prefabs/Chibi_Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        var smr = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name.Contains("Body"));
        if (smr == null) { Debug.LogWarning("No body found"); PrefabUtility.UnloadPrefabContents(root); return; }
        var mesh = new Mesh(); smr.BakeMesh(mesh, true);
        int faceIndex = System.Array.FindIndex(smr.sharedMaterials, m => m != null && m.name.EndsWith("Face"));
        var verts = mesh.vertices;
        var idx = mesh.GetIndices(faceIndex).Distinct().ToArray();
        Vector3 faceC = idx.Aggregate(Vector3.zero, (a, i) => a + verts[i]) / idx.Length;
        Vector3 allC = verts.Aggregate(Vector3.zero, (a, v) => a + v) / verts.Length;
        Vector3 dirLocal = smr.transform.TransformPoint(faceC) - smr.transform.TransformPoint(allC);
        Vector3 dirRoot = root.transform.InverseTransformDirection(dirLocal); dirRoot.y = 0;
        float yaw = Mathf.Atan2(dirRoot.x, dirRoot.z) * Mathf.Rad2Deg;   // where the face points, 0 = +Z (forward)
        var motor = root.GetComponent<ChibiMotor>();
        float fix = Mathf.Round(-yaw / 90f) * 90f;
        motor.yawOffset = fix;
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log($"[Farm Chaos] Chibi face points {yaw:F0} deg from forward -> yawOffset set to {fix}.");
    }
}
#endif
