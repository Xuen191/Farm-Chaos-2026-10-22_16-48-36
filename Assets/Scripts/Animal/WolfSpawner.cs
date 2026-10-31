using UnityEngine;
using System.Collections;

public class WolfSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject wolfObject;      // 💡 把場景中的 Wolf 拖到這裡
    public Transform spawnPoint;       // 💡 把出生點拖到這裡
    public float spawnInterval = 60f;  // 💡 每 60 秒出現一次

    void Start()
    {
        // 遊戲一開始先隱藏狼
        if (wolfObject != null) wolfObject.SetActive(false);
        
        // 啟動計時器
        StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            // 等待 60 秒
            yield return new WaitForSeconds(spawnInterval);

            // 讓狼出現
            SpawnWolf();
        }
    }

    void SpawnWolf()
    {
        if (wolfObject == null) return;

        Debug.Log("🐺 狼出現了！");

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

        // 💡 狼出現時，呼叫警告 UI
        WolfWarningUI warningUI = FindObjectOfType<WolfWarningUI>();
        if (warningUI != null)
        {
            warningUI.ShowWarningOnce();
        }
    }
}