#if UNITY_EDITOR
// Farm Chaos - makes the lobby buttons/panels look cute: big rounded corners, soft shadows,
// press feedback, and a styled code box. Doesn't move or rename anything.
// Menu: Tools > Farm Chaos > 14. Polish Lobby Buttons
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmLobbyPolish
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    static readonly Color32 Brown = new Color32(0x6B, 0x4A, 0x3A, 255);

    [MenuItem("Tools/Farm Chaos/14. Polish Lobby Buttons")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        if (!File.Exists(LobbyPath)) { EditorUtility.DisplayDialog("Farm Chaos", "Build the Lobby first (step 11).", "OK"); return; }
        EditorSceneManager.SaveOpenScenes();
        var scene = SceneManager.GetActiveScene().path == LobbyPath ? SceneManager.GetActiveScene() : EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var ui = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LobbyUI>(true)).FirstOrDefault();
        if (ui == null) { EditorUtility.DisplayDialog("Farm Chaos", "No LobbyUI found in the Lobby scene.", "OK"); return; }
        ApplyTo(ui);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Farm Chaos] Lobby buttons polished (scene saved).");
    }

    const string SpritePath = "Assets/Materials/Lobby/UI_Rounded.png";
    const int Corner = 60;   // corner radius in the sprite, in pixels

    // a clean white rounded square (no dark edge like Unity's UISprite)
    public static Sprite RoundedSprite()
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (s != null) return s;
        if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Lobby")) AssetDatabase.CreateFolder("Assets/Materials", "Lobby");
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        float r = Corner;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, r, N - r), cy = Mathf.Clamp(y + 0.5f, r, N - r);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                px[y * N + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SpritePath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = new Vector4(Corner, Corner, Corner, Corner);
        imp.spritePixelsPerUnit = 100;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    // put a direct child at a fixed distance from the panel's top (centre of the child)
    static void Place(RectTransform panel, string child, float yFromTop, float x = 0f)
    {
        var t = panel.Find(child) as RectTransform;
        if (t == null) return;
        Undo.RecordObject(t, "Polish Lobby");
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 1f);
        t.pivot = new Vector2(0.5f, 0.5f);
        t.anchoredPosition = new Vector2(x, yFromTop);
        EditorUtility.SetDirty(t);
    }

    public static void ApplyTo(LobbyUI ui)
    {
        var rounded = RoundedSprite();
        // 1) rounded corners + soft shadow on every sliced panel/button
        foreach (var img in ui.GetComponentsInChildren<Image>(true))
        {
            if (img.sprite == null || img.type != Image.Type.Sliced) continue;
            if (img.GetComponentInParent<TMP_InputField>(true) != null && img.GetComponent<TMP_InputField>() == null) continue;
            var size = img.rectTransform.rect.size;
            if (size.x < 1f) size = img.rectTransform.sizeDelta;
            float radius = Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.32f, 8f, 40f);
            Undo.RecordObject(img, "Polish Lobby");
            if (rounded != null) img.sprite = rounded;
            img.pixelsPerUnitMultiplier = Corner / radius;
            EditorUtility.SetDirty(img);

            var sh = img.GetComponent<Shadow>();
            if (sh == null) sh = Undo.AddComponent<Shadow>(img.gameObject);
            bool big = Mathf.Min(size.x, size.y) > 200f;
            sh.effectColor = new Color(0.42f, 0.29f, 0.23f, big ? 0.28f : 0.35f);
            sh.effectDistance = big ? new Vector2(0, -10) : new Vector2(0, -6);
            EditorUtility.SetDirty(sh);
        }

        // 2) buttons: gentle tint on hover / press
        foreach (var b in ui.GetComponentsInChildren<Button>(true))
        {
            Undo.RecordObject(b, "Polish Lobby");
            b.transition = Selectable.Transition.ColorTint;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f);
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            EditorUtility.SetDirty(b);
        }

        // 3) party panel: smaller, tucked into the top-right so it never covers the jump button
        if (ui.partyPanel != null)
        {
            var prt = (RectTransform)ui.partyPanel.transform;
            Undo.RecordObject(prt, "Polish Lobby");
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 1f);
            prt.pivot = new Vector2(1f, 1f);
            prt.localScale = Vector3.one * 0.82f;
            prt.anchoredPosition = new Vector2(-36f, -130f);
            EditorUtility.SetDirty(prt);
        }
        // even spacing inside the party panel (top-down)
        if (ui.partyPanel != null)
        {
            var p = (RectTransform)ui.partyPanel.transform;
            Undo.RecordObject(p, "Polish Lobby");
            p.sizeDelta = new Vector2(560f, 700f);
            Place(p, "Party_Title", -62f);
            Place(p, "Size_Label", -135f);
            for (int i = 1; i <= 5; i++) Place(p, "Size_" + i, -212f, -188f + (i - 1) * 94f);
            Place(p, "Code_Label", -300f);
            Place(p, "Code_Box", -365f);
            Place(p, "Status", -440f);
            Place(p, "Start_Button", -540f);
            Place(p, "Leave_Button", -640f);
        }
        // join panel: tighter, no big empty gap
        if (ui.joinPanel != null)
        {
            var j = (RectTransform)ui.joinPanel.transform;
            Undo.RecordObject(j, "Polish Lobby");
            j.sizeDelta = new Vector2(540f, 330f);
            Place(j, "Join_Title", -52f);
            Place(j, "Code_Input", -135f);
            Place(j, "Join_Message", -200f);
            Place(j, "Join_Button", -268f, -112f);
            Place(j, "Close_Button", -268f, 112f);
        }
        if (ui.partyStatus != null)
        {
            Undo.RecordObject(ui.partyStatus, "Polish Lobby");
            ui.partyStatus.rectTransform.sizeDelta = new Vector2(470f, 40f);
            ui.partyStatus.enableAutoSizing = true;
            ui.partyStatus.fontSizeMin = 16f; ui.partyStatus.fontSizeMax = 26f;
            EditorUtility.SetDirty(ui.partyStatus);
        }

        // 4) the code box
        var field = ui.codeInput;
        if (field != null)
        {
            var bg = field.GetComponent<Image>();
            if (bg != null)
            {
                Undo.RecordObject(bg, "Polish Lobby");
                bg.color = new Color32(0xC8, 0xE6, 0xD0, 255);   // mint, like the party code box
                if (rounded != null) bg.sprite = rounded;
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = Corner / 24f;
                EditorUtility.SetDirty(bg);
                var sh = bg.GetComponent<Shadow>();
                if (sh == null) sh = Undo.AddComponent<Shadow>(bg.gameObject);
                sh.effectColor = new Color(0.42f, 0.29f, 0.23f, 0.25f); sh.effectDistance = new Vector2(0, -4);
            }
            Undo.RecordObject(field, "Polish Lobby");
            var cb = field.colors;
            cb.normalColor = Color.white; cb.highlightedColor = new Color(0.97f, 0.97f, 0.97f);
            cb.selectedColor = Color.white; cb.pressedColor = new Color(0.92f, 0.92f, 0.92f);
            field.colors = cb;
            field.caretColor = Brown; field.customCaretColor = true; field.caretWidth = 3;
            field.selectionColor = new Color(0.96f, 0.72f, 0.76f, 0.6f);
            EditorUtility.SetDirty(field);
            if (field.textComponent != null)
            {
                Undo.RecordObject(field.textComponent, "Polish Lobby");
                field.textComponent.color = Brown;
                field.textComponent.fontStyle = FontStyles.Bold;
                field.textComponent.characterSpacing = 18f;
                EditorUtility.SetDirty(field.textComponent);
            }
            if (field.placeholder is TMP_Text ph)
            {
                Undo.RecordObject(ph, "Polish Lobby");
                ph.color = new Color(0.42f, 0.29f, 0.23f, 0.4f);
                ph.fontStyle = FontStyles.Bold;
                ph.characterSpacing = 18f;
                EditorUtility.SetDirty(ph);
            }
        }
    }
}
#endif
