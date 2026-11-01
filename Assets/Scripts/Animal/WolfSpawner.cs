using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class WolfSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject wolfObject;      
    public Transform spawnPoint;       
    public float spawnInterval = 60f;  

    private bool isServer;

    void Start()
    {
        isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

        if (isServer)
        {
            if (wolfObject != null) wolfObject.SetActive(false);
            StartCoroutine(SpawnLoop());
        }
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);
            
            if (isServer) SpawnWolf();
        }
    }

    void SpawnWolf()
    {
        if (wolfObject == null) return;


        if (spawnPoint != null)
        {
            wolfObject.transform.position = spawnPoint.position;
        }

        wolfObject.SetActive(true);

        WolfAI wolfAI = wolfObject.GetComponent<WolfAI>();
        if (wolfAI != null)
        {
            wolfAI.ResetWolf(); 
        }
        WolfWarningUI warningUI = FindObjectOfType<WolfWarningUI>();
        if (warningUI != null)
        {
            warningUI.ShowWarningOnce();
        }
    }
}