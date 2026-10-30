// Farm Chaos - electric gate that "pops" open when the farm loses power.
//
// HOW TO HOOK IT UP LATER (power station / electrical box):
//     ElectricGate.SetPower(false);   // power cut  -> every gate flickers and springs open
//     ElectricGate.SetPower(true);    // power back -> every gate swings shut again
//     ElectricGate.PowerChanged += on => { ... };   // listen for changes anywhere
// Single gate: GetComponent<ElectricGate>().PopOpen() / .Close()
//
// TESTING: in Play mode press P to toggle power, or right-click the component header > "Test: ..."
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ElectricGate : MonoBehaviour
{
    [Serializable]
    public class Leaf
    {
        public Transform hinge;
        [Tooltip("Angle (degrees) the leaf springs to when the power fails")]
        public float openAngle = -105f;
        [Tooltip("Seconds after the pop before this leaf releases (gives a cascade)")]
        public float delay;
        [NonSerialized] public float angle, vel;
        [NonSerialized] public Quaternion closedRot;
    }

    [Header("Parts (filled in by Tools > Farm Chaos > Build Electric Gates)")]
    public string areaName;
    public List<Leaf> leaves = new List<Leaf>();
    public List<Renderer> wires = new List<Renderer>();
    public Material wireOn, wireOff;

    [Header("Feel")]
    public float flickerTime = 0.45f;
    public float stiffness = 140f;   // higher = snappier pop
    public float damping = 7f;       // lower = more wobble/bounce
    public float closeTime = 0.9f;

    [Header("Optional sounds")]
    public AudioClip zapSound;
    public AudioClip popSound;
    public AudioClip closeSound;

    [Header("Testing")]
    public bool debugKeyP = true;

    public bool IsOpen { get; private set; }

    // ---------- global power state ----------
    public static bool PowerOn { get; private set; } = true;
    public static event Action<bool> PowerChanged;
    public static readonly List<ElectricGate> All = new List<ElectricGate>();

    public static void SetPower(bool on)
    {
        if (PowerOn == on) return;
        PowerOn = on;
        PowerChanged?.Invoke(on);
        foreach (var g in All.ToArray())
        {
            if (on) g.Close(); else g.PopOpen();
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { PowerOn = true; PowerChanged = null; All.Clear(); }

    void Awake()
    {
        foreach (var l in leaves)
            if (l.hinge != null) l.closedRot = l.hinge.localRotation;
    }

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    void Start()
    {
        SetWires(PowerOn);
        if (!PowerOn) PopOpen();
    }

    void Update()
    {
        if (!debugKeyP || All.Count == 0 || All[0] != this) return; // only one gate listens for the key
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame) pressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.P)) pressed = true;
#endif
        if (pressed) SetPower(!PowerOn);
    }

    [ContextMenu("Test: Power OFF (pop all gates)")] void TestOff() { if (Application.isPlaying) SetPower(false); }
    [ContextMenu("Test: Power ON (close all gates)")] void TestOn() { if (Application.isPlaying) SetPower(true); }
    [ContextMenu("Test: Pop this gate only")] void TestThis() { if (Application.isPlaying) PopOpen(); }

    public void PopOpen()
    {
        if (IsOpen) return;
        IsOpen = true;
        StopAllCoroutines();
        StartCoroutine(PopRoutine());
    }

    /// <summary>Open if closed, close if open (used by the gate switch box).</summary>
    public void Toggle() { if (IsOpen) Close(); else PopOpen(); }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        StopAllCoroutines();
        StartCoroutine(CloseRoutine());
    }

    IEnumerator PopRoutine()
    {
        Play(zapSound);
        float t = 0f;
        while (t < flickerTime)
        {
            SetWires(UnityEngine.Random.value > 0.45f);
            float step = UnityEngine.Random.Range(0.03f, 0.08f);
            t += step;
            yield return new WaitForSeconds(step);
        }
        SetWires(false);
        Play(popSound);

        float start = Time.time;
        foreach (var l in leaves) l.vel = 0f;
        bool settled = false;
        while (!settled)
        {
            settled = true;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            float elapsed = Time.time - start;
            foreach (var l in leaves)
            {
                if (l.hinge == null) continue;
                if (elapsed < l.delay) { settled = false; continue; }
                float acc = stiffness * (l.openAngle - l.angle) - damping * l.vel;
                l.vel += acc * dt;
                l.angle += l.vel * dt;
                Apply(l);
                if (Mathf.Abs(l.vel) > 1f || Mathf.Abs(l.openAngle - l.angle) > 0.5f) settled = false;
            }
            yield return null;
        }
    }

    IEnumerator CloseRoutine()
    {
        Play(closeSound);
        var from = new float[leaves.Count];
        for (int i = 0; i < leaves.Count; i++) { from[i] = leaves[i].angle; leaves[i].vel = 0f; }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.05f, closeTime);
            float e = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < leaves.Count; i++)
            {
                leaves[i].angle = Mathf.Lerp(from[i], 0f, e);
                Apply(leaves[i]);
            }
            yield return null;
        }
        SetWires(PowerOn);
    }

    void Apply(Leaf l)
    {
        if (l.hinge != null) l.hinge.localRotation = l.closedRot * Quaternion.Euler(0f, l.angle, 0f);
    }

    void SetWires(bool on)
    {
        var m = on ? wireOn : wireOff;
        if (m == null) return;
        foreach (var r in wires) if (r != null) r.sharedMaterial = m;
    }

    void Play(AudioClip c)
    {
        if (c != null) AudioSource.PlayClipAtPoint(c, transform.position);
    }
}
