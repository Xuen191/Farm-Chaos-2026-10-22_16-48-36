using UnityEngine;

public class FusePickup : Interactable
{
    public static bool playerHasFuse = false;

    void Start() { hintMessage = "Press E to pick up fuse"; }

    public override void Interact()
    {
        playerHasFuse = true;
        Debug.Log("Fuse picked up!");
        Destroy(gameObject); // remove from world
    }
}