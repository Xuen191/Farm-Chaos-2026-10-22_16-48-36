// Farm Chaos - what the lobby decided, carried into the farm scene.
// The HOST picks the farm rules on the lobby's settings board; online, everyone gets the host's choice.
using UnityEngine;

public static class PartySettings
{
    public static int MaxPlayers = 1;
    /// <summary>Biggest party allowed (1 to 6 players).</summary>
    public const int MaxPartySize = 6;
    /// <summary>How many players actually started tonight's round (more players -> a few more animals).</summary>
    public static int Players = 1;
    public static string JoinCode = "";
    public static int CircleNumber = 0;

    // ---- farm rules (host's choice)
    /// <summary>0 = few animals, 1 = normal, 2 = lots</summary>
    public static int Animals = 1;
    /// <summary>0 = random weather, 1 = always clear, 2 = weather off</summary>
    public static int Weather = 0;

    public static readonly string[] AnimalNames = { "Few", "Normal", "Lots" };
    public static readonly string[] WeatherNames = { "Random", "Always clear", "Off" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Load()
    {
        Players = 1;
        Animals = Mathf.Clamp(PlayerPrefs.GetInt("FarmChaos.Rules.Animals", 1), 0, 2);
        Weather = Mathf.Clamp(PlayerPrefs.GetInt("FarmChaos.Rules.Weather", 0), 0, 2);
    }

    /// <summary>Host changed a rule on the board: remember it for next time too.</summary>
    public static void SaveRules()
    {
        PlayerPrefs.SetInt("FarmChaos.Rules.Animals", Animals);
        PlayerPrefs.SetInt("FarmChaos.Rules.Weather", Weather);
        PlayerPrefs.Save();
    }

    /// <summary>Online: the host's rules arrived (not saved - they're the host's).</summary>
    public static void SetFromHost(int animals, int weather, int players = -1) { Animals = Mathf.Clamp(animals, 0, 2); Weather = Mathf.Clamp(weather, 0, 2); if (players > 0) Players = players; }
}
