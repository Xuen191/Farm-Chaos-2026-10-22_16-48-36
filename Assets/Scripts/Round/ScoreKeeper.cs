// Farm Chaos - SCORE for the night (each player has their own).
//   Farmers: chicken home 10, bunny/sheep 15, cow 25 - fix the power 50 - reset the breaker 20
//            hit the wolf 5, scare it off 30 - tie up the SPY +100 (the spy loses 50), tie up a farmer -30
//   Spy:     overload the power 40 - at sunrise +5 for every animal still out, +100 if never tied up
//   Sunrise: 70%+ of the animals home = FARMERS WIN (+50 each, plus 5 per animal home), otherwise THE SPY WINS (+50)
//            (no spy with fewer than 3 players: THE FARM IS SAVED / THE ANIMALS GOT AWAY)
// Paper money at sunrise = your final score / 5.
// The score shows top-left during the night, with little "+15" pop-ups.
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ScoreKeeper
{
    public const float WinShare = 0.7f;
    public static int Score { get; private set; }
    public static event Action<int, string> Scored;      // points, reason
    public static bool TiedUp;                           // (host) the spy got tied up at least once tonight

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Score = 0; Scored = null; TiedUp = false; Results = null; paid = false; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

    static void OnScene(Scene s, LoadSceneMode m)
    {
        if (UnityEngine.Object.FindAnyObjectByType<RoundClock>() == null) return;   // only on the farm
        Score = 0; TiedUp = false; Results = null; paid = false;
        AnimalBrain.Returned -= OnReturned; AnimalBrain.Returned += OnReturned;
        if (UnityEngine.Object.FindAnyObjectByType<ScoreHUD>() == null) new GameObject("Score_HUD").AddComponent<ScoreHUD>();
    }

    static int combo; static float lastHome = -99f;

    static void OnReturned(AnimalBrain a, bool local) { if (local) AnimalHome(a.kind.ToString()); }

    /// <summary>You brought an animal home (also called online when the host tells you).</summary>
    public static void AnimalHome(string kind)
    {
        int pts = kind == "Cow" ? 25 : kind == "Chicken" ? 10 : 15;
        Add(pts, kind + " home!");
        // COMBO: animals brought home close together
        combo = Time.time - lastHome < 5f ? combo + 1 : 1;
        lastHome = Time.time;
        if (combo >= 2) Add(10 * (combo - 1), "COMBO x" + combo + "!");
    }

    public static void Add(int points, string reason)
    {
        if (points == 0) return;
        Score += points;
        Scored?.Invoke(points, reason);
    }

    // ------------------------------------------------------------ sunrise results
    [Serializable] public class Entry { public ulong id; public string name; public int score; public bool spy; }
    [Serializable] public class Board
    {
        public bool spyMode, farmersWin; public int home, total; public string spyName = "";
        public List<Entry> entries = new List<Entry>();
    }
    /// <summary>Set at sunrise (offline: straight away; online: when the host's results arrive).</summary>
    public static Board Results { get; private set; }
    public static event Action ResultsReady;
    static bool paid;

    public static void CountAnimals(out int home, out int total)
    {
        home = 0; total = 0;
        foreach (var a in AnimalBrain.All)
        {
            if (a == null || !a.isActiveAndEnabled) continue;
            total++;
            if (a.state == AnimalBrain.State.Grazing || a.state == AnimalBrain.State.Returning) home++;
        }
    }

    /// <summary>Host / solo: work out the winner and everyone's final score (bonuses included).</summary>
    public static Board Decide(List<Entry> players, ulong spyId, bool spyTied)
    {
        CountAnimals(out int home, out int total);
        var b = new Board { home = home, total = total, spyMode = spyId != ulong.MaxValue };
        b.farmersWin = total == 0 || home >= Mathf.CeilToInt(total * WinShare);
        foreach (var e in players)
        {
            e.spy = b.spyMode && e.id == spyId;
            if (e.spy)
            {
                b.spyName = e.name;
                e.score += 5 * (total - home);                 // chaos: animals still out
                if (!spyTied) e.score += 100;                  // never caught
                if (!b.farmersWin) e.score += 50;
            }
            else
            {
                e.score += 5 * home;                           // team bonus: every animal home
                if (b.farmersWin) e.score += 50;
            }
        }
        b.entries = players.OrderByDescending(e => e.score).ToList();
        return b;
    }

    /// <summary>Everyone: the results are in - show them and pay out paper money once.</summary>
    public static void SetResults(Board b, ulong myId)
    {
        Results = b;
        var me = b.entries.FirstOrDefault(e => e.id == myId);
        if (me != null && !paid) { paid = true; LastPaper = Mathf.Max(0, me.score) / 5; Bank.Add(LastPaper); FinalScore = me.score; }
        ResultsReady?.Invoke();
    }
    public static int LastPaper { get; private set; }
    public static int FinalScore { get; private set; }

    /// <summary>My name for the scoreboard.</summary>
    public static string MyName()
    {
        var n = ChibiLook.LoadSaved().name;
        if (!string.IsNullOrEmpty(n)) return n;
        return NetPlayer.Local != null ? "Player " + NetPlayer.Local.Number : "You";
    }
}

/// <summary>Top-left "SCORE 120" + floating "+15 Chicken home!" pop-ups.</summary>
public class ScoreHUD : MonoBehaviour
{
    TextMeshProUGUI scoreText;
    RectTransform root;
    readonly List<(RectTransform r, TextMeshProUGUI t, float age)> pops = new List<(RectTransform, TextMeshProUGUI, float)>();
    float bump;

    void Start()
    {
        var canvasGo = new GameObject("Score_Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 40;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("Score_Pill", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        root.SetParent(canvasGo.transform, false);
        // top-right, just under the coin counter (the top-left belongs to the animal counter)
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(1f, 1f);
        root.anchoredPosition = new Vector2(-30, -122); root.sizeDelta = new Vector2(300, 74);
        root.GetComponent<Image>().color = new Color(1f, 0.96f, 0.92f, 0.92f);
        scoreText = new GameObject("Score_Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        scoreText.transform.SetParent(root, false);
        scoreText.rectTransform.anchorMin = Vector2.zero; scoreText.rectTransform.anchorMax = Vector2.one;
        scoreText.rectTransform.offsetMin = new Vector2(16, 4); scoreText.rectTransform.offsetMax = new Vector2(-16, -4);
        scoreText.fontSize = 40; scoreText.fontStyle = FontStyles.Bold; scoreText.alignment = TextAlignmentOptions.Left;
        scoreText.color = new Color32(0x6B, 0x4A, 0x3A, 255);
        ScoreKeeper.Scored += OnScored;
        Refresh();
    }

    void OnDestroy() { ScoreKeeper.Scored -= OnScored; }

    void Refresh() => scoreText.text = $"<color=#E06C8C>SCORE</color>  {ScoreKeeper.Score}";

    void OnScored(int pts, string reason)
    {
        Refresh(); bump = 1f;
        var t = new GameObject("Pop", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        t.transform.SetParent(root.parent, false);
        var r = t.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(1f, 1f); r.sizeDelta = new Vector2(700, 50);
        r.anchoredPosition = new Vector2(-40, -206);
        t.text = (pts > 0 ? "+" : "") + pts + "  " + reason;
        t.fontSize = 34; t.fontStyle = FontStyles.Bold; t.alignment = TextAlignmentOptions.Right;
        t.color = pts > 0 ? new Color32(0x3F, 0xAF, 0x5A, 255) : new Color32(0xE0, 0x4C, 0x5C, 255);
        t.outlineWidth = 0.2f; t.outlineColor = Color.white;
        pops.Add((r, t, 0f));
    }

    void Update()
    {
        bump = Mathf.MoveTowards(bump, 0f, Time.unscaledDeltaTime * 4f);
        if (root != null) root.localScale = Vector3.one * (1f + 0.12f * bump);
        for (int i = pops.Count - 1; i >= 0; i--)
        {
            var (r, t, age) = pops[i];
            age += Time.unscaledDeltaTime;
            if (r == null) { pops.RemoveAt(i); continue; }
            r.anchoredPosition = new Vector2(-40, -206 - age * 40f);
            t.alpha = 1f - Mathf.Clamp01((age - 1.6f) / 0.6f);
            if (age > 2.2f) { Destroy(r.gameObject); pops.RemoveAt(i); continue; }
            pops[i] = (r, t, age);
        }
    }
}
