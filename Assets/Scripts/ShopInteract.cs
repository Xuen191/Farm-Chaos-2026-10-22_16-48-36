using UnityEngine;

public class ShopInteract : Interactable
{
    public ShopManager shopManager; 

    void Start()
    {
        hintMessage = "Press E to open Shop";
    }

    public override void Interact()
    {
        if (shopManager != null)
        {
            shopManager.OpenShop(); // 這裡面已經會自動隱藏快捷欄了
        }
    }
}