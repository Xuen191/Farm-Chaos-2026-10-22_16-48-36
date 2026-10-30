// Farm Chaos - lobby screens: party panel (size 1-5, join code, HOST / START / LEAVE), join-by-code box, story intro.
// Online (when the Network object is in the Lobby):
//   roll into a circle -> HOST makes a real party + join code -> friends type the code -> host presses START
//   -> everyone plays the intro together -> the host loads the farm for the whole party.
// If going online fails (no internet / not signed in) it quietly falls back to playing solo offline.
using System.Collections;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    public static LobbyUI Instance { get; private set; }

    [Header("Party panel")]
    public GameObject partyPanel;
    public TMP_Text partyTitle;
    public TMP_Text codeText;
    public TMP_Text partyStatus;
    public Button[] sizeButtons = new Button[5];
    public Button startButton;
    public Button leaveButton;

    [Header("Join by code")]
    public Button openJoinButton;
    public GameObject joinPanel;
    public TMP_InputField codeInput;
    public Button joinButton;
    public Button closeJoinButton;
    public TMP_Text joinMessage;

    [Header("Start")]
    public TMP_Text hintBanner;
    public CanvasGroup fade;
    public TMP_Text countdown;
    public string farmScene = "Farm_Main";

    static readonly Color Picked = new Color32(0x8C, 0xCF, 0xA4, 255);
    static readonly Color NotPicked = new Color32(0xFF, 0xF0, 0xD9, 255);

    PartyCircle current;
    bool starting;
    TMP_Text startLabel;
    string defaultHint;

    static NetSession Net => NetSession.Instance;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        if (partyPanel != null) partyPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
        if (fade != null) { fade.alpha = 0f; fade.blocksRaycasts = false; }
        if (countdown != null) countdown.text = "";
        if (startButton != null) startLabel = startButton.GetComponentInChildren<TMP_Text>(true);
        if (hintBanner != null) defaultHint = hintBanner.text;
        if (codeInput != null) codeInput.characterLimit = 10;   // online codes can be longer than 5
        if (codeText != null) { codeText.enableAutoSizing = true; codeText.fontSizeMin = 24; codeText.fontSizeMax = codeText.fontSize; }

        for (int i = 0; i < sizeButtons.Length; i++)
        {
            int n = i + 1;
            if (sizeButtons[i] != null) sizeButtons[i].onClick.AddListener(() => PickSize(n));
        }
        if (startButton != null) startButton.onClick.AddListener(StartGame);
        if (leaveButton != null) leaveButton.onClick.AddListener(Leave);
        if (openJoinButton != null) openJoinButton.onClick.AddListener(() => { joinPanel.SetActive(true); if (joinMessage != null) joinMessage.text = ""; });
        if (closeJoinButton != null) closeJoinButton.onClick.AddListener(() => joinPanel.SetActive(false));
        if (joinButton != null) joinButton.onClick.AddListener(Join);
    }

    SpherePlayer player;
    float refreshTimer;

    void Update()
    {
        // don't roll the ball around while typing a code with WASD
        if (player == null) player = FindAnyObjectByType<SpherePlayer>();
        if (player != null)
        {
            bool typing = codeInput != null && codeInput.isFocused;
            if (player.enabled == typing) player.enabled = !typing;
        }

        // keep the player count / banner fresh while in an online party
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.5f;
            Refresh();
            UpdateBanner();
        }
    }

    public void OpenFor(PartyCircle c)
    {
        if (starting) return;
        current = c;
        if (partyPanel != null) partyPanel.SetActive(true);
        if (hintBanner != null) hintBanner.gameObject.SetActive(false);
        Refresh();
    }

    public void CloseFor(PartyCircle c)
    {
        if (current != c || starting) return;
        current = null;
        if (partyPanel != null) partyPanel.SetActive(false);
        if (hintBanner != null) hintBanner.gameObject.SetActive(true);
        UpdateBanner();
    }

    void PickSize(int n)
    {
        if (current == null || (Net != null && Net.InSession)) return;   // size is fixed once the party exists
        current.SetMaxPlayers(n);
        Refresh();
    }

    void Refresh()
    {
        if (current == null) return;
        bool online = Net != null && Net.InSession;
        if (partyTitle != null) partyTitle.text = $"Circle {current.circleNumber}";
        if (codeText != null) codeText.text = online ? Net.Code : (Net != null ? "- - -" : current.Code);
        if (partyStatus != null)
        {
            if (Net != null && Net.Busy) partyStatus.text = "Connecting...";
            else if (online) partyStatus.text = Net.IsHost
                ? $"{Net.PlayerCount}/{Net.MaxPlayers} players  ·  share the code with friends!"
                : $"{Net.PlayerCount}/{Net.MaxPlayers} players  ·  waiting for the host to start";
            else if (Net != null && !string.IsNullOrEmpty(Net.LastError)) partyStatus.text = "Offline: " + Net.LastError;
            else partyStatus.text = Net != null ? "Press HOST to open your party" : $"{current.Members}/{current.maxPlayers} players";
        }
        int size = online ? Net.MaxPlayers : current.maxPlayers;
        for (int i = 0; i < sizeButtons.Length; i++)
            if (sizeButtons[i] != null) sizeButtons[i].GetComponent<Image>().color = (i + 1 == size) ? Picked : NotPicked;

        if (startButton != null)
        {
            bool showStart = !online || Net.IsHost;
            startButton.gameObject.SetActive(showStart);
            startButton.interactable = Net == null || !Net.Busy;
            if (startLabel != null) startLabel.text = Net == null ? "START" : online ? "START" : "HOST";
        }
    }

    void UpdateBanner()
    {
        if (hintBanner == null || current != null) return;
        hintBanner.text = Net != null && Net.InSession
            ? (Net.IsHost ? $"Your party <b>{Net.Code}</b> · {Net.PlayerCount}/{Net.MaxPlayers} · roll into a circle to start"
                          : $"In party <b>{Net.Code}</b> · {Net.PlayerCount}/{Net.MaxPlayers} · waiting for the host")
            : defaultHint;
    }

    async void Leave()
    {
        if (Net != null && Net.InSession && !starting) await Net.Leave();
        Refresh(); UpdateBanner();
        if (current == null) return;
        // roll the player back out of the circle
        var p = FindAnyObjectByType<SpherePlayer>();
        if (p != null)
        {
            Vector3 away = p.transform.position - current.transform.position; away.y = 0;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            p.transform.position = current.transform.position + away.normalized * 4f + Vector3.up * 0.8f;
        }
    }

    async void Join()
    {
        string code = codeInput != null ? codeInput.text.Trim().ToUpperInvariant() : "";
        if (joinMessage == null) return;
        if (Net == null) { joinMessage.text = "Online play isn't set up in this build."; return; }
        if (code.Length < 4) { joinMessage.text = "Type the code your friend sees on their screen."; return; }
        if (Net.InSession) { joinMessage.text = "You're already in a party. Press Leave first."; return; }
        joinMessage.text = $"Joining <b>{code}</b>...";
        if (await Net.Join(code))
        {
            joinMessage.text = $"Joined <b>{Net.Code}</b>! Waiting for the host to start...";
            await System.Threading.Tasks.Task.Delay(1500);
            if (joinPanel != null) joinPanel.SetActive(false);
            UpdateBanner();
        }
        else joinMessage.text = "Couldn't join: " + Net.LastError;
    }

    async void StartGame()
    {
        if (current == null || starting) return;
        if (Net == null) { StartCoroutine(StartRoutine()); return; }          // no network object -> old offline flow
        if (Net.Busy) return;

        if (!Net.InSession)
        {
            // first press: open the party
            Refresh();
            bool ok = await Net.Host(current.maxPlayers);
            Refresh();
            if (!ok)
            {
                if (partyStatus != null) partyStatus.text = $"Couldn't go online ({Net.LastError}). Starting solo...";
                await System.Threading.Tasks.Task.Delay(1500);
                StartCoroutine(StartRoutine());
            }
            return;
        }
        // second press (host): everyone into the intro, then the farm
        if (Net.IsHost && NetPlayer.Local != null) NetPlayer.Local.BeginGame();
    }

    /// <summary>Called on every player when the host presses START in an online party.</summary>
    public void PlayOnlineIntro(bool isHost)
    {
        if (starting) return;
        StartCoroutine(OnlineIntro(isHost));
    }

    IEnumerator OnlineIntro(bool isHost)
    {
        starting = true;
        PartySettings.MaxPlayers = Net != null ? Net.MaxPlayers : 4;
        PartySettings.JoinCode = Net != null ? Net.Code : "";
        if (partyPanel != null) partyPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);
        if (fade != null) fade.blocksRaycasts = true;
        yield return PlayIntro();
        if (isHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene(farmScene, LoadSceneMode.Single);
        // clients: the host's scene load brings us along
    }

    IEnumerator PlayIntro()
    {
        var cut = GetComponent<IntroCutscene3D>();
        if (cut != null && cut.enabled) { if (fade != null) fade.alpha = 0f; yield return cut.Play(); yield break; }
        var intro = GetComponent<IntroStory>();
        if (intro != null) { if (fade != null) fade.alpha = 0f; yield return intro.Play(); yield break; }
        for (int n = 3; n >= 1; n--)
        {
            if (countdown != null) countdown.text = n.ToString();
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime;
                if (countdown != null) countdown.transform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(t * 3f));
                if (fade != null && n == 1) fade.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }
        if (countdown != null) countdown.text = "";
    }

    // offline / solo
    IEnumerator StartRoutine()
    {
        starting = true;
        PartySettings.MaxPlayers = current != null ? current.maxPlayers : 1;
        PartySettings.JoinCode = current != null ? current.Code : "";
        PartySettings.CircleNumber = current != null ? current.circleNumber : 1;
        if (partyPanel != null) partyPanel.SetActive(false);
        if (fade != null) fade.blocksRaycasts = true;
        yield return PlayIntro();
        SceneManager.LoadScene(farmScene);
    }
}
