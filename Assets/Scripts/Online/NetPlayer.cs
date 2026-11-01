// Farm Chaos - one of these is spawned for every player in an online party.
// Your own one is invisible and just follows your rolling ball; everyone else's shows up as a coloured ball
// with a name tag. It also keeps the shared things in sync:
//   * power cut / power fixed (power box)        * electric gate switches
//   * the round clock (host's clock wins)         * START: everyone plays the intro, then the host loads the farm
//   * SPY MODE: with 3+ players in the farm, the host secretly picks a spy; each player is told only their own role
//   * TONIGHT'S WEATHER: the host rolls it when the farm loads and everyone gets the same weather
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
    public static string SpyGoal = "Keep the farm in CHAOS until sunrise!\nOverload the power box\nOpen gates at their switch boxes\nYour bait is stinky and scares animals away\nLet go of animals you are leading\nDo not get caught!";
    public static string FarmerGoal = "One of you is a SPY sabotaging the farm!\nFix the power and round up the animals\nKeep an eye on each other";
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

    public int Number => (int)(OwnerClientId % 6) + 1;
    public Color Colour => Palette[(int)(OwnerClientId % (ulong)Palette.Length)];

    Transform follow;
    float clockTimer, animalTimer;

    /// <summary>This player's bean look (hair, hat, colours, face) as JSON - set by its owner, seen by everyone.</summary>
    readonly NetworkVariable<Unity.Collections.FixedString512Bytes> lookJson =
        new NetworkVariable<Unity.Collections.FixedString512Bytes>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public bool HasLook => lookJson.Value.Length > 0;

    /// <summary>Lobby ready pad: this player is standing on it (set by its owner, seen by everyone).</summary>
    readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public bool Ready => ready.Value;
    public void SetReady(bool r) { if (IsOwner && IsSpawned && ready.Value != r) ready.Value = r; }
    /// <summary>Their typed name (from their look), or "Player 2" etc.</summary>
    public string DisplayName { get; private set; } = "";
    public bool IsHostPlayer => OwnerClientId == NetworkManager.ServerClientId;

    /// <summary>Host: are all the friends standing on the ready pad? (true when there are no friends)</summary>
    public static bool AllFriendsReady(out int readyCount, out int friends)
    {
        readyCount = 0; friends = 0;
        foreach (var p in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
        {
            if (!p.IsSpawned || p.IsHostPlayer) continue;
            friends++; if (p.Ready) readyCount++;
        }
        return readyCount == friends;
    }

    bool tagInLobby;

    // the name tag floats straight above the bean's head (it used to ride on the rolling ball and spin round)
    void LateUpdate()
    {
        if (IsOwner || nameTag == null) return;
        var cam = Camera.main;
        nameTag.transform.position = transform.position + Vector3.up * 2.3f;
        if (cam != null) nameTag.transform.rotation = Quaternion.LookRotation(nameTag.transform.position - cam.transform.position);
    }
    void UpdateTag()
    {
        if (nameTag == null) return;
        string n = string.IsNullOrEmpty(DisplayName) ? "Player " + Number : DisplayName;
        bool inLobby = LobbyUI.Instance != null;
        nameTag.text = Ready && inLobby ? n + "\n<size=70%><color=#3FAF5A>READY!</color></size>" : n;
    }

    static string NameIn(string json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        try { var d = JsonUtility.FromJson<ChibiLook.Data>(json); return d != null && d.name != null ? d.name.Trim() : ""; } catch { return ""; }
    }

    // ------------------------------------------------------------ emotes (V / C) - everyone sees them
    void SendEmote(bool cheer) { if (IsOwner && IsSpawned) EmoteRpc(cheer); }

    [Rpc(SendTo.NotOwner)]
    void EmoteRpc(bool cheer)
    {
        var m = GetComponentInChildren<ChibiMotor>(true);
        if (m != null) m.Emote(cheer);
    }
    public string LookJson => lookJson.Value.ToString();
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
        DisplayName = NameIn(lookJson.Value.ToString());
        UpdateTag();
        lookJson.OnValueChanged += (_, v) => { DisplayName = NameIn(v.ToString()); UpdateTag(); ApplyLook(v.ToString()); };
        ready.OnValueChanged += (_, v) => UpdateTag();

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
            ShareMyLook();
            ChibiLook.Saved += ShareMyLook;                  // changed clothes in the wardrobe -> everyone sees it
            ChibiMotor.LocalEmote += SendEmote;              // wave / cheer -> everyone sees it
        }
        else ApplyLook(lookJson.Value.ToString());
    }

    // ------------------------------------------------------------ bean looks
    void ShareMyLook() { if (IsOwner) lookJson.Value = new Unity.Collections.FixedString512Bytes(JsonUtility.ToJson(ChibiLook.LoadSaved())); }

    public void ReapplyLook() => ApplyLook(lookJson.Value.ToString());

    /// <summary>Dress this (other) player's bean in their own saved look.</summary>
    public void ApplyLook(string json)
    {
        if (IsOwner || string.IsNullOrEmpty(json)) return;
        StartCoroutine(Soon());
        IEnumerator Soon()
        {
            yield return null;                                // the bean may still be waking up
            var look = GetComponentInChildren<ChibiLook>(true);
            if (look == null) yield break;
            try { JsonUtility.FromJsonOverwrite(json, look.data); } catch { yield break; }
            look.Apply();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            if (Local == this) Local = null;
            ChibiLook.Saved -= ShareMyLook;
            ChibiMotor.LocalEmote -= SendEmote;
            ElectricGate.PowerChanged -= OnLocalPower;
            GateSwitch.Toggled -= OnLocalGate;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoadedForAll;
        }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode m) { follow = null; placedInScene = ""; TintLocalBall(); SetReady(false); reported = false; if (IsServer) reports.Clear(); }

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
            ReportAtSunrise();
            // host keeps everyone's clock in step
            if (IsServer)
            {
                ShareRules();
                clockTimer -= Time.deltaTime;
                if (clockTimer <= 0f && RoundClock.Instance != null && RoundClock.Instance.Running)
                {
                    clockTimer = 2f;
                    ClockRpc(RoundClock.Instance.Elapsed);
                }
                // ...and everyone's animals (the host runs them, the others just play them back)
                animalTimer -= Time.deltaTime;
                if (animalTimer <= 0f && AnimalBrain.All.Count > 0)
                {
                    animalTimer = 0.1f;
                    AnimalsRpc(AnimalBrain.Pack());
                }
            }
        }
        else if (nameTag != null && Camera.main != null)
        {
            if (tagInLobby != (LobbyUI.Instance != null)) { tagInLobby = !tagInLobby; UpdateTag(); }   // READY! only shows in the lobby
        }
    }

    // each player starts on their own spawn spot in the farm
    void PlaceAtSpawnOnce()
    {
        string scene = follow.gameObject.scene.name;
        if (placedInScene == scene) return;
        placedInScene = scene;
        var spot = GameObject.Find("Spawn_" + Number);
        Vector3 nudge = Vector3.zero;
        if (spot == null)
        {   // players 5 and 6: the farm only has 4 spawn spots, so stand next to one of them
            spot = GameObject.Find("Spawn_" + ((Number - 1) % 4 + 1));
            nudge = new Vector3(1.6f, 0f, 0f);
        }
        if (spot == null) return;
        var rb = follow.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        follow.position = spot.transform.position + spot.transform.rotation * nudge + Vector3.up * 0.8f;
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
    public void BeginGame()
    {
        if (!IsServer) return;
        var (weather, weatherSeed) = TonightsWeather.RollForIntro();   // tonight's weather is picked now, so the intro can show it
        StartGameRpc(weather, weatherSeed, PartySettings.Animals, PartySettings.Weather, NetworkManager.Singleton.ConnectedClientsIds.Count);
    }

    [Rpc(SendTo.Everyone)]
    void StartGameRpc(int weather, int weatherSeed, int animals, int weatherRule, int players)
    {
        PartySettings.SetFromHost(animals, weatherRule, players);              // the host's farm rules (settings board)
        TonightsWeather.Preset(weather, weatherSeed);
        if (LobbyUI.Instance != null) LobbyUI.Instance.PlayOnlineIntro(NetworkManager.Singleton.IsServer);
    }

    // ------------------------------------------------------------ farm rules (lobby settings board)
    float rulesTimer;
    /// <summary>Host: keep everyone's settings board showing the host's rules.</summary>
    void ShareRules()
    {
        rulesTimer -= Time.deltaTime;
        if (rulesTimer > 0f) return;
        rulesTimer = 1f;
        RulesRpc(PartySettings.Animals, PartySettings.Weather);
        if (LobbyUI.Instance != null) DecoRpc(LobbyDeco.MyLayout);      // the host's lobby decorations, for everyone
    }

    [Rpc(SendTo.NotServer)]
    void DecoRpc(string layout) { LobbyDeco.HostLayout = layout; }

    [Rpc(SendTo.NotServer)]
    void RulesRpc(int animals, int weatherRule) { PartySettings.SetFromHost(animals, weatherRule); }

    // ------------------------------------------------------------ end of the night (Sunrise screen)
    /// <summary>How many friends pressed PLAY AGAIN (shown on the host's Sunrise screen).</summary>
    public static int AgainVotes { get; private set; }
    static readonly System.Collections.Generic.HashSet<ulong> againVoters = new System.Collections.Generic.HashSet<ulong>();

    /// <summary>Friend: "I want to play again".</summary>
    public void VotePlayAgain() { if (IsOwner) VoteAgainRpc(); }

    [Rpc(SendTo.Server)]
    void VoteAgainRpc() { againVoters.Add(OwnerClientId); AgainCountRpc(againVoters.Count); }

    [Rpc(SendTo.Everyone)]
    void AgainCountRpc(int votes) { AgainVotes = votes; }

    /// <summary>Host: take the whole party to a scene (a new night on the farm, or back to the lobby).</summary>
    public void HostLoadForEveryone(string scene)
    {
        if (!IsServer) return;
        againVoters.Clear(); AgainCountRpc(0);
        NetworkManager.SceneManager.LoadScene(scene, LoadSceneMode.Single);
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

    // ------------------------------------------------------------ farm animals (host -> everyone)
    [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
    void AnimalsRpc(byte[] data) { AnimalBrain.Unpack(data); }

    /// <summary>Host: this player brought an animal home - pay them (coins stay per player).</summary>
    public void GiveAnimalCoins(int coins, string kind) { if (IsServer) AnimalCoinsRpc(coins, kind); }
    [Rpc(SendTo.Owner)]
    void AnimalCoinsRpc(int coins, string kind) { if (CoinWallet.Exists) CoinWallet.Add(coins, kind + " returned"); ScoreKeeper.AnimalHome(kind); }

    // ------------------------------------------------------------ animals: grabbing + bait (the host runs the animals)
    public void SendCatch(int animalIndex, bool success) { if (IsOwner && IsSpawned) CatchServerRpc(animalIndex, success); }
    [Rpc(SendTo.Server)]
    void CatchServerRpc(int animalIndex, bool success)
    {
        var a = AnimalBrain.AtIndex(animalIndex);
        if (a != null) a.HostCatch(transform, false, this, success);
    }

    public void SendBait(Vector3 pos, string item) { if (IsOwner && IsSpawned) BaitServerRpc(pos, item); }
    [Rpc(SendTo.Server)]
    void BaitServerRpc(Vector3 pos, string item) { AnimalBait.Add(pos, item); }

    // ------------------------------------------------------------ spy sabotage
    /// <summary>Tell EVERYONE something happened somewhere ("where, not who").</summary>
    public static void Alert(string msg)
    {
        if (NetSession.Online && Local != null && Local.IsSpawned) Local.AlertServerRpc(msg);
        else ShowAlert(msg);
    }
    [Rpc(SendTo.Server)] void AlertServerRpc(string msg) { AlertAllRpc(msg); }
    [Rpc(SendTo.Everyone)] void AlertAllRpc(string msg) { ShowAlert(msg); }
    static void ShowAlert(string msg)
    {
        if (TonightsWeather.Instance != null) TonightsWeather.Instance.Toast(msg, 3f);
        Debug.Log("[Farm Chaos] " + msg);
    }

    /// <summary>Spy (fake helper): quietly let go of every animal following you.</summary>
    public void SendRelease() { if (IsOwner && IsSpawned) ReleaseServerRpc(); }
    [Rpc(SendTo.Server)]
    void ReleaseServerRpc()
    {
        int n = AnimalBrain.ReleaseFollowers(transform, out var kind, out var where);
        if (n > 0) AlertAllRpc(SpySabotage.LooseMessage(kind, n, where));
    }

    // ------------------------------------------------------------ the wolf (host runs it; friends' actions go to the host)
    /// <summary>Host / solo: the wolf ate an animal - everyone loses 10 coins and sees why.</summary>
    public static void WolfAte(string kind)
    {
        if (NetSession.Online && Local != null && Local.IsSpawned && Local.IsServer) Local.WolfAteRpc(kind);
        else ApplyWolfAte(kind);
    }
    [Rpc(SendTo.Everyone)] void WolfAteRpc(string kind) { ApplyWolfAte(kind); }
    static void ApplyWolfAte(string kind)
    {
        if (CoinWallet.Exists) CoinWallet.Add(-10, "Wolf ate an animal");
        if (TonightsWeather.Instance != null) TonightsWeather.Instance.Toast($"The wolf ate a {kind.ToLower()}!", 2.5f);
    }

    public void SendWolfBomb(Vector3 at, float radius) { if (IsOwner && IsSpawned) WolfBombServerRpc(at, radius); }
    [Rpc(SendTo.Server)] void WolfBombServerRpc(Vector3 at, float radius) { WolfAI.HostBomb(at, radius); }

    public void SendWolfHit() { if (IsOwner && IsSpawned) WolfHitServerRpc(); }
    [Rpc(SendTo.Server)]
    void WolfHitServerRpc()
    {
        var wolf = WolfAI.ActiveWolf;
        if (wolf != null && wolf.Hit()) GiveScore(30, "Wolf scared off!");   // points go to the player who landed the last hit
    }

    public void SendBone(Vector3 at) { if (IsOwner && IsSpawned) BoneServerRpc(at); }
    [Rpc(SendTo.Server)] void BoneServerRpc(Vector3 at) { WolfAI.BoneAt(at); }

#if UNITY_EDITOR
    /// <summary>Editor testing only: pretend to be the spy in a solo game (F4 in the farm).</summary>
    public static void TestToggleSpy() { IAmSpy = !IAmSpy; }
#endif

    // ------------------------------------------------------------ score: ties with the rope, points from the host, sunrise results
    /// <summary>Give this player points (runs on their own device).</summary>
    public void GiveScore(int points, string reason) { if (IsServer) ScoreToOwnerRpc(points, reason); }
    [Rpc(SendTo.Owner)]
    void ScoreToOwnerRpc(int points, string reason) { ScoreKeeper.Add(points, reason); }

    /// <summary>Rope: I'm tying up that player. The host decides the points (only the host knows who the spy is).</summary>
    public void RequestTie(ulong target, float seconds) { if (IsOwner && IsSpawned) TieServerRpc(target, seconds); }

    [Rpc(SendTo.Server)]
    void TieServerRpc(ulong target, float seconds)
    {
        bool spy = SpyClientId != ulong.MaxValue && target == SpyClientId;
        if (spy)
        {
            ScoreKeeper.TiedUp = true;
            GiveScore(100, "You caught the spy!");
            var s = Find(target); if (s != null) s.GiveScore(-50, "You got tied up!");
        }
        else GiveScore(-30, "Oops... wrong farmer!");
        TieEveryoneRpc(target, seconds);
    }

    [Rpc(SendTo.Everyone)]
    void TieEveryoneRpc(ulong target, float seconds)
    {
        var who = Find(target);
        if (who == null) return;
        if (who.IsOwner)
        {
            // it's me: can't move for a while
            var me = FindAnyObjectByType<SpherePlayer>();
            if (me != null) { me.Stun(seconds); RopeRing(me.transform, seconds); }
            if (TonightsWeather.Instance != null) TonightsWeather.Instance.Toast("You got tied up!", 2.5f);
        }
        else RopeRing(who.transform, seconds);
    }

    static NetPlayer Find(ulong client)
    {
        foreach (var p in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None)) if (p.IsSpawned && p.OwnerClientId == client) return p;
        return null;
    }

    static void RopeRing(Transform t, float seconds)
    {
        var old = t.Find("Rope_Tied"); if (old != null) Destroy(old.gameObject);
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Rope_Tied"; Destroy(ring.GetComponent<Collider>());
        ring.transform.SetParent(t, false); ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        var s = t.lossyScale; ring.transform.localScale = new Vector3(1.4f / Mathf.Max(s.x, 0.01f), 0.06f / Mathf.Max(s.y, 0.01f), 1.4f / Mathf.Max(s.z, 0.01f));
        ring.GetComponent<Renderer>().material.color = new Color(0.55f, 0.36f, 0.2f);
        Destroy(ring, seconds);
    }

    // sunrise: everyone sends their score to the host, the host works out the winner and sends the board back
    static readonly System.Collections.Generic.Dictionary<ulong, ScoreKeeper.Entry> reports = new System.Collections.Generic.Dictionary<ulong, ScoreKeeper.Entry>();
    bool reported;

    void ReportAtSunrise()
    {
        if (reported || RoundClock.Instance == null || !RoundClock.Instance.Finished) return;
        reported = true;
        ReportScoreRpc(ScoreKeeper.Score, ScoreKeeper.MyName());
        if (IsServer) StartCoroutine(GatherResults());
    }

    [Rpc(SendTo.Server)]
    void ReportScoreRpc(int score, string name) { reports[OwnerClientId] = new ScoreKeeper.Entry { id = OwnerClientId, name = name, score = score }; }

    IEnumerator GatherResults()
    {
        yield return new WaitForSecondsRealtime(2.5f);       // give everyone's score a moment to arrive
        var list = new System.Collections.Generic.List<ScoreKeeper.Entry>();
        foreach (var id in NetworkManager.ConnectedClientsIds)
        {
            if (reports.TryGetValue(id, out var e)) list.Add(e);
            else { var p = Find(id); list.Add(new ScoreKeeper.Entry { id = id, name = p != null && p.DisplayName != "" ? p.DisplayName : "Player " + (id % 6 + 1), score = 0 }); }
        }
        var board = ScoreKeeper.Decide(list, SpyClientId, ScoreKeeper.TiedUp);
        ResultsRpc(JsonUtility.ToJson(board));
    }

    [Rpc(SendTo.Everyone)]
    void ResultsRpc(string json)
    {
        ScoreKeeper.Board b = null;
        try { b = JsonUtility.FromJson<ScoreKeeper.Board>(json); } catch { }
        if (b != null) ScoreKeeper.SetResults(b, NetworkManager.LocalClientId);
    }

    // ------------------------------------------------------------ unstable power warning (host -> everyone)
    public void SendPowerWarning(float seconds) { if (IsServer) PowerWarnRpc(seconds); }
    [Rpc(SendTo.Everyone)]
    void PowerWarnRpc(float seconds) { PowerGrid.ShowWarning(seconds); }

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
        // tonight's weather: the host rolls it and everyone gets the same
        var (weather, weatherSeed) = TonightsWeather.Pick();
        WeatherRpc(weather, weatherSeed);
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

    [Rpc(SendTo.Everyone)]
    void WeatherRpc(int weather, int weatherSeed) { TonightsWeather.Receive(weather, weatherSeed); }

    [Rpc(SendTo.Owner)]
    void TellRoleRpc(bool spyMode, bool youAreSpy)
    {
        IAmSpy = spyMode && youAreSpy;
        if (!spyMode) return;
        StartCoroutine(ShowRoleSoon());
        IEnumerator ShowRoleSoon()
        {
            yield return new WaitForSeconds(1.2f);
            while (TonightsWeather.Showing) yield return null;   // after the weather roll
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
