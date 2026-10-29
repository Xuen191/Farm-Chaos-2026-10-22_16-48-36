using UnityEngine;
using System.Collections;

public class RopePickup : Interactable
{
    [Header("Settings")]
    public float respawnTime = 10f; // 繩子多久後重生
    private bool isPickedUp = false;
    
    private MeshRenderer[] meshRenderers;
    private Collider[] colliders;

    void Start()
    {
        // 💡 改成簡短的提示文字
        hintMessage = "Pick up Rope"; 
        
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void Interact()
    {
        if (isPickedUp) return;

        Debug.Log("🪢 撿起了繩子！");

        isPickedUp = true;
        SetVisuals(false);

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(respawnTime);
        isPickedUp = false;
        SetVisuals(true);
    }

    private void SetVisuals(bool show)
    {
        foreach (var mr in meshRenderers) mr.enabled = show;
        foreach (var col in colliders) col.enabled = show;
    }
}