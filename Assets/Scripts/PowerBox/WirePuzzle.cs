using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; 

public class WirePuzzle : MonoBehaviour
{
    [Header("UI References")]
    public GameObject fuseVisual;       
    public Button fuseSlotButton;       
    public Button mainSwitchButton;     
    
    // 🚨 IMPORTANT: Drag your Status Text object into this slot in the Inspector!
    public TextMeshProUGUI statusText; 

    [Header("Manual Book References")]
    public GameObject manualBookPanel;    
    public TextMeshProUGUI manualBookText; 

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip insertFuseSound;
    public AudioClip openBookSound;

    private Dictionary<string, int> wireStates = new Dictionary<string, int> { { "Red", 0 }, { "Blue", 0 }, { "Yellow", 0 } };
    private Dictionary<int, string> portStates = new Dictionary<int, string> { { 1, "" }, { 2, "" }, { 3, "" } };

    private bool isShuffled = false; 
    private PowerBox powerBox;
    private string selectedWire = "";
    private bool isFuseInserted = false;
    private bool puzzleSolved = false;

    private int correctPortRed, correctPortBlue, correctPortYellow;

    void Awake()
    {
        powerBox = FindObjectOfType<PowerBox>();
    }

    void OnEnable()
    {
        if (!isShuffled)
        {
            ShuffleAnswers();
            isShuffled = true;
        }
        
        UpdateManualBook();
        if (manualBookPanel != null) manualBookPanel.SetActive(false);

        // Safely update text
        if (statusText != null)
        {
            if (puzzleSolved) statusText.text = "POWER ON!";
            else if (!isFuseInserted) statusText.text = "Please insert the fuse first.";
            else statusText.text = "Select a wire, then pick a port.";
            
            statusText.color = Color.white;
        }

        if (fuseVisual != null) fuseVisual.SetActive(isFuseInserted); 
    }

    private void ShuffleAnswers()
    {
        int[] ports = { 1, 2, 3 };
        ports = ports.OrderBy(x => Random.value).ToArray();

        correctPortRed = ports[0]; 
        correctPortBlue = ports[1]; 
        correctPortYellow = ports[2]; 
    }

    private void UpdateManualBook()
    {
        if (manualBookText != null)
        {
            string redHex = "<color=#D32F2F>Red</color>";
            string blueHex = "<color=#1976D2>Blue</color>";
            string yellowHex = "<color=#F57C00>Yellow</color>";

            manualBookText.text = "<b>[ WIRING GUIDE ]</b>\n\n" +
                                  redHex + " Wire  ->  Port " + correctPortRed + "\n" +
                                  blueHex + " Wire ->  Port " + correctPortBlue + "\n" +
                                  yellowHex + " Wire -> Port " + correctPortYellow + "\n\n" +
                                  "<i>* Click connected wire to unplug.</i>";
        }
    }

    public void OpenManual() 
    { 
        if (manualBookPanel != null) manualBookPanel.SetActive(true); 

        if (audioSource != null && openBookSound != null)
        {
            audioSource.PlayOneShot(openBookSound);
        }
    }

    public void CloseManual() { if (manualBookPanel != null) manualBookPanel.SetActive(false); }

    public void ClickFuseSlot()
    {
        if (isFuseInserted || puzzleSolved) return;
        
        if (FusePickup.playerHasFuse)
        {
            isFuseInserted = true;
            FusePickup.playerHasFuse = false; 
            
            if (fuseVisual != null) fuseVisual.SetActive(true);
            if (fuseSlotButton != null) fuseSlotButton.interactable = false; 
            
            // Play Audio Safely
            if (audioSource != null && insertFuseSound != null) 
                audioSource.PlayOneShot(insertFuseSound);
            
            if (statusText != null)
            {
                statusText.text = "Fuse inserted. Now connect the wires.";
                statusText.color = Color.green;
            }
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = "You don't have a fuse! Find one first.";
                statusText.color = Color.red;
            }
        }
    }

    public void SelectWire(string color)
    {
        if (puzzleSolved || !isFuseInserted) 
        {
            if (statusText != null)
            {
                statusText.text = "Insert the fuse first!";
                statusText.color = Color.red;
            }
            return;
        }

        if (wireStates[color] != 0)
        {
            int oldPort = wireStates[color];
            wireStates[color] = 0;
            portStates[oldPort] = "";
            
            GameObject lineObj = GameObject.Find("UI_Line_" + color);
            if (lineObj != null) Destroy(lineObj);

            if (statusText != null)
            {
                statusText.text = color + " wire unplugged.";
                statusText.color = Color.white;
            }
            selectedWire = "";
            return;
        }

        selectedWire = color;
        if (statusText != null)
        {
            statusText.text = "Selected: " + color + " wire. Now pick a port.";
            statusText.color = Color.yellow;
        }
    }

    public void ClickPort1() { HandleConnection(1, "Port_1"); }
    public void ClickPort2() { HandleConnection(2, "Port_2"); }
    public void ClickPort3() { HandleConnection(3, "Port_3"); }

    private void HandleConnection(int portNumber, string portObjectName)
    {
        if (puzzleSolved || !isFuseInserted) return;

        if (string.IsNullOrEmpty(selectedWire))
        {
            if (statusText != null)
            {
                statusText.text = "Pick a wire first!";
                statusText.color = Color.red;
            }
            return;
        }

        if (portStates[portNumber] != "")
        {
            string existingColor = portStates[portNumber];
            wireStates[existingColor] = 0;
            GameObject lineObj = GameObject.Find("UI_Line_" + existingColor);
            if (lineObj != null) Destroy(lineObj);
        }

        wireStates[selectedWire] = portNumber;
        portStates[portNumber] = selectedWire;

        GameObject wireObj = GameObject.Find("Wire_" + selectedWire);
        GameObject portObj = GameObject.Find(portObjectName);
        
        if (wireObj != null && portObj != null)
            DrawUILine(selectedWire, wireObj.GetComponent<RectTransform>(), portObj.GetComponent<RectTransform>());

        // Check if all are correct
        if (wireStates["Red"] == correctPortRed && 
            wireStates["Blue"] == correctPortBlue && 
            wireStates["Yellow"] == correctPortYellow)
        {
            if (statusText != null)
            {
                statusText.text = "Wiring complete! Now flip the main switch.";
                statusText.color = Color.green;
            }
            if (mainSwitchButton != null) mainSwitchButton.interactable = true;
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = selectedWire + " connected to Port " + portNumber + ".";
                statusText.color = Color.white;
            }
            if (mainSwitchButton != null) mainSwitchButton.interactable = false;
        }

        selectedWire = "";
    }

    public void ClickMainSwitch()
    {
        if (puzzleSolved || wireStates["Red"] != correctPortRed || wireStates["Blue"] != correctPortBlue || wireStates["Yellow"] != correctPortYellow) return; 

        puzzleSolved = true;
        
        if (statusText != null)
        {
            statusText.text = "POWER ON!";
            statusText.color = Color.green;
        }
        
        if (mainSwitchButton != null) mainSwitchButton.interactable = false;

        if (powerBox != null) 
        {
            powerBox.TurnOnPower();
        }
    }

    public void ClosePuzzle()
    {
        gameObject.SetActive(false); 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void DrawUILine(string colorName, RectTransform startNode, RectTransform endNode)
    {
        GameObject lineObj = new GameObject("UI_Line_" + colorName);
        lineObj.transform.SetParent(this.transform, false); 

        Image lineImage = lineObj.AddComponent<Image>();
        if (colorName == "Red") lineImage.color = Color.red;
        else if (colorName == "Blue") lineImage.color = Color.blue;
        else if (colorName == "Yellow") lineImage.color = Color.yellow;

        RectTransform lineRect = lineObj.GetComponent<RectTransform>();

        Vector2 startPos = this.transform.InverseTransformPoint(startNode.position);
        Vector2 endPos = this.transform.InverseTransformPoint(endNode.position);

        startPos.y -= 30f; 
        endPos.y += 30f;   

        Vector2 dir = endPos - startPos;
        float distance = dir.magnitude;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        lineRect.localPosition = startPos + (dir / 2f);
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
     
        lineRect.sizeDelta = new Vector2(distance, 10f); 

        if (manualBookPanel != null)
        {
            lineObj.transform.SetSiblingIndex(manualBookPanel.transform.GetSiblingIndex());
        }
    }
}