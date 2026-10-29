using UnityEngine;
using System.Collections;

public class TorchPickup : Interactable
{
    private bool isPickedUp = false;
    private MeshRenderer[] meshRenderers;
    private Collider[] colliders;

    void Start()
    {
        // 💡 配合手機 UI，改成簡短的提示
        hintMessage = "Pick up Torch"; 
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void Interact()
    {
        if (isPickedUp) return;

        // 💡 改成抓取玩家（SpherePlayer）的位置，而不是相機
        GameObject player = GameObject.FindWithTag("Player"); 
        if (player == null) 
        {
            // 如果找不到 Tag，就用 ProximityInteractor 所在的物件
            var interactor = FindObjectOfType<ProximityInteractor>();
            if (interactor != null) player = interactor.gameObject;
        }

        if (player != null)
        {
            // 生成手電筒模型在玩家身上
            GameObject newModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            newModel.name = "MyHandheldTorchModel";
            newModel.transform.SetParent(player.transform);
            // 因為是球體，我們把它放在球體正上方一點點
            newModel.transform.localPosition = new Vector3(0, 0.5f, 0); 
            newModel.transform.localRotation = Quaternion.Euler(90, 0, 0); 
            newModel.transform.localScale = new Vector3(0.05f, 0.12f, 0.05f); 
            Destroy(newModel.GetComponent<Collider>()); 

            Renderer modelRenderer = newModel.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                Material unlitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                unlitMat.SetColor("_BaseColor", Color.black); 
                modelRenderer.material = unlitMat;
            }

            // 生成手電筒的燈光
            GameObject newLightObj = new GameObject("MyFlashlightLight");
            newLightObj.transform.SetParent(player.transform);
            newLightObj.transform.localPosition = new Vector3(0, 0.5f, 0.5f); // 放在球體前方
            newLightObj.transform.localRotation = Quaternion.identity; 

            Light spot = newLightObj.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.range = 50f;        
            spot.intensity = 3f;   
            spot.spotAngle = 60f; // 改成 60 度比較像手電筒，180 度會像探照燈
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