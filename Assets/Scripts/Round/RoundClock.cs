// Farm Chaos - the round clock. One round = one night, 11:00 PM -> 6:00 AM, squeezed into ~10 real minutes.
// Other scripts listen for the time:
//     RoundClock.Instance.HourChanged += hour => { if (hour == 1) ReleaseTheDog(); };   // hour is 0-23
//     RoundClock.Instance.RoundEnded  += () => ShowResults();
// Testing in the editor: HOLD F to fast-forward (20x).
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class RoundClock : MonoBehaviour
{
    public static RoundClock Instance { get; private set; }

    [Header("Round")]
    [Tooltip("How long one night lasts in real seconds (600 = 10 minutes)")]
    public float roundLengthSeconds = 600f;
    [Range(0, 23)] public int startHour = 23;   // 11 PM
    [Range(0, 23)] public int endHour = 6;      // 6 AM
    public bool startOnPlay = true;

    [Header("Testing")]
    [Tooltip("Hold F in the editor to speed the clock up by this much")]
    public float debugFastForward = 20f;

    public event Action RoundStarted;
    public event Action<int> HourChanged;        // new hour, 0-23
    public event Action RoundEnded;

    public float Elapsed { get; private set; }
    public bool Running { get; private set; }
    public bool Finished { get; private set; }

    /// <summary>0 at 11 PM, 1 at 6 AM.</summary>
    public float Progress => roundLengthSeconds <= 0 ? 1f : Mathf.Clamp01(Elapsed / roundLengthSeconds);
    public float TotalGameMinutes => ((endHour - startHour + 24) % 24) * 60f;
    /// <summary>In-game minutes since the round started (0..420).</summary>
    public float GameMinutes => Progress * TotalGameMinutes;
    public int Hour24 => (startHour + Mathf.FloorToInt(GameMinutes) / 60) % 24;
    public int Minute => Mathf.FloorToInt(GameMinutes) % 60;
    public float SecondsLeft => Mathf.Max(0f, roundLengthSeconds - Elapsed);

    public string TimeString
    {
        get
        {
            int h = Hour24 % 12; if (h == 0) h = 12;
            return $"{h}:{Minute:00} {(Hour24 < 12 ? "AM" : "PM")}";
        }
    }

    int lastHour = -1;

    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start() { if (startOnPlay) StartRound(); }

    public void StartRound()
    {
        Elapsed = 0f; Finished = false; Running = true;
        lastHour = Hour24;
        RoundStarted?.Invoke();
        HourChanged?.Invoke(lastHour);
    }

    /// <summary>Online: snap to the host's clock.</summary>
    public void SyncTo(float elapsed) { if (!Finished) Elapsed = Mathf.Clamp(elapsed, 0f, roundLengthSeconds); }

    public void Pause(bool paused) { if (!Finished) Running = !paused; }

    void Update()
    {
        if (!Running) return;
        float speed = 1f;
#if UNITY_EDITOR
        bool ff = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.fKey.isPressed) ff = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.F)) ff = true;
#endif
        if (ff) speed = debugFastForward;
#endif
        Elapsed += Time.deltaTime * speed;

        if (Hour24 != lastHour)
        {
            lastHour = Hour24;
            HourChanged?.Invoke(lastHour);
        }
        if (Elapsed >= roundLengthSeconds)
        {
            Elapsed = roundLengthSeconds;
            Running = false; Finished = true;
            RoundEnded?.Invoke();
        }
    }
}
