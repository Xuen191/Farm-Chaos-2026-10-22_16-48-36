using UnityEngine;

public class PlayerItemUsage : MonoBehaviour
{
    [Header("Prefabs & References")]
    public GameObject bombPrefab;        // Drag your Bomb Prefab here
    public GameObject baitPrefab;        // Drag your Bait Prefab here
    public Transform dropPoint;          // The position to throw from (Usually the MainCamera)

    [Header("Settings")]
    public float throwForce = 500f;      // Force applied to the bomb
    public float effectRadius = 10f;     // Radius of the bomb (used for visual/debug)

    void Update()
    {
        // Keyboard tests (For PC debugging)
        if (Input.GetKeyDown(KeyCode.Alpha1)) UseBomb();
        if (Input.GetKeyDown(KeyCode.Alpha2)) UseBait("Chicken Feed");
        if (Input.GetKeyDown(KeyCode.Alpha3)) UseCoffee();
    }

    // 💥 1. Use Bomb
    public void UseBomb()
    {
        if (InventoryManager.Instance.bomb <= 0)
        {
            Debug.Log("❌ No bombs left!");
            return;
        }

        InventoryManager.Instance.UseItem("Bomb"); 
        Debug.Log("💥 Bomb thrown!");

        if (bombPrefab != null && dropPoint != null)
        {
            // Spawn the bomb
            GameObject bomb = Instantiate(bombPrefab, dropPoint.position + dropPoint.forward, dropPoint.rotation);
            Rigidbody rb = bomb.GetComponent<Rigidbody>();
            if (rb != null) rb.AddForce(dropPoint.forward * throwForce);

            // Destroy it after 3 seconds
            Destroy(bomb, 3f);
            
            // Optional: If you want your teammate to handle the explosion, just leave it here.
            Debug.Log("Bomb will explode in 3 seconds. (Team member's script should handle the explosion logic)");
        }
    }

    // 🌾 2. Use Bait
    public void UseBait(string itemName)
    {
        if (InventoryManager.Instance.GetItemCount(itemName) <= 0)
        {
            Debug.Log("❌ No " + itemName + " left!");
            return;
        }

        InventoryManager.Instance.UseItem(itemName);
        Debug.Log("🌾 Bait dropped: " + itemName);

        if (baitPrefab != null && dropPoint != null)
        {
            // Spawn a physical bait object on the ground
            Vector3 baitPosition = dropPoint.position + dropPoint.forward * 3f;
            Instantiate(baitPrefab, baitPosition, Quaternion.identity);
        }
    }

    // ☕ 3. Drink Coffee
    public void UseCoffee()
    {
        if (InventoryManager.Instance.coffee <= 0)
        {
            Debug.Log("❌ No coffee left!");
            return;
        }

        InventoryManager.Instance.UseItem("Coffee");
        Debug.Log("☕ Drank coffee! Speed increased!");
        StartCoroutine(SpeedBoostRoutine());
    }

    // ☕ Speed Boost Logic
    private System.Collections.IEnumerator SpeedBoostRoutine()
    {
        // PlayerController is your movement script, right? If your teammate made it, make sure the name matches.
        PlayerController pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            float originalSpeed = pc.speed;
            pc.speed *= 1.5f; 
            yield return new WaitForSeconds(10f); 
            pc.speed = originalSpeed; 
            Debug.Log("Coffee effect ended.");
        }
    }
}