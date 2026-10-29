using UnityEngine;

public class FusePickup : Interactable
{
    public static bool playerHasFuse = false;

    [Header("Audio Settings")]
    public AudioClip pickupSound;

    void Start() { hintMessage = "Press E to pick up fuse"; }

    public override void Interact()
    {
        playerHasFuse = true;
        Debug.Log("Fuse picked up!");

        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, Camera.main.transform.position);
        }
        Destroy(gameObject); // remove from world
    }
}