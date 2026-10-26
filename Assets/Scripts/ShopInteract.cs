using UnityEngine;

public class ShopInteract : Interactable
{
    public ShopManager shopManager; // 把場景中的 ShopUI 拖到這裡

    void Start()
    {
        hintMessage = "Press E to open Shop";
    }

    public override void Interact()
    {
        if (shopManager != null)
        {
            shopManager.OpenShop();
        }
    }
}