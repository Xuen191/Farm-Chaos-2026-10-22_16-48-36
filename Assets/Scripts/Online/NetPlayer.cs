// Farm Chaos - one of these is spawned for every player in an online party.
// Your own one is invisible and just follows your rolling ball; everyone else's shows up as a coloured ball
// with a name tag. It also keeps the shared things in sync:
//   * power cut / power fixed (power box)        * electric gate switches
//   * the round clock (host's clock wins)         * START: everyone plays the intro, then the host loads the farm
//   * SPY MODE: with 3+ players in the farm, the host secretly picks a spy; each player is told only their own role
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class NetPlayer : NetworkBehaviour
{
    public static NetPlayer Local { get; private set; }
    /// <summary>True while we're applying something that came from the network (so we don't echo it back).</summary>
    public static bool ApplyingRemote { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Local = null; SpyClientId = ulong.MaxValue; IAmSpy = false; }

    [Header("Spy mode")]
    public static int MinPlayersForSpy = 3;
    public static string SpyGoal = "Secret mission: keep the farm in CHAOS until sunrise - mess with the power, open the gates, let the animals out... but don't get caught!";
    public static string FarmerGoal = "One of you is a SPY sabotaging the farm! Fix the power, round up the animals - and keep an eye on each other.";
    /// <summary>Host only: who the spy is this round (ulong.MaxValue = no spy).</summary>
    public static ulong SpyClientId { get; private set; } = ulong.MaxValue;
    /// <summary>On your own device: true if YOU are the spy this round.</summary>
    public static bool IAmSpy { get; private set; }

    public Renderer body;
    public TextMeshPro nameTag;

    static readonly Color[] Palette =
    {
        new Color32(0x86, 0xC3, 0xFA, 255), new Color32(0xF4, 0x9A, 0xB4, 255), new Color32(0x9B, 0xDE, 0xA8, 255),
        new Color32(0xFF, 0xD2, 0x7A, 255), new Color32(0xC3, 0xA6, 0xF2, 255),
    };

    public int Number => (int)(OwnerClientId % 5) + 1;
    public Color Colour => Palette[(int)(OwnerClientId % (ulong)Palette.Length)];

    Transform follow;
    float clockTimer;
    string placedInScene = "";

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);
        if (body != null)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", Colour);
            body.SetPropertyBlock(mpb);
        }
        if (nameTag != null) nameTag.text = "Player " + Number;

        if (IsOwner)
        {
            Local = this;
            // my own copy: invisible, no collider (my real ball is the scene's SpherePlayer)
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
            ElectricGate.PowerChanged += OnLocalPower;
            GateSwitch.Toggled += OnLocalGate;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TintLocalBall();
            if (IsServer && NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoadedForAll;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            if (Local == this) Local = null;
            ElectricGate.PowerChanged -= OnLocalPower;
            GateSwitch.Toggled -= OnLocalGate;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadedForAll;
        }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { follow = null; placedInScene = ""; TintLocalBall(); }

    void Update()
    {
        if (IsOwner)
        {
            if (follow == null) { var p = FindAnyObjectByType<SpherePlayer>(); if (p != null) follow = p.transform; }
            if (follow != null)
            {
                PlaceAtSpawnOnce();
                transform.SetPositionAndRotation(follow.position, follow.rotation);
            }
            // host keeps everyone's clock in step
            if (IsServer)
            {
                clockTimer -= Time.deltaTime;
                if (clockTimer <= 0f && RoundClock.Instance != null && RoundClock.Instance.Running)
                {
                    clockTimer = 2f;
                    ClockRpc(RoundClock.Instance.Elapsed);
                }
            }
        }
        else if (nameTag != null && Camera.main != null)
        {
            nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - Camera.main.transform.position);
        }
    }

    // each player starts on their own spawn spot in the farm
    void PlaceAtSpawnOnce()
    {
        string scene = follow.gameObject.scene.name;
        if (placedInScene == scene) return;
        placedInScene = scene;
        var spot = GameObject.Find("Spawn_" + Number);
        if (spot == null) return;
        var rb = follow.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        follow.position = spot.transform.position + Vector3.up * 0.8f;
    }

    void TintLocalBall()
    {
        StartCoroutine(TintSoon());
        IEnumerator TintSoon()
        {
            yield return null;
            var p = FindAnyObjectByType<SpherePlayer>();
            if (p == null) yield break;
            var r = p.GetComponent<Renderer>();
            if (r == null) yield break;
            var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", Colour);
            r.SetPropertyBlock(mpb);
        }
    }

    // ------------------------------------------------------------ START (lobby)
    /// <summary>Host only: everyone plays the story intro, then the host loads the farm for the whole party.</summary>
    public void BeginGame() { if (IsServer) StartGameRpc(); }

    [Rpc(SendTo.Everyone)]
    void StartGameRpc()
    {
        if (LobbyUI.Instance != null) LobbyUI.Instance.PlayOnlineIntro(NetworkManager.Singleton.IsServer);
    }

    // ------------------------------------------------------------ power
    void OnLocalPower(bool on)
    {
        if (ApplyingRemote || !IsSpawned) return;
        PowerToServerRpc(on);
    }

    [Rpc(SendTo.Server)]
    void PowerToServerRpc(bool on) { PowerToAllRpc(on); }

    [Rpc(SendTo.Everyone)]
    void PowerToAllRpc(bool on)
    {
        if (ElectricGate.PowerOn == on) return;
        ApplyingRemote = true;
        try
        {
            if (on)
            {
                var box = FindAnyObjectByType<PowerBox>();
                if (box != null && !box.powerOn) box.TurnOnPower();   // lights + gates + box state, like fixing it yourself
                else ElectricGate.SetPower(true);
            }
            else ElectricGate.SetPower(false);
        }
        finally { ApplyingRemote = false; }
    }

    // ------------------------------------------------------------ gate switches
    void OnLocalGate(string gateName, bool open)
    {
        if (ApplyingRemote || !IsSpawned) return;
        GateToServerRpc(gateName, open);
    }

    [Rpc(SendTo.Server)]
    void GateToServerRpc(string gateName, bool open) { GateToAllRpc(gateName, open); }

    [Rpc(SendTo.Everyone)]
    void GateToAllRpc(string gateName, bool open)
    {
        foreach (var g in ElectricGate.All)
        {
            if (g == null || g.name != gateName || g.IsOpen == open) continue;
            ApplyingRemote = true;
            try { if (open) g.PopOpen(); else g.Close(); }
            finally { ApplyingRemote = false; }
        }
    }

    // ------------------------------------------------------------ spy mode
    // host: once everyone has finished loading the farm, pick the roles
    void OnSceneLoadedForAll(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> done, System.Collections.Generic.List<ulong> timedOut)
    {
        if (!IsServer || LobbyUI.Instance != null) return;          // still in the lobby
        if (FindAnyObjectByType<RoundClock>() == null) return;      // not the farm
        var ids = new System.Collections.Generic.List<ulong>(NetworkManager.ConnectedClientsIds);
        SpyClientId = ids.Count >= MinPlayersForSpy ? ids[Random.Range(0, ids.Count)] : ulong.MaxValue;
        foreach (var id in ids)
        {
            var po = NetworkManager.ConnectedClients[id].PlayerObject;
            var np = po != null ? po.GetComponent<NetPlayer>() : null;
            if (np != null) np.TellRoleRpc(SpyClientId != ulong.MaxValue, id == SpyClientId);   // goes to that player only
        }
        if (SpyClientId != ulong.MaxValue) Debug.Log($"[Farm Chaos] Spy mode: {ids.Count} players, roles sent.");
    }

    [Rpc(SendTo.Owner)]
    void TellRoleRpc(bool spyMode, bool youAreSpy)
    {
        IAmSpy = spyMode && youAreSpy;
        if (!spyMode) return;
        StartCoroutine(ShowRoleSoon());
        IEnumerator ShowRoleSoon()
        {
            yield return new WaitForSeconds(1.2f);
            RoleCard.Show(IAmSpy ? "You are the SPY" : "You are a FARMER", IAmSpy ? SpyGoal : FarmerGoal, IAmSpy);
        }
    }

    // ------------------------------------------------------------ clock
    [Rpc(SendTo.NotServer)]
    void ClockRpc(float elapsed)
    {
        var c = RoundClock.Instance;
        if (c != null && Mathf.Abs(c.Elapsed - elapsed) > 1f) c.SyncTo(elapsed);
    }
}
