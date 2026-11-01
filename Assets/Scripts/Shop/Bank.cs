// Farm Chaos - PAPER MONEY: the money you keep between games (saved on this device).
//   * At sunrise, the coins you earned that night turn into paper money (RoundEndScreen calls CashIn).
//   * Spend it in the lobby shop (the farmhouse) on special bean colours and lobby decorations.
//   * New players start with a little welcome gift so they can try the shop.
// Testing in the editor: F7 in the lobby = +100 paper money.
using System;
using System.Collections.Generic;
using UnityEngine;

public static class Bank
{
    public const int WelcomeGift = 50;
    public static event Action Changed;

    static string Key => "FarmChaos.Bank" + ChibiLook.TestWindowSuffix;
    static string OwnedKey => "FarmChaos.Owned" + ChibiLook.TestWindowSuffix;
    static int money = -1;
    static HashSet<string> owned;

    public static int Money { get { Load(); return money; } }

    static void Load()
    {
        if (money >= 0) return;
        money = PlayerPrefs.GetInt(Key, WelcomeGift);
        owned = new HashSet<string>(PlayerPrefs.GetString(OwnedKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
    }

    static void Save()
    {
        PlayerPrefs.SetInt(Key, money);
        PlayerPrefs.SetString(OwnedKey, string.Join(",", owned));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { money = -1; owned = null; Changed = null; nightEarned = 0; cashedIn = false; }

    public static void Add(int amount) { if (amount <= 0) return; Load(); money += amount; Save(); }

    public static bool Owns(string id) { Load(); return owned.Contains(id); }

    public static bool Buy(string id, int price)
    {
        Load();
        if (owned.Contains(id) || money < price) return false;
        money -= price; owned.Add(id); Save();
        return true;
    }

    // ------------------------------------------------------------ tonight's earnings -> paper money
    static int nightEarned; static bool cashedIn;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnScene;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnScene;
        OnScene(default, default);
    }

    static void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        nightEarned = 0; cashedIn = false;
        if (CoinWallet.Exists) CoinWallet.Earned += (amount, reason) => nightEarned += amount;   // (the wallet clears its listeners each night)
    }

    /// <summary>Sunrise: turn tonight's earned coins into paper money (once). Returns how much was added.</summary>
    public static int CashIn()
    {
        if (cashedIn) return 0;
        cashedIn = true;
        Add(nightEarned);
        return nightEarned;
    }
}
