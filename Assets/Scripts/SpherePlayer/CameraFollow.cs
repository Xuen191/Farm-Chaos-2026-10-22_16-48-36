// Farm Chaos - follow camera you can orbit.
// PC/editor: right-mouse drag to orbit, scroll wheel to zoom.
// Phone: drag on the right half of the screen (not on the jump button) to orbit.
//
// Smooth by design: the camera follows a smoothed "focus" point near the player and orbits around it,
// so turning never fights the follow smoothing (that switch between "snap" and "smooth" was the stutter).
// Drag input is eased and capped, so a dropped frame or a finger jump can't flick the view.
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    [Tooltip("Starting offset from the player (also sets the starting distance and angle)")]
    public Vector3 offset = new Vector3(0f, 5.5f, -7.5f);
    [Tooltip("How quickly the camera catches up with the player (seconds, smaller = tighter)")]
    public float smoothTime = 0.12f;
    [Tooltip("Height above the ball the camera looks at (the chibi's chest/head)")]
    public float lookHeight = 0.9f;

    [Header("Orbit")]
    public float turnSpeed = 0.25f;      // degrees per pixel dragged (on a 1080p-tall screen)
    [Tooltip("How smooth turning feels (seconds). 0 = raw.")]
    public float turnSmoothing = 0.06f;
    [Tooltip("Ignore drag jumps bigger than this many pixels in one frame (finger lift/land glitches)")]
    public float maxDragPerFrame = 120f;
    public float minPitch = 10f, maxPitch = 70f;
    public float zoomSpeed = 1.5f;
    public float minDistance = 4f, maxDistance = 16f;
    [Tooltip("Pull the camera in if a wall/tree is between it and the player")]
    public bool pullInForWalls = true;

    /// <summary>Extra wobble added on top (earthquakes, thunder).</summary>
    public static Vector3 Shake;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Shake = Vector3.zero; }

    float yaw, pitch, distance;          // where the player wants the camera
    float curYaw, curPitch, curDist;     // where it actually is (eased)
    Vector3 focus, focusVel;

    /// <summary>Zoom in (+) or out (-) by this many metres (pinch on phones; the mouse wheel is handled below).</summary>
    public static void AddZoom(float metres) { pendingZoom += metres; }
    static float pendingZoom;
    const float ClosestZoom = 2f, FarthestZoom = 14f;      // players can zoom right in, but not far out

    void Start()
    {
        minDistance = Mathf.Min(minDistance, ClosestZoom);
        maxDistance = Mathf.Min(maxDistance, FarthestZoom);
        pendingZoom = 0f;
        Snap();
    }

    /// <summary>Jump straight to the right spot (start of scene, respawn, wardrobe closed...).</summary>
    public void Snap()
    {
        distance = Mathf.Clamp(offset.magnitude, minDistance, maxDistance);
        pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(offset.y / Mathf.Max(offset.magnitude, 0.01f), -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
        yaw = Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
        curYaw = yaw; curPitch = pitch; curDist = distance;
        if (target == null) return;
        focus = Focus(); focusVel = Vector3.zero;
        Place();
    }

    void OnEnable()
    {
        // coming back from the wardrobe (or any script that moved the camera): continue from where we are
        if (target == null) return;
        focus = Focus(); focusVel = Vector3.zero;
    }

    Vector3 Focus() => target.position + Vector3.up * lookHeight;

    void Place()
    {
        Quaternion rot = Quaternion.Euler(curPitch, curYaw, 0f);
        transform.SetPositionAndRotation(focus + rot * new Vector3(0f, 0f, -curDist) + Shake, rot);
    }

    void LateUpdate()
    {
        if (target == null) return;
        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);

        // ---- input (pixels -> degrees, same feel on any screen size)
        Vector2 look = MobileJoystick.LookDelta;
        if (look.sqrMagnitude > maxDragPerFrame * maxDragPerFrame) look = look.normalized * maxDragPerFrame;
        float scale = turnSpeed * 1080f / Mathf.Max(Screen.height, 1);
        yaw += look.x * scale;
        pitch = Mathf.Clamp(pitch - look.y * scale, minPitch, maxPitch);

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        if (pendingZoom != 0f) { distance = Mathf.Clamp(distance - pendingZoom, minDistance, maxDistance); pendingZoom = 0f; }

        // ---- ease the angles (frame-rate independent)
        float k = turnSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-dt / turnSmoothing);
        curYaw = Mathf.LerpAngle(curYaw, yaw, k);
        curPitch = Mathf.Lerp(curPitch, pitch, k);

        // ---- follow a smoothed point on the player, then orbit around it
        focus = Vector3.SmoothDamp(focus, Focus(), ref focusVel, smoothTime, Mathf.Infinity, dt);

        float want = distance;
        if (pullInForWalls)
        {
            Vector3 dir = Quaternion.Euler(curPitch, curYaw, 0f) * Vector3.back;
            if (Physics.SphereCast(focus, 0.25f, dir, out var hit, distance, ~0, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null)
                want = Mathf.Max(1.5f, hit.distance - 0.2f);
        }
        // pull in fast (so walls never cover the player), ease back out slowly (no pumping)
        float dk = want < curDist ? 1f - Mathf.Exp(-dt / 0.04f) : 1f - Mathf.Exp(-dt / 0.35f);
        curDist = Mathf.Lerp(curDist, want, dk);

        Place();
    }
}
