// Farm Chaos - the chibi's look: colours, hairstyle, hat and face expression.
// Your choices are saved on this device (PlayerPrefs) and loaded in every scene.
//   look.SetExpression(ChibiLook.Face.Surprised, 2f)   -> a reaction for 2 seconds, then back to your default face
using System;
using System.Collections.Generic;
using UnityEngine;

public class ChibiLook : MonoBehaviour
{
    public enum Face { Smile, Happy, Surprised, Angry, Sad, Sleepy, Wink, Determined, Love }

    [Serializable]
    public class Data
    {
        public int skin = 0, shirt = 0, outfit = 0, shoes = 0, hairColor = 0, hat = 0, hairStyle = 1, hatColor = 0, socks = 0;
        public int face = 0;   // default expression
    }

    // palettes (pastel + cozy)
    public static readonly Color[] SkinTones = { Hex("FFE3D3"), Hex("F6C6A4"), Hex("E0A47E"), Hex("B97A56"), Hex("8A5A3C"), Hex("FFE6DA") };
    public static readonly Color[] Clothes = { Hex("8EC5F0"), Hex("F4B8C1"), Hex("9BDEA8"), Hex("FFD27A"), Hex("C3A6F2"), Hex("FF9E7A"), Hex("FFF0D9"), Hex("6B6F8A") };
    public static readonly Color[] HairColors = { Hex("7A5240"), Hex("3A2E2A"), Hex("E8C27A"), Hex("F28FB1"), Hex("8EC5F0"), Hex("B98AE8"), Hex("E0603E"), Hex("F2F0EA") };
    public static readonly Color[] Shirts = { Hex("FFFFFF"), Hex("FFF0D9"), Hex("FDE2E8"), Hex("E2F1FF"), Hex("E6F7E4"), Hex("FFF6C9"), Hex("EDE4FF") };
    public static readonly Color[] ShoeColors = { Hex("5A3A33"), Hex("A87B5C"), Hex("E07A8E"), Hex("3E4A6B"), Hex("F2F0EA"), Hex("D9A441") };
    public static readonly string[] HairStyles = { "None", "Bob", "Spiky", "Pigtails", "Long" };
    public static readonly string[] Hats = { "None", "Straw", "Cap", "Chicken", "FlowerCrown", "Beret" };

    [Header("Set up by Tools > Farm Chaos > 22. Build Chibi Player")]
    public Renderer[] renderers;
    public Texture2D[] faces = new Texture2D[9];   // in Face enum order
    public bool loadSaved = true;

    public Data data = new Data();
    public event Action Changed;

    const string Key = "FarmChaos.ChibiLook";
    Face current; float faceUntil = -1f;
    MaterialPropertyBlock mpb;
    readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    void Awake()
    {
        foreach (var t in GetComponentsInChildren<Transform>(true)) if (!parts.ContainsKey(t.name)) parts[t.name] = t;
        if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
        if (loadSaved) Load();
        Apply();
    }

    void Update()
    {
        if (faceUntil > 0f && Time.time > faceUntil) { faceUntil = -1f; ShowFace((Face)data.face); }
    }

    // ------------------------------------------------------------ save / load
    public void Load()
    {
        var json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json)) { try { JsonUtility.FromJsonOverwrite(json, data); } catch { } }
    }

    public void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(data)); PlayerPrefs.Save(); }

    public static Data LoadSaved()
    {
        var d = new Data();
        var json = PlayerPrefs.GetString(Key, "");
        if (!string.IsNullOrEmpty(json)) { try { JsonUtility.FromJsonOverwrite(json, d); } catch { } }
        return d;
    }

    // ------------------------------------------------------------ apply
    public void Apply()
    {
        mpb ??= new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                Color? c = SlotColor(mats[i].name);
                if (c == null) continue;
                r.GetPropertyBlock(mpb, i);
                mpb.SetColor("_BaseColor", c.Value);
                r.SetPropertyBlock(mpb, i);
            }
        }
        for (int i = 1; i < HairStyles.Length; i++) SetActive("Hair_" + HairStyles[i], data.hairStyle == i);
        for (int i = 1; i < Hats.Length; i++) SetActive("Hat_" + Hats[i], data.hat == i);
        ShowFace(faceUntil > 0f ? current : (Face)data.face);
        Changed?.Invoke();
    }

    Color? SlotColor(string matName)
    {
        string n = matName.Replace(" (Instance)", "");
        if (n.EndsWith("HatMain")) return data.hat == 1 ? Hex("E9CF7A") : Clothes[Wrap(data.hatColor + 1, Clothes.Length)];
        if (n.EndsWith("HatAccent")) return Clothes[Wrap(data.hatColor, Clothes.Length)];
        if (n.EndsWith("Skin")) return SkinTones[Wrap(data.skin, SkinTones.Length)];
        if (n.EndsWith("Shirt")) return Shirts[Wrap(data.shirt, Shirts.Length)];
        if (n.EndsWith("Outfit")) return Clothes[Wrap(data.outfit, Clothes.Length)];
        if (n.EndsWith("Shoes")) return ShoeColors[Wrap(data.shoes, ShoeColors.Length)];
        if (n.EndsWith("Socks")) return Shirts[Wrap(data.socks, Shirts.Length)];
        if (n.EndsWith("Hair")) return HairColors[Wrap(data.hairColor, HairColors.Length)];
        if (n.EndsWith("Accent")) return Clothes[Wrap(data.outfit + 1, Clothes.Length)];
        return null;
    }

    static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
    static int Wrap(int i, int n) => ((i % n) + n) % n;

    void SetActive(string name, bool on) { if (parts.TryGetValue(name, out var t) && t.gameObject.activeSelf != on) t.gameObject.SetActive(on); }

    // ------------------------------------------------------------ faces
    /// <summary>Show a reaction face for a few seconds (0 = keep it).</summary>
    public void SetExpression(Face f, float seconds = 2f)
    {
        faceUntil = seconds > 0f ? Time.time + seconds : -1f;
        if (seconds <= 0f) data.face = (int)f;
        ShowFace(f);
    }

    void ShowFace(Face f)
    {
        current = f;
        var tex = faces != null && (int)f < faces.Length ? faces[(int)f] : null;
        if (tex == null) return;
        mpb ??= new MaterialPropertyBlock();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.Replace(" (Instance)", "").EndsWith("Face")) continue;
                r.GetPropertyBlock(mpb, i);
                mpb.SetTexture("_BaseMap", tex);
                r.SetPropertyBlock(mpb, i);
            }
        }
    }
}
