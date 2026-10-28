// Farm Chaos - the player's pockets: 4 slots, items of the same kind stack.
//   Inventory.Instance.Add("Coffee", icon);   Inventory.Instance.CanAdd("Coffee");
//   Inventory.Instance.RemoveOne(slotIndex);
using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [Serializable]
    public class Slot
    {
        public string item;      // null/empty = free
        public Sprite icon;
        public int count;
        public bool IsEmpty => string.IsNullOrEmpty(item) || count <= 0;
    }

    public const int SlotCount = 4;
    public Slot[] slots = new Slot[SlotCount];

    public static Inventory Instance { get; private set; }
    /// <summary>slot index that changed (-1 = several)</summary>
    public event Action<int> Changed;
    public event Action<int> Added;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < SlotCount; i++) slots[i] = new Slot();   // empty pockets each round
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    int Find(string item)
    {
        for (int i = 0; i < SlotCount; i++) if (!slots[i].IsEmpty && slots[i].item == item) return i;
        return -1;
    }

    int FirstFree()
    {
        for (int i = 0; i < SlotCount; i++) if (slots[i].IsEmpty) return i;
        return -1;
    }

    public bool CanAdd(string item) => Find(item) >= 0 || FirstFree() >= 0;

    public bool Add(string item, Sprite icon)
    {
        int i = Find(item);
        if (i < 0) i = FirstFree();
        if (i < 0) return false;
        var s = slots[i];
        if (s.IsEmpty) { s.item = item; s.icon = icon; s.count = 0; }
        s.count++;
        if (s.icon == null) s.icon = icon;
        Changed?.Invoke(i);
        Added?.Invoke(i);
        return true;
    }

    public void RemoveOne(int index)
    {
        if (index < 0 || index >= SlotCount || slots[index].IsEmpty) return;
        var s = slots[index];
        s.count--;
        if (s.count <= 0) { s.item = null; s.icon = null; s.count = 0; }
        Changed?.Invoke(index);
    }
}
