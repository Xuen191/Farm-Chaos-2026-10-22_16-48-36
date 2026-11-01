using System;
using UnityEngine;

public class CoinWallet : MonoBehaviour
{
    [Tooltip("Coins each player starts the round with (handy for testing the shop before tasks exist)")]
    public int startCoins = 50;

    public static int Coins { get; private set; }
    public static event Action<int> Changed;
    /// <summary>amount, reason - for "+10" pop-ups later.</summary>
    public static event Action<int, string> Earned;
    public static bool Exists { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Coins = 0; Changed = null; Earned = null; Exists = false; }

    void Awake()
    {
        Exists = true;
        Coins = startCoins;
        Changed = null; Earned = null;   // fresh round: forget listeners from the previous scene
    }

    void OnDestroy() { Exists = false; }

    public static void Add(int amount, string reason = "")
    {
        if (amount <= 0) return;
        Coins += amount;
        Earned?.Invoke(amount, reason);
        Changed?.Invoke(Coins);
    }

    public static bool TrySpend(int price)
    {
        if (price < 0 || Coins < price) return false;
        Coins -= price;
        Changed?.Invoke(Coins);
        return true;
    }
}
