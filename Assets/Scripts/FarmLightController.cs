using UnityEngine;

public class FarmLightController : MonoBehaviour
{
    private Light myLight;

    void Awake()
    {
        myLight = GetComponent<Light>();
    }

    public void TurnOnLight()
    {
        if (myLight != null)
        {
            myLight.enabled = true;
            Debug.Log("💡 Light's on！");
        }
    }

    public void TurnOffLight()
    {
        if (myLight != null)
        {
            myLight.enabled = false;
            Debug.Log("🌑 Light's Off！");
        }
    }
}