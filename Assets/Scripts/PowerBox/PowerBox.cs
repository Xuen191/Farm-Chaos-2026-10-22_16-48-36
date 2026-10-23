using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerBox : Interactable
{
    [Header("State")]
    public bool hasFuse = false;
    public bool wiresCorrect = false;
    public bool powerOn = false;

    [Header("References")]
    public Animator gateAnimator;       // your gate
    public GameObject wirePuzzleUI;     // UI panel for wiring
    public TextMeshProUGUI statusText;  // shows "No fuse / Wrong wires / Power ON"

    void Start() 
    { 
        // 預設提示（雖然會被 Interact 覆蓋，但留著無妨）
        hintMessage = "Press E to open power box"; 
        if(wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
    }

        void Update()
    {
        // 如果還沒插入保險絲
        if (!hasFuse)
        {
            if (FusePickup.playerHasFuse)
            {
                // 手上有保險絲，提示玩家插入
                hintMessage = "Press E to insert Fuse";
            }
            else
            {
                // 手上沒保險絲，提示玩家去尋找
                hintMessage = "Needs a Fuse (Find one first)";
            }
        }
        // 如果插了保險絲，但線沒接好
        else if (!wiresCorrect)
        {
            hintMessage = "Press E to repair wiring";
        }
        // 如果都弄好了
        else
        {
            hintMessage = "Press E to turn on Power";
        }
    }

    public override void Interact()
    {
        // Step 1: 需要保險絲
        if (!hasFuse)
        {
            if (FusePickup.playerHasFuse)
            {
                // 玩家有保險絲，按下 E 執行插入
                hasFuse = true;
                statusText.text = "Fuse inserted. Wires needed.";
                Debug.Log("Fuse inserted into box.");
            }
            else
            {
                // 玩家沒有保險絲，按下 E 提示找不到
                statusText.text = "You need a fuse first!";
                Debug.Log("You need a fuse first!");
            }
            return;
        }

        // Step 2: 接線解謎
        if (!wiresCorrect)
        {
            wirePuzzleUI.SetActive(true); 
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            statusText.text = "Inserted. Now connect the wires!";
            return;
        }

        // Step 3: 切換電源
        powerOn = !powerOn;
        statusText.text = powerOn ? "POWER ON" : "POWER OFF";
        Debug.Log("Power is now: " + powerOn);

        if (powerOn && gateAnimator != null)
        {
            gateAnimator.SetBool("IsOpen", false);
            Debug.Log("Gate closing!");
        }
    }

    // Called by the wiring puzzle when player solves it
    public void SetWiresCorrect()
    {
        wiresCorrect = true;
        wirePuzzleUI.SetActive(false);
        
        // 💡 關閉 UI 後，重新鎖定滑鼠，讓玩家恢復第一人稱視角
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        statusText.text = "Wires correct. Flip switch!";
        Debug.Log("Wires correct. Flip switch!");
    }
}