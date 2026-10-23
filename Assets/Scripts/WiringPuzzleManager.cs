using UnityEngine;

public class WiringPuzzleManager : MonoBehaviour
{
    public static WiringPuzzleManager Instance;

    public PowerBox powerBox;          // drag the PowerBox here
    public WireObject[] allWires;      // drag both wires in

    void Awake()
    {
        Instance = this;
    }

    public void OnWireConnected()
    {
        foreach (var wire in allWires)
        {
            if (!wire.isConnected) return; // still one missing
        }

        // All wires connected — tell the power box
        Debug.Log("All wires connected!");
        if (powerBox != null)
            powerBox.SetWiresCorrect();
    }
}