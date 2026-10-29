using UnityEngine;

public class InteractableEnemy : Interactable
{
    void Update()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            StickAttack attack = player.GetComponent<StickAttack>();
            if (attack != null)
            {
                if (attack.hasStick)
                {
                    hintMessage = "Tap USE to Hit Dog"; 
                }
                else
                {
                    hintMessage = "Find a Stick first!"; 
                }
            }
        }
    }

    public override void Interact()
    {
        // 留空，攻擊由 StickAttack 處理
    }
}