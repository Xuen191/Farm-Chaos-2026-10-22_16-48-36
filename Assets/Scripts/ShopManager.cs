using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopManager : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI coinText;      
    public GameObject shopPanel;          

    [Header("Popup Elements")]
    public GameObject itemDetailPopup;    
    public Image popupIcon;               
    public TextMeshProUGUI popupName;     
    public TextMeshProUGUI popupDesc;     
    public TextMeshProUGUI popupPrice;    

    [Header("Item Icons (Drag your 2D sprites here)")]
    public Sprite iconChickenFeed;
    public Sprite iconBomb;
    public Sprite iconCoffee;
    public Sprite iconMeatyBone;
    public Sprite iconNightVision;
    public Sprite iconMagicWhistle;
    public Sprite iconPigBait;
    public Sprite iconSheepBait;

    [Header("Player Data")]
    public int playerCoins = 100;         

    private int currentPrice;
    private string currentItemName;

    void Start()
    {
        UpdateCoinUI();
        if (itemDetailPopup != null) itemDetailPopup.SetActive(false);
    }

    public void UpdateCoinUI()
    {
        if (coinText != null)
            coinText.text = playerCoins.ToString(); 
    }

    // The main logic function
    public void ShowItemDetails(Sprite icon, string name, string desc, int price)
    {
        currentPrice = price;
        currentItemName = name;

        if (popupIcon != null) popupIcon.sprite = icon;
        if (popupName != null) popupName.text = name;
        if (popupDesc != null) popupDesc.text = desc;
        if (popupPrice != null) popupPrice.text = "$" + price;

        if (itemDetailPopup != null) itemDetailPopup.SetActive(true);
    }

    // ==========================================
    // WRAPPER FUNCTIONS FOR UI BUTTONS
    // ==========================================

    public void Btn_ClickChickenFeed() { ShowItemDetails(iconChickenFeed, "Chicken Feed", "Lure chickens back to the fence.", 10); }
    public void Btn_ClickBomb() { ShowItemDetails(iconBomb, "Bomb", "Throw it to scare away the bad dog or herd animals.", 25); }
    public void Btn_ClickCoffee() { ShowItemDetails(iconCoffee, "Coffee", "Increases movement speed by 50% for 10s.", 15); }
    public void Btn_ClickMeatyBone() { ShowItemDetails(iconMeatyBone, "Meaty Bone", "Throw it to distract the bad dog.", 20); }
    public void Btn_ClickNightVision() { ShowItemDetails(iconNightVision, "Night Vision Goggles", "See clearly in the dark.", 50); }
    public void Btn_ClickMagicWhistle() { ShowItemDetails(iconMagicWhistle, "Magic Whistle", "Attract nearby animals with sound.", 30); }
    public void Btn_ClickPigBait() { ShowItemDetails(iconPigBait, "Pig Slop", "Delicious feed to lure pigs.", 10); }
    public void Btn_ClickSheepBait() { ShowItemDetails(iconSheepBait, "Sheep Hay", "Fresh hay, sheep's favorite.", 10); }

    // ==========================================

    public void ConfirmPurchase()
    {
        if (playerCoins >= currentPrice)
        {
            playerCoins -= currentPrice;
            UpdateCoinUI();
            Debug.Log("Successfully bought: " + currentItemName);
            
            // Add logic to give the item to the player here

            ClosePopup();
        }
        else
        {
            Debug.Log("Not enough coins!");
            if (popupPrice != null) popupPrice.text = "<color=red>Not enough coins!</color>";
        }
    }

    public void ClosePopup()
    {
        if (itemDetailPopup != null) itemDetailPopup.SetActive(false);
    }

    public void OpenShop()
    {
        shopPanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None; 
        Cursor.visible = true;
    }

    public void CloseShop()
    {
        shopPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;
    }
}