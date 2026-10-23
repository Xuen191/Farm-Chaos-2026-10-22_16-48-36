using UnityEngine;

public class PortObject : MonoBehaviour
{
    public int portNumber = 1;

    [Header("Visuals")]
    public Material normalMat;
    public Material connectedMat;
    public Material wrongMat;

    private Renderer rend;
    private bool isConnected = false;

    void Start()
    {
        rend = GetComponent<Renderer>();
        rend.material = normalMat;
    }

    public void MarkConnected()
    {
        isConnected = true;
        rend.material = connectedMat;
    }

    public void FlashWrong()
    {
        StartCoroutine(FlashRoutine());
    }

    System.Collections.IEnumerator FlashRoutine()
    {
        rend.material = wrongMat;
        yield return new WaitForSeconds(0.3f);
        if (!isConnected) rend.material = normalMat;
    }
}