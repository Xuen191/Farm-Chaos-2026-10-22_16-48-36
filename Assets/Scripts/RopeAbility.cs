using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class RopeAbility : MonoBehaviour
{
    [Header("Settings")]
    public float tieRadius = 6f;         
    public float tieDuration = 20f;      

    [Header("Visuals & Audio")]
    public Material ropeMaterial;        
    public AudioClip tieSound;           

    private bool hasRope = false;
    private bool isTying = false; 

    public void PickUpRope()
    {
        hasRope = true;
        isTying = false;
    }

    public bool TryTieSpy(out string errorMessage)
    {
        errorMessage = "";
        if (!hasRope) { errorMessage = "⚠️ Find a rope first!"; return false; }
        if (isTying) { errorMessage = "⚠️ Already tying someone!"; return false; }

        // Farm Chaos: tie up whoever is closest - right guess (the spy) = +100 and the spy loses 50,
        // wrong guess (a farmer) = -30. Only the host knows who the spy is, so it decides (NetPlayer.RequestTie).
        if (NetPlayer.Local != null)
        {
            NetPlayer closest = null; float best = tieRadius;
            foreach (var np in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
            {
                if (np == NetPlayer.Local || !np.IsSpawned) continue;
                float d = Vector3.Distance(np.transform.position, transform.position);
                if (d < best) { best = d; closest = np; }
            }
            if (closest == null) { errorMessage = "Nobody close enough to tie up!"; return false; }
            if (tieSound != null) AudioSource.PlayClipAtPoint(tieSound, closest.transform.position);
            NetPlayer.Local.RequestTie(closest.OwnerClientId, tieDuration);
            hasRope = false;                                   // one rope = one tie
            return true;
        }
        if (NetSession.Online || NetPlayer.Local == null) { errorMessage = "Nobody here to tie up!"; return false; }

        Collider[] allPlayers = Physics.OverlapSphere(transform.position, tieRadius);
        Transform targetSpy = null;

        // 💡 尋找間諜：比對 NetPlayer 的間諜 ID
        foreach (var col in allPlayers)
        {
            Transform root = col.transform.root; 
            NetworkObject netObj = root.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                // 如果這個人的 ClientID 等於伺服器指定的間諜 ID
                if (NetPlayer.SpyClientId != ulong.MaxValue && netObj.OwnerClientId == NetPlayer.SpyClientId)
                {
                    targetSpy = root; 
                    break;
                }
            }
        }

        if (targetSpy == null) { errorMessage = "⚠️ No spy nearby to tie!"; return false; }

        if (tieSound != null) AudioSource.PlayClipAtPoint(tieSound, targetSpy.position);

        NetworkObject targetNetObj = targetSpy.GetComponent<NetworkObject>();
        if (targetNetObj == null) { errorMessage = "⚠️ Target has no NetworkObject!"; return false; }

        // 💡 發送 RPC 給所有人，大家各自去抓那個 ID
        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
                TieSpyClientRpc(targetNetObj.OwnerClientId, tieDuration);
            else
                TieSpyServerRpc(targetNetObj.OwnerClientId, tieDuration);
        }
        else
        {
            StartTieVisuals(targetSpy.gameObject, tieDuration);
        }
        
        return true; 
    }

    [Rpc(SendTo.Server)]
    void TieSpyServerRpc(ulong spyClientId, float duration) 
    { 
        TieSpyClientRpc(spyClientId, duration); 
    }

    [Rpc(SendTo.Everyone)]
    void TieSpyClientRpc(ulong spyClientId, float duration)
    {
        // 💡 所有玩家的電腦都會收到這個指令，然後在場景中尋找那個 ClientID 的玩家
        foreach (var np in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
        {
            if (np.OwnerClientId == spyClientId)
            {
                GameObject target = np.gameObject;
                StartTieVisuals(target, duration);
                break;
            }
        }
    }

    void StartTieVisuals(GameObject target, float duration)
    {
        if (target.transform.Find("RopeVisual") != null) return; 

        // 💡 1. 暴力停用他的移動腳本（絕對定住！）
        SpherePlayer spyMove = target.GetComponent<SpherePlayer>();
        if (spyMove != null) 
        {
            spyMove.enabled = false; // 直接關閉！
        }

        // 💡 2. 鎖住他的物理
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null) 
        { 
            rb.linearVelocity = Vector3.zero; 
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true; 
        }

        // 💡 3. 生成視覺繩子
        GameObject ropeVisual = new GameObject("RopeVisual");
        ropeVisual.transform.SetParent(target.transform);
        ropeVisual.transform.localPosition = Vector3.zero; 

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(ring.GetComponent<Collider>()); 
        ring.transform.SetParent(ropeVisual.transform, false);
        ring.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f); 
        ring.transform.localPosition = Vector3.zero; 
        
        Renderer rend = ring.GetComponent<Renderer>();
        Material mat = ropeMaterial != null ? ropeMaterial : new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.4f, 0.2f, 0.1f) };
        rend.material = mat;

        // 💡 4. 啟動計時器，時間到自動解除
        StartCoroutine(UnlockAfterTime(target, ropeVisual, duration));
    }

    IEnumerator UnlockAfterTime(GameObject target, GameObject rope, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (rope != null) Destroy(rope);

        // 💡 解除物理鎖與恢復腳本
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
        
        SpherePlayer spyMove = target.GetComponent<SpherePlayer>();
        if (spyMove != null) spyMove.enabled = true; // 恢復移動
    }
}