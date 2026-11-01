// Farm Chaos - little helpers for the lobby's pop-up panels (shop, decorating) and simple 3D shapes.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class LobbyKit
{
    public static readonly Color Cream = new Color32(0xFF, 0xF6, 0xEC, 248), Pink = new Color32(0xF4, 0xB8, 0xC1, 255),
        Mint = new Color32(0x8C, 0xCF, 0xA4, 255), Brown = new Color32(0x6B, 0x4A, 0x3A, 255), Berry = new Color32(0xE0, 0x6C, 0x8C, 255),
        Grey = new Color32(0xD8, 0xD2, 0xCC, 255), White = new Color32(0xFF, 0xFF, 0xFF, 235);

    public static Sprite Rounded;

    public static RectTransform Box(Transform parent, Color c, Vector2 size, Vector2 anchor, Vector2 pos, bool blocks = false)
    {
        var g = new GameObject("Box", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.color = c; img.raycastTarget = blocks;
        if (Rounded != null) { img.sprite = Rounded; img.type = Image.Type.Sliced; }
        return r;
    }

    public static Button Btn(Transform parent, string label, Color c, Vector2 size, Vector2 pos, float font, UnityEngine.Events.UnityAction click)
    {
        var r = Box(parent, c, size, new Vector2(0.5f, 0.5f), pos, true);
        r.name = "Btn_" + label;
        Txt(r, label, font, Brown, Vector2.zero, size);
        var b = r.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.82f, 0.82f, 0.82f); cb.disabledColor = new Color(0.8f, 0.8f, 0.8f, 0.6f); b.colors = cb;
        if (click != null) b.onClick.AddListener(click);
        return b;
    }

    public static TextMeshProUGUI Txt(Transform parent, string text, float size, Color c, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = Mathf.Min(14f, size); t.fontSizeMax = size;
        t.rectTransform.sizeDelta = box; t.rectTransform.anchoredPosition = pos;
        return t;
    }

    /// <summary>A tiny green banknote + amount, e.g. for "120".</summary>
    public static TextMeshProUGUI MoneyTag(Transform parent, Vector2 anchor, Vector2 pos, float scale = 1f)
    {
        var bg = Box(parent, new Color32(0xE9, 0xF7, 0xE4, 245), new Vector2(230, 70) * scale, anchor, pos);
        bg.name = "Money_Tag";
        var note = Box(bg, new Color32(0x7C, 0xC6, 0x7E, 255), new Vector2(58, 36) * scale, new Vector2(0f, 0.5f), new Vector2(42, 0) * scale);
        Box(note, new Color32(0xC9, 0xEB, 0xC2, 255), new Vector2(18, 18) * scale, new Vector2(0.5f, 0.5f), Vector2.zero);
        var t = Txt(bg, "0", 36 * scale, new Color32(0x3E, 0x7D, 0x45, 255), new Vector2(30, 0) * scale, new Vector2(150, 60) * scale);
        t.alignment = TextAlignmentOptions.Left;
        return t;
    }

    // ------------------------------------------------------------ 3D
    public static Material Paint(Color c, bool glow = false) => LobbyRulesBoard.Paint(c, glow);

    public static Transform Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Color c, bool solid = false, bool glow = false)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name;
        if (!solid) Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale;
        g.GetComponent<Renderer>().material = Paint(c, glow);
        return g.transform;
    }

    public static bool EscPressed()
    {
        bool esc = false;
#if ENABLE_INPUT_SYSTEM
        esc |= UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        esc |= Input.GetKeyDown(KeyCode.Escape);
#endif
        return esc;
    }

    public static float FlatDistance(Vector3 a, Vector3 b) { a.y = 0f; b.y = 0f; return Vector3.Distance(a, b); }

    /// <summary>Is a full-screen lobby panel (shop, customizer) open? Then other pop-ups stay quiet.</summary>
    public static bool BusyPanel => LobbyShop.IsOpen || (ChibiWardrobeUI.Instance != null && ChibiWardrobeUI.Instance.IsOpen);
}
