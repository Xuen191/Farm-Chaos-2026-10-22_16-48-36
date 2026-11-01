// Farm Chaos - the host's FARM RULES board in the lobby (left of the party circle).
// Everyone can read it; when the HOST rolls up to it a little panel opens to change:
//   Animals: Few / Normal / Lots        Weather: Random / Always clear / Off
// Online, the host's choice is shown on everyone's board and used in the farm (PartySettings).
// Made by LobbyUI at the start of the lobby - nothing to set up.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyRulesBoard : MonoBehaviour
{
    public static readonly Vector3 Spot = new Vector3(-10.6f, 0f, 0.5f);
    const float Reach = 3.2f;

    static readonly Color Mint = new Color32(0x8C, 0xCF, 0xA4, 255), Cream = new Color32(0xFF, 0xF6, 0xEC, 245), Pink = new Color32(0xF4, 0xB8, 0xC1, 255),
        Brown = new Color32(0x6B, 0x4A, 0x3A, 255), Wood = new Color32(0xD9, 0xB9, 0x9B, 255), Board = new Color32(0xFF, 0xF0, 0xD9, 255);

    TextMeshPro boardText;
    RectTransform panel;
    TextMeshProUGUI animalsText, weatherText, panelNote;
    Button[] arrows = new Button[4];
    SpherePlayer me;
    Sprite rounded;

    public static LobbyRulesBoard Make(Transform canvas, Sprite rounded, Material lit)
    {
        var go = new GameObject("Farm_Rules_Board");
        go.transform.position = Spot;
        go.transform.rotation = Quaternion.Euler(0f, 25f, 0f);   // turned a little toward the middle
        var b = go.AddComponent<LobbyRulesBoard>();
        b.rounded = rounded;
        b.BuildBoard(lit);
        b.BuildPanel(canvas);
        return b;
    }

    void BuildBoard(Material lit)
    {
        Cube("Post_L", new Vector3(-1.05f, 0.95f, 0f), new Vector3(0.16f, 1.9f, 0.16f), Wood, lit);
        Cube("Post_R", new Vector3(1.05f, 0.95f, 0f), new Vector3(0.16f, 1.9f, 0.16f), Wood, lit);
        Cube("Board", new Vector3(0f, 1.75f, 0f), new Vector3(2.5f, 1.55f, 0.12f), Board, lit);
        Cube("Board_Top", new Vector3(0f, 2.6f, 0f), new Vector3(2.7f, 0.16f, 0.2f), Pink, lit);
        Cube("Pin_L", new Vector3(-1.0f, 2.4f, -0.08f), new Vector3(0.12f, 0.12f, 0.04f), new Color32(0xE0, 0x6C, 0x8C, 255), lit);
        Cube("Pin_R", new Vector3(1.0f, 2.4f, -0.08f), new Vector3(0.12f, 0.12f, 0.04f), Mint, lit);
        boardText = new GameObject("Rules_Text").AddComponent<TextMeshPro>();
        boardText.transform.SetParent(transform, false);
        boardText.transform.localPosition = new Vector3(0f, 1.72f, -0.075f);
        boardText.rectTransform.sizeDelta = new Vector2(2.3f, 1.4f);
        boardText.fontSize = 2.2f; boardText.alignment = TextAlignmentOptions.Center; boardText.color = Brown;
        boardText.enableAutoSizing = true; boardText.fontSizeMin = 0.5f; boardText.fontSizeMax = 2.4f;
    }

    void Cube(string n, Vector3 p, Vector3 s, Color c, Material lit)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
        g.transform.SetParent(transform, false); g.transform.localPosition = p; g.transform.localScale = s;
        var r = g.GetComponent<Renderer>();
        r.material = Paint(c);
    }

    void BuildPanel(Transform canvas)
    {
        panel = Box(canvas, Cream, new Vector2(620, 330), new Vector2(0f, 0.5f), new Vector2(340, 40));
        Txt(panel, "FARM RULES", 46, new Color32(0xE0, 0x6C, 0x8C, 255), new Vector2(0, 125), new Vector2(580, 60));
        animalsText = Row(panel, "ANIMALS", 40f, 0);
        weatherText = Row(panel, "WEATHER", -55f, 2);
        panelNote = Txt(panel, "", 24, new Color(0.42f, 0.29f, 0.23f, 0.75f), new Vector2(0, -130), new Vector2(580, 34));
        panelNote.fontStyle = FontStyles.Normal;
        panel.gameObject.SetActive(false);
    }

    TextMeshProUGUI Row(RectTransform parent, string label, float y, int arrowIndex)
    {
        var l = Txt(parent, label, 26, new Color(0.42f, 0.29f, 0.23f, 0.7f), new Vector2(0, y + 40), new Vector2(560, 30));
        l.fontStyle = FontStyles.Normal;
        var value = Txt(parent, "", 40, Brown, new Vector2(0, y), new Vector2(300, 56));
        bool animals = arrowIndex == 0;
        arrows[arrowIndex] = Btn(parent, "<", new Vector2(-210, y), () => Change(animals, -1));
        arrows[arrowIndex + 1] = Btn(parent, ">", new Vector2(210, y), () => Change(animals, +1));
        return value;
    }

    void Change(bool animals, int dir)
    {
        if (!CanEdit()) return;
        if (animals) PartySettings.Animals = (PartySettings.Animals + dir + 3) % 3;
        else PartySettings.Weather = (PartySettings.Weather + dir + 3) % 3;
        PartySettings.SaveRules();
    }

    static TMP_FontAsset symbols; static bool symbolsLooked;
    /// <summary>A plain font for symbols the cute game font doesn't have (&lt; &gt; + -).</summary>
    public static TMP_FontAsset Symbols
    {
        get { if (!symbolsLooked) { symbolsLooked = true; symbols = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF"); } return symbols; }
    }

    public static Material Paint(Color c, bool glow = false)
    {
        var m = new Material(Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        m.color = c;
        return m;
    }

    static bool CanEdit() => !NetSession.Online || Unity.Netcode.NetworkManager.Singleton.IsServer;

    void Update()
    {
        string a = PartySettings.AnimalNames[PartySettings.Animals], w = PartySettings.WeatherNames[PartySettings.Weather];
        string text = $"<b><color=#E06C8C>FARM RULES</color></b>\n<size=80%>Animals  <b>{a}</b>\nWeather  <b>{w}</b></size>";
        if (boardText.text != text) boardText.text = text;

        if (me == null) me = FindAnyObjectByType<SpherePlayer>();
        bool near = false;
        if (me != null) { Vector3 d = me.transform.position - transform.position; d.y = 0f; near = d.magnitude < Reach; }
        if (panel.gameObject.activeSelf != near) panel.gameObject.SetActive(near);
        if (!near) return;
        animalsText.text = a; weatherText.text = w;
        bool edit = CanEdit();
        foreach (var b in arrows) b.gameObject.SetActive(edit);
        panelNote.text = edit ? (NetSession.Online ? "Your friends see these too" : "Saved for next time")
                              : "Only the host can change the rules";
    }

    // ------------------------------------------------------------ ui helpers
    RectTransform Box(Transform parent, Color c, Vector2 size, Vector2 anchor, Vector2 pos)
    {
        var g = new GameObject("Rules_Panel", typeof(RectTransform), typeof(Image)); g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.anchorMin = r.anchorMax = anchor; r.sizeDelta = size; r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.color = c; img.raycastTarget = true;
        if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
        var sh = g.AddComponent<Shadow>(); sh.effectColor = new Color(0.42f, 0.29f, 0.23f, 0.25f); sh.effectDistance = new Vector2(0, -5);
        return r;
    }

    Button Btn(RectTransform parent, string label, Vector2 pos, UnityEngine.Events.UnityAction click)
    {
        var g = new GameObject("Arrow_" + label, typeof(RectTransform), typeof(Image), typeof(Button)); g.transform.SetParent(parent, false);
        var r = (RectTransform)g.transform; r.sizeDelta = new Vector2(90, 70); r.anchoredPosition = pos;
        var img = g.GetComponent<Image>(); img.color = Pink; if (rounded != null) { img.sprite = rounded; img.type = Image.Type.Sliced; }
        var t = Txt(r, label, 48, Brown, Vector2.zero, r.sizeDelta);
        if (Symbols != null) t.font = Symbols;                      // the game font has no < > arrows
        var b = g.GetComponent<Button>(); b.onClick.AddListener(click);
        return b;
    }

    TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color c, Vector2 pos, Vector2 box)
    {
        var g = new GameObject("Text", typeof(RectTransform)); g.transform.SetParent(parent, false);
        var t = g.AddComponent<TextMeshProUGUI>();
        t.text = text; t.fontSize = size; t.color = c; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false;
        t.rectTransform.sizeDelta = box; t.rectTransform.anchoredPosition = pos;
        return t;
    }
}
