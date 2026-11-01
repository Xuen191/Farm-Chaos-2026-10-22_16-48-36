using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode; // 💡 加入 Netcode

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

    [Header("Audio Settings")]
    public AudioSource audioSource;    
    public AudioClip powerOnSound; 
    public AudioClip switchClickSound;
    public AudioClip openBoxSound;   
    
    private FarmLightController farmLight; 

    void Start() 
    { 
        hintMessage = "Press E to open power box"; 
        if(wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
        
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
        if (powerOn) { if (NetPlayer.IAmSpy && PowerGrid.SpyOverload()) ScoreKeeper.Add(40, "Sneaky overload!"); return; }   // Farm Chaos: spy sabotage
        if (tripped) { if (!resetting) { StartCoroutine(ResetBreaker()); ScoreKeeper.Add(20, "Breaker reset!"); } return; }   // quick fix after the first one
        
        if (audioSource != null && openBoxSound != null) 
            audioSource.PlayOneShot(openBoxSound);
            
        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(true);
        
        GameObject hotbar = GameObject.Find("HotbarPanel");
        if (hotbar != null) hotbar.SetActive(false);

        if (statusText != null) statusText.text = ""; 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // 💡 這個函數會由 WirePuzzle 完成解謎後呼叫
    public void TurnOnPower()
    {
        if (powerOn) return; 

        // 如果我們是連線狀態
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // 如果是主機，直接執行並廣播
                ApplyPowerState();
                PowerOnClientRpc();
            }
            else
            {
                // 如果是客機，發送請求給主機
                RequestPowerOnServerRpc();
            }
        }
        else
        {
            // 單機測試
            ApplyPowerState();
        }
    }

    // 💡 客機發送給主機的請求
    [Rpc(SendTo.Server)]
    void RequestPowerOnServerRpc()
    {
        // 主機收到後，執行通電並廣播給所有人
        ApplyPowerState();
        PowerOnClientRpc();
    }

    // 💡 主機發送給所有人的廣播
    [Rpc(SendTo.Everyone)]
    void PowerOnClientRpc()
    {
        ApplyPowerState();
    }

    // 💡 真正執行「通電」效果的函數
    void ApplyPowerState()
    {
        if (powerOn) return; // 避免重複執行

        powerOn = true;
        wiresCorrect = true;

        ElectricGate.SetPower(true);
        
        if (farmLight != null) farmLight.TurnOnLight();

        if (audioSource != null)
        {
            if (switchClickSound != null) audioSource.PlayOneShot(switchClickSound);
            if (powerOnSound != null) audioSource.PlayOneShot(powerOnSound);
        }

        if (farmGate != null) farmGate.CloseGate();

        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
        
        GameObject hotbar = GameObject.Find("HotbarPanel");
        if (hotbar != null) hotbar.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (statusText != null) statusText.text = "POWER ON";
    }
}