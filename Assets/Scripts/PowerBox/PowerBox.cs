using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PowerBox : Interactable
{
    [Header("State")]
    public bool hasFuse = false; 
    public bool wiresCorrect = false;
    public bool powerOn = false;
    [Tooltip("Farm Chaos: the power dropped after it was fixed - press E to reset the breaker (no puzzle)")]
    public bool tripped = false;
    bool resetting;

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
        ElectricGate.PowerChanged += OnGridPower;
    }

    void OnDestroy() { ElectricGate.PowerChanged -= OnGridPower; }

    // Farm Chaos: the power can drop again later (unstable grid, lightning, spy) -> breaker trips
    void OnGridPower(bool on)
    {
        if (on || !wiresCorrect || !powerOn) return;
        powerOn = false; tripped = true;
        if (farmLight != null) farmLight.TurnOffLight();
        if (statusText != null) statusText.text = "POWER OUT";
    }

    System.Collections.IEnumerator ResetBreaker()
    {
        resetting = true;
        if (audioSource != null && switchClickSound != null) audioSource.PlayOneShot(switchClickSound);
        if (statusText != null) statusText.text = "Resetting...";
        yield return new WaitForSeconds(1.2f);
        resetting = false; tripped = false;
        TurnOnPower();
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
            if (tripped) hintMessage = resetting ? "Resetting the breaker..." : "Power's out! Press E to reset the breaker";
            else if (powerOn && NetPlayer.IAmSpy) hintMessage = PowerGrid.SpyCooldownLeft > 0f ? "Power is ON" : "Press E to secretly overload the power";
            else hintMessage = powerOn ? "Power is ON" : "Press E to open power box";
        }
    }

    public override void Interact()
    {
        if (powerOn) { if (NetPlayer.IAmSpy) PowerGrid.SpyOverload(); return; }   // Farm Chaos: spy sabotage
        if (tripped) { if (!resetting) StartCoroutine(ResetBreaker()); return; }   // quick fix after the first one
        
        if (audioSource != null && openBoxSound != null) 
            audioSource.PlayOneShot(openBoxSound);
            
        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(true);
        
        // 💡 隱藏快捷欄 (透過名字尋找)
        GameObject hotbar = GameObject.Find("HotbarPanel");
        if (hotbar != null) hotbar.SetActive(false);

        if (statusText != null) statusText.text = ""; 
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void TurnOnPower()
    {
        if (powerOn) return; 

        powerOn = true;
        wiresCorrect = true;

        // Farm Chaos: power back on -> every electric pen gate swings shut
        ElectricGate.SetPower(true);
        
        if (farmLight != null) farmLight.TurnOnLight();

        if (audioSource != null)
        {
            if (switchClickSound != null) audioSource.PlayOneShot(switchClickSound);
            if (powerOnSound != null) audioSource.PlayOneShot(powerOnSound);
        }

        if (farmLight != null) farmLight.TurnOnLight();
        if (farmGate != null) farmGate.CloseGate();

        if (wirePuzzleUI != null) wirePuzzleUI.SetActive(false);
        
        // 💡 顯示快捷欄
        GameObject hotbar = GameObject.Find("HotbarPanel");
        if (hotbar != null) hotbar.SetActive(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (statusText != null) statusText.text = "POWER ON";
    }
}