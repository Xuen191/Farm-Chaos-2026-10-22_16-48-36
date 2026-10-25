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
    public GateController farmGate;
    public Animator gateAnimator;       
    public GameObject wirePuzzleUI;     
    public TextMeshProUGUI statusText;  
    
    private FarmLightController farmLight; 

    void Start() 
    { 
        hintMessage = "Press E to open power box"; 
        if(wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
        
        // 自動尋找燈光
        farmLight = FindObjectOfType<FarmLightController>();
        if (farmLight != null) farmLight.TurnOffLight();
    }

    void Update()
    {
        if (!wiresCorrect)
        {
            if (FusePickup.playerHasFuse) hintMessage = "Press E to open power box (Fuse ready)";
            else hintMessage = "Press E to open power box (Need Fuse)";
        }
        else
        {
            hintMessage = powerOn ? "Power is ON" : "Press E to open power box";
        }
    }

    public override void Interact()
    {
        // 如果通電了，玩家按 E 就只是單純打開 UI 看，或者不給按
        if (powerOn) return; 

        // 還沒通電，打開 UI 讓玩家去解謎
        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(true); 
        if (statusText != null) statusText.text = ""; 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 💡 這是給 WirePuzzle 呼叫的通電函數
    public void TurnOnPower()
    {
        if (powerOn) return; 

        powerOn = true;
        wiresCorrect = true;
        
        if (farmLight != null) farmLight.TurnOnLight();

        if (farmGate != null) 
        {
            farmGate.CloseGate();
        }

        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        if (statusText != null) statusText.text = "POWER ON";
    }
}