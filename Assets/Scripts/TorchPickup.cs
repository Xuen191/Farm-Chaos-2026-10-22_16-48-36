using UnityEngine;
using System.Collections;

public class TorchPickup : Interactable
{
    private bool isPickedUp = false;
    private MeshRenderer[] meshRenderers;
    private Collider[] colliders;

    void Start()
    {
        hintMessage = "Press E to pick up Torch";
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void Interact()
    {
        if (isPickedUp) return;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            GameObject newModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            newModel.name = "MyHandheldTorchModel";
            newModel.transform.SetParent(mainCam.transform);
            newModel.transform.localPosition = new Vector3(0.25f, -0.25f, 0.4f); 
            newModel.transform.localRotation = Quaternion.Euler(90, 0, 0); 
            newModel.transform.localScale = new Vector3(0.05f, 0.12f, 0.05f); 
            Destroy(newModel.GetComponent<Collider>()); 

            Renderer modelRenderer = newModel.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                Material unlitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                unlitMat.SetColor("_BaseColor", new Color(0f, 0f, 0f)); 
                modelRenderer.material = unlitMat;
            }

            GameObject newLightObj = new GameObject("MyFlashlightLight");
            newLightObj.transform.SetParent(mainCam.transform);
            newLightObj.transform.localPosition = new Vector3(0.2f, -0.2f, 0.5f); 
            newLightObj.transform.localRotation = Quaternion.Euler(5f, -5f, 0); 

            Light spot = newLightObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 100f;        
            spot.intensity = 3f;   
            spot.spotAngle = 180f;    
            spot.color = new Color(1f, 0.95f, 0.8f); 
            spot.shadows = LightShadows.None;
        }

        isPickedUp = true;
        SetVisuals(false);

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(10f);
        isPickedUp = false;
        SetVisuals(true);
    }

    private void SetVisuals(bool show)
    {
        foreach (var mr in meshRenderers) mr.enabled = show;
        foreach (var col in colliders) col.enabled = show;
    }
}