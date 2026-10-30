// Farm Chaos - short story intro played after START in the lobby.
// 5 slides built from simple shapes: bedtime -> too hot -> power out -> animals loose -> FARM CHAOS!
// Tap / click to go to the next slide, or press Skip.
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IntroStory : MonoBehaviour
{
    public Sprite circle;      // round sprite (Knob)
    public Sprite rounded;     // rounded-rectangle sprite (UISprite)
    public float slideSeconds = 3.2f;

    RectTransform stage;
    bool skip, next;
    readonly List<System.Action<float>> animators = new List<System.Action<float>>();

    // ---------------------------------------------------------------- public
    public IEnumerator Play()
    {
        skip = false;
        BuildStage();
        yield return Slide(Night);
        if (!skip) yield return Slide(Hot);
        if (!skip) yield return Slide(PowerOut);
        if (!skip) yield return Slide(Window);
        if (!skip) yield return Slide(Title);
        // leave a dark screen behind while the farm loads
        Clear(); Fill(stage, new Color32(0x1B, 0x1D, 0x38, 255));
    }

    // ---------------------------------------------------------------- frame
    void BuildStage()
    {
        if (stage != null) Destroy(stage.gameObject);
        var go = new GameObject("Intro_Stage", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(transform, false);
        stage = go.GetComponent<RectTransform>();
        stage.anchorMin = Vector2.zero; stage.anchorMax = Vector2.one; stage.offsetMin = stage.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);   // catches taps
        go.GetComponent<Button>().onClick.AddListener(() => next = true);
        stage.SetAsLastSibling();
    }

    IEnumerator Slide(System.Action<RectTransform> build)
    {
        Clear();
        var slide = new GameObject("Slide", typeof(RectTransform), typeof(CanvasGroup)).GetComponent<RectTransform>();
        slide.SetParent(stage, false); Stretch(slide);
        var cg = slide.GetComponent<CanvasGroup>(); cg.blocksRaycasts = false; cg.alpha = 0f;
        build(slide);
        SkipButton(slide);

        next = false;
        float t = 0f;
        while (t < slideSeconds && !next && !skip)
        {
            t += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(t / 0.35f);
            foreach (var a in animators) a(t);
            yield return null;
        }
        // quick fade out
        float f = 0f;
        while (f < 0.2f && !skip) { f += Time.unscaledDeltaTime; cg.alpha = 1f - f / 0.2f; yield return null; }
    }

    void SkipButton(RectTransform slide)
    {
        var b = Img(slide, rounded, new Color(1f, 1f, 1f, 0.85f), new Vector2(190, 70), new Vector2(1f, 1f), new Vector2(-130, -70));
        b.GetComponent<Image>().raycastTarget = true;
        var btn = b.gameObject.AddComponent<Button>(); btn.onClick.AddListener(() => skip = true);
        var t = Txt(b, "Skip  >", 34, new Color32(0x6B, 0x4A, 0x3A, 255), Vector2.zero, new Vector2(180, 64));
        // the slide's CanvasGroup doesn't block raycasts, so let this one through
        var cg = b.gameObject.AddComponent<CanvasGroup>(); cg.ignoreParentGroups = true; cg.blocksRaycasts = true;
    }

    void Clear()
    {
        animators.Clear();
        if (stage == null) return;
        for (int i = stage.childCount - 1; i >= 0; i--) Destroy(stage.GetChild(i).gameObject);
    }

    // ---------------------------------------------------------------- slides
    void Night(RectTransform s)
    {
        Fill(s, new Color32(0x2B, 0x2D, 0x5C, 255));
        var rng = new System.Random(3);
        for (int i = 0; i < 26; i++)
        {
            var star = Img(s, circle, new Color(1f, 0.97f, 0.85f), Vector2.one * (6 + rng.Next(10)), new Vector2(0.5f, 0.5f),
                           new Vector2(rng.Next(-900, 900), rng.Next(-60, 480)));
            float ph = (float)rng.NextDouble() * 6f; var img = star.GetComponent<Image>();
            animators.Add(t => { var c = img.color; c.a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(t * 2f + ph)); img.color = c; });
        }
        var moon = Img(s, circle, new Color32(0xFF, 0xE9, 0xA8, 255), new Vector2(240, 240), new Vector2(0.5f, 0.5f), new Vector2(-40, 210));
        Img(s, circle, new Color32(0x2B, 0x2D, 0x5C, 255), new Vector2(215, 215), new Vector2(0.5f, 0.5f), new Vector2(30, 250));
        for (int i = 0; i < 3; i++)
        {
            var z = Txt(s, "Z", 60 - i * 12, new Color32(0xF4, 0xB8, 0xC1, 255), new Vector2(170 + i * 60, 150 + i * 60), new Vector2(100, 100));
            int k = i; Vector2 basePos = z.rectTransform.anchoredPosition;
            animators.Add(t => { float a = (t * 0.6f + k * 0.33f) % 1f; z.rectTransform.anchoredPosition = basePos + new Vector2(Mathf.Sin(a * 6f) * 10f, a * 60f); z.alpha = 1f - a; });
        }
        Caption(s, "11:00 PM on the farm...", "Time for bed. Night night!");
    }

    void Hot(RectTransform s)
    {
        Fill(s, new Color32(0xF7, 0xA9, 0x86, 255));
        var hot = Txt(s, "HOT!", 220, new Color32(0xE0, 0x4C, 0x5C, 255), new Vector2(0, 200), new Vector2(900, 260));
        hot.outlineWidth = 0.15f; hot.outlineColor = new Color32(0xFF, 0xF0, 0xD9, 255);
        animators.Add(t => { hot.rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 14f) * 6f); hot.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * 9f)); });
        var rng = new System.Random(5);
        for (int i = 0; i < 9; i++)
        {
            var d = Img(s, circle, new Color32(0x9F, 0xD8, 0xF5, 255), new Vector2(34, 46), new Vector2(0.5f, 0.5f), Vector2.zero);
            float x = rng.Next(-650, 650), speed = 260f + rng.Next(200), off = (float)rng.NextDouble() * 3f;
            animators.Add(t => { float y = 520f - ((t + off) * speed) % 700f; d.anchoredPosition = new Vector2(x, y); });
        }
        Caption(s, "...but it's sooo hot!", "You wake up all sweaty.");
    }

    void PowerOut(RectTransform s)
    {
        var bg = Fill(s, new Color32(0xFF, 0xF3, 0xC4, 255));
        var glow = Img(s, circle, new Color(1f, 0.9f, 0.5f, 0.5f), new Vector2(460, 460), new Vector2(0.5f, 0.5f), new Vector2(0, 190));
        var bulb = Img(s, circle, new Color32(0xFF, 0xE0, 0x66, 255), new Vector2(230, 230), new Vector2(0.5f, 0.5f), new Vector2(0, 210));
        Img(s, rounded, new Color32(0x8D, 0x96, 0xA3, 255), new Vector2(110, 80), new Vector2(0.5f, 0.5f), new Vector2(0, 70));
        var title = CaptionTitle(s, "The fan stopped...");
        var sub = CaptionSub(s, "Wait... what's that?");
        Color lit = new Color32(0xFF, 0xF3, 0xC4, 255), dark = new Color32(0x1B, 0x1C, 0x33, 255);
        var bulbImg = bulb.GetComponent<Image>(); var glowImg = glow.GetComponent<Image>(); var bgImg = bg.GetComponent<Image>();
        bool done = false;
        animators.Add(t =>
        {
            float off = 0f;
            if (t > 1.0f && t < 1.8f) off = Mathf.PerlinNoise(t * 18f, 0.3f) > 0.5f ? 1f : 0f;   // flicker
            else if (t >= 1.8f) off = 1f;
            bgImg.color = Color.Lerp(lit, dark, off);
            bulbImg.color = Color.Lerp(new Color32(0xFF, 0xE0, 0x66, 255), new Color32(0x5A, 0x60, 0x6B, 255), off);
            glowImg.enabled = off < 0.5f;
            title.color = Color.Lerp(new Color32(0x6B, 0x4A, 0x3A, 255), Color.white, off);
            sub.color = title.color;
            if (t >= 1.8f && !done) { done = true; title.text = "The POWER is OUT!"; sub.text = "Everything goes dark..."; }
        });
    }

    void Window(RectTransform s)
    {
        Fill(s, new Color32(0xC9, 0xDD, 0xF5, 255));
        var frame = Img(s, rounded, new Color32(0xFF, 0xF0, 0xD9, 255), new Vector2(620, 420), new Vector2(0.5f, 0.5f), new Vector2(0, 170));
        var night = Img(frame, rounded, new Color32(0x2B, 0x2D, 0x5C, 255), new Vector2(560, 360), new Vector2(0.5f, 0.5f), Vector2.zero);
        Img(night, rounded, new Color32(0x6C, 0x9A, 0x5E, 255), new Vector2(560, 110), new Vector2(0.5f, 0f), new Vector2(0, 55));   // grass
        // bouncing animals: chick, sheep, cow, bunny (simple blobs with eyes)
        Color[] cols = { new Color32(0xFF, 0xE0, 0x66, 255), Color.white, new Color32(0xD9, 0xB9, 0x9B, 255), new Color32(0xF4, 0xB8, 0xC1, 255) };
        float[] sizes = { 50, 76, 90, 56 };
        for (int i = 0; i < cols.Length; i++)
        {
            var a = Img(night, circle, cols[i], Vector2.one * sizes[i], new Vector2(0.5f, 0f), Vector2.zero);
            Img(a, circle, new Color32(0x2E, 0x2A, 0x3A, 255), Vector2.one * 9, new Vector2(0.65f, 0.65f), Vector2.zero);
            Img(a, circle, new Color32(0x2E, 0x2A, 0x3A, 255), Vector2.one * 9, new Vector2(0.35f, 0.65f), Vector2.zero);
            int k = i; float sp = 1.3f + k * 0.35f, size = sizes[i];
            animators.Add(t =>
            {
                float x = Mathf.Sin(t * sp + k * 1.7f) * 200f;
                float y = 70f + size / 2f + Mathf.Abs(Mathf.Sin(t * (5f + k))) * 60f;
                a.anchoredPosition = new Vector2(x, y);
                a.localScale = new Vector3(Mathf.Sign(Mathf.Cos(t * sp + k * 1.7f)), 1, 1);
            });
        }
        // window cross bars
        Img(frame, rounded, new Color32(0xFF, 0xF0, 0xD9, 255), new Vector2(18, 360), new Vector2(0.5f, 0.5f), Vector2.zero);
        Img(frame, rounded, new Color32(0xFF, 0xF0, 0xD9, 255), new Vector2(560, 18), new Vector2(0.5f, 0.5f), Vector2.zero);
        var bang = Txt(s, "!!", 150, new Color32(0xE0, 0x4C, 0x5C, 255), new Vector2(390, 330), new Vector2(200, 200));
        bang.outlineWidth = 0.15f; bang.outlineColor = Color.white;
        animators.Add(t => bang.rectTransform.anchoredPosition = new Vector2(390 + Mathf.Sin(t * 30f) * 8f, 330));
        Caption(s, "You peek outside...", "The animals are LOOSE!");
    }

    void Title(RectTransform s)
    {
        Fill(s, new Color32(0xF4, 0xB8, 0xC1, 255));
        var rng = new System.Random(9);
        for (int i = 0; i < 18; i++)
        {
            Color c = i % 3 == 0 ? new Color32(0xC8, 0xE6, 0xD0, 255) : i % 3 == 1 ? new Color32(0xFF, 0xF0, 0xD9, 255) : new Color32(0xC9, 0xDD, 0xF5, 255);
            var dot = Img(s, circle, c, Vector2.one * (18 + rng.Next(30)), new Vector2(0.5f, 0.5f), new Vector2(rng.Next(-900, 900), rng.Next(-500, 500)));
            float sp = 20f + rng.Next(40); Vector2 p0 = dot.anchoredPosition;
            animators.Add(t => dot.anchoredPosition = p0 + new Vector2(0, -t * sp));
        }
        var title = Txt(s, "FARM CHAOS!", 170, Color.white, new Vector2(0, 120), new Vector2(1500, 230));
        title.outlineWidth = 0.22f; title.outlineColor = new Color32(0xE0, 0x6C, 0x8C, 255);
        animators.Add(t => { float k = Mathf.Clamp01(t / 0.5f); float b = 1f + Mathf.Sin(k * Mathf.PI) * 0.25f; title.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k) * b; });
        var sub = Txt(s, "Round up the animals & fix the power before sunrise!", 46, new Color32(0x6B, 0x4A, 0x3A, 255), new Vector2(0, -60), new Vector2(1500, 80));
        sub.fontStyle = FontStyles.Bold;
    }

    // ---------------------------------------------------------------- helpers
    void Caption(RectTransform s, string title, string sub) { CaptionTitle(s, title); CaptionSub(s, sub); }

    TextMeshProUGUI CaptionTitle(RectTransform s, string text)
    {
        var t = Txt(s, text, 76, Color.white, new Vector2(0, -170), new Vector2(1600, 110));
        t.outlineWidth = 0.18f; t.outlineColor = new Color32(0x2A, 0x23, 0x40, 255);
        return t;
    }

    TextMeshProUGUI CaptionSub(RectTransform s, string text)
    {
        var t = Txt(s, text, 44, Color.white, new Vector2(0, -260), new Vector2(1600, 70));
        t.fontStyle = FontStyles.Normal;
        return t;
    }

    RectTransform Fill(RectTransform parent, Color c)
    {
        var r = Img(parent, null, c, Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero);
        Stretch(r); r.SetAsFirstSibling();
        return r;
    }

    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    RectTransform Img(RectTransform parent, Sprite sprite, Color color, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var g = new GameObject("Shape", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = g.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = size; r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.sprite = sprite; img.color = color; img.raycastTarget = false;
        if (sprite == rounded && sprite != null) img.type = Image.Type.Sliced;
        return r;
    }

    TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color color, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.fontStyle = FontStyles.Bold; t.color = color; t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false; t.textWrappingMode = TextWrappingModes.NoWrap;
        var r = t.rectTransform; r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = box; r.anchoredPosition = pos;
        return t;
    }
}
