using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Gate Settings")]
    public Transform leftGate;   
    public Transform rightGate;  
    
    public float slideDistance = 3f; // 門往兩側滑開的距離
    public float speed = 3f;

    private Vector3 leftClosedPos;
    private Vector3 rightClosedPos;
    private bool isOpening = false;
    private bool isClosing = false;

    void Start()
    {
        // 記錄兩扇門一開始的關閉位置
        if (leftGate != null) leftClosedPos = leftGate.localPosition;
        if (rightGate != null) rightClosedPos = rightGate.localPosition;

        // 💡 讓門在一開始遊戲時，就直接處於「打開」的狀態
        if (leftGate != null) leftGate.localPosition = leftClosedPos + new Vector3(-slideDistance, 0, 0);
        if (rightGate != null) rightGate.localPosition = rightClosedPos + new Vector3(slideDistance, 0, 0);
    }
    
    void Update()
    {
        if (isOpening)
        {
            // 左門往左移，右門往右移
            MoveGate(leftGate, leftClosedPos + new Vector3(-slideDistance, 0, 0));
            MoveGate(rightGate, rightClosedPos + new Vector3(slideDistance, 0, 0));
        }
        else if (isClosing)
        {
            // 回到原本關閉的位置
            MoveGate(leftGate, leftClosedPos);
            MoveGate(rightGate, rightClosedPos);
        }
    }

    private void MoveGate(Transform gate, Vector3 targetPos)
    {
        if (gate != null)
        {
            gate.localPosition = Vector3.MoveTowards(gate.localPosition, targetPos, speed * Time.deltaTime);
        }
    }

    public void CloseGate()
    {
        isClosing = true;
        isOpening = false;
        Debug.Log("Gate is closing!");
    }

    public void OpenGate()
    {
        isOpening = true;
        isClosing = false;
        Debug.Log("Gate is opening!");
    }
}