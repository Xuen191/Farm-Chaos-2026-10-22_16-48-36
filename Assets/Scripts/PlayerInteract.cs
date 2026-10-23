using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    public float interactDistance = 3f;
    public TMPro.TextMeshProUGUI hintText; 
    public Image crosshair; // 💡 新增：把 UI 準星拖進來

    void Update()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
        {
            Interactable interactable = hit.collider.GetComponent<Interactable>();
            
            if (interactable != null)
            {
                // 💡 瞄準到可互動物件：準星變色、顯示提示字
                if (crosshair != null) crosshair.color = Color.green; 
                if (hintText != null) hintText.text = interactable.hintMessage;

                if (Input.GetKeyDown(KeyCode.E))
                {
                    interactable.Interact();
                }
                return; 
            }
        }
        
        // 💡 沒瞄準到東西：準星變回白色、清空提示字
        if (crosshair != null) crosshair.color = Color.white;
        if (hintText != null) hintText.text = "";
    }
}