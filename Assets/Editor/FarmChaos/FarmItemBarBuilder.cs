#if UNITY_EDITOR
// Farm Chaos - adds the item bar (4 pockets + fuse badge + message line) and item effects.
// Menu: Tools > Farm Chaos > 10. Add Item Bar
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FarmItemBarBuilder
{
    const string Root = "Item_Bar_UI";

    [MenuItem("Tools/Farm Chaos/10. Add Item Bar")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Farm Chaos", "Stop Play mode first.", "OK"); return; }
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        foreach (var t in all.Where(t => t != null && t.name == Root).ToList()) if (t != null) Undo.DestroyObjectImmediate(t.gameObject);

        // ---- runtime systems on _Round
        var round = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "_Round");
        if (round == null) { EditorUtility.DisplayDialog("Farm Chaos", "Add the Round Clock first (step 8).", "OK"); return; }
        foreach (var c in round.GetComponents<Inventory>()) Undo.DestroyObjectImmediate(c);
        foreach (var c in round.GetComponents<ItemEffects>()) Undo.DestroyObjectImmediate(c);
        Undo.AddComponent<Inventory>(round);
        var fx = Undo.AddComponent<ItemEffects>(round);
        fx.poofMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Lighting/Mat_light_pool.mat");

        // ---- canvas
        var parent = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "_Player");
        var go = new GameObject(Root, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(go, "Item Bar");
        if (parent != null) go.transform.SetParent(parent.transform, false);
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        var rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        float slot = 120f, gap = 16f;
        float barW = Inventory.SlotCount * slot + (Inventory.SlotCount - 1) * gap + 36f;

        var bar = Img("Bar", go.transform, rounded, new Color(0.08f, 0.09f, 0.2f, 0.55f), new Vector2(barW, slot + 30f), new Vector2(0.5f, 0f), true);
        bar.anchoredPosition = new Vector2(0, 95);

        var ui = go.AddComponent<ItemBarUI>();
        for (int i = 0; i < Inventory.SlotCount; i++)
        {
            var s = Img("Slot_" + (i + 1), bar, rounded, new Color(1f, 0.95f, 0.88f, 0.92f), new Vector2(slot, slot), new Vector2(0f, 0.5f), true);
            s.anchoredPosition = new Vector2(18f + slot / 2f + i * (slot + gap), 0);
            var btn = s.gameObject.AddComponent<Button>();
            var colors = btn.colors; colors.pressedColor = new Color(0.85f, 0.95f, 0.88f); btn.colors = colors;
            var icon = Img("Icon", s, null, Color.white, new Vector2(92, 92), new Vector2(0.5f, 0.5f), false);
            icon.GetComponent<Image>().enabled = false;
            var key = Text("Key", s, (i + 1).ToString(), 26, new Vector2(30, 30), new Vector2(0f, 1f), new Color32(0x9C, 0x76, 0x56, 255));
            key.rectTransform.anchoredPosition = new Vector2(18, -16);
            var count = Text("Count", s, "", 30, new Vector2(70, 34), new Vector2(1f, 0f), new Color32(0x6B, 0x4A, 0x3A, 255));
            count.rectTransform.anchoredPosition = new Vector2(-30, 18); count.alignment = TextAlignmentOptions.Right;
            ui.slots[i] = new ItemBarUI.SlotView { root = s, button = btn, icon = icon.GetComponent<Image>(), count = count };
        }

        // fuse badge (only shows while carrying the fuse)
        var badge = Img("Fuse_Badge", go.transform, knob, new Color32(0xFF, 0xD2, 0x3F, 240), new Vector2(116, 116), new Vector2(0.5f, 0f), false);
        badge.anchoredPosition = new Vector2(-(barW / 2f) - 80f, 95);
        var fuseSprite = all.Select(t => t != null ? t.GetComponent<Image>() : null).FirstOrDefault(im => im != null && im.name == "FuseVisual")?.sprite;
        var fi = Img("Fuse_Icon", badge, fuseSprite, Color.white, new Vector2(84, 64), new Vector2(0.5f, 0.55f), false);
        fi.GetComponent<Image>().preserveAspect = true;
        var fl = Text("Label", badge, "FUSE", 24, new Vector2(110, 30), new Vector2(0.5f, 0f), new Color32(0x6B, 0x4A, 0x3A, 255));
        fl.rectTransform.anchoredPosition = new Vector2(0, 20);
        badge.gameObject.SetActive(false);
        ui.fuseBadge = badge;

        // message line above the bar
        var toast = Text("Message", go.transform, "", 40, new Vector2(1000, 60), new Vector2(0.5f, 0f), Color.white);
        toast.rectTransform.anchoredPosition = new Vector2(0, 205);
        toast.outlineWidth = 0.18f; toast.outlineColor = new Color32(0x2A, 0x23, 0x40, 255);
        ui.toast = toast;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = go;
        Debug.Log("[Farm Chaos] Item bar added (scene saved). Buy something at the HQ shop, then press 1-4 or tap the slot to use it.");
    }

    static RectTransform Img(string name, Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 anchor, bool sliced)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(Image));
        g.transform.SetParent(parent, false);
        var rt = g.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size;
        var img = g.GetComponent<Image>(); img.sprite = sprite; img.color = color;
        if (sliced && sprite != null) img.type = Image.Type.Sliced;
        img.raycastTarget = name.StartsWith("Slot_") || name == "Bar";
        return rt;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text, float size, Vector2 box, Vector2 anchor, Color color)
    {
        var g = new GameObject(name, typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.color = color; t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = box;
        return t;
    }
}
#endif
