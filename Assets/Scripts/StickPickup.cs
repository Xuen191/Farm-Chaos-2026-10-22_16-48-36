using UnityEngine;
using System.Collections;

public class StickPickup : Interactable
{
    private bool isPickedUp = false;
    private MeshRenderer[] meshRenderers;
    private Collider[] colliders;
    private GameObject playerObj;

    void Start()
    {
        hintMessage = "Pick up Stick"; 
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void Interact()
    {
        if (isPickedUp) return;

        playerObj = GameObject.FindWithTag("Player"); 
        if (playerObj == null) 
        {
            var interactor = FindObjectOfType<ProximityInteractor>();
            if (interactor != null) playerObj = interactor.gameObject;
        }

        if (playerObj != null)
        {
            // 生成木棍模型
            GameObject newModel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            newModel.name = "MyHandheldStickModel";
            newModel.transform.SetParent(playerObj.transform);
            newModel.transform.localPosition = new Vector3(0, 0.5f, 0); 
            newModel.transform.localRotation = Quaternion.Euler(90, 0, 0); 
            newModel.transform.localScale = new Vector3(0.05f, 0.3f, 0.05f); 
            Destroy(newModel.GetComponent<Collider>()); 

            Renderer modelRenderer = newModel.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                Material unlitMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                unlitMat.SetColor("_BaseColor", new Color(0.4f, 0.2f, 0.1f)); // 木頭色
                modelRenderer.material = unlitMat;
            }

            // 啟動攻擊能力
            StickAttack attack = playerObj.GetComponent<StickAttack>();
            if (attack != null) 
            {
                attack.PickUpStick(); 
            }

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