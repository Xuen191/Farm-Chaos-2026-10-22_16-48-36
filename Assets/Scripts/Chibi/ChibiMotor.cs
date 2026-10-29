// Farm Chaos - makes the chibi walk/run/jump along with the rolling ball underneath.
// The ball still does all the physics (invisible); the chibi stands on it, faces where you're going,
// and plays Idle / Walk / Run / Jump. Also does little reactions:
//   power cut -> surprised face, running -> determined, standing still a while -> sleepy,
//   V = wave, C = cheer (emotes).
// Works for your own ball AND for other players' balls online (reads their movement).
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ChibiMotor : MonoBehaviour
{
    public Animator animator;
    public ChibiLook look;
    [Tooltip("Ball radius - the chibi's feet go at the bottom of the ball")]
    public float ballRadius = 0.6f;
    public float turnSpeed = 720f;
    [Tooltip("Rotate the model if it faces the wrong way")]
    public float yawOffset = 0f;
    public bool listenForEmoteKeys = true;
    /// <summary>Wardrobe mode: stand still and turn to face the camera.</summary>
    [System.NonSerialized] public bool posing;

    Transform ball;
    SpherePlayer player;     // set for your own ball
    Rigidbody rb;
    Vector3 lastPos;
    float yaw, idleTime, smoothSpeed;
    bool wasGrounded = true;

    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int GroundedId = Animator.StringToHash("Grounded");
    static readonly int WaveId = Animator.StringToHash("Wave");
    static readonly int CheerId = Animator.StringToHash("Cheer");
    static readonly int FallId = Animator.StringToHash("Fall");

    void Start()
    {
        ball = transform.parent;
        if (ball != null)
        {
            player = ball.GetComponent<SpherePlayer>();
            rb = ball.GetComponent<Rigidbody>();
            // hide the ball's own look, keep its collider
            var mr = ball.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
            var body = ball.Find("Body"); if (body != null && body.GetComponent<Renderer>() != null) body.GetComponent<Renderer>().enabled = false;
            lastPos = ball.position;
            yaw = transform.eulerAngles.y;
        }
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (look == null) look = GetComponentInChildren<ChibiLook>();
        if (player != null) ElectricGate.PowerChanged += OnPower;
        // another player's ball online: dress them in their party colour
        var np = ball != null ? ball.GetComponent<NetPlayer>() : null;
        if (np != null && look != null)
        {
            if (np.HasLook) np.ReapplyLook();                                 // already know their look
            else { look.data.outfit = (int)(np.OwnerClientId % 5); look.Apply(); }   // until their look arrives: party colour
        }
        listenForEmoteKeys &= player != null;
    }

    void OnDestroy() { ElectricGate.PowerChanged -= OnPower; }

    void OnPower(bool on)
    {
        if (look == null) return;
        if (!on) look.SetExpression(ChibiLook.Face.Surprised, 2.5f);
        else { look.SetExpression(ChibiLook.Face.Happy, 2.5f); if (animator != null) animator.SetTrigger(CheerId); }
    }

    void LateUpdate()
    {
        if (ball == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);

        // movement of the ball (works for remote players too)
        Vector3 vel = rb != null && !rb.isKinematic ? rb.linearVelocity : (ball.position - lastPos) / dt;
        lastPos = ball.position;
        Vector3 flat = new Vector3(vel.x, 0f, vel.z);
        float speed = flat.magnitude;
        smoothSpeed = Mathf.Lerp(smoothSpeed, speed, 1f - Mathf.Exp(-10f * dt));

        bool grounded = player != null ? player.IsGrounded
            : Physics.Raycast(ball.position, Vector3.down, ballRadius + 0.2f, ~0, QueryTriggerInteraction.Ignore);

        if (posing)
        {
            speed = 0f; smoothSpeed = Mathf.Lerp(smoothSpeed, 0f, 1f - Mathf.Exp(-10f * dt));
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 to = cam.transform.position - ball.position; to.y = 0f;
                if (to.sqrMagnitude > 0.001f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg - yawOffset, turnSpeed * dt);
            }
            idleTime = 0f;
        }
        // stand on the ball, face the way we're going (ignores the ball's rolling)
        if (speed > 0.4f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg, turnSpeed * dt);
        transform.SetPositionAndRotation(ball.position - Vector3.up * ballRadius, Quaternion.Euler(0f, yaw + yawOffset, 0f));

        if (animator != null)
        {
            animator.SetFloat(SpeedId, smoothSpeed);
            animator.SetBool(GroundedId, grounded);
        }

        // little face reactions
        if (look != null)
        {
            if (wasGrounded && !grounded && vel.y > 1f) look.SetExpression(ChibiLook.Face.Happy, 0.8f);
            if (speed > 5.5f && grounded) look.SetExpression(ChibiLook.Face.Determined, 0.4f);
            idleTime = speed < 0.2f ? idleTime + dt : 0f;
            if (idleTime > 12f) look.SetExpression(ChibiLook.Face.Sleepy, 0.5f);
        }
        wasGrounded = grounded;

        if (listenForEmoteKeys && !ProximityInteractor.UIOpen)
        {
            bool wave = false, cheer = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null) { wave = Keyboard.current.vKey.wasPressedThisFrame; cheer = Keyboard.current.cKey.wasPressedThisFrame; }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            wave |= Input.GetKeyDown(KeyCode.V); cheer |= Input.GetKeyDown(KeyCode.C);
#endif
            if (wave) Emote(false);
            if (cheer) Emote(true);
        }
    }

    float hopT = -1f;
    /// <summary>Little happy hop (used by the wardrobe when you change something).</summary>
    public void Hop() { hopT = 0f; }

    void Update()
    {
        if (hopT < 0f || animator == null) return;
        hopT += Time.deltaTime * 4f;
        float k = Mathf.Sin(Mathf.Clamp01(hopT) * Mathf.PI);
        animator.transform.localScale = baseScale * (1f + 0.08f * k);
        if (hopT >= 1f) { hopT = -1f; animator.transform.localScale = baseScale; }
    }
    Vector3 baseScale = Vector3.one;
    void Awake() { if (animator != null) baseScale = animator.transform.localScale; }

    /// <summary>Earthquake: if standing on the ground, fall over (floppy) and get back up.</summary>
    public bool Knock()
    {
        if (!wasGrounded || posing) return false;          // mid-jump = you dodged it!
        if (animator != null) animator.SetTrigger(FallId);
        if (look != null) look.SetExpression(ChibiLook.Face.Surprised, 0.7f);
        StartCoroutine(Dizzy());
        return true;
        System.Collections.IEnumerator Dizzy()
        {
            yield return new WaitForSeconds(0.7f);
            if (look != null) look.SetExpression(ChibiLook.Face.Sad, 1.1f);
        }
    }

    public void Emote(bool cheer)
    {
        if (animator != null) animator.SetTrigger(cheer ? CheerId : WaveId);
        if (look != null) look.SetExpression(cheer ? ChibiLook.Face.Love : ChibiLook.Face.Wink, 1.6f);
    }
}
