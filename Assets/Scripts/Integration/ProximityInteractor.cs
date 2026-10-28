// Farm Chaos - lets the rolling sphere use your friend's Interactable objects (power box, fuse, shop...).
// Roll close to something -> its hint shows + a USE button appears. Tap USE (phone) or press E (PC).
// While a full-screen UI is open (shop, wiring puzzle) the ball and the on-screen controls pause.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ProximityInteractor : MonoBehaviour
{
    [Tooltip("How close (in units) the ball must be to use something")]
    public float useRadius = 3f;
    public TMP_Text hintText;          // your friend's HintText is reused
    public Button useButton;
    public TMP_Text useButtonLabel;
    [Tooltip("Full-screen panels that pause the player while open (shop, wiring puzzle...)")]
    public GameObject[] modalPanels;
    [Tooltip("On-screen joystick canvas, hidden while a panel is open")]
    public Canvas mobileControls;

    /// <summary>True while the shop / wiring puzzle (or any modal panel) is open.</summary>
    public static bool UIOpen { get; private set; }

    Interactable current;
    float rescan;
    List<Interactable> all = new List<Interactable>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { UIOpen = false; }

    void Start()
    {
        if (useButton != null)
        {
            useButton.onClick.AddListener(Use);
            useButton.gameObject.SetActive(false);
        }
    }

    void OnDisable() { UIOpen = false; }

    void Update()
    {
        UIOpen = modalPanels != null && modalPanels.Any(p => p != null && p.activeInHierarchy);
        if (mobileControls != null) mobileControls.enabled = !UIOpen;

        if (UIOpen) { Show(null); return; }

        rescan -= Time.unscaledDeltaTime;
        if (rescan <= 0f) { all = FindObjectsByType<Interactable>(FindObjectsSortMode.None).ToList(); rescan = 0.5f; }

        Interactable best = null; float bestD = useRadius;
        Vector3 me = transform.position;
        foreach (var it in all)
        {
            if (it == null || !it.isActiveAndEnabled) continue;
            float d = Distance(it, me);
            if (d < bestD) { bestD = d; best = it; }
        }
        Show(best);

        bool ePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ePressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.E)) ePressed = true;
#endif
        if (ePressed) Use();
    }

    void LateUpdate()
    {
        // this game never locks the mouse (touch controls / mouse-drag camera)
        if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }

    static float Distance(Interactable it, Vector3 p)
    {
        float best = Vector3.Distance(it.transform.position, p);
        foreach (var c in it.GetComponentsInChildren<Collider>())
        {
            if (!c.enabled) continue;
            float d = Vector3.Distance(c.ClosestPoint(p), p);
            if (d < best) best = d;
        }
        return best;
    }

    void Show(Interactable it)
    {
        current = it;
        bool show = it != null;
        if (useButton != null && useButton.gameObject.activeSelf != show) useButton.gameObject.SetActive(show);
        if (hintText != null)
        {
            string msg = show ? it.hintMessage : "";
            if (show && Application.isMobilePlatform) msg = msg.Replace("Press E to", "Tap USE to");
            if (hintText.text != msg) hintText.text = msg;
        }
    }

    public void Use()
    {
        if (current == null || UIOpen) return;
        current.Interact();
    }
}
