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
    public Sprite iconCarrot; 
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
    
    public void Btn_ClickChickenFeed() 
    { 
        ShowItemDetails(iconChickenFeed, "Chicken Feed", "Scatter these seeds to lure hungry chickens back to their pens.", 5); 
    }

    public void Btn_ClickCarrot() 
    { 
        ShowItemDetails(iconCarrot, "Carrot", "A crunchy treat! Bunnies will hop straight towards it.", 5); 
    }

    public void Btn_ClickFreshHay() 
    { 
        ShowItemDetails(iconFreshHay, "Fresh Hay", "Sweet-smelling grass. Sheep and cows can't resist it.", 5); 
    }

    public void Btn_ClickCoffee() 
    { 
        ShowItemDetails(iconCoffee, "Coffee", "Drink up! Increases your rolling speed by 50% for 10 seconds.", 10); 
    }

    public void Btn_ClickBomb() 
    { 
        ShowItemDetails(iconBomb, "Bomb", "Boom! Scares away the wolf instantly and knocks animals out of the way.", 25); 
    }

    public void Btn_ClickNightVision() 
    { 
        ShowItemDetails(iconNightVision, "Night Vision Goggles", "Turns the darkest night into a bright green day for 25 seconds.", 20); 
    }

    public void Btn_ClickMagicWhistle() 
    { 
        ShowItemDetails(iconMagicWhistle, "Magic Whistle", "Blow it to instantly pull all nearby animals towards you. Has a 20s cooldown.", 20); 
    }

    public void Btn_ClickMeatyBone() 
    { 
        ShowItemDetails(iconMeatyBone, "Meaty Bone", "Throw this juicy bone to distract the wolf and stop it from eating your animals.", 10); 
    }

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