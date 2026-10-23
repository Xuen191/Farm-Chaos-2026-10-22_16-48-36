using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class WirePuzzle : MonoBehaviour
{
    [Header("Settings (Puzzle Answer)")]
    public string wire1Color = "Red";
    public int wire1Port = 1;
    public string wire2Color = "Blue";
    public int wire2Port = 2;

    // 內部自動抓取的變數
    private PowerBox powerBox;
    private TextMeshProUGUI statusText;
    
    // 內部狀態
    private string selectedWire = "";
    private bool wire1Done = false;
    private bool wire2Done = false;
    private bool puzzleSolved = false;

    void Awake()
    {
        powerBox = FindObjectOfType<PowerBox>();
        
        GameObject textObj = GameObject.Find("StatusText");
        if (textObj != null)
            statusText = textObj.GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (statusText != null)
        {
            statusText.text = "Select a wire, then pick a port.";
            statusText.color = Color.white;
        }
    }

    // 綁定給 Wire_Red 和 Wire_Blue 的按鈕
    public void SelectWire(string color)
    {
        if (puzzleSolved) return;

        selectedWire = color;
        if (statusText != null)
        {
            statusText.text = "Selected: " + color + " wire. Now pick a port.";
            statusText.color = Color.yellow;
        }
    }

    // 綁定給 Port_1 的按鈕
    public void ClickPort1()
    {
        HandleConnection(1, "Port_1");
    }

    // 綁定給 Port_2 的按鈕
    public void ClickPort2()
    {
        HandleConnection(2, "Port_2");
    }

    private void HandleConnection(int portNumber, string portObjectName)
    {
        if (puzzleSolved) return;

        if (string.IsNullOrEmpty(selectedWire))
        {
            if (statusText != null) statusText.text = "Pick a wire first!";
            return;
        }

        bool matchedFirst = !wire1Done && selectedWire == wire1Color && portNumber == wire1Port;
        bool matchedSecond = !wire2Done && selectedWire == wire2Color && portNumber == wire2Port;

        if (matchedFirst || matchedSecond)
        {
            if (matchedFirst) wire1Done = true;
            if (matchedSecond) wire2Done = true;

            if (statusText != null)
            {
                statusText.text = "Connection OK!";
                statusText.color = Color.green;
            }

            // 🚀 尋找對應的 UI 物件並畫線
            GameObject wireObj = GameObject.Find(selectedWire == "Red" ? "Wire_Red" : "Wire_Blue");
            GameObject portObj = GameObject.Find(portObjectName);
            
            if (wireObj != null && portObj != null)
            {
                DrawUILine(selectedWire, wireObj.GetComponent<RectTransform>(), portObj.GetComponent<RectTransform>());
            }
        }
        else
        {
            if (statusText != null)
            {
                statusText.text = "Wrong connection! Spark!";
                statusText.color = Color.red;
            }
        }

        selectedWire = "";

        if (wire1Done && wire2Done)
        {
            puzzleSolved = true;
            if (statusText != null)
            {
                statusText.text = "Wiring complete! You can close this.";
                statusText.color = Color.green;
            }

            if (powerBox != null)
                powerBox.SetWiresCorrect();
        }
    }

    // 綁定給 CloseButton 的按鈕
    public void ClosePuzzle()
    {
        gameObject.SetActive(false); 
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // 🌟 純 UI 畫線函數
    private void DrawUILine(string colorName, RectTransform startNode, RectTransform endNode)
    {
        // 1. 生成一條線 (作為 WirePuzzleUI 的子物件)
        GameObject lineObj = new GameObject("UI_Line_" + colorName);
        lineObj.transform.SetParent(this.transform, false); 

        // 2. 加上 Image 組件並設定顏色
        Image lineImage = lineObj.AddComponent<Image>();
        lineImage.color = (colorName == "Red") ? Color.red : Color.blue;

        // 3. 取得 RectTransform
        RectTransform lineRect = lineObj.GetComponent<RectTransform>();
        
        // 4. 計算起點和終點的距離與角度
        Vector3 startPos = startNode.position;
        Vector3 endPos = endNode.position;
        
        float distance = Vector3.Distance(startPos, endPos);
        float angle = Mathf.Atan2(endPos.y - startPos.y, endPos.x - startPos.x) * Mathf.Rad2Deg;

        // 5. 設定線條的位置(放在兩點中間)、旋轉和長度
        lineRect.position = startPos + (endPos - startPos) / 2f;
        lineRect.rotation = Quaternion.Euler(0, 0, angle);
        lineRect.sizeDelta = new Vector2(distance, 10f); // 10f 是線條粗細
    }
}