// Farm Chaos - THE party circle in the lobby.
//   Host: roll in to open your party (HOST / START).
//   Friends (online): roll in = "I'm ready!" (READY! shows under your name); START unlocks when every friend is in.
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PartyCircle : MonoBehaviour
{
    public int circleNumber = 1;
    [Range(1, 6)] public int maxPlayers = 4;
    public Renderer ring;
    public Material idleMaterial, activeMaterial;
    public TMP_Text sign;
    public string playerTag = "Player";

    public int Members { get; private set; }
    public string Code { get; private set; }

    const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    Vector3 ringScale;

    void Start()
    {
        GetComponent<Collider>().isTrigger = true;
        if (ring != null) ringScale = ring.transform.localScale;
        Refresh();
    }

    bool IsPlayer(Collider c) =>
        c.CompareTag(playerTag) || (c.attachedRigidbody != null && c.attachedRigidbody.CompareTag(playerTag));

    void OnTriggerEnter(Collider c)
    {
        if (!IsPlayer(c)) return;
        Members = 1;
        inside = true;
        if (string.IsNullOrEmpty(Code)) Code = MakeCode();
        Refresh();
        if (LobbyUI.Instance != null) LobbyUI.Instance.OpenFor(this);
    }

    void OnTriggerExit(Collider c)
    {
        if (!IsPlayer(c)) return;
        Members = 0;
        inside = false;
        Code = null;
        Refresh();
        if (LobbyUI.Instance != null) LobbyUI.Instance.CloseFor(this);
    }

    /// <summary>Make the circle bigger (sx across, sz deep) and move its sign to the front edge, facing you.</summary>
    public void Resize(float sx, float sz)
    {
        foreach (Transform t in transform)
            if (t.name == "Ring" || t.name == "Inner" || t.name == "Dot")
            { var s = t.localScale; t.localScale = new Vector3(s.x * sx, s.y, s.z * sz); }
        if (ring != null) ringScale = ring.transform.localScale;
        var box = GetComponent<BoxCollider>();
        if (box != null) box.size = new Vector3(box.size.x * sx, box.size.y, box.size.z * sz);
        // the sign stood behind the circle - put it at the front edge (toward the path you come in on)
        float front = -(ring != null ? ring.transform.localScale.z * 0.5f : 2.3f) - 0.5f;
        foreach (Transform t in transform)
            if (t.name == "Sign_Post" || t.name == "Sign_Board" || (sign != null && t == sign.transform))
            {
                var p = t.localPosition;
                t.localPosition = new Vector3(p.x - 2.6f, p.y, front + (p.z - 2.7f));   // a little to the left so it doesn't block the middle
            }
    }

    public void SetMaxPlayers(int n)
    {
        maxPlayers = Mathf.Clamp(n, 1, PartySettings.MaxPartySize);
        Refresh();
    }

    bool inside;

    void Update()
    {
        // online friends: standing in the circle = ready
        bool online = NetSession.Online && NetPlayer.Local != null;
        bool host = online && Unity.Netcode.NetworkManager.Singleton.IsServer;
        if (online && !host) NetPlayer.Local.SetReady(inside);
        if (sign != null)
        {
            string text;
            if (online && host)
            {
                NetPlayer.AllFriendsReady(out int r, out int n);
                text = n == 0 ? "PARTY\n<size=70%>waiting for friends</size>" : $"PARTY\n<size=70%>{r}/{n} friends ready</size>";
            }
            else if (online) text = inside ? "READY!\n<size=70%>waiting for the host</size>" : "PARTY\n<size=70%>roll in when ready</size>";
            else text = Members > 0 ? "PARTY\n<size=70%>here we go!</size>" : "PARTY\n<size=70%>roll in to host</size>";
            if (sign.text != text) sign.text = text;
        }
        if (ring == null) return;
        float pulse = Members > 0 ? 1f + 0.04f * Mathf.Sin(Time.time * 4f) : 1f;
        ring.transform.localScale = new Vector3(ringScale.x * pulse, ringScale.y, ringScale.z * pulse);
    }

    void Refresh()
    {
        if (ring != null && idleMaterial != null && activeMaterial != null)
            ring.sharedMaterial = Members > 0 ? activeMaterial : idleMaterial;
    }

    void OnDisable() { if (NetPlayer.Local != null) NetPlayer.Local.SetReady(false); }

    static string MakeCode()
    {
        var c = new char[5];
        for (int i = 0; i < c.Length; i++) c[i] = Letters[Random.Range(0, Letters.Length)];
        return new string(c);
    }
}
