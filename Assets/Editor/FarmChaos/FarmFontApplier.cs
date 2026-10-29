#if UNITY_EDITOR
// Farm Chaos - switch every text in the game to a cute font.
// 1) Put a .ttf/.otf font file in Assets/Fonts   2) Tools > Farm Chaos > 12. Apply Cute Font
// Converts it to a TextMeshPro font, makes it the project default, and applies it to every text in
// the Lobby and Farm_Main scenes (keeping outlines). The old font stays as a fallback for odd symbols.
// Your friend's Game.unity test scene is not touched.
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class FarmFontApplier
{
    const string FontFolder = "Assets/Fonts";
    static readonly string[] Scenes = { "Assets/Scenes/Lobby.unity", "Assets/Scenes/Farm_Main.unity" };

    [MenuItem("Tools/Farm Chaos/12. Apply Cute Font")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!AssetDatabase.IsValidFolder(FontFolder)) AssetDatabase.CreateFolder("Assets", "Fonts");

        var fontPath = Directory.GetFiles(FontFolder).Where(f => f.EndsWith(".ttf", System.StringComparison.OrdinalIgnoreCase) || f.EndsWith(".otf", System.StringComparison.OrdinalIgnoreCase))
                                .Select(f => f.Replace('\\', '/')).OrderBy(f => f).FirstOrDefault();
        if (fontPath == null)
        {
            EditorUtility.DisplayDialog("Farm Chaos", "Put a .ttf or .otf font file into Assets/Fonts first, then run this again.", "OK");
            return;
        }
        var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
        if (font == null) { EditorUtility.DisplayDialog("Farm Chaos", "Unity couldn't read " + fontPath, "OK"); return; }

        // ---- make (or reuse) the TextMeshPro font asset
        string name = Path.GetFileNameWithoutExtension(fontPath);
        string assetPath = FontFolder + "/" + name + " SDF.asset";
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa == null)
        {
            fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (fa == null) { EditorUtility.DisplayDialog("Farm Chaos", "Couldn't convert the font. Try a different .ttf file.", "OK"); return; }
            fa.name = name + " SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.atlasTextures != null && fa.atlasTextures.Length > 0 && fa.atlasTextures[0] != null)
            { fa.atlasTextures[0].name = name + " Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa); }
            fa.material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
        }

        // keep the old default as a fallback for characters the cute font doesn't have
        var oldDefault = TMP_Settings.defaultFontAsset;
        if (oldDefault != null && oldDefault != fa)
        {
            if (fa.fallbackFontAssetTable == null) fa.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!fa.fallbackFontAssetTable.Contains(oldDefault)) fa.fallbackFontAssetTable.Add(oldDefault);
        }
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();

        // ---- project default (new texts use it automatically)
        var settings = Resources.Load<TMP_Settings>("TMP Settings");
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            var p = so.FindProperty("m_defaultFontAsset");
            if (p != null) { p.objectReferenceValue = fa; so.ApplyModifiedProperties(); }
        }

        // ---- every text in the game scenes
        string returnTo = EditorSceneManager.GetActiveScene().path;
        EditorSceneManager.SaveOpenScenes();
        int changed = 0;
        foreach (var scenePath in Scenes)
        {
            if (!File.Exists(scenePath)) continue;
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (t.font == fa) continue;
                    float ow = 0f; Color32 oc = Color.black;
                    var m = t.fontSharedMaterial;
                    if (m != null && m.HasProperty(ShaderUtilities.ID_OutlineWidth)) { ow = m.GetFloat(ShaderUtilities.ID_OutlineWidth); oc = m.GetColor(ShaderUtilities.ID_OutlineColor); }
                    Undo.RecordObject(t, "Cute Font");
                    t.font = fa;
                    t.fontSharedMaterial = fa.material;
                    if (ow > 0.001f) { t.outlineWidth = ow; t.outlineColor = oc; }
                    EditorUtility.SetDirty(t);
                    changed++;
                }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(returnTo) && File.Exists(returnTo)) EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);

        Debug.Log($"[Farm Chaos] Cute font '{name}' applied to {changed} texts in the Lobby and Farm_Main, and set as the default for new texts.");
    }
}
#endif
