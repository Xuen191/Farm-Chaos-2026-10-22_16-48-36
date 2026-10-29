using UnityEngine;

public class FuseSpawner : MonoBehaviour
{
    public GameObject fusePrefab; // Drag your FusePickup prefab here in the Inspector

    void Start()
    {
        // Get all the child spawn points (Fuse_Spawn_1, 2, 3, etc.)
        int childCount = transform.childCount;
        
        if (childCount > 0 && fusePrefab != null)
        {
            // Pick a random spawn point
            int randomIndex = Random.Range(0, childCount);
            Transform spawnPoint = transform.GetChild(randomIndex);

            // Spawn the fuse at that point's location
            Instantiate(fusePrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}