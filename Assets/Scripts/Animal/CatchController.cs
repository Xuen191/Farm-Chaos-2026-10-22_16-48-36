// Farm Chaos - GRABBING animals (on your own device).
// Get close to an escaped animal (sneak - running scares it off!) and a ring starts shrinking around it.
// Press GRAB (G, or the GRAB button on a phone) while the ring is in the GREEN zone:
//   good timing -> caught, it follows you home      bad timing -> it panics and bolts
// Animals eating bait are calm: the ring stays green, any grab works.
// Online, your grab goes to the host (who runs the animals) through NetPlayer.
// Nothing to set up: switches itself on in the farm.
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CatchController : MonoBehaviour
{
    public float reach = 2.2f;
    const float Big = 1.9f, Small = 0.25f, ZoneLo = 0.45f, ZoneHi = 0.95f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
    static void OnScene(Scene s, LoadSceneMode m)
    {
        if (FindAnyObjectByType<RoundClock>() == null || FindAnyObjectByType<CatchController>() != null) return;
        new GameObject("Catch_Controller").AddComponent<CatchController>();
    }

    SpherePlayer me;
    AnimalBrain target;
    LineRenderer ring, zoneLo, zoneHi;
    float phase, cooldown, flash, press;
    RectTransform grabBtn; Image btnImg; Canvas btnCanvas; TextMeshProUGUI hint;

    void Start()
    {
        ring = Circle("Grab_Ring", 0.09f);
        zoneLo = Circle("Grab_Zone_In", 0.05f);
        zoneHi = Circle("Grab_Zone_Out", 0.05f);
        foreach (var z in new[] { zoneLo, zoneHi }) z.startColor = z.endColor = new Color(0.35f, 0.85f, 0.45f, 0.9f);
        BuildButton();
        Show(false);
    }

    LineRenderer Circle(string n, float width)
    {
        var lr = new GameObject(n).AddComponent<LineRenderer>();
        lr.transform.SetParent(transform, false);
        lr.loop = true; lr.positionCount = 40; lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = width;
        lr.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        lr.material.color = Color.white;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return lr;
    }

    static void Draw(LineRenderer lr, Vector3 c, float r)
    {
        for (int i = 0; i < lr.positionCount; i++)
        {
            float a = i * Mathf.PI * 2f / lr.positionCount;
            lr.SetPosition(i, c + new Vector3(Mathf.Cos(a) * r, 0.08f, Mathf.Sin(a) * r));
        }
    }

    void BuildButton()
    {
        var stick = FindAnyObjectByType<MobileJoystick>();
        if (stick == null || stick.jumpButton == null) return;
        var jump = stick.jumpButton;
        btnCanvas = jump.GetComponentInParent<Canvas>();
        grabBtn = new GameObject("Grab_Button", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        grabBtn.SetParent(jump.parent, false);
        grabBtn.anchorMin = jump.anchorMin; grabBtn.anchorMax = jump.anchorMax; grabBtn.pivot = jump.pivot;
        grabBtn.sizeDelta = jump.sizeDelta * 0.85f;
        grabBtn.anchoredPosition = jump.anchoredPosition + new Vector2(-jump.sizeDelta.x * 1.15f, -10f);
        var img = btnImg = grabBtn.GetComponent<Image>(); var ji = jump.GetComponent<Image>();
        if (ji != null) { img.sprite = ji.sprite; img.type = ji.type; }
        img.color = new Color32(0x8C, 0xCF, 0xA4, 235); img.raycastTarget = false;
        var t = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(grabBtn, false); t.rectTransform.sizeDelta = grabBtn.sizeDelta;
        t.text = "GRAB"; t.fontSize = 34; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.raycastTarget = false;
        hint = t;
    }

    void Show(bool on)
    {
        ring.enabled = zoneLo.enabled = zoneHi.enabled = on;
        if (grabBtn != null && grabBtn.gameObject.activeSelf != on) grabBtn.gameObject.SetActive(on);
    }

    void Update()
    {
        if (me == null) me = FindAnyObjectByType<SpherePlayer>();
        cooldown -= Time.deltaTime; flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 3f);
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4)) { NetPlayer.TestToggleSpy(); if (TonightsWeather.Instance != null) TonightsWeather.Instance.Toast(NetPlayer.IAmSpy ? "TEST  you are the spy" : "TEST  farmer again", 2f); }
#endif
        target = Nearest();
        if (me == null || ProximityInteractor.UIOpen || me.Stunned) { Show(false); return; }
        if (target == null)
        {   // the spy, leading animals: a LET GO button instead (fake helper)
            int followers = NetPlayer.IAmSpy ? MyFollowers() : 0;
            if (followers == 0 || grabBtn == null) { Show(false); return; }
            ring.enabled = zoneLo.enabled = zoneHi.enabled = false;
            {
                if (!grabBtn.gameObject.activeSelf) grabBtn.gameObject.SetActive(true);
                press = Mathf.MoveTowards(press, 0f, Time.deltaTime * 4f);
                grabBtn.localScale = Vector3.one * (1f - 0.18f * press);
                btnImg.color = new Color32(0xB9, 0x8E, 0xD9, 235); hint.text = "LET GO";
                if (cooldown <= 0f && GrabPressed()) { cooldown = 1f; press = 1f; LetGo(followers); }
            }
            return;
        }
        Show(true);

        Vector3 c = target.transform.position;
        bool calm = target.Calm;
        phase = (phase + Time.deltaTime / target.GrabTime) % 1f;
        float r = calm ? (ZoneLo + ZoneHi) * 0.5f : Mathf.Lerp(Big, Small, phase);
        bool inZone = calm || (r >= ZoneLo && r <= ZoneHi);
        Draw(ring, c, r); Draw(zoneLo, c, ZoneLo); Draw(zoneHi, c, ZoneHi);
        Color rc = inZone ? new Color(0.35f, 0.9f, 0.45f) : Color.white;
        ring.startColor = ring.endColor = Color.Lerp(rc, new Color(1f, 0.4f, 0.4f), flash);
        press = Mathf.MoveTowards(press, 0f, Time.deltaTime * 4f);
        if (grabBtn != null)
        {
            grabBtn.localScale = Vector3.one * ((inZone ? 1.1f : 0.95f) - 0.18f * press);
            btnImg.color = inZone ? new Color32(0x5C, 0xD6, 0x7A, 255) : new Color32(0xA9, 0xC9, 0xB4, 200);
            hint.text = inZone ? "GRAB!" : "GRAB";
        }

        if (cooldown <= 0f && GrabPressed())
        {
            cooldown = 0.6f; press = 1f;
            if (!inZone) flash = 1f;
            Grab(target, inZone);
            phase = 0f;
        }
    }

    AnimalBrain Nearest()
    {
        if (me == null) return null;
        AnimalBrain best = null; float bd = reach;
        foreach (var a in AnimalBrain.All)
        {
            if (a == null || !a.Catchable) continue;
            Vector3 d = a.transform.position - me.transform.position; d.y = 0f;
            if (d.magnitude < bd) { bd = d.magnitude; best = a; }
        }
        if (best != target) phase = 0f;
        return best;
    }

    bool GrabPressed()
    {
        bool key = false;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null) key |= kb.gKey.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        key |= Input.GetKeyDown(KeyCode.G);
#endif
        if (key) return true;
        if (grabBtn == null || !grabBtn.gameObject.activeInHierarchy) return false;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase == TouchPhase.Began && OnGrabButton(t.position)) return true;
            }
            return false;
        }
        return Input.GetMouseButtonDown(0) && OnGrabButton(Input.mousePosition);
    }

    // same round, a-bit-generous hit test the jump button uses
    Camera UICam => btnCanvas != null && btnCanvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? btnCanvas.rootCanvas.worldCamera : null;
    Vector2 Centre() => RectTransformUtility.WorldToScreenPoint(UICam, grabBtn.TransformPoint(grabBtn.rect.center));
    float Radius() => grabBtn.rect.width * 0.5f * (btnCanvas != null ? btnCanvas.rootCanvas.scaleFactor : 1f) * 1.25f;
    bool OnGrabButton(Vector2 screen) => Vector2.Distance(screen, Centre()) <= Radius();

    // how many animals are trotting along right behind me
    int MyFollowers()
    {
        int n = 0;
        foreach (var a in AnimalBrain.All)
        {
            if (a == null || a.state != AnimalBrain.State.Following) continue;
            Vector3 d = a.transform.position - me.transform.position; d.y = 0f;
            if (d.magnitude < 6f) n++;
        }
        return n;
    }

    // spy: secretly let go of the animals you're leading - they scatter (everyone gets a "got loose" alert)
    void LetGo(int followers)
    {
        if (NetSession.Online && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            if (NetPlayer.Local != null) NetPlayer.Local.SendRelease();
        }
        else
        {
            int n = AnimalBrain.ReleaseFollowers(me.transform, out var kind, out var where);
            if (n > 0) NetPlayer.Alert(SpySabotage.LooseMessage(kind, n, where));
        }
        ScoreKeeper.Add(Mathf.Min(followers, 3) * 10, "Oops... let go!");
    }

    void Grab(AnimalBrain a, bool success)
    {
        if (NetSession.Online && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            if (NetPlayer.Local != null) NetPlayer.Local.SendCatch(AnimalBrain.IndexOf(a), success);
        }
        else a.HostCatch(me.transform, true, null, success);
        if (TonightsWeather.Instance != null)
            TonightsWeather.Instance.Toast(success ? "Got it! Lead it home" : "Missed! Wait for the green ring", 1.4f);
    }
}
