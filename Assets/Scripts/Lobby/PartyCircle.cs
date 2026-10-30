// Farm Chaos - a party circle in the lobby. Roll into it to start a party.
using TMPro;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PartyCircle : MonoBehaviour
{
    public int circleNumber = 1;
    [Range(1, 5)] public int maxPlayers = 4;
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
        if (string.IsNullOrEmpty(Code)) Code = MakeCode();
        Refresh();
        if (LobbyUI.Instance != null) LobbyUI.Instance.OpenFor(this);
    }

    void OnTriggerExit(Collider c)
    {
        if (!IsPlayer(c)) return;
        Members = 0;
        Code = null;
        Refresh();
        if (LobbyUI.Instance != null) LobbyUI.Instance.CloseFor(this);
    }

    public void SetMaxPlayers(int n)
    {
        maxPlayers = Mathf.Clamp(n, 1, 5);
        Refresh();
    }

    void Update()
    {
        if (ring == null) return;
        float pulse = Members > 0 ? 1f + 0.04f * Mathf.Sin(Time.time * 4f) : 1f;
        ring.transform.localScale = new Vector3(ringScale.x * pulse, ringScale.y, ringScale.z * pulse);
    }

    void Refresh()
    {
        if (ring != null && idleMaterial != null && activeMaterial != null)
            ring.sharedMaterial = Members > 0 ? activeMaterial : idleMaterial;
        if (sign != null)
            sign.text = $"Circle {circleNumber}\n<size=70%>{Members}/{maxPlayers} players</size>";
    }

    static string MakeCode()
    {
        var c = new char[5];
        for (int i = 0; i < c.Length; i++) c[i] = Letters[Random.Range(0, Letters.Length)];
        return new string(c);
    }
}
