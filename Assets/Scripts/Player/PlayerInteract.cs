using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 5f; // 拉長一點比較好點
    public TextMeshProUGUI hintText; 
    public Image crosshair;

    private Interactable currentTarget;

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            
            if (interactable != null)
            {
                currentTarget = interactable;
                if (hintText != null) hintText.text = interactable.hintMessage;
                if (crosshair != null) crosshair.color = Color.blue;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    // 💡 如果是手電筒，直接執行暴力開燈法！
                    if (interactable.gameObject.name.Contains("Flashlight"))
                    {
                        GameObject lightObj = GameObject.Find("TorchLight");
                        GameObject modelObj = GameObject.Find("HeldFlashlight");
                        
                        if (lightObj != null) lightObj.SetActive(true); // 開燈
                        if (modelObj != null) modelObj.SetActive(true); // 顯示手中模型
                        
                        Destroy(interactable.gameObject); // 刪除桌上的手電筒
                        Debug.Log("手電筒打開了！");
                    }
                    else
                    {
                        interactable.Interact();
                    }
                }
                return; 
            }
        }
        
        currentTarget = null;
        if (hintText != null) hintText.text = "";
        if (crosshair != null) crosshair.color = Color.white;
    }
}