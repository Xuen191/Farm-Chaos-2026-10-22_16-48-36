#if UNITY_EDITOR
// Farm Chaos - sets up online play.
// Menu: Tools > Farm Chaos > 21. Set Up Online Play
//  - makes the networked player prefab (Assets/Prefabs/Online/Net_Player.prefab): coloured ball + name tag
//  - adds a "Network" object to the Lobby: NetworkManager + UnityTransport + NetSession
// Test with 3 players on one PC: Window > Multiplayer > Multiplayer Play Mode, tick Player 2 + Player 3, press Play.
using System.IO;
using System.Linq;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmOnlineSetup
{
    const string Dir = "Assets/Prefabs/Online";
    const string PrefabPath = Dir + "/Net_Player.prefab";
    const string ListPath = Dir + "/Farm_NetworkPrefabs.asset";
    const string LobbyPath = "Assets/Scenes/Lobby.unity";

    [MenuItem("Tools/Farm Chaos/21. Set Up Online Play")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/Prefabs", "Online");

        var prefab = MakePlayerPrefab();
        AssetDatabase.DeleteAsset(ListPath);   // not needed (an older version made it)
        AssetDatabase.SaveAssets();

        EditorSceneManager.SaveOpenScenes();
        var scene = SceneManager.GetActiveScene().path == LobbyPath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "Network").ToList()) Object.DestroyImmediate(old);

        var go = new GameObject("Network");
        go.transform.SetAsFirstSibling();
        var nm = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();
        go.AddComponent<NetSession>();
        nm.NetworkConfig ??= new NetworkConfig();
        nm.NetworkConfig.NetworkTransport = transport;
        nm.NetworkConfig.PlayerPrefab = prefab;
        nm.NetworkConfig.EnableSceneManagement = true;
        nm.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();   // the player prefab registers itself; listing it twice is an error
        EditorUtility.SetDirty(nm);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        Debug.Log("[Farm Chaos] Online play set up: Network object in the Lobby + Net_Player prefab.\n" +
                  "Test 3 players: Window > Multiplayer > Multiplayer Play Mode -> tick Player 2 and Player 3 -> Play.\n" +
                  "Player 1: roll into a circle, HOST, read the code. Players 2-3: Join with code. Player 1: START.");
    }

    static GameObject MakePlayerPrefab()
    {
        var root = new GameObject("Net_Player");
        root.AddComponent<NetworkObject>();
        var nt = root.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // each player moves their own ball
        nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
        nt.InLocalSpace = false; nt.Interpolate = true;
        var np = root.AddComponent<NetPlayer>();

        var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        body.name = "Body"; body.transform.SetParent(root.transform, false);
        body.transform.localScale = Vector3.one * 1.2f;   // same size as the player ball
        body.GetComponent<Renderer>().sharedMaterial = BodyMat();
        np.body = body.GetComponent<Renderer>();

        var tag = new GameObject("Name_Tag"); tag.transform.SetParent(root.transform, false);
        tag.transform.localPosition = new Vector3(0, 1.25f, 0);
        var tmp = tag.AddComponent<TextMeshPro>();
        tmp.text = "Player"; tmp.fontSize = 3f; tmp.fontStyle = FontStyles.Bold; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center; tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
        tmp.outlineWidth = 0.25f; tmp.outlineColor = new Color32(0x2A, 0x23, 0x40, 255);
        np.nameTag = tmp;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static Material BodyMat()
    {
        string p = Dir + "/Mat_net_player.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m != null) return m;
        m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mat_net_player" };
        m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.6f);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }
}
#endif
