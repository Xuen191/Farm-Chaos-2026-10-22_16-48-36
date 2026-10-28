// Farm Chaos - what each shop item does when you use it from the item bar.
// Returns true if the item was used up.
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
    [Header("Bomb")]
    public float bombThrowDistance = 4f;
    public float bombRadius = 6f;
    public float bombForce = 9f;
    public Material poofMaterial;   // soft glow material (Mat_light_pool)

    public static ItemEffects Instance { get; private set; }
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    SpherePlayer Player => FindAnyObjectByType<SpherePlayer>();

    /// <summary>Use an item. message = short text to show; returns whether it got used up.</summary>
    public bool Use(string item, out string message)
    {
        var p = Player;
        switch (item)
        {
            case "Coffee":
                if (p == null) { message = "No player!"; return false; }
                StartCoroutine(Coffee(p));
                message = "Coffee! Zoom zoom!";
                return true;

            case "Night Vision Goggles":
                if (p == null) { message = "No player!"; return false; }
                StartCoroutine(Goggles(p));
                message = "Night vision on!";
                return true;

            case "Bomb":
                if (p == null) { message = "No player!"; return false; }
                Bomb(p);
                message = "BOOM!";
                return true;

            case "Chicken Feed":
            case "Pig Slop":
            case "Sheep Hay":
            case "Meaty Bone":
            case "Magic Whistle":
                message = "No animals to lure here yet!";
                return false;   // keep it for later

            default:
                message = "Can't use that here.";
                return false;
        }
    }

    float coffeeUntil = -1f, goggleUntil = -1f;

    IEnumerator Coffee(SpherePlayer p)
    {
        if (Time.time < coffeeUntil) { coffeeUntil += coffeeSeconds; yield break; }   // already buzzing: just last longer
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
        var l = p.GetComponentsInChildren<Light>().FirstOrDefault(x => x.type == LightType.Point);
        bool made = false;
        if (l == null)
        {
            var go = new GameObject("Goggles_Light"); go.transform.SetParent(p.transform, false);
            l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.75f, 1f, 0.8f); l.shadows = LightShadows.None;
            made = true;
        }
        float r0 = l.range, i0 = l.intensity; Color c0 = l.color;
        float t = 0f;
        while (t < 0.5f) { t += Time.deltaTime; l.range = Mathf.Lerp(r0, goggleLightRange, t / 0.5f); l.intensity = Mathf.Lerp(i0, Mathf.Max(i0, 3f), t / 0.5f); yield return null; }
        l.color = Color.Lerp(c0, new Color(0.75f, 1f, 0.8f), 0.5f);   // soft green tint
        while (Time.time < goggleUntil) yield return null;
        t = 0f;
        while (t < 1f && l != null) { t += Time.deltaTime; l.range = Mathf.Lerp(goggleLightRange, r0, t); l.intensity = Mathf.Lerp(Mathf.Max(i0, 3f), i0, t); yield return null; }
        if (l != null) { if (made) Destroy(l.gameObject); else { l.range = r0; l.intensity = i0; l.color = c0; } }
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
            float scale = rb.GetComponent<SpherePlayer>() != null ? 0.35f : 1f;   // just a little nudge for the player
            rb.AddExplosionForce(bombForce * scale, at, bombRadius, 1.2f, ForceMode.VelocityChange);
        }
        StartCoroutine(Poof(at));
    }

    IEnumerator Poof(Vector3 at)
    {
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
