using UnityEngine;
using TMPro;

public class StickAttack : MonoBehaviour
{
    [Header("Attack Settings")]
    public float attackRange = 2f;      
    public float attackRadius = 1.5f;   
    public LayerMask enemyLayers;       

    [Header("Visual Feedback")]
    public float lungeForce = 5f;       
    public GameObject hitEffectPrefab; 

    [Header("Audio Feedback")]
    public AudioClip swingSound; 
    public AudioClip hitSound;   

    [Header("Status")]
    public bool hasStick = false;

    [Header("UI Feedback")]
    public TMP_Text hintText; 

    public void PickUpStick()
    {
        hasStick = true;
        Debug.Log("🪵 玩家獲得木棍！");
    }

    public void HitAttack()
    {
        if (!hasStick) return;
        
        Debug.Log("⚡ 揮舞木棍攻擊！");

        // 💡 播放揮舞音效
        if (swingSound != null)
        {
            // 建立一個暫時的喇叭來播放音效 (這樣保證聽得到)
            GameObject tempAudio = new GameObject("TempAudio");
            tempAudio.transform.position = transform.position;
            AudioSource source = tempAudio.AddComponent<AudioSource>();
            source.clip = swingSound;
            source.Play();
            Destroy(tempAudio, swingSound.length);
        }

        // 💡 衝刺
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) rb.AddForce(transform.forward * lungeForce, ForceMode.Impulse);

        // 💡 攻擊判定
        Vector3 attackPos = transform.position + transform.forward * attackRange;
        Collider[] hitEnemies = Physics.OverlapSphere(attackPos, attackRadius, enemyLayers);

        Debug.Log($"🔍 打到了 {hitEnemies.Length} 個東西。");

        foreach (Collider enemy in hitEnemies)
        {
            Debug.Log("✅ 擊中了：" + enemy.name);
            
            // 💡 播放擊中音效
            if (hitSound != null)
            {
                GameObject tempAudioHit = new GameObject("TempAudioHit");
                tempAudioHit.transform.position = enemy.transform.position;
                AudioSource source = tempAudioHit.AddComponent<AudioSource>();
                source.clip = hitSound;
                source.Play();
                Destroy(tempAudioHit, hitSound.length);
            }

            // 💡 特效
            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
                Destroy(effect, 1f); 
            }

             WolfAI wolf = enemy.GetComponent<WolfAI>();
            if (wolf != null)
            {
                ScoreKeeper.Add(5, "Bonk!");
                if (wolf.Hit()) ScoreKeeper.Add(30, "Wolf scared off!");   // online friends: the host decides and sends the +30
            }
        }
    }
}