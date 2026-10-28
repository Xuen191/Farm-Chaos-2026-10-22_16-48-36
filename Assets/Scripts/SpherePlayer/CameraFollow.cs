// Farm Chaos - follow camera you can orbit.
// PC/editor: right-mouse drag to orbit, scroll wheel to zoom.
// Phone: drag on the right half of the screen (not on the jump button) to orbit.
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("Starting offset from the player (also sets the starting distance and angle)")]
    public Vector3 offset = new Vector3(0f, 11f, -9f);
    public float smoothTime = 0.12f;
    public float lookHeight = 0.5f;

    [Header("Orbit")]
    public float turnSpeed = 0.25f;      // degrees per pixel dragged
    public float minPitch = 15f, maxPitch = 80f;
    public float zoomSpeed = 2f;
    public float minDistance = 5f, maxDistance = 25f;
    [Tooltip("Pull the camera in if a wall/tree is between it and the player")]
    public bool pullInForWalls = false;

    float yaw, pitch, distance;
    Vector3 vel;

    void Start()
    {
        distance = offset.magnitude;
        pitch = Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(distance, 0.01f), -1f, 1f)) * Mathf.Rad2Deg;
        yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
        if (target != null) { transform.position = Desired(distance); transform.LookAt(Focus()); }
    }

    Vector3 Focus() => target.position + Vector3.up * lookHeight;

    Vector3 Desired(float dist) => Focus() + Quaternion.Euler(pitch, yaw, 0f) * new Vector3(0f, 0f, -dist);

    void LateUpdate()
    {
        if (target == null) return;

        Vector2 look = MobileJoystick.LookDelta;
        yaw += look.x * turnSpeed;
        pitch = Mathf.Clamp(pitch - look.y * turnSpeed, minPitch, maxPitch);

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);

        float dist = distance;
        if (pullInForWalls)
        {
            Vector3 dir = (Desired(distance) - Focus()).normalized;
            if (Physics.SphereCast(Focus(), 0.3f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
                dist = Mathf.Max(1.5f, hit.distance - 0.2f);
        }

        bool turning = look.sqrMagnitude > 0f;
        Vector3 goal = Desired(dist);
        // follow smoothly, but snap rotation input straight away so turning feels responsive
        transform.position = turning ? goal : Vector3.SmoothDamp(transform.position, goal, ref vel, smoothTime);
        transform.LookAt(Focus());
    }
}
