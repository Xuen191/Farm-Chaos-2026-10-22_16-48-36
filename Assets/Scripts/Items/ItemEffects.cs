using System.Collections;
using System.Collections.Generic; 
using UnityEngine;

public class ItemEffects : MonoBehaviour
{
    [Header("Baits (Food)")]
    public GameObject chickenFeedPrefab;
    public GameObject carrotPrefab; 
    public GameObject freshHayPrefab;
    public float foodThrowForce = 200f;
    
    [Header("Bait Settings")]
    public int maxAnimalsAttracted = 3; 
    public float baitRadius = 15f;      

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
    
    private float nextWhistleTime = 0f;

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

    public bool Use(string item, out string message)
    {
        var p = Player;
        message = "Can't use that here."; 
        
        if (p == null) { message = "No player!"; return false; }

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
                if (foodDropSound != null) AudioSource.PlayClipAtPoint(foodDropSound, p.transform.position);
                ThrowFood(chickenFeedPrefab, p, "Chicken Feed"); 
                message = "Dropped chicken feed!";
                return true;

            case "Carrot":
                if (foodDropSound != null) AudioSource.PlayClipAtPoint(foodDropSound, p.transform.position);
                ThrowFood(carrotPrefab, p, "Carrot");
                message = "Dropped carrot!";
                return true;

            case "Fresh Hay":
                if (foodDropSound != null) AudioSource.PlayClipAtPoint(foodDropSound, p.transform.position);
                ThrowFood(freshHayPrefab, p, "Fresh Hay");
                message = "Dropped fresh hay!";
                return true;

            case "Meaty Bone":
                if (meatDropSound != null) AudioSource.PlayClipAtPoint(meatDropSound, p.transform.position);
                
                if (meatyBonePrefab != null)
                {
                    var cam = Camera.main;
                    Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : p.transform.forward;
                    Vector3 spawnPos = p.transform.position + fwd * 2f + Vector3.up * 0.5f;
                    
                    GameObject bone = Instantiate(meatyBonePrefab, spawnPos, Quaternion.identity);
                    
                    Rigidbody rb = bone.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        Vector3 throwDir = (fwd + Vector3.up * 0.3f).normalized;
                        rb.AddForce(throwDir * meatThrowForce);
                    }

                    DroppedFood pickupScript = bone.AddComponent<DroppedFood>();
                    pickupScript.itemName = "Meaty Bone"; 
                    if (bone.GetComponent<Collider>() == null) bone.AddComponent<BoxCollider>();

                    WolfAI[] wolves = FindObjectsByType<WolfAI>(FindObjectsSortMode.None);
                    foreach (var wolf in wolves)
                    {
                        wolf.DistractedByBone(bone.transform);
                    }
                }
                
                message = "Dropped meaty bone!";
                return true;

            case "Magic Whistle":
                if (Time.time < nextWhistleTime)
                {
                    float timeLeft = Mathf.Ceil(nextWhistleTime - Time.time);
                    message = "Whistle cooling down... (" + timeLeft + "s)";
                    return false;
                }

                nextWhistleTime = Time.time + whistleCooldown;
                
                if (whistleSound != null) AudioSource.PlayClipAtPoint(whistleSound, p.transform.position);

                AnimalBrain[] allAnimals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
                
                if (allAnimals.Length == 0)
                {
                    message = "Whistle blown, but the farm is empty!";
                }
                else
                {
                    string attractedAnimals = "";
                    int count = 0;
                    
                    foreach (var animal in allAnimals)
                    {
                        animal.AttractedTo(p.transform.position); 
                        
                        if (attractedAnimals == "") attractedAnimals = animal.name;
                        else attractedAnimals += ", " + animal.name;
                        
                        count++;
                    }
                    message = "Whistle blown! Attracted " + count + " animals.";
                }
                return false; 

            case "Rope":
                RopeAbility rope = p.GetComponent<RopeAbility>();
                if (rope != null)
                {
                    string errorMsg;
                    bool success = rope.TryTieSpy(out errorMsg); 
                    
                    if (success) { message = "Tied the spy!"; return true; }
                    else { message = errorMsg; return false; }
                }
                message = "Can't use rope here.";
                return false;

            default:
                return false;
        }
    }

    private void ThrowFood(GameObject prefab, SpherePlayer p, string itemName)
    {
        if (prefab == null) return;

        var cam = Camera.main;
        Vector3 fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : p.transform.forward;
        Vector3 spawnPos = p.transform.position + fwd * 2f + Vector3.up * 0.5f;
        
        GameObject food = Instantiate(prefab, spawnPos, Quaternion.identity);
        
        Rigidbody rb = food.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 throwDir = (fwd + Vector3.up * 0.3f).normalized;
            rb.AddForce(throwDir * foodThrowForce);
        }

        AttractAnimals(spawnPos, itemName);

        DroppedFood pickupScript = food.AddComponent<DroppedFood>();
        pickupScript.itemName = itemName;

        if (food.GetComponent<Collider>() == null) food.AddComponent<BoxCollider>();
    }

    private void AttractAnimals(Vector3 position, string foodType)
    {
        AnimalBrain[] allAnimals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
        
        int count = 0;
        foreach (var animal in allAnimals)
        {

            if (foodType == "Carrot" && animal.kind != AnimalBrain.Kind.Bunny) continue;
            if (foodType == "Chicken Feed" && animal.kind != AnimalBrain.Kind.Chicken) continue;
            if (foodType == "Fresh Hay" && animal.kind != AnimalBrain.Kind.Sheep && animal.kind != AnimalBrain.Kind.Cow) continue;

            if (count >= maxAnimalsAttracted) break; 

            animal.AttractedTo(position);
            
            count++;
        }

        if (count == 0)
        {
            Debug.LogWarning("⚠️ 找不到任何動物可以吸引！");
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

        if (wearGogglesSound != null) AudioSource.PlayClipAtPoint(wearGogglesSound, p.transform.position);

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

        Collider[] hitEnemies = Physics.OverlapSphere(at, bombRadius + 1f);
        foreach (var enemy in hitEnemies)
        {
            WolfAI wolf = enemy.GetComponent<WolfAI>();
            if (wolf != null)
            {
                Destroy(wolf.gameObject); 
            }
        }

        StartCoroutine(Poof(at));
    }

    IEnumerator Poof(Vector3 at)
    {
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