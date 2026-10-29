// Farm Chaos - what each shop item does when you use it from the item bar.
using System.Collections;
using System.Linq;
using UnityEngine;

public class ItemEffects : MonoBehaviour
{
    [Header("Coffee")]
    public float coffeeSpeedBoost = 1.5f;
    public float coffeeSeconds = 10f;
    
    [Header("Night Vision Goggles")]
    public float goggleSeconds = 25f;
    public float goggleLightRange = 18f;
    public TMPro.TextMeshProUGUI nightVisionTimerText;
    
    [Header("Bomb")]
    public float bombThrowDistance = 4f;
    public float bombRadius = 6f;
    public float bombForce = 9f;
    public Material poofMaterial;

    [Header("Meaty Bone")]
    public GameObject meatyBonePrefab; 
    public float meatThrowForce = 400f;

    [Header("Magic Whistle")]
    public float whistleRadius = 15f;
    public float whistleCooldown = 20f; 
    public GameObject whistleEffectPrefab;
    
    private float nextWhistleTime = 0f;

    // 💡 新增：音效設定區
    [Header("Audio Settings")]
    public AudioClip bombExplodeSound;
    public AudioClip foodDropSound;
    public AudioClip whistleSound;
    public AudioClip meatDropSound;
    public AudioClip wearGogglesSound;
    public AudioClip coffeeDrinkSound;

    public static ItemEffects Instance { get; private set; }
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    SpherePlayer Player => FindAnyObjectByType<SpherePlayer>();

    /// <summary>Use an item. message = short text to show; returns whether it got used up.</summary>
    public bool Use(string item, out string message)
    {
        var p = Player;
        
        // 💡 先給 message 一個預設值，防止報錯
        message = "Can't use that here."; 
        
        if (p == null) 
        { 
            message = "No player!"; 
            return false; 
        }

        switch (item)
        {
            case "Coffee":
                StartCoroutine(Coffee(p));
                message = "Coffee! Zoom zoom!";
                return true;

            case "Night Vision Goggles":
                StartCoroutine(Goggles(p));
                message = "Night vision on!";
                return true;

            case "Bomb":
                Bomb(p);
                message = "BOOM!";
                return true;

            case "Chicken Feed":
            case "Pig Slop":
            case "Fresh Hay":
                if (foodDropSound != null) AudioSource.PlayClipAtPoint(foodDropSound, p.transform.position);
                message = "Dropped food!";
                return true;

            case "Meaty Bone":
                if (meatDropSound != null) AudioSource.PlayClipAtPoint(meatDropSound, p.transform.position);
                message = "Dropped meaty bone!";
                return true;

            case "Magic Whistle":
                // 💡 檢查冷卻時間
                if (Time.time < nextWhistleTime)
                {
                    float timeLeft = Mathf.Ceil(nextWhistleTime - Time.time);
                    message = "Whistle cooling down... (" + timeLeft + "s)";
                    return false; // 回傳 false，不消耗數量
                }

                nextWhistleTime = Time.time + whistleCooldown;
                
                if (whistleSound != null) AudioSource.PlayClipAtPoint(whistleSound, p.transform.position);

                if (whistleEffectPrefab != null)
                {
                    GameObject effect = Instantiate(whistleEffectPrefab, p.transform.position, Quaternion.identity);
                    effect.transform.SetParent(p.transform);
                    Destroy(effect, 2f);
                }

                Collider[] animals = Physics.OverlapSphere(p.transform.position, whistleRadius, LayerMask.GetMask("Animal"));
                foreach (var animal in animals)
                {
                    Debug.Log("🐑 動物被口哨吸引了：" + animal.name);
                }

                message = "Whistle blown!";
                return false;

            default:
                return false;
        }
    }

    float coffeeUntil = -1f, goggleUntil = -1f;

    IEnumerator Coffee(SpherePlayer p)
    {
        if (Time.time < coffeeUntil) { coffeeUntil += coffeeSeconds; yield break; }
        coffeeUntil = Time.time + coffeeSeconds;
        float baseSpeed = p.moveSpeed, baseAccel = p.acceleration;
        p.moveSpeed = baseSpeed * coffeeSpeedBoost;
        p.acceleration = baseAccel * coffeeSpeedBoost;
        while (Time.time < coffeeUntil) yield return null;
        if (p != null) { p.moveSpeed = baseSpeed; p.acceleration = baseAccel; }
    }

    IEnumerator Goggles(SpherePlayer p)
    {
        if (Time.time < goggleUntil) { goggleUntil += goggleSeconds; yield break; }
        goggleUntil = Time.time + goggleSeconds + 0.5f;

        if (nightVisionTimerText != null) 
        {
            nightVisionTimerText.gameObject.SetActive(true);
            nightVisionTimerText.color = Color.green;
        }

        Color originalAmbient = RenderSettings.ambientLight;
        RenderSettings.ambientLight = new Color(0.5f, 0.8f, 0.5f); 

        while (Time.time < goggleUntil)
        {
            float timeLeft = goggleUntil - Time.time;
            

            if (nightVisionTimerText != null)
            {
                nightVisionTimerText.text = "Night Vision: " + Mathf.CeilToInt(timeLeft) + "s";
                
                if (timeLeft <= 5f) nightVisionTimerText.color = Color.red;
            }

            yield return null;
        }

        RenderSettings.ambientLight = originalAmbient;
        if (nightVisionTimerText != null) nightVisionTimerText.gameObject.SetActive(false);
    }

    void Bomb(SpherePlayer p)
    {
        var cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
        Vector3 at = p.transform.position + fwd * bombThrowDistance;
        if (Physics.Raycast(at + Vector3.up * 5f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;

        foreach (var c in Physics.OverlapSphere(at, bombRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var rb = c.attachedRigidbody;
            if (rb == null || rb.isKinematic) continue;
            float scale = rb.GetComponent<SpherePlayer>() != null ? 0.35f : 1f;
            rb.AddExplosionForce(bombForce * scale, at, bombRadius, 1.2f, ForceMode.VelocityChange);
        }
        StartCoroutine(Poof(at));
    }

    IEnumerator Poof(Vector3 at)
    {
        // 💡 播放爆炸音效
        if (bombExplodeSound != null) AudioSource.PlayClipAtPoint(bombExplodeSound, at);

        var flash = new GameObject("Bomb_Flash").AddComponent<Light>();
        flash.transform.position = at + Vector3.up; flash.type = LightType.Point; flash.range = 10f; flash.color = new Color(1f, 0.8f, 0.5f); flash.shadows = LightShadows.None;
        GameObject puff = null; MaterialPropertyBlock mpb = null; Renderer pr = null; Color pc = Color.white;
        if (poofMaterial != null)
        {
            puff = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(puff.GetComponent<Collider>());
            puff.transform.position = at + Vector3.up * 0.1f; puff.transform.rotation = Quaternion.Euler(90, 0, 0);
            pr = puff.GetComponent<Renderer>(); pr.sharedMaterial = poofMaterial; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mpb = new MaterialPropertyBlock(); pc = poofMaterial.GetColor("_BaseColor");
        }
        float t = 0f, dur = 0.6f;
        while (t < dur)
        {
            t += Time.deltaTime; float k = t / dur;
            flash.intensity = Mathf.Lerp(8f, 0f, k);
            if (puff != null)
            {
                puff.transform.localScale = Vector3.one * Mathf.Lerp(1f, bombRadius * 2.2f, Mathf.Sqrt(k));
                pr.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", new Color(pc.r * 1.6f, pc.g * 1.4f, pc.b, pc.a * 1.6f * (1f - k))); pr.SetPropertyBlock(mpb);
            }
            yield return null;
        }
        Destroy(flash.gameObject);
        if (puff != null) Destroy(puff);
    }
}