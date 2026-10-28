using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    [Header("Inventory Counts")]
    public int chickenFeed = 0;
    public int pigBait = 0;   // 💡 補回宣告
    public int sheepBait = 0; // 💡 補回宣告
    public int bomb = 0;
    public int coffee = 0;
    public int rope = 0; 

    [Header("Hotbar Settings")]
    public GameObject hotbarPanel;         // 底部快捷欄的容器
    public GameObject hotbarSlotPrefab;    // 快捷欄格子的 Prefab

    [Header("Bag Settings")]
    public GameObject fullInventoryPanel;  // 大背包面板
    public GameObject bagSlotPrefab;       // 大背包格子的 Prefab
    public Transform bagContentParent;     // 大背包裡面用來裝格子的容器

    // 紀錄裝備在快捷欄的四個道具
    private string[] equippedItems = new string[4] { "", "", "", "" };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 💡 如果 Inspector 拖不進去，讓程式碼自己去場景裡找！
        if (hotbarPanel == null) 
        {
            hotbarPanel = GameObject.Find("HotbarPanel");
            if (hotbarPanel != null) Debug.Log("✅ 程式碼成功找到 HotbarPanel！");
        }

        if (fullInventoryPanel == null) 
        {
            fullInventoryPanel = GameObject.Find("FullInventoryPanel");
            if (fullInventoryPanel != null) Debug.Log("✅ 程式碼成功找到 FullInventoryPanel！");
        }

        if (bagContentParent == null)
        {
            GameObject contentObj = GameObject.Find("Content");
            if (contentObj != null) 
            {
                bagContentParent = contentObj.transform;
                Debug.Log("✅ 程式碼成功找到 Content！");
            }
        }
    }

    void Start()
    {
        if (fullInventoryPanel != null) fullInventoryPanel.SetActive(false);
        UpdateHotbarUI();
    }

    // ================= 自動抓圖魔法 =================
    public Sprite GetIconByName(string itemName)
    {
        Sprite loadedIcon = Resources.Load<Sprite>("Icon/" + itemName);
        if (loadedIcon == null)
        {
            Debug.LogWarning("⚠️ 找不到圖片：" + itemName);
        }
        return loadedIcon;
    }

    // ================= 道具管理 =================

    public void AddItem(string itemName)
    {
        switch (itemName)
        {
            case "Chicken Feed": chickenFeed++; break;
            case "Pig Slop": pigBait++; break;
            case "Sheep Hay": sheepBait++; break;
            case "Bomb": bomb++; break;
            case "Coffee": coffee++; break;
            case "Rope": rope++; break;
        }
        Debug.Log("獲得道具：" + itemName);
    }

    public void UseItem(string itemName)
    {
        switch (itemName)
        {
            case "Chicken Feed": if(chickenFeed > 0) chickenFeed--; break;
            case "Pig Slop": if(pigBait > 0) pigBait--; break;
            case "Sheep Hay": if(sheepBait > 0) sheepBait--; break;
            case "Bomb": if(bomb > 0) bomb--; break;
            case "Coffee": if(coffee > 0) coffee--; break;
            case "Rope": if(rope > 0) rope--; break;
        }
        UpdateHotbarUI(); 
    }

    public int GetItemCount(string itemName)
    {
        switch (itemName)
        {
            case "Chicken Feed": return chickenFeed;
            case "Pig Slop": return pigBait;
            case "Sheep Hay": return sheepBait;
            case "Bomb": return bomb;
            case "Coffee": return coffee;
            case "Rope": return rope;
            default: return 0;
        }
    }

    // ================= 背包與快捷欄生成 =================

    public void OpenFullInventory()
    {
        Debug.Log("🚨 嘗試打開大背包！");
        
        if (fullInventoryPanel == null || bagContentParent == null) return;

        GameObject shopUI = GameObject.Find("ShopUI");
        if (shopUI != null) shopUI.SetActive(false);

        fullInventoryPanel.SetActive(true);
        if (hotbarPanel != null) hotbarPanel.SetActive(false);

        // 清空舊的格子
        foreach (Transform child in bagContentParent) 
        {
            Destroy(child.gameObject);
        }

        Debug.Log("🚨 開始生成格子，目前炸彈數量：" + bomb + "，雞飼料數量：" + chickenFeed);

        // 💡 生成格子 (只有兩個參數：名稱, 數量)
        CreateBagSlot("Bomb", bomb);
        CreateBagSlot("Chicken Feed", chickenFeed);
        CreateBagSlot("Pig Slop", pigBait);
        CreateBagSlot("Sheep Hay", sheepBait);
        CreateBagSlot("Coffee", coffee);
        CreateBagSlot("Rope", rope);
    }

    private void CreateBagSlot(string itemName, int count)
    {
        if (count <= 0) return; 

        Debug.Log("✅ 正在生成格子：" + itemName);

        GameObject slot = Instantiate(bagSlotPrefab, bagContentParent);
        
        // 自動抓圖並設定
        Image iconImage = slot.transform.Find("Icon").GetComponent<Image>();
        if (iconImage != null) iconImage.sprite = GetIconByName(itemName);
        
        // 設定文字數量
        TextMeshProUGUI countText = slot.transform.Find("CountText").GetComponent<TextMeshProUGUI>();
        if (countText != null) countText.text = count.ToString();

        // 綁定點擊事件
        slot.GetComponent<Button>().onClick.AddListener(() => {
            EquipItem(itemName);
            CloseFullInventory(); 
        });
    }

    public void CloseFullInventory()
    {
        if (fullInventoryPanel != null) fullInventoryPanel.SetActive(false);
        if (hotbarPanel != null) hotbarPanel.SetActive(true);
    }


    public void EquipItem(string itemName)
    {
        for (int i = 0; i < equippedItems.Length; i++)
            if (equippedItems[i] == itemName) return;

        for (int i = 0; i < equippedItems.Length; i++)
        {
            if (string.IsNullOrEmpty(equippedItems[i]))
            {
                equippedItems[i] = itemName;
                UpdateHotbarUI();
                return;
            }
        }
        Debug.Log("快捷欄滿了！");
    }

    public void UpdateHotbarUI()
    {
        if (hotbarPanel == null || hotbarSlotPrefab == null) 
        {
            Debug.LogWarning("⚠️ HotbarPanel 或 Slot Prefab 沒有綁定！");
            return;
        }

        // 清空舊的格子 (保留 Background)
        foreach (Transform child in hotbarPanel.transform) 
        {
            if (child.name.Contains("Slot"))
            {
                Destroy(child.gameObject);         
            }
        }

        for (int i = 0; i < 4; i++)
        {
            GameObject slot = Instantiate(hotbarSlotPrefab, hotbarPanel.transform);
            
            RectTransform rect = slot.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localPosition = Vector3.zero; 
                rect.localScale = Vector3.one;
            }

            string itemName = equippedItems[i];

            // 💡 加上防呆：如果找不到子物件，就報警告，不要讓它當機
            Transform iconTransform = slot.transform.Find("Icon");
            Transform countTransform = slot.transform.Find("CountText");

            if (iconTransform == null || countTransform == null)
            {
                Debug.LogError("❌ 你的 Slot Prefab 裡面缺少 'Icon' 或 'CountText' 子物件！請檢查名字有沒有拼錯。");
                continue; // 跳過這個格子，繼續下一個
            }

            Image iconImage = iconTransform.GetComponent<Image>();
            TextMeshProUGUI countText = countTransform.GetComponent<TextMeshProUGUI>();

            if (string.IsNullOrEmpty(itemName))
            {
                iconImage.gameObject.SetActive(false);
                countText.text = "";
            }
            else
            {
                iconImage.gameObject.SetActive(true);
                countText.text = GetItemCount(itemName).ToString();
                
                // 自動抓圖
                iconImage.sprite = GetIconByName(itemName);

                // 綁定使用道具
                slot.GetComponent<Button>().onClick.AddListener(() => UseEquippedItem(itemName));
            }
        }
    }

    private void UseEquippedItem(string itemName)
    {
        Debug.Log("使用了快捷欄道具：" + itemName);
        // 這裡呼叫你的 PlayerItemUsage
    }

    public void ToggleHotbar(bool show)
    {
        if (hotbarPanel != null)
        {
            hotbarPanel.SetActive(show);
        }
    }
}