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
        Vector3 target = (fwd * moveInput.y + right * moveInput.x) * moveSpeed;

        Vector3 v = rb.linearVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);
        float accel = IsGrounded ? acceleration : acceleration * 0.4f;   // a little air control
        Vector3 next = Vector3.MoveTowards(horiz, target, accel * Time.fixedDeltaTime);
        rb.AddForce(next - horiz, ForceMode.VelocityChange);

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
