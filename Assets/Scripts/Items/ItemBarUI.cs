// Farm Chaos - item bar at the bottom of the screen.
// Tap a slot (phone) or press 1-4 (PC) to use that item. A gold badge shows when you carry the fuse.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ItemBarUI : MonoBehaviour
{
    [System.Serializable]
    public class SlotView
    {
        public RectTransform root;
        public Button button;
        public Image icon;
        public TMP_Text count;
    }

    public SlotView[] slots = new SlotView[Inventory.SlotCount];
    public RectTransform fuseBadge;
    public TMP_Text toast;

    float[] bounce = new float[Inventory.SlotCount];
    float toastTimer;
    Inventory inv;

    void Start()
    {
        inv = Inventory.Instance;
        for (int i = 0; i < slots.Length; i++)
        {
            int idx = i;
            if (slots[i]?.button != null) slots[i].button.onClick.AddListener(() => UseSlot(idx));
        }
        if (inv != null)
        {
            inv.Changed += _ => Refresh();
            inv.Added += i => { if (i >= 0 && i < bounce.Length) { bounce[i] = 1f; Toast("Got " + inv.slots[i].item + "!"); } };
        }
        if (toast != null) toast.text = "";
        Refresh();
    }

    void Update()
    {
        if (inv == null) { inv = Inventory.Instance; if (inv != null) { inv.Changed += _ => Refresh(); Refresh(); } }

        // keys 1-4
        if (!ProximityInteractor.UIOpen)
            for (int i = 0; i < slots.Length; i++)
                if (KeyDown(i)) UseSlot(i);

        for (int i = 0; i < slots.Length; i++)
        {
            bounce[i] = Mathf.MoveTowards(bounce[i], 0f, Time.unscaledDeltaTime * 3f);
            if (slots[i]?.root != null) slots[i].root.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(bounce[i] * Mathf.PI));
        }

        if (fuseBadge != null && fuseBadge.gameObject.activeSelf != FusePickup.playerHasFuse)
            fuseBadge.gameObject.SetActive(FusePickup.playerHasFuse);

        if (toast != null && toastTimer > 0f)
        {
            toastTimer -= Time.unscaledDeltaTime;
            var c = toast.color; c.a = Mathf.Clamp01(toastTimer / 0.4f); toast.color = c;
        }
    }

    static bool KeyDown(int i)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            var key = i == 0 ? kb.digit1Key : i == 1 ? kb.digit2Key : i == 2 ? kb.digit3Key : kb.digit4Key;
            if (key.wasPressedThisFrame) return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Alpha1 + i)) return true;
#endif
        return false;
    }

    void Refresh()
    {
        if (inv == null) return;
        for (int i = 0; i < slots.Length; i++)
        {
            var v = slots[i]; if (v == null) continue;
            var s = inv.slots[i];
            bool has = s != null && !s.IsEmpty;
            if (v.icon != null) { v.icon.enabled = has && s.icon != null; v.icon.sprite = has ? s.icon : null; v.icon.preserveAspect = true; }
            if (v.count != null) v.count.text = has && s.count > 1 ? "x" + s.count : "";
        }
    }

    public void UseSlot(int i)
    {
        if (inv == null || i < 0 || i >= inv.slots.Length) return;
        var s = inv.slots[i];
        if (s == null || s.IsEmpty) { Toast("Empty pocket"); return; }
        string msg = "";
        bool used = ItemEffects.Instance != null && ItemEffects.Instance.Use(s.item, out msg);
        if (used) { inv.RemoveOne(i); bounce[i] = 1f; }
        Toast(msg);
    }

    public void Toast(string msg)
    {
        if (toast == null || string.IsNullOrEmpty(msg)) return;
        toast.text = msg;
        var c = toast.color; c.a = 1f; toast.color = c;
        toastTimer = 2f;
    }
}
