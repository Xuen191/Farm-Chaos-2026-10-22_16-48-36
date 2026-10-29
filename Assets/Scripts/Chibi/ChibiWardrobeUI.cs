// Farm Chaos - simple wardrobe in the lobby.
// Tap "Customize": the camera zooms in on your chibi, pick a part on the right (Hair, Hat, Outfit...),
// then swipe left/right (or tap the arrows) to try options. "Done" saves it for every game.
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChibiWardrobeUI : MonoBehaviour
{
    public Sprite rounded;

    class Cat
    {
        public string name; public int count; public float focus, dist;
        public Func<ChibiLook.Data, int> get; public Action<ChibiLook.Data, int> set;
        public Func<int, string> label; public Func<int, Color?> swatch;
    }

    ChibiLook look; ChibiMotor motor; SpherePlayer player; CameraFollow follow; Camera cam;
    RectTransform root; TextMeshProUGUI optionText, countText; Image swatchImg;
    readonly List<Cat> cats = new List<Cat>();
    readonly List<Image> tabImgs = new List<Image>();
    readonly List<(GameObject go, bool was)> hidden = new List<(GameObject, bool)>();
    Canvas[] otherCanvases;
    int cat;
    bool open;

    static readonly Color Cream = new Color32(0xFF, 0xF6, 0xEC, 245), Brown = new Color32(0x6B, 0x4A, 0x3A, 255),
        Pink = new Color32(0xF4, 0xB8, 0xC1, 255), Mint = new Color32(0x8C, 0xCF, 0xA4, 255), TabOff = new Color32(0xFF, 0xFF, 0xFF, 230);

    void Start()
    {
        var b = Btn(transform, "Customize", Pink, new Vector2(320, 84), new Vector2(0f, 1f), new Vector2(200, -170), 34);
        b.onClick.AddListener(Open);
        string Pretty(string s) => s == "FlowerCrown" ? "Flower Crown" : s == "Straw" ? "Straw Hat" : s == "Chicken" ? "Chicken!" : s;
        Cat C(string n, int count, float focus, float dist, Func<ChibiLook.Data, int> g, Action<ChibiLook.Data, int> s, Func<int, string> l, Func<int, Color?> sw = null)
            => new Cat { name = n, count = count, focus = focus, dist = dist, get = g, set = s, label = l, swatch = sw };
        cats.Add(C("Hair", ChibiLook.HairStyles.Length, 0.95f, 1.55f, d => d.hairStyle, (d, i) => d.hairStyle = i, i => ChibiLook.HairStyles[i]));
        cats.Add(C("Hair Colour", ChibiLook.HairColors.Length, 0.95f, 1.55f, d => d.hairColor, (d, i) => d.hairColor = i, i => "Colour", i => ChibiLook.HairColors[i]));
        cats.Add(C("Hat", ChibiLook.Hats.Length, 1.05f, 1.75f, d => d.hat, (d, i) => d.hat = i, i => Pretty(ChibiLook.Hats[i])));
        cats.Add(C("Hat Colour", ChibiLook.Clothes.Length, 1.05f, 1.75f, d => d.hatColor, (d, i) => d.hatColor = i, i => "Colour", i => ChibiLook.Clothes[i]));
        cats.Add(C("Face", 9, 0.92f, 1.35f, d => d.face, (d, i) => d.face = i, i => ((ChibiLook.Face)i).ToString()));
        cats.Add(C("Overalls", ChibiLook.Clothes.Length, 0.55f, 2.1f, d => d.outfit, (d, i) => d.outfit = i, i => "Colour", i => ChibiLook.Clothes[i]));
        cats.Add(C("Shirt", ChibiLook.Shirts.Length, 0.6f, 2.1f, d => d.shirt, (d, i) => d.shirt = i, i => "Colour", i => ChibiLook.Shirts[i]));
        cats.Add(C("Socks", ChibiLook.Shirts.Length, 0.25f, 1.7f, d => d.socks, (d, i) => d.socks = i, i => "Colour", i => ChibiLook.Shirts[i]));
        cats.Add(C("Shoes", ChibiLook.ShoeColors.Length, 0.22f, 1.7f, d => d.shoes, (d, i) => d.shoes = i, i => "Colour", i => ChibiLook.ShoeColors[i]));
        cats.Add(C("Skin", ChibiLook.SkinTones.Length, 0.75f, 2.0f, d => d.skin, (d, i) => d.skin = i, i => "Tone", i => ChibiLook.SkinTones[i]));
    }

    // ------------------------------------------------------------ open / close
    void Open()
    {
        foreach (var m in FindObjectsByType<ChibiMotor>(FindObjectsSortMode.None))
            if (m.transform.parent != null && m.transform.parent.GetComponent<SpherePlayer>() != null)
            { motor = m; look = m.look; player = m.transform.parent.GetComponent<SpherePlayer>(); }
        if (look == null || open) return;
        open = true;
        cam = Camera.main;
        follow = cam != null ? cam.GetComponent<CameraFollow>() : null;
        if (follow != null) follow.enabled = false;
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; rb.isKinematic = true; }   // stay put (the lobby re-enables input)
            player.enabled = false;
        }
        motor.posing = true;

        // lock the view direction ONCE, so the camera (and the background) never orbits while you dress up
        if (cam != null)
        {
            camPos0 = cam.transform.position; camRot0 = cam.transform.rotation;
            lockDir = cam.transform.position - motor.transform.position; lockDir.y = 0f;
            if (lockDir.sqrMagnitude < 0.01f) lockDir = -motor.transform.forward;
            lockDir.Normalize();
        }

        // hide the rest of the lobby UI while dressing up
        hidden.Clear();
        for (int i = 0; i < transform.childCount; i++)
        {
            var c = transform.GetChild(i).gameObject;
            if (root != null && c == root.gameObject) continue;
            hidden.Add((c, c.activeSelf)); c.SetActive(false);
        }
        var mine = GetComponentInParent<Canvas>();
        otherCanvases = Array.FindAll(FindObjectsByType<Canvas>(FindObjectsSortMode.None), c => c.isRootCanvas && c != mine && c.enabled);
        foreach (var c in otherCanvases) c.enabled = false;

        if (root == null) Build();
        root.gameObject.SetActive(true);
        Select(0);
    }

    void Close()
    {
        if (!open) return;
        open = false;
        look.Save();
        root.gameObject.SetActive(false);
        foreach (var (go, was) in hidden) if (go != null) go.SetActive(was);
        if (otherCanvases != null) foreach (var c in otherCanvases) if (c != null) c.enabled = true;
        if (cam != null && follow == null) cam.transform.SetPositionAndRotation(camPos0, camRot0);
        if (follow != null) follow.enabled = true;
        if (player != null)
        {
            var rb = player.GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = false;
            player.enabled = true;
        }
        if (motor != null) motor.posing = false;
        look.SetExpression(ChibiLook.Face.Happy, 1.2f);
    }

    // ------------------------------------------------------------ camera
    Vector3 lockDir = Vector3.back, camPos0; Quaternion camRot0;

    void LateUpdate()
    {
        if (!open || cam == null || motor == null) return;
        var c = cats[cat];
        Vector3 feet = motor.transform.position;
        Vector3 flat = lockDir;   // fixed when the wardrobe opened - no drifting/spinning
        Vector3 right = Vector3.Cross(Vector3.up, -flat).normalized;
        Vector3 focus = feet + Vector3.up * c.focus + right * (c.dist * 0.15f);   // chibi sits left of centre, menu on the right
        Vector3 pos = focus + flat * c.dist + Vector3.up * c.dist * 0.18f;
        float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
        cam.transform.position = Vector3.Lerp(cam.transform.position, pos, k);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(focus - cam.transform.position), k);
    }

    // ------------------------------------------------------------ choosing
    void Select(int i)
    {
        cat = i;
        for (int t = 0; t < tabImgs.Count; t++) tabImgs[t].color = t == cat ? Mint : TabOff;
        ShowOption();
    }

    void Step(int dir)
    {
        var c = cats[cat];
        int v = ((c.get(look.data) + dir) % c.count + c.count) % c.count;
        c.set(look.data, v);
        if (c.name == "Face") look.SetExpression((ChibiLook.Face)v, 0f);
        look.Apply();
        if (c.name != "Face") look.SetExpression(ChibiLook.Face.Happy, 0.7f);
        motor.Hop();
        ShowOption();
    }

    void ShowOption()
    {
        var c = cats[cat];
        int v = c.get(look.data);
        optionText.text = c.label(v);
        countText.text = $"{c.name}  ·  {v + 1}/{c.count}";
        var sw = c.swatch?.Invoke(v);
        swatchImg.gameObject.SetActive(sw.HasValue);
        if (sw.HasValue) swatchImg.color = sw.Value;
        optionText.rectTransform.anchoredPosition = new Vector2(sw.HasValue ? 30 : 0, 8);
    }

    // ------------------------------------------------------------ build
    void Build()
    {
        root = new GameObject("Wardrobe_UI", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(transform, false); Stretch(root);

        // swipe anywhere
        var swipe = new GameObject("Swipe_Area", typeof(RectTransform), typeof(Image), typeof(SwipeCatcher));
        swipe.transform.SetParent(root, false); Stretch((RectTransform)swipe.transform);
        swipe.GetComponent<Image>().color = new Color(0, 0, 0, 0.001f);
        swipe.GetComponent<SwipeCatcher>().Swiped += d => Step(d);

        var title = Box(root, Cream, new Vector2(380, 80), new Vector2(0.5f, 1f), new Vector2(-180, -70));
        Txt(title, "My Farmer", 44, new Color32(0xE0, 0x6C, 0x8C, 255), Vector2.zero, new Vector2(360, 70));

        // part tabs down the right side
        var panel = Box(root, Cream, new Vector2(300, 140 + cats.Count * 78), new Vector2(1f, 0.5f), new Vector2(-190, 20));
        float top = (cats.Count - 1) * 39f;
        for (int i = 0; i < cats.Count; i++)
        {
            int k = i;
            var t = Btn(panel, cats[i].name, TabOff, new Vector2(250, 66), new Vector2(0.5f, 0.5f), new Vector2(0, top - i * 78), 28);
            t.onClick.AddListener(() => Select(k));
            tabImgs.Add(t.GetComponent<Image>());
        }

        // option picker at the bottom: < [ option ] >
        var bar = Box(root, Cream, new Vector2(520, 120), new Vector2(0.5f, 0f), new Vector2(-180, 120));
        optionText = Txt(bar, "", 44, Brown, new Vector2(0, 8), new Vector2(420, 60));
        countText = Txt(bar, "", 24, new Color(0.42f, 0.29f, 0.23f, 0.7f), new Vector2(0, -36), new Vector2(420, 34));
        countText.fontStyle = FontStyles.Normal;
        swatchImg = Box(bar, Color.white, new Vector2(46, 46), new Vector2(0.5f, 0.5f), new Vector2(-95, 8)).GetComponent<Image>();
        var prev = Btn(root, "<", Pink, new Vector2(120, 120), new Vector2(0.5f, 0f), new Vector2(-180 - 340, 120), 64);
        prev.onClick.AddListener(() => Step(-1));
        var next = Btn(root, ">", Pink, new Vector2(120, 120), new Vector2(0.5f, 0f), new Vector2(-180 + 340, 120), 64);
        next.onClick.AddListener(() => Step(1));
        var hint = Txt(root, "Swipe left / right to try things on", 26, Color.white, new Vector2(-180, 210), new Vector2(700, 40));
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        hint.outlineWidth = 0.2f; hint.outlineColor = new Color32(0x6B, 0x4A, 0x3A, 255); hint.fontStyle = FontStyles.Normal;

        var done = Btn(root, "Done", Mint, new Vector2(240, 96), new Vector2(1f, 0f), new Vector2(-190, 120), 40);
        done.onClick.AddListener(Close);
    }

    // ------------------------------------------------------------ ui helpers
    static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

    RectTransform Box(Transform parent, Color c, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var g = new GameObject("Box", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.color = c; img.raycastTarget = false;
        if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 60f / Mathf.Clamp(Mathf.Min(size.x, size.y) * 0.45f, 8f, 44f); }
        var sh = g.AddComponent<Shadow>(); sh.effectColor = new Color(0.42f, 0.29f, 0.23f, 0.25f); sh.effectDistance = new Vector2(0, -5);
        return r;
    }

    Button Btn(Transform parent, string label, Color c, Vector2 size, Vector2 anchor, Vector2 pos, float font)
    {
        var r = Box(parent, c, size, anchor, pos);
        r.GetComponent<Image>().raycastTarget = true;
        Txt(r, label, font, Brown, Vector2.zero, size);
        var b = r.gameObject.AddComponent<Button>();
        var cb = b.colors; cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f); cb.pressedColor = new Color(0.82f, 0.82f, 0.82f); b.colors = cb;
        return b;
    }

    TextMeshProUGUI Txt(Transform parent, string text, float size, Color c, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        var r = t.rectTransform; r.sizeDelta = box; r.anchoredPosition = pos;
        return t;
    }
}

/// <summary>Turns a horizontal swipe (finger or mouse drag) into +1 / -1.</summary>
public class SwipeCatcher : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public event Action<int> Swiped;
    Vector2 start;
    public void OnBeginDrag(PointerEventData e) { start = e.position; }
    public void OnDrag(PointerEventData e) { }
    public void OnEndDrag(PointerEventData e)
    {
        float dx = e.position.x - start.x;
        if (Mathf.Abs(dx) > Screen.width * 0.05f) Swiped?.Invoke(dx < 0 ? 1 : -1);
    }
}
