// Farm Chaos - hides a building's roof while the player is inside, so the top-down camera can see in.
// The roof still casts its shadow, so the inside stays nicely shaded.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Collider))]
public class RoofHider : MonoBehaviour
{
    public Transform roof;
    public string playerTag = "Player";

    readonly HashSet<Collider> inside = new HashSet<Collider>();
    Renderer[] renderers;

    void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
        renderers = roof != null ? roof.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
    }

    bool IsPlayer(Collider c) =>
        c.CompareTag(playerTag) || (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag(playerTag));

    void OnTriggerEnter(Collider c) { if (IsPlayer(c)) { inside.Add(c); Apply(); } }
    void OnTriggerExit(Collider c) { if (inside.Remove(c)) Apply(); }

    void Apply()
    {
        bool hide = inside.Count > 0;
        foreach (var r in renderers)
            if (r != null) r.shadowCastingMode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
    }
}
