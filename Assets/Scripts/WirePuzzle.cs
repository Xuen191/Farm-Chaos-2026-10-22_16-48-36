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

    [Header("Manual Book References")]
    public GameObject manualBookPanel;    
    public TextMeshProUGUI manualBookText; 

    // 紀錄每條線目前的狀態 (存 Port 編號，0 代表未接)
    private Dictionary<string, int> wireStates = new Dictionary<string, int> { { "Red", 0 }, { "Blue", 0 }, { "Yellow", 0 } };
    // 紀錄每個 Port 被哪條線佔用 (存顏色，空字串代表沒人)
    private Dictionary<int, string> portStates = new Dictionary<int, string> { { 1, "" }, { 2, "" }, { 3, "" } };

    private bool isShuffled = false; // 💡 確保謎題只隨機一次

    private PowerBox powerBox;
    private TextMeshProUGUI statusText;
    
    private string selectedWire = "";
    private bool isFuseInserted = false;
    private bool puzzleSolved = false;

    // 記住正確答案 (只在第一次打開時生成)
    private int correctPortRed, correctPortBlue, correctPortYellow;

    void Awake()
    {
        powerBox = FindObjectOfType<PowerBox>();
        GameObject textObj = GameObject.Find("StatusText");
        if (textObj != null) statusText = textObj.GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        // 💡 如果還沒洗牌過，才進行隨機，確保關掉重開答案不會變
        if (!isShuffled)
        {
            ShuffleAnswers();
            isShuffled = true;
        }
        
        UpdateManualBook();
        if (manualBookPanel != null) manualBookPanel.SetActive(false);

        // 根據目前狀態更新提示
        if (puzzleSolved) statusText.text = "POWER ON!";
        else if (!isFuseInserted) 
        {
            statusText.text = "Please insert the fuse first.";
            if (fuseVisual != null) fuseVisual.SetActive(false); 
        }
        else 
        {
            statusText.text = "Select a wire, then pick a port.";
            if (fuseVisual != null) fuseVisual.SetActive(true);
        }
        
        statusText.color = Color.white;
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

    public void OpenManual() { if (manualBookPanel != null) manualBookPanel.SetActive(true); }
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
            statusText.text = "Fuse inserted. Now connect the wires.";
            statusText.color = Color.green;
        }
        else
        {
            statusText.text = "You don't have a fuse! Find one first.";
            statusText.color = Color.red;
        }
    }

    public void SelectWire(string color)
    {
        if (puzzleSolved || !isFuseInserted) 
        {
            statusText.text = "Insert the fuse first!";
            statusText.color = Color.red;
            return;
        }

        // 💡 如果點擊已經接好的線，就把它拔掉 (斷開連線)
        if (wireStates[color] != 0)
        {
            int oldPort = wireStates[color];
            wireStates[color] = 0;
            portStates[oldPort] = "";
            
            // 刪除對應的畫線
            GameObject lineObj = GameObject.Find("UI_Line_" + color);
            if (lineObj != null) Destroy(lineObj);

            statusText.text = color + " wire unplugged.";
            statusText.color = Color.white;
            selectedWire = "";
            return;
        }

        selectedWire = color;
        statusText.text = "Selected: " + color + " wire. Now pick a port.";
        statusText.color = Color.yellow;
    }

    public void ClickPort1() { HandleConnection(1, "Port_1"); }
    public void ClickPort2() { HandleConnection(2, "Port_2"); }
    public void ClickPort3() { HandleConnection(3, "Port_3"); }

    private void HandleConnection(int portNumber, string portObjectName)
    {
        if (puzzleSolved || !isFuseInserted) return;

        if (string.IsNullOrEmpty(selectedWire))
        {
            statusText.text = "Pick a wire first!";
            statusText.color = Color.red;
            return;
        }

        // 💡 如果這個插孔已經被別的線佔用了，先把它拔掉
        if (portStates[portNumber] != "")
        {
            string existingColor = portStates[portNumber];
            wireStates[existingColor] = 0;
            GameObject lineObj = GameObject.Find("UI_Line_" + existingColor);
            if (lineObj != null) Destroy(lineObj);
        }

        // 綁定新線
        wireStates[selectedWire] = portNumber;
        portStates[portNumber] = selectedWire;

        GameObject wireObj = GameObject.Find("Wire_" + selectedWire);
        GameObject portObj = GameObject.Find(portObjectName);
        if (wireObj != null && portObj != null)
            DrawUILine(selectedWire, wireObj.GetComponent<RectTransform>(), portObj.GetComponent<RectTransform>());

        // 檢查是否全部正確
        if (wireStates["Red"] == correctPortRed && 
            wireStates["Blue"] == correctPortBlue && 
            wireStates["Yellow"] == correctPortYellow)
        {
            statusText.text = "Wiring complete! Now flip the main switch.";
            statusText.color = Color.green;
            if (mainSwitchButton != null) mainSwitchButton.interactable = true;
        }
        else
        {
            statusText.text = selectedWire + " connected to Port " + portNumber + ".";
            statusText.color = Color.white;
            if (mainSwitchButton != null) mainSwitchButton.interactable = false;
        }

        selectedWire = "";
    }

    public void ClickMainSwitch()
    {
        // 確保線都接對才觸發
        if (puzzleSolved || wireStates["Red"] != correctPortRed || wireStates["Blue"] != correctPortBlue || wireStates["Yellow"] != correctPortYellow) return; 

        puzzleSolved = true;
        statusText.text = "POWER ON!";
        statusText.color = Color.green;
        
        if (mainSwitchButton != null) mainSwitchButton.interactable = false;

        // 💡 這裡直接呼叫 PowerBox 的通電函數（不需要再按 E 了）
        if (powerBox != null) 
        {
            powerBox.TurnOnPower(); // 我們會在 PowerBox 裡新增這個函數
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
        Vector3 startPos = startNode.position;
        Vector3 endPos = endNode.position;
        
        float distance = Vector3.Distance(startPos, endPos);
        float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;

        lineRect.position = startPos + (endPos - startPos) / 2f;
        lineRect.rotation = Quaternion.Euler(0, 0, angle);
        lineRect.sizeDelta = new Vector2(distance, 10f); 

        if (manualBookPanel != null)
        {
            lineObj.transform.SetSiblingIndex(manualBookPanel.transform.GetSiblingIndex());
        }
    }
}