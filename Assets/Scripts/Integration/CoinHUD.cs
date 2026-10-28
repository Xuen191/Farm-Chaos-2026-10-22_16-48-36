// Farm Chaos - keeps a coin counter text in sync with the round's CoinWallet, with a little bounce.
using TMPro;
using UnityEngine;

public class CoinHUD : MonoBehaviour
{
    public TMP_Text coinText;
    public RectTransform bounceTarget;
    float punch;
    int shown = int.MinValue;

    void Start()
    {
        CoinWallet.Changed += _ => punch = 1f;
        Refresh();
    }

    void Update()
    {
        Refresh();
        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 3f);
        if (bounceTarget != null) bounceTarget.localScale = Vector3.one * (1f + 0.2f * Mathf.Sin(punch * Mathf.PI));
    }

    void Refresh()
    {
        if (coinText == null || shown == CoinWallet.Coins) return;
        shown = CoinWallet.Coins;
        coinText.text = shown.ToString();
    }
}
