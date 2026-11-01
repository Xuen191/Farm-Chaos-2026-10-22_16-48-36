// Farm Chaos - removes the dog kennel corner (doghouse, its area marker and the path to it) from Farm_Main.
// The trees there (Kennel_Grove) stay as scenery.
// Menu: Tools > Farm Chaos > 30. Remove Dog Kennel   (safe to run twice - it only removes what's still there)
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmRemoveKennel
{
    const string ScenePath = "Assets/Scenes/Farm_Main.unity";
    static readonly string[] Paths = { "Zones/Dog_Kennel", "Zones/dogkennelarea", "Farm_Paths_Rounded/Path_Dogkennel" };

    [MenuItem("Tools/Farm Chaos/30. Remove Dog Kennel")]
    static void Run()
    {
        if (Application.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        int removed = 0;
        foreach (var p in Paths)
        {
            var go = GameObject.Find(p);
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
            removed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Farm Chaos] Removed {removed} dog kennel object(s) from Farm_Main and saved the scene.");
    }
}
