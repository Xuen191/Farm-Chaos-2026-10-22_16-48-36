// Farm Chaos - the secret role card shown at the start of an online round (spy mode).
// Built at runtime; tap anywhere (or wait) to close.
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoleCard : MonoBehaviour
{
    public static void Show(string title, string goal, bool spy)
    {
        var go = new GameObject("Role_Card", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 200;
        var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 0.5f;
        go.AddComponent<RoleCard>().Build(title, goal, spy);
    }

    CanvasGroup group;

    void Build(string title, string goal, bool spy)
    {
        var dim = Box(transform, new Color(0.08f, 0.07f, 0.16f, 0.7f), Vector2.zero, Vector2.zero);
        var drt = (RectTransform)dim; drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one; drt.offsetMin = drt.offsetMax = Vector2.zero;
        dim.gameObject.AddComponent<Button>().onClick.AddListener(Close);
        group = gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;

        Color cardC = spy ? new Color32(0x3A, 0x33, 0x46, 255) : new Color32(0xFF, 0xF0, 0xD9, 255);
        Color titleC = spy ? new Color32(0xF4, 0xB8, 0xC1, 255) : new Color32(0x5E, 0xA8, 0x6E, 255);
        Color textC = spy ? new Color32(0xFF, 0xF0, 0xD9, 255) : new Color32(0x6B, 0x4A, 0x3A, 255);
        var card = Box(transform, cardC, new Vector2(1100, 680), Vector2.zero);

        Txt(card, spy ? "SHH..." : "ROUND START", 34, titleC, new Vector2(0, 280), FontStyles.Bold);
        var t = Txt(card, title, 92, titleC, new Vector2(0, 190), FontStyles.Bold);
        t.outlineWidth = 0.15f; t.outlineColor = spy ? new Color32(0x1B, 0x1D, 0x38, 255) : Color.white;
        var g = Txt(card, goal, 36, textC, new Vector2(0, -40), FontStyles.Normal);
        g.textWrappingMode = TextWrappingModes.Normal; g.rectTransform.sizeDelta = new Vector2(980, 330);
        g.wordSpacing = 30f; g.lineSpacing = 18f; g.characterSpacing = 2f;   // this font's spaces are very narrow
        g.alignment = TextAlignmentOptions.Center; g.enableAutoSizing = true; g.fontSizeMin = 26; g.fontSizeMax = 36;
        var hint = Txt(card, spy ? "Only you can see this. Tap to hide." : "Tap to start!", 30, new Color(textC.r, textC.g, textC.b, 0.6f), new Vector2(0, -290), FontStyles.Italic);
        hint.wordSpacing = 25f; t.wordSpacing = 20f;
        StartCoroutine(Life());
    }

    IEnumerator Life()
    {
        for (float a = 0; a < 1f; a += Time.unscaledDeltaTime * 3f) { group.alpha = a; yield return null; }
        group.alpha = 1f;
        yield return new WaitForSecondsRealtime(8f);
        Close();
    }

    void Close()
    {
        if (this == null) return;
        StopAllCoroutines();
        Destroy(gameObject);
    }

    static Transform Box(Transform parent, Color c, Vector2 size, Vector2 pos)
    {
        var g = new GameObject("Box", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.sizeDelta = size; r.anchoredPosition = pos;
        g.GetComponent<Image>().color = c;
        return g.transform;
    }

    static TextMeshProUGUI Txt(Transform parent, string text, float size, Color c, Vector2 pos, FontStyles style)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = style; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        t.rectTransform.sizeDelta = new Vector2(1040, size * 1.4f); t.rectTransform.anchoredPosition = pos;
        return t;
    }
}
