using UnityEngine;

public class WireObject : MonoBehaviour
{
    [Header("Config")]
    public string wireColor = "Red";
    public int requiredPort = 1;      // which port this wire must connect to

    [Header("Visuals")]
    public Material normalMat;
    public Material selectedMat;
    public Material wrongMat;
    public Material connectedMat;

    [HideInInspector] public bool isConnected = false;

    private Renderer rend;
    private Vector3 startPos;
    private Quaternion startRot;

    void Start()
    {
        rend = GetComponent<Renderer>();
        startPos = transform.position;
        startRot = transform.rotation;
        rend.material = normalMat;
    }

    public void SetSelected(bool on)
    {
        if (isConnected) return;
        rend.material = on ? selectedMat : normalMat;
    }

    public void ConnectTo(PortObject port)
    {
        isConnected = true;
        rend.material = connectedMat;

        // Snap the wire to the port position
        transform.position = port.transform.position;
        transform.rotation = port.transform.rotation;

        // Tell the puzzle manager to check if all wires are done
        WiringPuzzleManager.Instance.OnWireConnected();
    }

    public void FlashWrong()
    {
        StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        rend.material = wrongMat;
        yield return new WaitForSeconds(0.3f);
        rend.material = normalMat;
    }
}