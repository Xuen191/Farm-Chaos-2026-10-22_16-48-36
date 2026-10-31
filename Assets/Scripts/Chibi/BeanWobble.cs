// Farm Chaos - floppy, ragdoll-ish follow-through for the bean.
// On top of the normal walk/run animations, the head and arms lag behind and wobble like springs whenever
// the bean speeds up, stops, turns or lands - so it still walks normally, just floppier.
// Purely visual (no physics), and it works for other players online too (it only watches movement).
using UnityEngine;

public class BeanWobble : MonoBehaviour
{
    [Header("How floppy")]
    [Tooltip("Degrees of head tilt per m/s² of speeding up / slowing down / turning")]
    public float headAmount = 0.55f;
    public float armAmount = 1.1f;
    public float bodyAmount = 0.22f;
    [Tooltip("Springiness: lower = slower, wobblier")]
    public float stiffness = 55f;
    [Tooltip("Lower = wobbles longer")]
    public float damping = 6f;
    public float maxAngle = 30f;

    Transform head, spine, armL, armR;
    Animator anim;
    Quaternion restHead, restSpine, restArmL, restArmR;
    Vector3 lastPos, lastVel, acc;
    bool primed;
    Vector2 headS, headV, armS, armV, bodyS, bodyV;   // x = forward nod, y = side roll

    void Start()
    {
        var all = GetComponentsInChildren<Transform>(true);
        head = Find(all, "head"); spine = Find(all, "spine"); armL = Find(all, "arm.L"); armR = Find(all, "arm.R");
        anim = GetComponentInChildren<Animator>();
        restHead = Rest(head); restSpine = Rest(spine); restArmL = Rest(armL); restArmR = Rest(armR);
    }

    static Transform Find(Transform[] all, string n) { foreach (var t in all) if (t.name == n) return t; return null; }
    static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;
    static void Reset(Transform t, Quaternion q) { if (t != null) t.localRotation = q; }

    void LateUpdate()   // runs after the Animator, so we add on top of this frame's pose
    {
        // the wobble is added on top of the Animator's pose each frame. If nothing is animating the bones
        // (no animations hooked up), put them back first - otherwise the tilts pile up and the head spins round
        if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null)
        { Reset(head, restHead); Reset(spine, restSpine); Reset(armL, restArmL); Reset(armR, restArmR); }

        float dt = Mathf.Clamp(Time.deltaTime, 0.0001f, 0.05f);
        Vector3 vel = (transform.position - lastPos) / dt;
        lastPos = transform.position;
        if (!primed) { primed = true; lastVel = vel; return; }
        Vector3 rawAcc = (vel - lastVel) / dt; lastVel = vel;
        if (rawAcc.sqrMagnitude > 400f * 400f) rawAcc = Vector3.zero;          // teleports / respawns
        acc = Vector3.Lerp(acc, rawAcc, 1f - Mathf.Exp(-18f * dt));             // smooth out physics jitter

        Vector3 local = transform.InverseTransformDirection(acc);              // z = forward, x = right
        // things hanging off a body lag behind it: speed up -> tip back, brake -> tip forward,
        // swerve right -> swing left, land -> nod down
        Vector2 push = new Vector2(-local.z + Mathf.Max(0f, acc.y) * 0.35f, local.x);

        Spring(ref headS, ref headV, push * headAmount, dt);
        Spring(ref armS, ref armV, push * armAmount, dt);
        Spring(ref bodyS, ref bodyV, push * bodyAmount, dt);

        Vector3 right = transform.right, fwd = transform.forward;
        Apply(spine, bodyS, right, fwd);
        Apply(head, headS, right, fwd);
        Apply(armL, armS, right, fwd);
        Apply(armR, armS, right, fwd);
    }

    void Spring(ref Vector2 s, ref Vector2 v, Vector2 target, float dt)
    {
        target = Vector2.ClampMagnitude(target, maxAngle);
        v += ((target - s) * stiffness - v * damping) * dt;
        s += v * dt;
        s = Vector2.ClampMagnitude(s, maxAngle);
    }

    static void Apply(Transform bone, Vector2 s, Vector3 right, Vector3 fwd)
    {
        if (bone == null) return;
        bone.rotation = Quaternion.AngleAxis(s.x, right) * Quaternion.AngleAxis(s.y, fwd) * bone.rotation;
    }
}
