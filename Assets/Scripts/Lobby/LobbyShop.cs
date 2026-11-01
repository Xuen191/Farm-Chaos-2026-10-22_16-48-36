// Farm Chaos - the FARM SHOP in the lobby's farmhouse. Roll up to the front door to open it.
//   BEAN tab: special colours for your sweater, hat and hair (then pick them in Customize)
//   DECO tab: decorations for the lobby's sparkly spots (LobbyDeco)
// Paid with paper money (Bank) - you earn it at sunrise from the coins you got that night.
// Your paper money also shows in the top corner of the lobby.
// Made by LobbyUI at the start of the lobby - nothing to set up.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyShop : MonoBehaviour
{
    public static readonly Vector3 Door = new Vector3(0f, 0f, 12.4f);
    const float Enter = 2.0f, Rearm = 3.2f;
    public static bool IsOpen { get; private set; }

    RectTransform panel, grid;
    TextMeshProUGUI money, hudMoney, note;
    Image tabBean, tabDeco;
    bool decoTab, armed = true;
    SpherePlayer me;
    float noteUntil;

    public static LobbyShop Make(Transform canvas)
    {
        var s = new GameObject("Farm_Shop").AddComponent<LobbyShop>();
        s.Build(canvas);
        s.BuildSign();
        return s;
    }

    void BuildSign()
    {
        // a hanging SHOP sign + a glowing mat in front of the farmhouse door
        var root = new GameObject("Shop_Door").transform; root.position = Door;
        LobbyKit.Prim(PrimitiveType.Cylinder, root, "Door_Mat", new Vector3(0f, 0.04f, 0f), new Vector3(2.6f, 0.02f, 1.6f), LobbyKit.Mint);
        LobbyKit.Prim(PrimitiveType.Cylinder, root, "Post_L", new Vector3(-1.6f, 1.3f, 0.6f), new Vector3(0.12f, 1.3f, 0.12f), new Color32(0xA8, 0x7B, 0x5C, 255), true);
        LobbyKit.Prim(PrimitiveType.Cylinder, root, "Post_R", new Vector3(1.6f, 1.3f, 0.6f), new Vector3(0.12f, 1.3f, 0.12f), new Color32(0xA8, 0x7B, 0x5C, 255), true);
        LobbyKit.Prim(PrimitiveType.Cube, root, "Bar", new Vector3(0f, 2.6f, 0.6f), new Vector3(3.4f, 0.12f, 0.12f), new Color32(0xA8, 0x7B, 0x5C, 255));
        LobbyKit.Prim(PrimitiveType.Cube, root, "Sign", new Vector3(0f, 2.15f, 0.6f), new Vector3(2.0f, 0.65f, 0.1f), LobbyKit.Pink);
        var t = new GameObject("Shop_Text").AddComponent<TextMeshPro>();
        t.transform.SetParent(root, false); t.transform.localPosition = new Vector3(0f, 2.15f, 0.54f);
        t.text = "FARM SHOP"; t.fontSize = 3f; t.color = Color.white; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center;
        t.rectTransform.sizeDelta = new Vector2(1.9f, 0.6f); t.enableAutoSizing = true; t.fontSizeMin = 0.5f; t.fontSizeMax = 3f;
        t.outlineWidth = 0.2f; t.outlineColor = LobbyKit.Berry;
    }

    void Build(Transform canvas)
    {
        hudMoney = LobbyKit.MoneyTag(canvas, new Vector2(1f, 1f), new Vector2(-135, -55), 0.85f);   // tucked into the top-right corner

        panel = LobbyKit.Box(canvas, new Color(0.1f, 0.08f, 0.18f, 0.55f), Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, true);
        panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.sizeDelta = Vector2.zero; panel.name = "Shop_Panel";
        if (panel.GetComponent<Image>() != null) panel.GetComponent<Image>().sprite = null;
        var card = LobbyKit.Box(panel, LobbyKit.Cream, new Vector2(1500, 900), new Vector2(0.5f, 0.5f), new Vector2(0, -10), true);
        LobbyKit.Txt(card, "FARM SHOP", 72, LobbyKit.Berry, new Vector2(0, 385), new Vector2(800, 90));
        money = LobbyKit.MoneyTag(card, new Vector2(1f, 1f), new Vector2(-170, -70));
        var bean = LobbyKit.Btn(card, "BEAN", LobbyKit.Mint, new Vector2(240, 80), new Vector2(-140, 290), 40, () => Tab(false));
        var deco = LobbyKit.Btn(card, "DECO", LobbyKit.White, new Vector2(240, 80), new Vector2(140, 290), 40, () => Tab(true));
        tabBean = bean.GetComponent<Image>(); tabDeco = deco.GetComponent<Image>();
        grid = new GameObject("Items", typeof(RectTransform)).GetComponent<RectTransform>();
        grid.SetParent(card, false); grid.sizeDelta = new Vector2(1400, 560); grid.anchoredPosition = new Vector2(0, -20);
        note = LobbyKit.Txt(card, "", 30, LobbyKit.Brown, new Vector2(0, -360), new Vector2(1100, 44));
        note.fontStyle = FontStyles.Normal;
        LobbyKit.Btn(card, "BYE!", LobbyKit.Pink, new Vector2(220, 84), new Vector2(560, -370), 40, Close);
        panel.gameObject.SetActive(false);
        Bank.Changed += Refresh;
        Refresh();
    }

    void OnDestroy() { Bank.Changed -= Refresh; IsOpen = false; }

    void Tab(bool deco) { decoTab = deco; Refresh(); }

    void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (me != null)
        {
            me.enabled = false;
            var rb = me.GetComponent<Rigidbody>(); if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        panel.gameObject.SetActive(true);
        panel.SetAsLastSibling();
        Say(Bank.Money < 25 ? "Earn paper money at sunrise... every coin you get in a night counts!" : "Welcome! What would you like?");
        Refresh();
    }

    void Close()
    {
        IsOpen = false;
        panel.gameObject.SetActive(false);
        if (me != null) me.enabled = true;
    }

    void Say(string s) { note.text = s; noteUntil = Time.unscaledTime + 3f; }

    void Refresh()
    {
        string m = Bank.Money.ToString();
        if (hudMoney != null) hudMoney.text = m;
        if (money != null) money.text = m;
        if (grid == null || !panel.gameObject.activeSelf) return;
        tabBean.color = decoTab ? LobbyKit.White : LobbyKit.Mint;
        tabDeco.color = decoTab ? LobbyKit.Mint : LobbyKit.White;
        for (int i = grid.childCount - 1; i >= 0; i--) Destroy(grid.GetChild(i).gameObject);
        var items = decoTab ? ShopCatalog.Decos : ShopCatalog.Colours;
        int cols = 5;
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            int r = i / cols, c = i % cols;
            var slot = LobbyKit.Box(grid, LobbyKit.White, new Vector2(255, 260), new Vector2(0.5f, 0.5f), new Vector2((c - 2) * 272f, 140 - r * 280f));
            var pic = LobbyKit.Box(slot, it.colour, new Vector2(110, 110), new Vector2(0.5f, 0.5f), new Vector2(0, 55));
            if (it.deco) LobbyKit.Txt(pic, DecoIcon(it.id), 46, Color.white, Vector2.zero, new Vector2(110, 110));
            LobbyKit.Txt(slot, it.name, 28, LobbyKit.Brown, new Vector2(0, -20), new Vector2(240, 40));
            bool owned = Bank.Owns(it.id);
            var item = it;
            var b = LobbyKit.Btn(slot, owned ? "OWNED" : "BUY " + it.price, owned ? LobbyKit.Grey : (Bank.Money >= it.price ? LobbyKit.Mint : LobbyKit.Pink),
                new Vector2(200, 64), new Vector2(0, -85), 30, () => Buy(item));
            b.interactable = !owned;
        }
    }

    // a letter "icon" on each decoration's picture (the game font has no emoji)
    static string DecoIcon(string id) => id.Substring(0, 1).ToUpperInvariant();

    void Buy(ShopCatalog.Item it)
    {
        if (Bank.Owns(it.id)) return;
        if (Bank.Money < it.price) { Say($"Not enough paper money... {it.name} costs {it.price}"); return; }
        if (Bank.Buy(it.id, it.price))
            Say(it.deco ? $"{it.name} bought! Roll onto a sparkly spot in the lobby to place it"
                        : $"{it.name} bought! Find it in Customize for your sweater hat or hair");
    }

    void Update()
    {
        if (note != null && noteUntil > 0f && Time.unscaledTime > noteUntil) { note.text = ""; noteUntil = 0f; }
#if UNITY_EDITOR
        bool f7 = false;
#if ENABLE_INPUT_SYSTEM
        f7 |= UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f7Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        f7 |= Input.GetKeyDown(KeyCode.F7);
#endif
        if (f7) Bank.Add(100);                                  // testing: +100 paper money
#endif
        if (IsOpen && LobbyKit.EscPressed()) { Close(); return; }
        if (me == null) me = FindAnyObjectByType<SpherePlayer>();
        if (me == null || IsOpen) return;
        float d = LobbyKit.FlatDistance(me.transform.position, Door);
        if (d > Rearm) armed = true;
        if (armed && d < Enter && !LobbyKit.BusyPanel && !ChibiMotor.Typing()) { armed = false; Open(); }
    }
}
