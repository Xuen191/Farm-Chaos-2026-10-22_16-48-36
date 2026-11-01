// Farm Chaos - the farm animals' brain (used instead of AnimalController, which is for steering an animal with WASD).
//
//   In the pen ....... grazes and wanders around inside its pen
//   Gate pops open ... (power cut) runs out through the gate and roams the farm, scurrying away from players
//   Caught ........... touch an escaped animal and it follows you in a little conga line
//   Returned ......... lead it back into its OWN pen (or right up to its gate) -> coins for you!
//   If the gate is still open, a returned animal settles for a while... then may wander off again. Fix the power!
//
// ONLINE: only the host runs the animals; it sends everyone their positions ~10x a second and the other
// players' games just play them back smoothly (so everyone chases the same herd). The host sees every
// player, so anyone can catch an animal; whoever returns it gets the coins (coins stay per player).
//
// Set up by Tools > Farm Chaos > 26. Put Animals In Pens.
using System.Collections.Generic;
using UnityEngine;

public class AnimalBrain : MonoBehaviour
{
    public enum Kind { Chicken, Bunny, Sheep, Cow }
    public enum State { Grazing, Escaping, Roaming, Following, Returning }

    [Header("Who")]
    public Kind kind;
    public int coins = 5;

    [Header("Home pen (filled in by the setup tool)")]
    public Vector3 homeCentre;
    public float homeRadius = 4f;
    public ElectricGate gate;

    [Header("Speeds (m/s)")]
    public float walkSpeed = 1f;
    public float runSpeed = 4f;
    [Tooltip("Players closer than this make an escaped animal run away")]
    public float scareDistance = 1f;
    [Tooltip("Touch distance to catch it")]
    public float catchDistance = 1.4f;

    public State state { get; private set; } = State.Grazing;
    public static readonly List<AnimalBrain> All = new List<AnimalBrain>();
    /// <summary>Fires when someone returns an animal to its pen (animal, was it the local player).</summary>
    public static event System.Action<AnimalBrain, bool> Returned;

    Transform leader; bool leaderIsLocal; NetPlayer leaderNet; int followIndex;
    Vector3 target; float retargetAt, calmUntil, escapeAt = -1f, nextLeave;
    Transform body, legFL, legFR, legRL, legRR;
    Quaternion bodyRest; Vector3 bodyRestPos;
    float stride, speedNow;
    Vector3 lastPos;

    void OnEnable() { All.Add(this); }
    void OnDisable() { All.Remove(this); }

    int seed;   // makes each animal zig-zag / peck a little differently
    void Awake()
    {
        seed = Random.Range(0, 100000);
        // the friend's AnimalController steers an animal with WASD - switch it off for farm animals
        var steer = GetComponent<EasyPrimitiveAnimals.AnimalController>(); if (steer != null) steer.enabled = false;
        var rb = GetComponent<Rigidbody>(); if (rb != null) rb.isKinematic = true;
    }

    void Start()
    {
        body = transform.Find("BaseAnimal");
        if (body != null) { bodyRest = body.localRotation; bodyRestPos = body.localPosition; }
        legFL = FindDeep("EPA_FL"); legFR = FindDeep("EPA_FR"); legRL = FindDeep("EPA_RL"); legRR = FindDeep("EPA_RR");
        if (legFL == null) { legFL = FindDeep("Leg_L_top"); legFR = FindDeep("Leg_R_top"); }   // chicken: two legs
        MeasurePen();
        NewWanderTarget();
        lastPos = transform.position;
        if (gate != null && gate.IsOpen) escapeAt = Time.time + Random.Range(0.2f, 2.5f);
    }

    Transform FindDeep(string n) { foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }

    void OnGateChanged()
    {
        if (gate == null) return;
        if (gate.IsOpen && state == State.Grazing && Time.time > calmUntil) escapeAt = Time.time + Random.Range(0.2f, 2.5f);
    }

    bool gateWasOpen;
    void Update()
    {
        float dt = Time.deltaTime;
        bool open = gate != null && gate.IsOpen;
        if (open != gateWasOpen) { gateWasOpen = open; OnGateChanged(); }
        if (Puppet) { PuppetUpdate(dt); Animate(dt); return; }      // online, not the host: just follow the host's copy
        if (hopping) { DoHop(dt); Animate(dt); return; }

        switch (state)
        {
            case State.Grazing:
                if (escapeAt > 0f && Time.time >= escapeAt && open) { escapeAt = -1f; state = State.Escaping; break; }
                if (open && Time.time > calmUntil && Time.time > nextLeave)
                {   // the gate is still open... maybe wander off again
                    nextLeave = Time.time + 10f;
                    if (Random.value < 0.3f) { state = State.Escaping; break; }
                }
                if (Time.time > retargetAt || Flat(target - transform.position).magnitude < 0.4f) NewWanderTarget();
                MoveTowards(target, walkSpeed * (kind == Kind.Chicken ? 1.2f : 1f), dt, stayHome: true, idleChance: 0.35f);
                break;

            case State.Escaping:   // out through the gate
            {
                Vector3 g = gate != null ? gate.transform.position : homeCentre;
                Vector3 outDir = Flat(g - homeCentre).normalized;
                Vector3 exitPoint = g + outDir * 4f;
                bool inside = Inside(transform.position, -0.5f);
                MoveTowards(inside ? g + outDir * 0.5f : exitPoint, runSpeed, dt);
                if (!open && inside) { state = State.Grazing; NewWanderTarget(); break; }        // gate shut again before it got out
                if (stuckFor > 2f) StartHop(exitPoint);                                          // wedged at the gate: hop out
                if (Flat(transform.position - exitPoint).magnitude < 1.2f || (!inside && Flat(transform.position - homeCentre).magnitude > Reach(transform.position) + 5f))
                { state = State.Roaming; NewRoamTarget(); }
                break;
            }

            case State.Roaming:
            {
                CheckCatch();
                if (state != State.Roaming) break;

                var threat = NearestPlayer(out float d);
                if (threat != null && d < scareDistance)
                {   
                    Vector3 away = Flat(transform.position - threat.position).normalized;
                    if (kind == Kind.Chicken || kind == Kind.Bunny) 
                        away = Quaternion.Euler(0, Mathf.Sin(Time.time * 6f + seed) * 50f, 0) * away;
                    
                    MoveTowards(transform.position + away * 3f, runSpeed, dt);
                }
                else
                {
                    if (Time.time > retargetAt || Flat(target - transform.position).magnitude < 0.6f) NewRoamTarget();
                    MoveTowards(target, walkSpeed * 1.3f, dt, idleChance: 0.2f);
                }
                break;
            }

            case State.Following:
            {
                if (leader == null) { state = State.Roaming; NewRoamTarget(); break; }
                Vector3 spot = leader.position - Flat(leader.forward).normalized * 1.5f;
                float dist = Flat(spot - transform.position).magnitude;
                MoveTowards(spot, Mathf.Clamp(dist * 2.5f, 0f, runSpeed * 1.6f), dt, stopAt: 0.3f);
                if (stuckFor > 1.2f && dist > 2.5f) StartHop(transform.position + Flat(spot - transform.position).normalized * 2.2f);   // stuck behind a fence: hop over it
                // home? (inside its pen, or right at its gate)
                bool home = Inside(transform.position, 0.3f)
                         || (gate != null && Flat(transform.position - gate.transform.position).magnitude < 3f);
                if (home) Return();
                break;
            }

            case State.Returning:   // trot to a spot inside
                MoveTowards(target, runSpeed, dt, stayHome: true);
                if (Flat(target - transform.position).magnitude < 0.5f || stuckFor > 1.5f) { state = State.Grazing; NewWanderTarget(); }
                break;
        }
        Animate(dt);
    }

    void CheckCatch()
    {
        foreach (var a in All)
        {
            if (a != this && a.state == State.Following)
            {
                return; 
            }
        }

        foreach (var p in Players())
        {
            if (Flat(p.t.position - transform.position).magnitude > catchDistance) continue;
            
            leader = p.t; 
            leaderIsLocal = p.local; 
            leaderNet = p.local ? null : p.t.GetComponent<NetPlayer>();
            followIndex = 0; 
            state = State.Following;
            
            return;
        }
    }

    void Return()
    {
        bool local = leaderIsLocal;
        var was = leader;
        leader = null;
        state = State.Returning;
        target = RandomInside(0.5f);
        if (!Inside(transform.position, -0.3f) && gate != null)
        {   // outside the fence (at the gate): hop over / through into the pen
            Vector3 g = gate.transform.position;
            StartHop(g + Flat(homeCentre - g).normalized * 2.2f);
        }
        calmUntil = Time.time + 25f; nextLeave = calmUntil;
        if (local && CoinWallet.Exists) CoinWallet.Add(coins, kind + " returned");
        else if (leaderNet != null) leaderNet.GiveAnimalCoins(coins, kind.ToString());   // online: pay the player who brought it home
        leaderNet = null;
        Returned?.Invoke(this, local);
        // the next animals in the line move up
        foreach (var a in All) if (a.state == State.Following && a.leader == was && a.followIndex > 0) a.followIndex--;
    }

    struct P { public Transform t; public bool local; }
    static readonly List<P> players = new List<P>();
    static int playersFrame = -1;
    static List<P> Players()
    {
        if (playersFrame == Time.frameCount) return players;
        playersFrame = Time.frameCount; players.Clear();
        foreach (var sp in FindObjectsByType<SpherePlayer>(FindObjectsSortMode.None)) players.Add(new P { t = sp.transform, local = true });
        foreach (var np in FindObjectsByType<NetPlayer>(FindObjectsSortMode.None))
            if (!np.IsOwner) players.Add(new P { t = np.transform, local = false });
        return players;
    }

    Transform NearestPlayer(out float best)
    {
        best = float.MaxValue; Transform t = null;
        foreach (var p in Players()) { float d = Flat(p.t.position - transform.position).magnitude; if (d < best) { best = d; t = p.t; } }
        return t;
    }

    // ---------------------------------------------------------------- moving
    void NewWanderTarget()
    {
        target = RandomInside(0.85f);
        retargetAt = Time.time + Random.Range(3f, 7f);
    }

    void NewRoamTarget()
    {
        Vector2 r = Random.insideUnitCircle * 9f;
        Vector3 c = Flat(transform.position) * 0.85f;                      // drift loosely around, not too far from the middle
        target = new Vector3(Mathf.Clamp(c.x + r.x, -42f, 42f), 0f, Mathf.Clamp(c.z + r.y, -42f, 42f));
        retargetAt = Time.time + Random.Range(2.5f, 6f);
    }

    // ---- the real shape of the pen: how far the fence is in 32 directions (measured once at the start)
    const int Dirs = 32;
    readonly float[] reach = new float[Dirs];
    void MeasurePen()
    {
        float fallback = homeRadius * 0.85f;
        Vector3 from = new Vector3(homeCentre.x, homeCentre.y + 0.5f, homeCentre.z);
        for (int i = 0; i < Dirs; i++)
        {
            float a = i * Mathf.PI * 2f / Dirs;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(from, dir, homeRadius * 2.2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.normal.y > 0.6f || h.collider.GetComponentInParent<AnimalBrain>() != null) continue;
                if (h.collider.GetComponentInParent<SpherePlayer>() != null || h.collider.GetComponentInParent<NetPlayer>() != null) continue;
                if (h.distance < best) best = h.distance;
            }
            reach[i] = best < float.MaxValue ? Mathf.Max(0.6f, best - 0.9f) : fallback;   // stay ~1m off the fence
        }
    }

    float Reach(Vector3 p)
    {
        Vector3 d = Flat(p - homeCentre);
        if (d.sqrMagnitude < 0.0001f) return reach[0];
        float a = Mathf.Repeat(Mathf.Atan2(d.z, d.x), Mathf.PI * 2f) / (Mathf.PI * 2f) * Dirs;
        int i = Mathf.FloorToInt(a) % Dirs; float f = a - Mathf.Floor(a);
        return Mathf.Lerp(reach[i], reach[(i + 1) % Dirs], f);
    }

    bool Inside(Vector3 p, float extra) => Flat(p - homeCentre).magnitude <= Reach(p) + extra;

    Vector3 RandomInside(float frac)
    {
        float a = Random.value * Mathf.PI * 2f;
        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        return homeCentre + dir * Reach(homeCentre + dir) * Mathf.Sqrt(Random.value) * frac;
    }

    // ---- hopping (over a fence when stuck, or over the shut gate to get home)
    bool hopping; Vector3 hopFrom, hopTo; float hopT;
    void StartHop(Vector3 to)
    {
        hopping = true; hopFrom = transform.position; hopTo = to; hopTo.y = GroundY(to); hopT = 0f; stuckFor = 0f;
    }
    void DoHop(float dt)
    {
        hopT += dt / 0.65f;
        float k = Mathf.Clamp01(hopT);
        Vector3 p = Vector3.Lerp(hopFrom, hopTo, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 1.1f;
        transform.position = p;
        Vector3 d = Flat(hopTo - hopFrom);
        if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
        speedNow = 0f;
        if (k >= 1f) hopping = false;
    }

    float pauseUntil, stuckFor;
    static readonly float[] Detours = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };
    void MoveTowards(Vector3 to, float speed, float dt, bool stayHome = false, float idleChance = 0f, float stopAt = 0.05f)
    {
        if (Time.time < pauseUntil) { speedNow = 0f; return; }
        Vector3 d = Flat(to - transform.position);
        if (d.magnitude < stopAt) { speedNow = 0f; stuckFor = 0f; if (idleChance > 0f && Random.value < idleChance * dt * 2f) pauseUntil = Time.time + Random.Range(0.8f, 2.5f); return; }
        Vector3 want = d.normalized, dir = Vector3.zero;
        float side = seed % 2 == 0 ? 1f : -1f;
        foreach (var off in Detours)                                 // steer round fences, walls, trees...
        {
            var tryDir = Quaternion.Euler(0f, off * side, 0f) * want;
            if (!Blocked(tryDir)) { dir = tryDir; break; }
        }
        if (dir == Vector3.zero) { speedNow = 0f; stuckFor += dt; if (stuckFor > 0.6f) retargetAt = 0f; return; }
        float step = Mathf.Min(speed * dt, d.magnitude);
        Vector3 next = transform.position + dir * step;
        if (stayHome && !Inside(next, 0f))
        {
            Vector3 off = Flat(next - homeCentre);
            next = homeCentre + off.normalized * Reach(next) + Vector3.up * next.y;
        }
        next.y = GroundY(next);
        float moved = Flat(next - transform.position).magnitude;
        stuckFor = moved < step * 0.3f ? stuckFor + dt : 0f;
        transform.position = next;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 360f * dt);
        speedNow = moved / Mathf.Max(dt, 0.0001f);
        if (idleChance > 0f && Random.value < idleChance * dt * 0.4f) pauseUntil = Time.time + Random.Range(0.6f, 2f);
    }

    bool Blocked(Vector3 dir)
    {
        Vector3 from = transform.position + Vector3.up * 0.4f;
        foreach (var h in Physics.SphereCastAll(from, 0.2f, dir, 0.7f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<AnimalBrain>() != null) continue;
            if (h.collider.GetComponentInParent<SpherePlayer>() != null || h.collider.GetComponentInParent<NetPlayer>() != null) continue;
            if (h.normal.y > 0.6f) continue;     
            
            string objName = h.collider.gameObject.name.ToLower();
            if (objName.Contains("bush") || objName.Contains("rabbit_area") || objName.Contains("grass")) 
                continue;

            return true;
        }
        return false;
    }

    float GroundY(Vector3 p)
    {
        // the highest flat surface no more than a small step above us (never tree tops / roofs / fences)
        float best = float.MinValue;
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, transform.position.y + 0.6f, p.z), Vector3.down, 4f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.normal.y < 0.7f || h.collider.transform.IsChildOf(transform) || h.collider.GetComponentInParent<AnimalBrain>() != null) continue;
            if (h.collider.GetComponentInParent<SpherePlayer>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? best : transform.position.y;
    }

    // ---------------------------------------------------------------- looking alive
    void Animate(float dt)
    {
        float moving = Mathf.Clamp01(speedNow / Mathf.Max(0.1f, walkSpeed));
        stride += dt * (4f + speedNow * 2.2f);
        float swing = Mathf.Sin(stride) * 28f * moving;
        if (legFL != null) legFL.localRotation = Quaternion.Euler(swing, 0, 0);
        if (legRR != null) legRR.localRotation = Quaternion.Euler(swing, 0, 0);
        if (legFR != null) legFR.localRotation = Quaternion.Euler(-swing, 0, 0);
        if (legRL != null) legRL.localRotation = Quaternion.Euler(-swing, 0, 0);
        if (body == null) return;
        float bob = 0f, tilt = 0f;
        switch (kind)
        {
            case Kind.Bunny: bob = Mathf.Abs(Mathf.Sin(stride * 0.5f)) * 0.22f * moving; tilt = -Mathf.Sin(stride * 0.5f) * 10f * moving; break;   // hop hop
            case Kind.Chicken:
                bob = Mathf.Abs(Mathf.Sin(stride)) * 0.05f * moving;
                if (speedNow < 0.05f) tilt = Mathf.Max(0f, Mathf.Sin(Time.time * 3f + seed)) * 35f;                             // pecking
                break;
            default: bob = Mathf.Abs(Mathf.Sin(stride)) * 0.03f * moving; break;
        }
        if (state == State.Following) bob += Mathf.Abs(Mathf.Sin(Time.time * 10f)) * 0.04f;                                                  // happy bounce
        body.localPosition = bodyRestPos + Vector3.up * bob;
        body.localRotation = bodyRest * Quaternion.Euler(tilt, 0, 0);
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    // ================================================================ online sync (host -> everyone)
    static bool Puppet => NetSession.Online && !Unity.Netcode.NetworkManager.Singleton.IsServer;
    Vector3 netPos; float netYaw, netSpeed; bool hasNet;

    void PuppetUpdate(float dt)
    {
        if (!hasNet) { speedNow = 0f; return; }
        float k = 1f - Mathf.Exp(-12f * dt);
        if ((transform.position - netPos).sqrMagnitude > 25f) transform.position = netPos;   // way off: snap
        else transform.position = Vector3.Lerp(transform.position, netPos, k);
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, netYaw, 0f), k);
        speedNow = netSpeed;
    }

    static List<AnimalBrain> sorted; static int sortedCount = -1;
    static List<AnimalBrain> Sorted()
    {   // every player's farm has the same animals with the same names -> same order everywhere
        if (sorted == null || sortedCount != All.Count) { sorted = new List<AnimalBrain>(All); sorted.Sort((a, b) => string.CompareOrdinal(a.name, b.name)); sortedCount = All.Count; }
        return sorted;
    }

    /// <summary>Host: every animal's position, facing, speed and mood in one small message.</summary>
    public static byte[] Pack()
    {
        var list = Sorted();
        var w = new System.IO.BinaryWriter(new System.IO.MemoryStream());
        w.Write((byte)list.Count);
        foreach (var a in list)
        {
            var p = a.transform.position;
            w.Write(p.x); w.Write(p.y); w.Write(p.z);
            w.Write((short)Mathf.RoundToInt(a.transform.eulerAngles.y * 10f));
            w.Write((byte)Mathf.Clamp(Mathf.RoundToInt(a.speedNow * 20f), 0, 255));
            w.Write((byte)a.state);
        }
        return ((System.IO.MemoryStream)w.BaseStream).ToArray();
    }

    /// <summary>Everyone else: apply the host's message.</summary>
    public static void Unpack(byte[] data)
    {
        var list = Sorted();
        var r = new System.IO.BinaryReader(new System.IO.MemoryStream(data));
        int n = r.ReadByte();
        for (int i = 0; i < n; i++)
        {
            Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            float yaw = r.ReadInt16() / 10f; float spd = r.ReadByte() / 20f; var st = (State)r.ReadByte();
            if (i >= list.Count) continue;
            var a = list[i];

            if (a == null) continue;
            
            if (!a.hasNet) { a.transform.position = p; a.transform.rotation = Quaternion.Euler(0f, yaw, 0f); }
            a.netPos = p; a.netYaw = yaw; a.netSpeed = spd; a.state = st; a.hasNet = true;
        }
    }

    public void AttractedTo(Vector3 targetPosition)
    {
        if (state == State.Following || state == State.Escaping) return;

        target = targetPosition;
        retargetAt = Time.time + 10f; 
        calmUntil = Time.time + 10f;
        
        if (state != State.Roaming)
        {
            state = State.Roaming;
        }
        
    }
}
