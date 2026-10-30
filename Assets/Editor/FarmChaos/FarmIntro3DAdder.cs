#if UNITY_EDITOR
// Farm Chaos - adds the 3D story intro to the Lobby (plays after START instead of the 2D slides).
// Menu: Tools > Farm Chaos > 15. Add 3D Story Intro
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmIntro3DAdder
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string MatFolder = "Assets/Materials/Intro";

    [MenuItem("Tools/Farm Chaos/15. Add 3D Story Intro")]
    public static void Add()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!File.Exists(LobbyPath)) { EditorUtility.DisplayDialog("Farm Chaos", "Build the Lobby first (step 11).", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = SceneManager.GetActiveScene().path == LobbyPath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var ui = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LobbyUI>(true)).FirstOrDefault();
        if (ui == null) { EditorUtility.DisplayDialog("Farm Chaos", "No LobbyUI found in the Lobby scene.", "OK"); return; }

        var cut = ui.GetComponent<IntroCutscene3D>();
        if (cut == null) cut = Undo.AddComponent<IntroCutscene3D>(ui.gameObject);
        Undo.RecordObject(cut, "3D Story Intro");
        cut.litBase = MakeMat("Intro_Lit", "Universal Render Pipeline/Lit");
        cut.unlitBase = MakeMat("Intro_Unlit", "Universal Render Pipeline/Unlit");
        cut.farmhouse = Prefab("Bld_FarmerHouse");
        cut.tree = Prefab("Env_Tree_01");
        cut.bush = Prefab("Env_Bush_01");
        cut.rounded = FarmLobbyPolish.RoundedSprite();
        cut.enabled = true;
        EditorUtility.SetDirty(cut);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Farm Chaos] 3D story intro added to the Lobby (scene saved). Press Play, roll into a circle and press START. Untick IntroCutscene3D on Lobby_UI to go back to the 2D slides.");
    }

    static Material MakeMat(string name, string shader)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Intro");
        string path = MatFolder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        var sh = Shader.Find(shader);
        if (sh == null) { Debug.LogWarning("[Farm Chaos] Shader not found: " + shader); return null; }
        m = new Material(sh) { name = name };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static GameObject Prefab(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Prefab"))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(p);
        }
        return null;
    }
}
#endif
