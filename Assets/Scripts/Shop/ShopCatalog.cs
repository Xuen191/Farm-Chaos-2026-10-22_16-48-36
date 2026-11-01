// Farm Chaos - everything the lobby shop sells (prices in paper money).
//   BEAN: special colours (sweater / hat colour / hair colour) - the palettes' extra colours at the end of ChibiLook's lists
//   DECO: decorations you place on the lobby's sparkly spots (built by LobbyDeco)
using UnityEngine;

public static class ShopCatalog
{
    public struct Item { public string id, name; public int price; public Color colour; public bool deco; }

    // must match the order of the extra colours added to ChibiLook.Clothes / HairColors
    public static readonly Item[] Colours =
    {
        new Item { id = "col0", name = "Gold",           price = 60, colour = new Color32(0xF2, 0xC9, 0x4C, 255) },
        new Item { id = "col1", name = "Midnight",       price = 40, colour = new Color32(0x3B, 0x3F, 0x6B, 255) },
        new Item { id = "col2", name = "Lavender Dream", price = 40, colour = new Color32(0xD9, 0xB8, 0xF5, 255) },
        new Item { id = "col3", name = "Strawberry",     price = 40, colour = new Color32(0xFF, 0x7A, 0x9A, 255) },
        new Item { id = "col4", name = "Cocoa",          price = 30, colour = new Color32(0x8B, 0x5E, 0x4A, 255) },
        new Item { id = "col5", name = "Mint Choc",      price = 30, colour = new Color32(0x7F, 0xD1, 0xB9, 255) },
    };

    public static readonly Item[] Decos =
    {
        new Item { id = "flowers",  name = "Flower Bed",      price = 25,  colour = new Color32(0xF4, 0x9A, 0xB0, 255), deco = true },
        new Item { id = "picnic",   name = "Picnic Blanket",  price = 30,  colour = new Color32(0xE0, 0x4C, 0x5C, 255), deco = true },
        new Item { id = "balloons", name = "Balloon Bunch",   price = 35,  colour = new Color32(0x8E, 0xC5, 0xF0, 255), deco = true },
        new Item { id = "lanterns", name = "Lanterns",        price = 40,  colour = new Color32(0xFF, 0xD2, 0x7A, 255), deco = true },
        new Item { id = "mushroom", name = "Giant Mushroom",  price = 45,  colour = new Color32(0xE0, 0x6C, 0x8C, 255), deco = true },
        new Item { id = "scarecrow",name = "Scarecrow",       price = 50,  colour = new Color32(0xD6, 0xB7, 0x5A, 255), deco = true },
        new Item { id = "swing",    name = "Tree Swing",      price = 60,  colour = new Color32(0xA8, 0x7B, 0x5C, 255), deco = true },
        new Item { id = "arch",     name = "Fairy Light Arch",price = 70,  colour = new Color32(0xC3, 0xA6, 0xF2, 255), deco = true },
        new Item { id = "pond",     name = "Duck Pond",       price = 80,  colour = new Color32(0x86, 0xC3, 0xFA, 255), deco = true },
        new Item { id = "fountain", name = "Mini Fountain",   price = 100, colour = new Color32(0xC9, 0xDD, 0xF5, 255), deco = true },
    };

    public static Item? Find(string id)
    {
        foreach (var i in Colours) if (i.id == id) return i;
        foreach (var i in Decos) if (i.id == id) return i;
        return null;
    }

    /// <summary>Wardrobe: palette index -> is it free or owned? (the first 8 are always free)</summary>
    public static bool ColourAllowed(int paletteIndex)
    {
        int extra = paletteIndex - ChibiLook.FreeColours;
        return extra < 0 || extra >= Colours.Length || Bank.Owns(Colours[extra].id);
    }
}
