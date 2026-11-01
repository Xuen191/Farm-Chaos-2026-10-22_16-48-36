// Farm Chaos - floating on-screen joystick, jump button and look-drag.
// LEFT half of the screen: touch & drag to move (joystick appears under your thumb).
// RIGHT half: tap the pink button to jump, drag anywhere else to turn the camera.
// In the editor the mouse acts as a finger; right-mouse drag also turns the camera.
// SNEAKING on a phone: keep your thumb inside the small inner circle -> the knob turns green and you creep
// slowly (same as holding Shift on a keyboard). Push past the circle to run.
// Reads input directly, so no EventSystem is needed.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MobileJoystick : MonoBehaviour
{
    public RectTransform background;
    public RectTransform handle;
    public RectTransform jumpButton;
    [Tooltip("How far the handle can move from the centre, in canvas units")]
    public float radius = 110f;
    [Tooltip("Show the on-screen controls when not on a phone (they still work with the mouse)")]
    public bool showInEditor = true;

    /// <summary>Joystick direction, -1..1 on each axis.</summary>
    public static Vector2 Value { get; private set; }
    /// <summary>True while the thumb is inside the inner sneak circle.</summary>
    public static bool Sneaking { get; private set; }
    const float SneakZone = 0.55f;     // inner part of the stick (0..1)
    const float SneakPush = 0.32f;     // how hard you move while sneaking (Shift on a keyboard is 0.35)
    Image handleImg, sneakRing; Color handleColour = Color.white; TextMeshProUGUI sneakLabel;
    /// <summary>Camera look drag this frame, in screen pixels.</summary>
    public static Vector2 LookDelta { get; private set; }
    static bool jumpFlag;
    public static bool ConsumeJump() { bool j = jumpFlag; jumpFlag = false; return j; }

    RectTransform canvasRect;
    Canvas canvas;
    Vector2 restPos;
    int moveFinger = -1, lookFinger = -1;
    bool mouseMoving, mouseLooking;
    Vector2 originScreen, lastLookPos;
    float jumpFlash;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Value = Vector2.zero; LookDelta = Vector2.zero; jumpFlag = false; Sneaking = false; }

    void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
        Input.simulateMouseWithTouches = false;
        if (!Application.isMobilePlatform && !showInEditor)
            foreach (Transform c in transform) c.gameObject.SetActive(false);
    }

    void Start()
    {
        // resting spot: bottom-left corner, whatever the phone's screen shape
        Canvas.ForceUpdateCanvases();
        restPos = new Vector2(-canvasRect.rect.width / 2f + 260f, -canvasRect.rect.height / 2f + 240f);
        if (background != null) background.anchoredPosition = restPos;
        BuildSneakRing();
    }

    // a faint inner circle on the stick showing where sneaking starts, plus a little SNEAK label
    void BuildSneakRing()
    {
        if (background == null || handle == null) return;
        handleImg = handle.GetComponent<Image>();
        if (handleImg != null) handleColour = handleImg.color;
        var bgImg = background.GetComponent<Image>();
        var ring = new GameObject("Sneak_Ring", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        ring.SetParent(background, false); ring.SetSiblingIndex(0);
        ring.anchorMin = ring.anchorMax = ring.pivot = new Vector2(0.5f, 0.5f);
        float d = radius * SneakZone * 2f + handle.rect.width * 0.5f;
        ring.sizeDelta = new Vector2(d, d);
        sneakRing = ring.GetComponent<Image>();
        if (bgImg != null) { sneakRing.sprite = bgImg.sprite; sneakRing.type = bgImg.type; }
        sneakRing.color = new Color(0.55f, 0.85f, 0.62f, 0.25f); sneakRing.raycastTarget = false;

        sneakLabel = new GameObject("Sneak_Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        sneakLabel.transform.SetParent(background, false);
        sneakLabel.rectTransform.anchoredPosition = new Vector2(0f, background.rect.height * 0.5f + 28f);
        sneakLabel.rectTransform.sizeDelta = new Vector2(220f, 44f);
        sneakLabel.text = "SNEAKING"; sneakLabel.fontSize = 30; sneakLabel.fontStyle = FontStyles.Bold;
        sneakLabel.alignment = TextAlignmentOptions.Center; sneakLabel.color = new Color32(0x8C, 0xE0, 0xA4, 255);
        sneakLabel.outlineWidth = 0.2f; sneakLabel.outlineColor = new Color32(0x2E, 0x4A, 0x36, 255);
        sneakLabel.raycastTarget = false; sneakLabel.enabled = false;
    }

    void ShowSneak(bool on)
    {
        Sneaking = on;
        if (handleImg != null) handleImg.color = on ? new Color32(0x8C, 0xE0, 0xA4, 255) : handleColour;
        if (sneakRing != null) sneakRing.color = new Color(0.55f, 0.85f, 0.62f, on ? 0.5f : 0.25f);
        if (sneakLabel != null) sneakLabel.enabled = on;
    }

    void OnDisable() { Value = Vector2.zero; LookDelta = Vector2.zero; }

    // clicked into another window / alt-tabbed: let go of everything, so the ball doesn't keep rolling
    void OnApplicationFocus(bool focused) { if (!focused) ReleaseAll(); }
    void OnApplicationPause(bool paused) { if (paused) ReleaseAll(); }
    void ReleaseAll()
    {
        if (moveFinger != -1 || mouseMoving) End();
        moveFinger = lookFinger = -1; mouseMoving = mouseLooking = false;
        Value = Vector2.zero; LookDelta = Vector2.zero;
    }

    bool OnJumpButton(Vector2 screenPos)
    {
        if (jumpButton == null || !jumpButton.gameObject.activeInHierarchy) return false;
        Vector2 centre = RectTransformUtility.WorldToScreenPoint(null, jumpButton.position);
        float r = jumpButton.rect.width * 0.5f * (canvas != null ? canvas.scaleFactor : 1f) * 1.3f; // a bit generous
        return Vector2.Distance(screenPos, centre) <= r;
    }

    static bool OverUI(int pointerId = -1)
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null) return false;
        return pointerId >= 0 ? es.IsPointerOverGameObject(pointerId) : es.IsPointerOverGameObject();
    }

    void Update()
    {
        Vector2 look = Vector2.zero;

        // a full-screen panel (shop, wiring puzzle) is open: controls off
        if (ProximityInteractor.UIOpen)
        {
            if (moveFinger != -1 || mouseMoving) { moveFinger = -1; mouseMoving = false; End(); }
            lookFinger = -1; mouseLooking = false; LookDelta = Vector2.zero; jumpFlag = false;
            return;
        }

        if (Input.touchCount > 0)
        {
            mouseMoving = mouseLooking = false;
            // a finger we were tracking has vanished without an "ended" (it happens): let go of it
            bool moveAlive = false, lookAlive = false;
            for (int i = 0; i < Input.touchCount; i++) { var f = Input.GetTouch(i).fingerId; moveAlive |= f == moveFinger; lookAlive |= f == lookFinger; }
            if (moveFinger != -1 && !moveAlive) { moveFinger = -1; End(); }
            if (lookFinger != -1 && !lookAlive) lookFinger = -1;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began)
                {
                    if (OverUI(t.fingerId)) continue;   // tapping a UI button (e.g. USE)
                    if (t.position.x < Screen.width * 0.5f)
                    {
                        if (moveFinger == -1) { moveFinger = t.fingerId; Begin(t.position); }
                    }
                    else if (OnJumpButton(t.position)) Jump();
                    else if (lookFinger == -1) { lookFinger = t.fingerId; lastLookPos = t.position; }
                }

                bool ended = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                if (t.fingerId == moveFinger)
                {
                    if (ended) { moveFinger = -1; End(); } else Drag(t.position);
                }
                else if (t.fingerId == lookFinger)
                {
                    if (ended) lookFinger = -1;
                    else { look += t.position - lastLookPos; lastLookPos = t.position; }
                }
            }
        }
        else
        {
            if (moveFinger != -1) { moveFinger = -1; End(); }
            lookFinger = -1;
            // the button is already up but we missed the "let go" (released outside the window, focus change...)
            if (mouseMoving && !Input.GetMouseButton(0)) { mouseMoving = false; End(); }
            if (mouseLooking && !Input.GetMouseButton(0)) mouseLooking = false;

            // left mouse = a fake finger (editor / Device Simulator)
            Vector2 mp = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) && !OverUI())
            {
                if (mp.x < Screen.width * 0.5f) { mouseMoving = true; Begin(mp); }
                else if (OnJumpButton(mp)) Jump();
                else { mouseLooking = true; lastLookPos = mp; }
            }
            if (mouseMoving && Input.GetMouseButton(0)) Drag(mp);
            if (mouseMoving && Input.GetMouseButtonUp(0)) { mouseMoving = false; End(); }

            // right mouse drag (or left-drag on the right half) = turn the camera
            if (Input.GetMouseButtonDown(1)) lastLookPos = mp;
            if (Input.GetMouseButton(1) || (mouseLooking && Input.GetMouseButton(0)))
            {
                look += mp - lastLookPos;
                lastLookPos = mp;
            }
            if (Input.GetMouseButtonUp(0)) mouseLooking = false;
        }

        LookDelta = look;

        if (jumpButton != null)
        {
            jumpFlash = Mathf.MoveTowards(jumpFlash, 0f, Time.unscaledDeltaTime * 4f);
            jumpButton.localScale = Vector3.one * (1f - 0.15f * jumpFlash);
        }
    }

    void Begin(Vector2 screenPos)
    {
        originScreen = screenPos;
        // background is centre-anchored, so a local point in the canvas == its anchored position
        if (background != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out var local))
            background.anchoredPosition = local;
        Drag(screenPos);
    }

    void Drag(Vector2 screenPos)
    {
        float scale = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = (screenPos - originScreen) / scale;
        Vector2 clamped = Vector2.ClampMagnitude(delta, radius);
        if (handle != null) handle.anchoredPosition = clamped;
        Vector2 v = clamped / radius;
        if (v.magnitude < 0.12f) { Value = Vector2.zero; ShowSneak(false); }           // small dead zone
        else if (v.magnitude < SneakZone) { Value = v.normalized * SneakPush; ShowSneak(true); }   // inner circle: sneak
        else { Value = v; ShowSneak(false); }                                           // outer part: run
    }

    void End()
    {
        Value = Vector2.zero; ShowSneak(false);
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (background != null) background.anchoredPosition = restPos;
    }

    void Jump()
    {
        jumpFlag = true;
        jumpFlash = 1f;
    }
}
