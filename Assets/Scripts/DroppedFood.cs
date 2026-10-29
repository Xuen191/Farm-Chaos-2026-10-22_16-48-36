using UnityEngine;

public class DroppedFood : Interactable
{
    public string itemName; // 記得在生成時把名稱傳進來！

    void Start()
    {
        if (string.IsNullOrEmpty(itemName)) itemName = "Food"; // 防呆
        hintMessage = "Pick up " + itemName;
    }

    public override void Interact()
    {
        if (Inventory.Instance != null)
        {
            // 💡 自動去 Resources/Icons 資料夾找圖片
            Sprite icon = Resources.Load<Sprite>("Icon/" + itemName); 
            
            bool success = Inventory.Instance.Add(itemName, icon);
            if (success) Destroy(gameObject); 
        }
    }
}