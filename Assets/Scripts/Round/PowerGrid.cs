// Farm Chaos - the farm's power is UNSTABLE.
//   * The first fix of the night is the big one (fuse + wiring puzzle at the power box).
//   * After that, every minute or two the power flickers (3 second warning: lamps flicker + buzz)... and drops.
//     The gates pop open again and any animals in their pens can get loose.
//   * Later outages are a quick fix: press E at the power box to reset the breaker (no new fuse, no puzzle).
//   * Outages come closer together towards sunrise, more often in a Thunderstorm (lightning can knock it out),
//     and the SPY can overload the power box on purpose.
// Online: only the host decides when the power drops; everyone gets the warning and the outage.
// Nothing to set up: it switches itself on in any scene that has a power box.
using UnityEngine;
using UnityEngine.SceneManagement;

public class PowerGrid : MonoBehaviour
{
    public static PowerGrid Instance { get; private set; }

    [Header("How unstable")]
    [Tooltip("Seconds of power before it drops, early in the night (min, max)")]
    public Vector2 outageEvery = new Vector2(75f, 130f);
    [Tooltip("...and near sunrise")]
    public Vector2 outageEveryLate = new Vector2(40f, 65f);
    public float warningTime = 3f;
    [Tooltip("Thunderstorm: outages come this much sooner (0.6 = 40% sooner)")]
    public float thunderMultiplier = 0.6f;
    [Tooltip("Chance that a lightning strike knocks the power out")]
    [Range(0, 1)] public float lightningCutChance = 0.3f;
    [Tooltip("Spy overload cooldown (seconds)")]
    public float spyCooldown = 45f;

    float nextOutage = -1f; bool warned; float spyReadyAt;
    AudioSource buzz;

    // ---------------------------------------------------------------- auto-setup
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene;
        OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    static void OnScene(Scene s, LoadSceneMode m)
    {
        if (Instance != null || FindAnyObjectByType<PowerBox>() == null) return;
        new GameObject("Power_Grid").AddComponent<PowerGrid>();
    }

    static bool Authority => !NetSession.Online || Unity.Netcode.NetworkManager.Singleton.IsServer;

    void Awake() { Instance = this; }
    void OnEnable() { ElectricGate.PowerChanged += OnPower; }
    void OnDisable() { ElectricGate.PowerChanged -= OnPower; if (Instance == this) Instance = null; }

    void Start()
    {
        buzz = gameObject.AddComponent<AudioSource>(); buzz.spatialBlend = 0f; buzz.volume = 0.5f;
        if (ElectricGate.PowerOn && AnyBoxFixed()) Schedule();
    }

    static bool AnyBoxFixed() { var b = FindAnyObjectByType<PowerBox>(); return b != null && b.wiresCorrect; }

    void OnPower(bool on)
    {
        if (on) Schedule();
        else { nextOutage = -1f; warned = false; }
    }

    void Schedule()
    {
        float p = RoundClock.Instance != null ? RoundClock.Instance.Progress : 0f;
        Vector2 r = Vector2.Lerp(outageEvery, outageEveryLate, p);
        float t = Random.Range(r.x, r.y);
        if (TonightsWeather.HasRolled && TonightsWeather.Current == TonightsWeather.Kind.Thunderstorm) t *= thunderMultiplier;
        nextOutage = Time.time + t; warned = false;
    }

    void Update()
    {
        if (!Authority || nextOutage < 0f || !ElectricGate.PowerOn) return;
        if (RoundClock.Instance != null && RoundClock.Instance.Finished) return;
        if (!warned && Time.time >= nextOutage - warningTime) { warned = true; SendWarning(); }
        if (Time.time >= nextOutage) { nextOutage = -1f; Cut(); }
    }

    // ---------------------------------------------------------------- outages
    void SendWarning()
    {
        if (NetSession.Online && NetPlayer.Local != null) NetPlayer.Local.SendPowerWarning(warningTime);
        else ShowWarning(warningTime);
    }

    /// <summary>Everyone: lamps flicker, buzz, "the power is flickering!"</summary>
    public static void ShowWarning(float seconds)
    {
        PowerLamps.Warn(seconds);
        if (TonightsWeather.Instance != null) TonightsWeather.Instance.Toast("The power is flickering!", seconds);
        if (Instance != null && Instance.buzz != null)
        {
            if (Instance.buzz.clip == null) Instance.buzz.clip = MakeBuzz();
            Instance.buzz.Play();
        }
    }

    /// <summary>Host / offline: knock the power out (synced to everyone by NetPlayer).</summary>
    public static void Cut()
    {
        if (!Authority || !ElectricGate.PowerOn) return;
        ElectricGate.SetPower(false);
    }

    /// <summary>Thunderstorm: a lightning strike might take the power out.</summary>
    public static void LightningStrike()
    {
        if (Instance == null || !AnyBoxFixed()) return;
        if (Random.value < Instance.lightningCutChance) Cut();
    }

    /// <summary>The spy pressed E at a working power box.</summary>
    public static bool SpyOverload()
    {
        if (Instance == null || !ElectricGate.PowerOn || Time.time < Instance.spyReadyAt) return false;
        Instance.spyReadyAt = Time.time + Instance.spyCooldown;
        ElectricGate.SetPower(false);          // goes through the normal power sync, so it looks like any outage
        return true;
    }

    public static float SpyCooldownLeft => Instance == null ? 0f : Mathf.Max(0f, Instance.spyReadyAt - Time.time);

    // a short electrical buzz, made in code (no sound file needed)
    static AudioClip MakeBuzz()
    {
        int rate = 22050, n = rate * 3;
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate;
            float crackle = Mathf.PerlinNoise(t * 40f, 0f) > 0.55f ? 1f : 0.35f;
            data[i] = (Mathf.Sin(2 * Mathf.PI * 120f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 240f * t) * 0.25f) * crackle * 0.35f * Mathf.Clamp01((3f - t) * 2f);
        }
        var c = AudioClip.Create("PowerBuzz", n, 1, rate, false);
        c.SetData(data, 0);
        return c;
    }
}
