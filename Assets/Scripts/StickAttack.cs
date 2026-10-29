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
    private AudioSource audioSource; 

    [Header("Status")]
    public bool hasStick = false;

    [Header("UI Feedback")]
    public TMP_Text hintText; 

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }
    }

    public void PickUpStick()
    {
        hasStick = true;
        Debug.Log("🪵 玩家獲得木棍！可以使用 USE 按鈕攻擊了！");

        if (hintText != null)
        {
            StartCoroutine(ShowTutorialHint("🪵 獲得木棍！按下 USE 可以打擊敵人！"));
        }
    }

    private System.Collections.IEnumerator ShowTutorialHint(string message)
    {
        string originalText = hintText.text;
        hintText.text = message;
        hintText.color = Color.yellow; 
        yield return new WaitForSeconds(3f);
        
        if (hintText.text == message) 
        {
            hintText.text = originalText;
            hintText.color = Color.white;
        }
    }

    public void HitAttack()
    {
        if (!hasStick)
        {
            Debug.Log("❌ 你沒有木棍，無法攻擊！");
            return; 
        }
        
        Debug.Log("⚡ 揮舞木棍攻擊！");

        if (audioSource != null && swingSound != null)
            audioSource.PlayOneShot(swingSound);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.AddForce(transform.forward * lungeForce, ForceMode.Impulse);

        Vector3 attackPos = transform.position + transform.forward * attackRange;
        Collider[] hitEnemies = Physics.OverlapSphere(attackPos, attackRadius, enemyLayers);

        foreach (Collider enemy in hitEnemies)
        {
            Debug.Log("✅ 擊中了：" + enemy.name);
            
            if (audioSource != null && hitSound != null)
                audioSource.PlayOneShot(hitSound);

            if (hitEffectPrefab != null)
            {
                GameObject effect = Instantiate(hitEffectPrefab, enemy.transform.position, Quaternion.identity);
                Destroy(effect, 1f); 
            }
        }
    }
}