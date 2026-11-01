// Farm Chaos - "Sunrise!" screen at 6:00 AM. Stops the players and offers PLAY AGAIN or BACK TO LOBBY.
//   Solo:    PLAY AGAIN reloads the farm, BACK TO LOBBY goes to the lobby.
//   Online - the HOST decides for the party:
//     PLAY AGAIN    -> everyone goes straight into a new night together (new weather, new spy)
//     BACK TO LOBBY -> everyone goes back to the lobby together, still in the same party (same code)
//   Online - friends:
//     PLAY AGAIN    -> "I'm ready!" (the host sees how many are ready)
//     LEAVE TO LOBBY-> leaves the party and goes back to the lobby on their own
// (Coin results can be added to this panel later.)
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundEndScreen : MonoBehaviour
{
    public GameObject panel;
    public Button playAgainButton;
    public CanvasGroup fade;
    public string lobbyScene = "Lobby";

    RoundClock clock;
    float shown = -1f;
    Button lobbyButton;
    TMP_Text againLabel, lobbyLabel, status;
    bool voted, leaving;

    static bool Online => NetSession.Online && NetPlayer.Local != null;
    static bool IsHost => Online && Unity.Netcode.NetworkManager.Singleton.IsServer;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(PlayAgain);
            againLabel = playAgainButton.GetComponentInChildren<TMP_Text>(true);
            BuildLobbyButton();
        }
        // this font has no ":" - write the time without it
        if (panel != null) foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true)) if (t.name == "Subtitle") t.text = "6 AM... the night is over!";
        BuildResults();
        clock = RoundClock.Instance;
        if (clock != null) clock.RoundEnded += Show;
        ScoreKeeper.ResultsReady += ShowResults;
    }

    void OnDestroy() { if (clock != null) clock.RoundEnded -= Show; ScoreKeeper.ResultsReady -= ShowResults; }

    // ------------------------------------------------------------ results board (who won, the spy, everyone's score)
    TMP_Text titleText, subText, paperText;
    RectTransform board;

    void BuildResults()
    {
        if (panel == null) return;
        foreach (var t in panel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t.name == "Title") titleText = t;
            if (t.name == "Subtitle") subText = t;
        }
        if (titleText == null) return;
        var card = (RectTransform)titleText.transform.parent;
        card.sizeDelta = new Vector2(Mathf.Max(card.sizeDelta.x, 940f), 900f);
        foreach (Transform c in card) if (c.name == "Sun") c.gameObject.SetActive(false);
        titleText.rectTransform.anchoredPosition = new Vector2(0, -85);
        titleText.enableAutoSizing = true; titleText.fontSizeMin = 40; titleText.fontSizeMax = 92;
        titleText.rectTransform.sizeDelta = new Vector2(880, 110);
        subText.rectTransform.anchoredPosition = new Vector2(0, -180);
        subText.rectTransform.sizeDelta = new Vector2(880, 80);
        subText.enableAutoSizing = true; subText.fontSizeMin = 22; subText.fontSizeMax = 38;
        subText.textWrappingMode = TextWrappingModes.Normal;

        board = new GameObject("Score_Board", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(card, false);
        board.anchorMin = board.anchorMax = new Vector2(0.5f, 1f); board.pivot = new Vector2(0.5f, 1f);
        board.anchoredPosition = new Vector2(0, -240); board.sizeDelta = new Vector2(820, 340);

        paperText = new GameObject("Paper_Money", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        paperText.transform.SetParent(card, false);
        paperText.rectTransform.anchorMin = paperText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        paperText.rectTransform.anchoredPosition = new Vector2(0, 236); paperText.rectTransform.sizeDelta = new Vector2(860, 50);
        paperText.fontSize = 34; paperText.fontStyle = FontStyles.Bold; paperText.alignment = TextAlignmentOptions.Center;
        paperText.color = new Color32(0x3E, 0x7D, 0x45, 255); paperText.raycastTarget = false;
    }

    void ShowResults()
    {
        var b = ScoreKeeper.Results;
        if (b == null || titleText == null) return;
        bool farmers = b.farmersWin;
        if (b.spyMode)
        {
            titleText.text = farmers ? "FARMERS WIN!" : "THE SPY WINS!";
            titleText.color = farmers ? (Color)new Color32(0x3F, 0xAF, 0x5A, 255) : new Color32(0x8E, 0x5C, 0xD6, 255);
            subText.text = $"The spy was <color=#8E5CD6>{b.spyName}</color>!\n<size=80%>{b.home}/{b.total} animals home by sunrise</size>";
        }
        else
        {
            titleText.text = farmers ? "THE FARM IS SAVED!" : "THE ANIMALS GOT AWAY...";
            titleText.color = farmers ? (Color)new Color32(0x3F, 0xAF, 0x5A, 255) : new Color32(0xE0, 0x6C, 0x4C, 255);
            subText.text = $"{b.home}/{b.total} animals home by sunrise";
        }

        for (int i = board.childCount - 1; i >= 0; i--) Destroy(board.GetChild(i).gameObject);
        ulong me = Online ? Unity.Netcode.NetworkManager.Singleton.LocalClientId : 0;
        int n = Mathf.Min(b.entries.Count, PartySettings.MaxPartySize);
        for (int i = 0; i < n; i++)
        {
            var e = b.entries[i];
            bool won = b.spyMode ? (e.spy != farmers) : farmers;
            var row = new GameObject("Row", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            row.SetParent(board, false);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f); row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0, -i * 62f); row.sizeDelta = new Vector2(820, 56);   // fits 6 rows
            row.GetComponent<Image>().color = won ? new Color32(0xD8, 0xF0, 0xDC, 255) : new Color32(0xF3, 0xE9, 0xE0, 255);
            string rank = i == 0 ? "1ST" : i == 1 ? "2ND" : i == 2 ? "3RD" : (i + 1) + "TH";
            Cell(row, rank, new Vector2(-340, 0), 120, TextAlignmentOptions.Center, i == 0 ? new Color32(0xE0, 0xA8, 0x2C, 255) : new Color32(0x9A, 0x86, 0x78, 255));
            string name = e.name + (e.spy ? "  <size=70%><color=#8E5CD6>SPY</color></size>" : "") + (e.id == me ? "  <size=70%>YOU</size>" : "");
            Cell(row, name, new Vector2(-20, 0), 480, TextAlignmentOptions.Left, new Color32(0x6B, 0x4A, 0x3A, 255));
            Cell(row, e.score.ToString(), new Vector2(320, 0), 160, TextAlignmentOptions.Right, new Color32(0xE0, 0x6C, 0x8C, 255));
        }
        paperText.text = ScoreKeeper.LastPaper > 0 ? $"Your score {ScoreKeeper.FinalScore}... +{ScoreKeeper.LastPaper} paper money!" : $"Your score {ScoreKeeper.FinalScore}";
    }

    void Cell(RectTransform row, string text, Vector2 pos, float width, TextAlignmentOptions align, Color c)
    {
        var t = new GameObject("Cell", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(row, false);
        t.rectTransform.anchoredPosition = pos; t.rectTransform.sizeDelta = new Vector2(width, 54);
        t.text = text; t.fontSize = 36; t.fontStyle = FontStyles.Bold; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = 36; t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    // a second button next to PLAY AGAIN, plus a little status line above them
    void BuildLobbyButton()
    {
        var againRt = (RectTransform)playAgainButton.transform;
        var go = Instantiate(playAgainButton.gameObject, againRt.parent);
        go.name = "Back_To_Lobby";
        lobbyButton = go.GetComponent<Button>();
        lobbyButton.onClick.RemoveAllListeners();
        lobbyButton.onClick.AddListener(BackToLobby);
        lobbyLabel = go.GetComponentInChildren<TMP_Text>(true);
        var img = go.GetComponent<Image>(); if (img != null) img.color = new Color32(0xF4, 0x9A, 0xB0, 255);

        var size = againRt.sizeDelta; size.x = Mathf.Min(size.x, 350f);
        var lobbyRt = (RectTransform)go.transform;
        againRt.sizeDelta = lobbyRt.sizeDelta = size;
        Vector2 basePos = againRt.anchoredPosition;
        againRt.anchoredPosition = basePos + new Vector2(-190f, 0f);
        lobbyRt.anchoredPosition = basePos + new Vector2(190f, 0f);
        foreach (var l in new[] { againLabel, lobbyLabel })
            if (l != null) { l.enableAutoSizing = true; l.fontSizeMin = 22; l.fontSizeMax = 40; l.rectTransform.sizeDelta = new Vector2(size.x - 24f, size.y - 10f); }

        var st = new GameObject("Status", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        st.transform.SetParent(againRt.parent, false);
        st.rectTransform.anchorMin = st.rectTransform.anchorMax = againRt.anchorMin;
        st.rectTransform.anchoredPosition = basePos + new Vector2(0f, size.y * 0.5f + 34f);
        st.rectTransform.sizeDelta = new Vector2(760f, 50f);
        st.fontSize = 30; st.alignment = TextAlignmentOptions.Center; st.color = new Color32(0x6B, 0x4A, 0x3A, 255);
        st.raycastTarget = false;
        status = st;
    }

    void Show()
    {
        if (panel != null) panel.SetActive(true);
        shown = 0f; voted = false; leaving = false;
        // the results: solo = right now; online = when the host has everyone's scores
        if (titleText != null) { titleText.text = "SUNRISE!"; subText.text = Online ? "Counting the scores..." : ""; }
        if (paperText != null) paperText.text = "";
        if (!Online)
        {
            var me = new System.Collections.Generic.List<ScoreKeeper.Entry> { new ScoreKeeper.Entry { id = 0, name = ScoreKeeper.MyName(), score = ScoreKeeper.Score } };
            ScoreKeeper.SetResults(ScoreKeeper.Decide(me, ulong.MaxValue, false), 0);
        }
        else if (ScoreKeeper.Results != null) ShowResults();
        // freeze every sphere player
        foreach (var p in FindObjectsByType<SpherePlayer>(FindObjectsSortMode.None))
        {
            p.enabled = false;
            var rb = p.GetComponent<Rigidbody>();
            if (rb != null) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
        Refresh();
    }

    void Update()
    {
        if (shown < 0f) return;
        shown += Time.unscaledDeltaTime;
        if (fade != null) fade.alpha = Mathf.Clamp01(shown / 0.8f);
        Refresh();
    }

    void Refresh()
    {
        if (againLabel == null) return;
        if (!Online)
        {
            againLabel.text = "PLAY AGAIN"; if (lobbyLabel != null) lobbyLabel.text = "BACK TO LOBBY";
            if (status != null) status.text = "";
        }
        else if (IsHost)
        {
            againLabel.text = "PLAY AGAIN"; if (lobbyLabel != null) lobbyLabel.text = "BACK TO LOBBY";
            int friends = Mathf.Max(0, Unity.Netcode.NetworkManager.Singleton.ConnectedClientsIds.Count - 1);
            if (status != null) status.text = friends == 0 ? "You decide for the party" : $"{NetPlayer.AgainVotes}/{friends} friends want to play again";
        }
        else
        {
            againLabel.text = voted ? "READY!" : "PLAY AGAIN"; if (lobbyLabel != null) lobbyLabel.text = "LEAVE TO LOBBY";
            if (status != null) status.text = leaving ? "Leaving the party..." : voted ? "Waiting for the host to start again..." : "The host picks what happens next";
        }
        if (playAgainButton != null) playAgainButton.interactable = !leaving && !(Online && !IsHost && voted);
        if (lobbyButton != null) lobbyButton.interactable = !leaving;
    }

    public void PlayAgain()
    {
        if (!Online) { Load(SceneManager.GetActiveScene().name); return; }
        if (IsHost) { NetPlayer.Local.HostLoadForEveryone(SceneManager.GetActiveScene().name); return; }
        if (!voted) { voted = true; NetPlayer.Local.VotePlayAgain(); }
    }

    public void BackToLobby()
    {
        if (!Online) { Load(lobbyScene); return; }
        if (IsHost) { NetPlayer.Local.HostLoadForEveryone(lobbyScene); return; }
        if (!leaving) StartCoroutine(LeaveThenLobby());
    }

    IEnumerator LeaveThenLobby()
    {
        leaving = true;
        var t = NetSession.Instance != null ? NetSession.Instance.Leave() : null;
        while (t != null && !t.IsCompleted) yield return null;
        Load(lobbyScene);
    }

    static void Load(string scene)
    {
        TonightsWeather.FromIntro = false;
        SceneManager.LoadScene(scene);   // the scene must be in Build Profiles' scene list
    }
}
