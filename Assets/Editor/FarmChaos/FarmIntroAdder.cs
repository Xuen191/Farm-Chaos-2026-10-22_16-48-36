#if UNITY_EDITOR
// Farm Chaos - adds the story intro to the existing Lobby scene (without rebuilding the lobby).
// Menu: Tools > Farm Chaos > 13. Add Story Intro
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmIntroAdder
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Farm Chaos/13. Add Story Intro")]
    public static void Add()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!File.Exists(LobbyPath)) { EditorUtility.DisplayDialog("Farm Chaos", "Build the Lobby first (step 11).", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = SceneManager.GetActiveScene().path == LobbyPath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var ui = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LobbyUI>(true)).FirstOrDefault();
        if (ui == null) { EditorUtility.DisplayDialog("Farm Chaos", "No LobbyUI found in the Lobby scene.", "OK"); return; }
        var intro = ui.GetComponent<IntroStory>();
        if (intro == null) intro = Undo.AddComponent<IntroStory>(ui.gameObject);
        Undo.RecordObject(intro, "Story Intro");
        intro.circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        intro.rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        EditorUtility.SetDirty(intro);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Farm Chaos] Story intro added to the Lobby (scene saved). Press Play, roll into a circle and press START.");
    }
}
#endif
