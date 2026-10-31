using UnityEngine;
using System.Collections;

public class RopePickup : Interactable
{
    [Header("Settings")]
    public float respawnTime = 10f;
    private bool isPickedUp = false;
    
    private MeshRenderer[] meshRenderers;
    private Collider[] colliders;

    [Header("Audio")]
    public AudioClip pickupSound;

    void Start()
    {
        hintMessage = "Pick up Rope"; 
        
        meshRenderers = GetComponentsInChildren<MeshRenderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void Interact()
    {
        if (isPickedUp) return;

        if (Inventory.Instance != null)
        {
            bool alreadyHasRope = false;
            for (int i = 0; i < Inventory.Instance.slots.Length; i++)
            {
                if (!Inventory.Instance.slots[i].IsEmpty && Inventory.Instance.slots[i].item == "Rope")
                {
                    alreadyHasRope = true;
                    break;
                }
            }

            if (pickupSound != null) 
{
    AudioSource.PlayClipAtPoint(pickupSound, transform.position);
}

            if (!alreadyHasRope)
            {
                Sprite icon = Resources.Load<Sprite>("Icon/Rope");
                Inventory.Instance.Add("Rope", icon);
            }

            ItemBarUI barUI = FindObjectOfType<ItemBarUI>();
            if (barUI != null) barUI.Toast("Got a Rope! Use it to tie the Spy.");
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            RopeAbility ropeAbility = player.GetComponent<RopeAbility>();
            if (ropeAbility != null) ropeAbility.PickUpRope();
        }

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