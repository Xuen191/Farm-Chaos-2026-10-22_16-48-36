#if UNITY_EDITOR
// Farm Chaos - Tools > Farm Chaos > 24. Add Tonight's Weather
// Sets up the weather icons/particle materials (Assets/Resources/Weather) and adds a "Tonights_Weather"
// object to Farm_Main. Safe to run again. Your friend's Game.unity is not touched.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FarmWeatherSetup
{
    const string Dir = "Assets/Resources/Weather";
    const string FarmScene = "Assets/Scenes/Farm_Main.unity";

    [MenuItem("Tools/Farm Chaos/24. Add Tonight's Weather")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!Directory.Exists(Dir)) { EditorUtility.DisplayDialog("Farm Chaos", "Missing " + Dir + " (the weather pictures).", "OK"); return; }

        // ---- textures: icons + card as sprites, particle shapes as plain textures
        foreach (var file in Directory.GetFiles(Dir, "*.png"))
        {
            string p = file.Replace('\\', '/');
            var ti = (TextureImporter)AssetImporter.GetAtPath(p);
            if (ti == null) continue;
            string n = Path.GetFileNameWithoutExtension(p);
            bool particle = n == "drop" || n == "streak" || n == "soft";
            ti.textureType = particle ? TextureImporterType.Default : TextureImporterType.Sprite;
            if (!particle) { ti.spriteImportMode = SpriteImportMode.Single; ti.spritePixelsPerUnit = 100f; }
            if (n == "card") ti.spriteBorder = new Vector4(62, 62, 62, 62);
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = particle;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }

        // ---- particle materials (in Resources so they're always in the build)
        MakeMat("Mat_Rain", "drop", new Color(0.8f, 0.88f, 1f, 0.6f));
        MakeMat("Mat_Wind", "streak", new Color(1f, 1f, 1f, 0.45f));
        MakeMat("Mat_Dust", "soft", new Color(0.85f, 0.75f, 0.6f, 0.5f));
        AssetDatabase.SaveAssets();

        // ---- add the weather object to the farm
        if (!File.Exists(FarmScene)) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't find " + FarmScene, "OK"); return; }
        string back = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        var scene = EditorSceneManager.OpenScene(FarmScene, OpenSceneMode.Single);
        var existing = Object.FindAnyObjectByType<TonightsWeather>();
        if (existing == null)
        {
            var go = new GameObject("Tonights_Weather");
            go.AddComponent<TonightsWeather>();
            var round = GameObject.Find("_Round");
            if (round != null) go.transform.SetParent(round.transform, false);
            AssignSounds(go.GetComponent<TonightsWeather>());
        }
        else AssignSounds(existing);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(back) && back != FarmScene && File.Exists(back)) EditorSceneManager.OpenScene(back, OpenSceneMode.Single);

        Debug.Log("[Farm Chaos] Tonight's Weather added to Farm_Main. Press Play to see the roll. " +
                  "To test one weather: select _Round/Tonights_Weather, tick Force Weather and pick one.");
    }

    static void MakeMat(string name, string tex, Color c)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) { Debug.LogWarning("[Farm Chaos] URP particle shader not found."); return; }
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + "/" + tex + ".png");
        m.SetTexture("_BaseMap", t);
        m.SetColor("_BaseColor", c);
        // transparent, alpha blended, no depth write
        m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
    }

    // use your friend's sounds if they fit (all optional)
    static void AssignSounds(TonightsWeather w)
    {
        if (w == null) return;
        var so = new SerializedObject(w);
        void Try(string field, params string[] names)
        {
            var p = so.FindProperty(field);
            if (p == null || p.objectReferenceValue != null) return;
            foreach (var n in names)
                foreach (var g in AssetDatabase.FindAssets(n + " t:AudioClip"))
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g));
                    if (clip != null) { p.objectReferenceValue = clip; return; }
                }
        }
        Try("rollTick", "deep-click", "pick");
        Try("rollLand", "whistle", "paid");
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
