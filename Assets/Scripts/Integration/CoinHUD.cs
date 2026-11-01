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
        // pin the coin counter to the top-right corner (it was placed from the middle, so on wide / tall screens
        // - like the extra test players' windows or some phones - it ended up off the screen)
        if (bounceTarget != null)
        {
            bounceTarget.anchorMin = bounceTarget.anchorMax = bounceTarget.pivot = new Vector2(1f, 1f);
            bounceTarget.anchoredPosition = new Vector2(-30f, -30f);
        }
        CoinWallet.Changed += _ => punch = 1f;
        Refresh();
    }

    void Update()
    {
        if (bounceTarget != null && bounceTarget.anchorMin != Vector2.one)
        {   // keep it pinned to the top-right corner
            bounceTarget.anchorMin = bounceTarget.anchorMax = bounceTarget.pivot = Vector2.one;
            bounceTarget.anchoredPosition = new Vector2(-30f, -30f);
        }
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
