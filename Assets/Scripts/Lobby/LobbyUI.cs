// Farm Chaos - lobby screens: party panel (size 1-5, join code, HOST / START / LEAVE), join-by-code box, story intro.
// Online (when the Network object is in the Lobby):
//   roll into a circle -> HOST makes a real party + join code -> friends type the code -> host presses START
//   -> everyone plays the intro together -> the host loads the farm for the whole party.
// If going online fails (no internet / not signed in) it quietly falls back to playing solo offline.
using System.Collections;
using System.Linq;
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

        AddSixthSizeButton();
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
        SetUpCircles();
    }

    // one party circle for everything: the host rolls in to host / start, friends roll in to say "ready"
    void SetUpCircles()
    {
        var circles = FindObjectsByType<PartyCircle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (circles.Length == 0) return;
        var party = circles.FirstOrDefault(c => c.circleNumber == 2) ?? circles[0];
        foreach (var c in circles) if (c != party) c.gameObject.SetActive(false);
        party.circleNumber = 1;
        party.gameObject.name = "Party_Circle";
        party.transform.position = new Vector3(0f, party.transform.position.y, -1f);   // right in the middle of the plaza

        party.Resize(3.0f, 1.75f);                                                    // nearly fills the round plaza

        // the host's farm rules board (left of the plaza)
        var ring = party.ring != null ? party.ring.sharedMaterial : null;
        Material lit = ring != null && ring.shader.name.Contains("Lit") ? ring : null;
        Sprite rounded = partyPanel != null && partyPanel.GetComponent<Image>() != null ? partyPanel.GetComponent<Image>().sprite : null;
        var canvas = GetComponentInParent<Canvas>();
        LobbyRulesBoard.Make(canvas != null ? canvas.transform : transform, rounded, lit);

        // paper money shop (farmhouse door), decoration spots, and the lobby animals + practice pen
        LobbyKit.Rounded = rounded;
        var ui = canvas != null ? canvas.transform : transform;
        LobbyShop.Make(ui);
        LobbyDeco.Make(ui);
        var intro = GetComponent<IntroCutscene3D>();
        LobbyCritters.Make(intro != null ? intro.chickenPrefab : null, intro != null ? intro.bunnyPrefab : null);
    }

    SpherePlayer player;
    float refreshTimer;
    bool wasTyping;

    void Update()
    {
        // don't roll the ball around while typing a code with WASD
        if (player == null) player = FindAnyObjectByType<SpherePlayer>();
        if (player != null)
        {
            bool typing = ChibiMotor.Typing();
            if (typing != wasTyping) { wasTyping = typing; player.enabled = !typing; }
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
        if (partyTitle != null) partyTitle.text = "Your Party";
        int readyN = 0, friendsN = 0;
        bool allReady = !online || NetPlayer.AllFriendsReady(out readyN, out friendsN);
        if (codeText != null) codeText.text = online ? Net.Code : (Net != null ? "- - -" : current.Code);
        if (partyStatus != null)
        {
            if (Net != null && Net.Busy) partyStatus.text = "Connecting...";
            else if (online) partyStatus.text = Net.IsHost
                ? (friendsN == 0 ? $"{Net.PlayerCount}/{Net.MaxPlayers} players  ·  share the code with friends!"
                                 : $"{Net.PlayerCount}/{Net.MaxPlayers} players  ·  {readyN}/{friendsN} friends in the circle")
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
            startButton.interactable = (Net == null || !Net.Busy) && (!online || !Net.IsHost || allReady);
            if (startLabel != null) startLabel.text = Net == null ? "START" : online ? (allReady ? "START" : "WAITING...") : "HOST";
        }
    }

    void UpdateBanner()
    {
        if (hintBanner == null || current != null) return;
        hintBanner.text = Net != null && Net.InSession
            ? (Net.IsHost ? $"Your party <b>{Net.Code}</b> · {Net.PlayerCount}/{Net.MaxPlayers} · roll into the party circle to start"
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
            await Awaitable.WaitForSecondsAsync(1.5f);   // works in web builds too (Task.Delay can hang there)
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
                await Awaitable.WaitForSecondsAsync(1.5f);   // works in web builds too (Task.Delay can hang there)
                StartCoroutine(StartRoutine());
            }
            return;
        }
        // second press (host): everyone into the intro, then the farm - once every friend is on the ready pad
        if (Net.IsHost && NetPlayer.Local != null && NetPlayer.AllFriendsReady(out _, out _)) NetPlayer.Local.BeginGame();
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

    /// <summary>Testing (F9 in the editor): play the story intro solo and go to the farm, no circle needed.</summary>
    public void TestStart() { if (!starting && (Net == null || !Net.InSession)) StartCoroutine(StartRoutine()); }

    // parties go up to 6 now: the scene has buttons 1-5, so copy the "5" button into a "6" next to it
    void AddSixthSizeButton()
    {
        if (sizeButtons == null || sizeButtons.Length != 5 || sizeButtons[4] == null || sizeButtons[3] == null) return;
        var five = (RectTransform)sizeButtons[4].transform;
        var six = Instantiate(five.gameObject, five.parent);
        six.name = "Size_6";
        var b = six.GetComponent<Button>(); b.onClick.RemoveAllListeners();
        foreach (var t in six.GetComponentsInChildren<TMP_Text>(true)) if (t.text.Trim() == "5") t.text = "6";
        var list = new System.Collections.Generic.List<Button>(sizeButtons) { b };
        sizeButtons = list.ToArray();
        // squeeze all 6 into the space the 5 used to take, so none sticks out of the panel
        var first = (RectTransform)sizeButtons[0].transform;
        float size = first.sizeDelta.x;
        float left = first.anchoredPosition.x - size * 0.5f, right = five.anchoredPosition.x + size * 0.5f;
        float newSize = Mathf.Min(size, (right - left) / 6f - 6f);
        float step = (right - left - newSize) / 5f;
        for (int i = 0; i < 6; i++)
        {
            var r = (RectTransform)sizeButtons[i].transform;
            r.sizeDelta = new Vector2(newSize, newSize);
            r.anchoredPosition = new Vector2(left + newSize * 0.5f + step * i, r.anchoredPosition.y);
            foreach (var t in r.GetComponentsInChildren<TMP_Text>(true)) { var tr = t.rectTransform; if (tr.anchorMin == tr.anchorMax) tr.sizeDelta = new Vector2(newSize, newSize); }
        }
    }

    // offline / solo
    IEnumerator StartRoutine()
    {
        starting = true;
        PartySettings.MaxPlayers = current != null ? current.maxPlayers : 1;
        PartySettings.Players = 1;
        PartySettings.JoinCode = current != null ? current.Code : "";
        PartySettings.CircleNumber = current != null ? current.circleNumber : 1;
        if (partyPanel != null) partyPanel.SetActive(false);
        if (fade != null) fade.blocksRaycasts = true;
        yield return PlayIntro();
        SceneManager.LoadScene(farmScene);
    }
}
