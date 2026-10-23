using UnityEngine;

public class WireClickHandler : MonoBehaviour
{
    public float clickDistance = 5f;
    public LayerMask clickableLayers; // set to "Default" or a custom "Wiring" layer

    private WireObject selectedWire = null;

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickDistance, clickableLayers))
            return;

        // Did we click a wire?
        WireObject wire = hit.collider.GetComponent<WireObject>();
        if (wire != null)
        {
            SelectWire(wire);
            return;
        }

        // Did we click a port?
        PortObject port = hit.collider.GetComponent<PortObject>();
        if (port != null && selectedWire != null)
        {
            TryConnect(selectedWire, port);
        }
    }

    void SelectWire(WireObject wire)
    {
        // Deselect previous
        if (selectedWire != null)
            selectedWire.SetSelected(false);

        selectedWire = wire;
        selectedWire.SetSelected(true);
        Debug.Log("Selected wire: " + wire.wireColor);
    }

    void TryConnect(WireObject wire, PortObject port)
    {
        if (wire.isConnected) return;

        bool correct = wire.requiredPort == port.portNumber;

        if (correct)
        {
            wire.ConnectTo(port);
            port.MarkConnected();
            Debug.Log("Correct connection!");
        }
        else
        {
            wire.FlashWrong();
            port.FlashWrong();
            Debug.Log("Wrong connection!");
        }

        wire.SetSelected(false);
        selectedWire = null;
    }
}