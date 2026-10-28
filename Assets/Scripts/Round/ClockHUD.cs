// Farm Chaos - on-screen clock ("11:00 PM"), bounces each new hour and turns warm toward sunrise.
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClockHUD : MonoBehaviour
{
    public TMP_Text timeText;
    public RectTransform pill;
    public Image icon;
    public Color nightIcon = new Color(0.85f, 0.9f, 1f);
    public Color dawnIcon = new Color(1f, 0.62f, 0.35f);
    public Color nightText = Color.white;
    public Color dawnText = new Color(1f, 0.86f, 0.62f);

    RoundClock clock;
    float punch;

    void Start()
    {
        clock = RoundClock.Instance;
        if (clock != null) clock.HourChanged += _ => punch = 1f;
    }

    void Update()
    {
        if (clock == null) { clock = RoundClock.Instance; if (clock == null) return; }
        if (timeText != null) timeText.text = clock.TimeString;

        // warm up during the last hour and a half
        float dawn = Mathf.InverseLerp(clock.TotalGameMinutes - 90f, clock.TotalGameMinutes, clock.GameMinutes);
        if (icon != null) icon.color = Color.Lerp(nightIcon, dawnIcon, dawn);
        if (timeText != null) timeText.color = Color.Lerp(nightText, dawnText, dawn);

        punch = Mathf.MoveTowards(punch, 0f, Time.unscaledDeltaTime * 3f);
        if (pill != null) pill.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(punch * Mathf.PI));
    }
}
