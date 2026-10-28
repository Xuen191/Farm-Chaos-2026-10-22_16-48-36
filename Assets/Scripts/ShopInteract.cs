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

            shopManager.OpenShop();


            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.ToggleHotbar(false);
            }
        }
    }
}