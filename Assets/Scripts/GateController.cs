using UnityEngine;

public class GateController : MonoBehaviour
{
    [Header("Gate Settings")]
    public Transform leftGate;   
    public Transform rightGate;  
    
    [Header("Rotation Settings")]
    public float openAngle = 90f;    // 門打開時旋轉的角度 (90度就是往外推開)
    public float speed = 100f;       // 門旋轉的速度

    private Quaternion leftClosedRot;
    private Quaternion rightClosedRot;
    private Quaternion leftOpenRot;
    private Quaternion rightOpenRot;

    private bool isOpening = false;
    private bool isClosing = false;

    void Start()
    {
        if (leftGate != null) 
        {
            leftClosedRot = leftGate.localRotation;
            leftOpenRot = leftClosedRot * Quaternion.Euler(0, -openAngle, 0); 
        }
        
        if (rightGate != null) 
        {
            rightClosedRot = rightGate.localRotation;
            rightOpenRot = rightClosedRot * Quaternion.Euler(0, openAngle, 0); 
        }

        if (leftGate != null) leftGate.localRotation = leftOpenRot;
        if (rightGate != null) rightGate.localRotation = rightOpenRot;
    }
    void Update()
    {
        if (isOpening)
        {
            if (leftGate != null) leftGate.localRotation = Quaternion.RotateTowards(leftGate.localRotation, leftOpenRot, speed * Time.deltaTime);
            if (rightGate != null) rightGate.localRotation = Quaternion.RotateTowards(rightGate.localRotation, rightOpenRot, speed * Time.deltaTime);
        }
        else if (isClosing)
        {
            if (leftGate != null) leftGate.localRotation = Quaternion.RotateTowards(leftGate.localRotation, leftClosedRot, speed * Time.deltaTime);
            if (rightGate != null) rightGate.localRotation = Quaternion.RotateTowards(rightGate.localRotation, rightClosedRot, speed * Time.deltaTime);
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