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
    public Sprite iconFreshHay;

    [Header("Player Data")]
    public int playerCoins = 100;   

    [Header("Audio Settings")]
    public AudioSource audioSource; 
    public AudioClip paidSound;      

    private int currentPrice;
    private string currentItemName;
    private Sprite currentItemIcon;

    void Start()
    {
        if (CoinWallet.Exists) CoinWallet.Changed += _ => UpdateCoinUI();
        UpdateCoinUI();
        if (itemDetailPopup != null) itemDetailPopup.SetActive(false);
    }

    public void UpdateCoinUI()
    {
        if (CoinWallet.Exists) playerCoins = CoinWallet.Coins;
        if (coinText != null)
            coinText.text = playerCoins.ToString(); 
    }

    // 顯示商品詳情
    public void ShowItemDetails(Sprite icon, string name, string desc, int price)
    {
        currentPrice = price;
        currentItemName = name;
        currentItemIcon = icon;

        if (popupIcon != null) popupIcon.sprite = icon;
        if (popupName != null) popupName.text = name;
        if (popupDesc != null) popupDesc.text = desc;
        if (popupPrice != null) popupPrice.text = "$" + price;

        if (itemDetailPopup != null) itemDetailPopup.SetActive(true);
    }

    public void Btn_ClickChickenFeed() { ShowItemDetails(iconChickenFeed, "Chicken Feed", "Lure chickens back to the fence.", 5); }
    public void Btn_ClickBomb() { ShowItemDetails(iconBomb, "Bomb", "Throw it to scare away the bad dog or herd animals.", 25); }
    public void Btn_ClickCoffee() { ShowItemDetails(iconCoffee, "Coffee", "Increases movement speed by 50% for 10s.", 5); }
    public void Btn_ClickMeatyBone() { ShowItemDetails(iconMeatyBone, "Meaty Bone", "Throw it to distract the bad dog.", 10); }
    public void Btn_ClickNightVision() { ShowItemDetails(iconNightVision, "Night Vision Goggles", "See clearly in the dark.", 20); }
    public void Btn_ClickMagicWhistle() { ShowItemDetails(iconMagicWhistle, "Magic Whistle", "Attract nearby animals with sound.", 20); }
    public void Btn_ClickPigBait() { ShowItemDetails(iconPigBait, "Pig Slop", "Delicious feed to lure pigs.", 5); }
    public void Btn_ClickFreshHay() { ShowItemDetails(iconFreshHay, "Fresh Hay", "Fresh hay, cow's or sheep's favorite.", 5); }

    // ==========================================

    public void ConfirmPurchase()
    {
        if (CoinWallet.Exists && CoinWallet.TrySpend(currentPrice))
        {
            if (Inventory.Instance != null)
            {
                Inventory.Instance.Add(currentItemName, currentItemIcon);
            }

            if (audioSource != null && paidSound != null)
            {
                audioSource.PlayOneShot(paidSound);
            }

            ClosePopup();
        }
        else if (!CoinWallet.Exists && playerCoins >= currentPrice)
        {
            playerCoins -= currentPrice;
            UpdateCoinUI();
            
            if (Inventory.Instance != null)
            {
                Inventory.Instance.Add(currentItemName, currentItemIcon);
            }

            if (audioSource != null && paidSound != null)
            {
                audioSource.PlayOneShot(paidSound);
            }

            ClosePopup();
        }
        else
        {
            Debug.Log("❌ 金幣不足，無法購買！");
            // 你可以在這裡讓價格文字變紅，或者播放錯誤音效
        }
    }

    public void ClosePopup()
    {
        if (itemDetailPopup != null) itemDetailPopup.SetActive(false);
    }

    public void OpenShop()
    {
        if (shopPanel != null) shopPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None; 
        Cursor.visible = true;
    }

    public void CloseShop()
    {
        if (shopPanel != null) shopPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;
    }
}