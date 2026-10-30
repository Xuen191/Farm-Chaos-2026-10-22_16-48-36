using UnityEngine;
using UnityEngine.UI; 

public class PlayerDropTool : MonoBehaviour
{
    [Header("UI References")]
    public GameObject dropButtonObj; 

    void Update()
    {
        if (dropButtonObj == null) return;

        StickAttack attack = GetComponent<StickAttack>();
        
        if (attack != null && attack.hasStick)
        {
            if (!dropButtonObj.activeSelf) dropButtonObj.SetActive(true);
        }
        else
        {
            if (dropButtonObj.activeSelf) dropButtonObj.SetActive(false);
        }
    }

    public void DropCurrentTool()
    {
        Transform stickModel = transform.Find("MyHandheldStickModel");
        
        if (stickModel != null)
        {
            Destroy(stickModel.gameObject);
            
            StickAttack attack = GetComponent<StickAttack>();
            if (attack != null) 
            {
                attack.hasStick = false;
  
        }
    }
}
}