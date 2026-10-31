// Farm Chaos - online sessions (Unity Multiplayer Services: Relay + Lobby + Netcode for GameObjects).
// Lives on the "Network" object in the Lobby (with NetworkManager + UnityTransport) and survives scene loads.
//   await NetSession.Instance.Host(4)      -> creates a party, returns the join code
//   await NetSession.Instance.Join(code)   -> joins a friend's party from anywhere (no same Wi-Fi needed)
// Each Multiplayer Play Mode virtual player signs in with its own random profile, so 3 test players on one PC work.
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

[DefaultExecutionOrder(-10000)]   // before the NetworkManager on the same object (see Awake)
public class NetSession : MonoBehaviour
{
    public static NetSession Instance { get; private set; }

    public ISession Session { get; private set; }
    public string LastError { get; private set; } = "";
    public bool Busy { get; private set; }

    public bool InSession => Session != null;
    public bool IsHost => Session != null && Session.IsHost;
    public string Code => Session != null ? Session.Code : "";
    public int PlayerCount => Session != null ? Session.PlayerCount : 0;
    public int MaxPlayers => Session != null ? Session.MaxPlayers : 0;

    /// <summary>True once Netcode is actually running (host or connected client).</summary>
    public static bool Online => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    void Awake()
    {
        // coming back to the Lobby while still in a party: this scene's Network object is a spare copy.
        // Switch it off BEFORE its NetworkManager wakes up (destroying a live one would shut the party down).
        if (Instance != null && Instance != this) { gameObject.SetActive(false); Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    async Task<bool> SignIn()
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                // a different profile per running copy -> each test player gets its own player ID
                var options = new InitializationOptions().SetProfile("p" + UnityEngine.Random.Range(100000, 999999));
                await UnityServices.InitializeAsync(options);
            }
            while (UnityServices.State == ServicesInitializationState.Initializing) await Task.Yield();
            if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync();
            return true;
        }
        catch (Exception e) { LastError = Short(e); Debug.LogWarning("[Farm Chaos] Online sign-in failed: " + e.Message); return false; }
    }

    public async Task<bool> Host(int maxPlayers)
    {
        if (Busy) return false;
        Busy = true; LastError = "";
        try
        {
            if (!await SignIn()) return false;
            var options = new SessionOptions { MaxPlayers = Mathf.Clamp(maxPlayers, 1, 5), IsPrivate = true }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);
            Debug.Log($"[Farm Chaos] Hosting party {Session.Code} for up to {Session.MaxPlayers} players.");
            return true;
        }
        catch (Exception e) { LastError = Short(e); Debug.LogWarning("[Farm Chaos] Couldn't host: " + e.Message); Session = null; return false; }
        finally { Busy = false; }
    }

    public async Task<bool> Join(string code)
    {
        if (Busy) return false;
        Busy = true; LastError = "";
        try
        {
            if (!await SignIn()) return false;
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code.Trim().ToUpperInvariant());
            Debug.Log($"[Farm Chaos] Joined party {Session.Code} ({Session.PlayerCount}/{Session.MaxPlayers}).");
            return true;
        }
        catch (Exception e) { LastError = Short(e); Debug.LogWarning("[Farm Chaos] Couldn't join: " + e.Message); Session = null; return false; }
        finally { Busy = false; }
    }

    public async Task Leave()
    {
        var s = Session; Session = null;
        try { if (s != null) await s.LeaveAsync(); }
        catch (Exception e) { Debug.LogWarning("[Farm Chaos] Leave: " + e.Message); }
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
    }

    static string Short(Exception e)
    {
        var m = e.Message ?? "error";
        if (m.Contains("not found", StringComparison.OrdinalIgnoreCase)) return "that code wasn't found";
        if (m.Contains("full", StringComparison.OrdinalIgnoreCase)) return "that party is full";
        if (m.Contains("network", StringComparison.OrdinalIgnoreCase) || m.Contains("connect", StringComparison.OrdinalIgnoreCase)) return "no internet connection";
        return m.Length > 60 ? m.Substring(0, 60) + "..." : m;
    }
}
