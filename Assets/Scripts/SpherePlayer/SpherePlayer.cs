// Farm Chaos - simple rolling sphere player.
// Controls: WASD / arrow keys + Space (keyboard), or the on-screen joystick + jump button (touch / mouse).
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpherePlayer : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 7f;        // top speed (units per second)
    public float acceleration = 30f;    // how quickly it reaches top speed
    public float jumpSpeed = 6.5f;
    [Tooltip("Movement is relative to this camera. Leave empty to use the main camera.")]
    public Transform cameraTransform;

    [Header("Respawn")]
    public float fallLimitY = -10f;

    Rigidbody rb;
    float radius;
    Vector3 spawnPoint;
    Vector2 moveInput;
    bool jumpQueued;

    public bool IsGrounded { get; private set; }

    /// <summary>Weather: 1 = normal grip, lower = slippery mud (you slide).</summary>
    public static float Grip = 1f;
    /// <summary>Weather: a push on the player in m/s² (extra shoves).</summary>
    public static Vector3 Push = Vector3.zero;
    /// <summary>Weather: the wind (m/s). Walking into it is slow, walking with it is fast, standing still you drift.</summary>
    public static Vector3 Wind = Vector3.zero;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Grip = 1f; Push = Vector3.zero; Wind = Vector3.zero; }

    float stunnedUntil = -1f;
    /// <summary>Knocked over (earthquake): can't move or jump for a moment.</summary>
    public void Stun(float seconds) { stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds); }
    public bool Stunned => Time.time < stunnedUntil;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = 50f;
        var sc = GetComponent<SphereCollider>();
        radius = sc != null ? sc.radius * transform.lossyScale.x : 0.5f;
        spawnPoint = transform.position;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // keyboard (editor / PC)
        Vector2 kb = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        // touch joystick (phone) or mouse-drag joystick (editor)
        Vector2 stick = MobileJoystick.Value;
        moveInput = Vector2.ClampMagnitude(kb + stick, 1f);

        // shop / wiring puzzle open, or knocked over -> stand still
        if (ProximityInteractor.UIOpen || Stunned) { moveInput = Vector2.zero; MobileJoystick.ConsumeJump(); return; }

        if (Input.GetKeyDown(KeyCode.Space) || MobileJoystick.ConsumeJump()) jumpQueued = true;
    }

    void FixedUpdate()
    {
        IsGrounded = Physics.Raycast(transform.position, Vector3.down, radius + 0.15f, ~0, QueryTriggerInteraction.Ignore);

        // camera-relative direction on the ground plane
        Vector3 fwd = Vector3.forward, right = Vector3.right;
        if (cameraTransform != null)
        {
            fwd = cameraTransform.forward; fwd.y = 0; fwd.Normalize();
            right = cameraTransform.right; right.y = 0; right.Normalize();
        }
        Vector3 dir = fwd * moveInput.y + right * moveInput.x;
        float speed = moveSpeed;
        if (Wind.sqrMagnitude > 0.01f && dir.sqrMagnitude > 0.01f)
        {
            // into the wind: down to ~35% speed; side wind: ~75%; with the wind: a bit faster
            float into = Vector3.Dot(dir.normalized, -Wind.normalized);
            speed *= into > 0f ? Mathf.Lerp(0.75f, 0.35f, into) : Mathf.Lerp(0.75f, 1.05f, -into);
        }
        Vector3 target = dir * speed + Wind * 0.4f;       // the wind also carries you along

        Vector3 v = rb.linearVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);
        float accel = (IsGrounded ? acceleration : acceleration * 0.4f) * Grip;   // a little air control; mud = less grip
        Vector3 next = Vector3.MoveTowards(horiz, target, accel * Time.fixedDeltaTime);
        rb.AddForce(next - horiz, ForceMode.VelocityChange);
        if (Push != Vector3.zero) rb.AddForce(Push, ForceMode.Acceleration);   // wind / earthquake

        if (jumpQueued)
        {
            if (IsGrounded) rb.AddForce(Vector3.up * (jumpSpeed - Mathf.Max(0f, v.y)), ForceMode.VelocityChange);
            jumpQueued = false;
        }

        if (transform.position.y < fallLimitY) Respawn();
    }

    public void Respawn()
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = spawnPoint;
    }
}
