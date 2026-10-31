// Farm Chaos - lets the rolling sphere use your friend's Interactable objects (power box, fuse, shop...).
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
    public TMP_Text hintText;          
    public Button useButton;
    public TMP_Text useButtonLabel;
    public GameObject[] modalPanels;
    public Canvas mobileControls;

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

        if (Input.GetMouseButtonDown(1)) 
        {
            StickPickup pickup = FindObjectOfType<StickPickup>();
            if (pickup != null) pickup.DropStick(gameObject);
        }
    }

    void LateUpdate()
    {
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
        if (UIOpen) return;

        // 💡 判斷是否為敵人：只要身上有 InteractableEnemy 腳本，就當作是敵人！
        if (current != null && current.GetComponent<InteractableEnemy>() != null)
        {
            StickAttack attack = GetComponent<StickAttack>();
            if (attack != null)
            {
                attack.HitAttack(); 
            }
            return; 
        }

        if (current != null) 
        {
            current.Interact();
        }
        else 
        {
            StickAttack attack = GetComponent<StickAttack>();
            if (attack != null) attack.HitAttack();
        }
    }
}