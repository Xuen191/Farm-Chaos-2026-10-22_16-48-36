// Farm Chaos ONLINE notes: only the HOST runs the wolf's brain. Everyone else sees a copy that just follows the
// host's wolf (it rides along in the animal sync - AnimalBrain.Pack). Bombs, stick hits and bones from other
// players are sent to the host (NetPlayer). Eaten animals are marked "Gone" instead of destroyed, so every
// player's farm keeps the same list of animals.
using UnityEngine;
using System.Collections.Generic;

public class WolfAI : MonoBehaviour
{
    public enum State { Roaming, Eating, Fleeing, Distracted }
    
    [Header("Settings")]
    public float moveSpeed = 3.5f;
    public float roamRadius = 15f;       
    public float eatCooldown = 20f;      
    public int maxHitsBeforeFlee = 3;    

    [Header("References")]
    public Transform spawnPoint;         

    private Rigidbody rb;
    private Vector3 roamTarget;
    private float nextEatTime;
    private int hitCount = 0;
    private Transform currentTargetAnimal;
    private Vector3 fleeTarget;

    public State CurrentState { get; private set; } = State.Roaming;

    // ---- online
    static readonly List<WolfAI> all = new List<WolfAI>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics() { all.Clear(); }
    static bool Puppet => NetSession.Online && Unity.Netcode.NetworkManager.Singleton != null && !Unity.Netcode.NetworkManager.Singleton.IsServer;
    Vector3 netPos; float netYaw; bool hasNet;
    float stuckTimer, chaseTime; Vector3 lastPos;
    void Awake() { if (!all.Contains(this)) all.Add(this); }
    void OnDestroy() { all.Remove(this); }
    static WolfAI Main
    {
        get
        {
            all.RemoveAll(w => w == null);
            if (all.Count == 0) foreach (var w in FindObjectsByType<WolfAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) all.Add(w);
            return all.Count > 0 ? all[0] : null;
        }
    }

    /// <summary>Host: add the wolf to the sync message.</summary>
    public static void PackWolf(System.IO.BinaryWriter w)
    {
        var wolf = Main;
        if (wolf == null) { w.Write((byte)0); return; }
        w.Write((byte)1);
        w.Write((byte)(wolf.gameObject.activeInHierarchy ? 1 : 0));
        var p = wolf.transform.position; w.Write(p.x); w.Write(p.y); w.Write(p.z);
        w.Write((short)Mathf.RoundToInt(wolf.transform.eulerAngles.y * 10f));
        w.Write((byte)wolf.CurrentState);
    }

    /// <summary>Everyone else: show the wolf where the host's wolf is (or hide it).</summary>
    public static void UnpackWolf(System.IO.BinaryReader r)
    {
        if (r.ReadByte() == 0) return;
        bool active = r.ReadByte() == 1;
        Vector3 p = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        float yaw = r.ReadInt16() / 10f; var st = (State)r.ReadByte();
        var wolf = Main; if (wolf == null) return;
        if (wolf.gameObject.activeSelf != active)
        {
            wolf.gameObject.SetActive(active);
            if (active)
            {
                wolf.transform.position = p;
                var ui = FindAnyObjectByType<WolfWarningUI>(); if (ui != null) ui.ShowWarningOnce();
            }
        }
        wolf.netPos = p; wolf.netYaw = yaw; wolf.CurrentState = st; wolf.hasNet = true;
    }

    /// <summary>Bomb went off here: wolves in range are blown away (online friends ask the host).</summary>
    public static void BombAt(Vector3 at, float radius)
    {
        if (Puppet) { if (NetPlayer.Local != null) NetPlayer.Local.SendWolfBomb(at, radius); return; }
        HostBomb(at, radius);
    }
    public static void HostBomb(Vector3 at, float radius)
    {
        foreach (var w in all.ToArray())
            if (w != null && w.gameObject.activeInHierarchy && (w.transform.position - at).magnitude <= radius) w.Banish();
    }
    /// <summary>Host: blown up -> gone until the spawner brings a wolf back.</summary>
    void Banish()
    {
        currentTargetAnimal = null; hitCount = 0; CurrentState = State.Roaming;
        if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
        gameObject.SetActive(false);
    }

    /// <summary>A player hit this wolf with the stick. Returns true if that scared it off (host / solo).</summary>
    public bool Hit()
    {
        if (Puppet) { if (NetPlayer.Local != null) NetPlayer.Local.SendWolfHit(); return false; }
        bool was = IsFleeing; GetHit(); return !was && IsFleeing;
    }

    /// <summary>Someone threw a bone here (online friends ask the host).</summary>
    public static void BoneAt(Vector3 at)
    {
        if (Puppet) { if (NetPlayer.Local != null) NetPlayer.Local.SendBone(at); return; }
        var target = new GameObject("Bone_Target").transform; target.position = at;
        Object.Destroy(target.gameObject, 20f);
        foreach (var w in all.ToArray()) if (w != null && w.gameObject.activeInHierarchy) w.DistractedByBone(target);
    }

    public static WolfAI ActiveWolf { get { var w = Main; return w != null && w.gameObject.activeInHierarchy ? w : null; } }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null && Puppet) { rb.isKinematic = true; rb.useGravity = false; }   // online friend: just a copy of the host's wolf
        else if (rb != null)
        {rb.isKinematic = false; 
rb.useGravity = true;   
rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ; 
            rb.interpolation = RigidbodyInterpolation.Interpolate; 
        }
        if (spawnPoint == null) 
        {
            GameObject sp = new GameObject("WolfSpawnPoint");
            sp.transform.position = transform.position;
            spawnPoint = sp.transform;
        }

        roamTarget = GetRandomRoamPoint();
        nextEatTime = Time.time + eatCooldown;
    }

    void Update()
    {
        if (Puppet)
        {
            if (!hasNet) return;
            float k = 1f - Mathf.Exp(-12f * Time.deltaTime);
            if ((transform.position - netPos).sqrMagnitude > 25f) transform.position = netPos;
            else transform.position = Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0f, netYaw, 0f), k);
            return;
        }
        if (ProximityInteractor.UIOpen && !NetSession.Online) return;

        // stuck against a fence / building for a while: try somewhere else
        if ((transform.position - lastPos).sqrMagnitude > 0.09f) { lastPos = transform.position; stuckTimer = 0f; }
        else if ((stuckTimer += Time.deltaTime) > 2.5f)
        {
            stuckTimer = 0f;
            if (CurrentState == State.Eating) { currentTargetAnimal = null; CurrentState = State.Roaming; nextEatTime = Time.time + 5f; }
            roamTarget = GetRandomRoamPoint();
        }

        switch (CurrentState)
        {
            case State.Roaming:
                DoRoaming();
                break;
            case State.Eating:
                DoEating();
                break;
            case State.Fleeing:
                DoFleeing();
                break;
            case State.Distracted:
                DoDistracted();
                break;
        }
    }

    void DoRoaming()
    {
        MoveTowards(roamTarget, moveSpeed * 0.5f);
        
        if (Vector3.Distance(transform.position, roamTarget) < 1f)
        {
            roamTarget = GetRandomRoamPoint();
        }

        if (Time.time >= nextEatTime)
        {
            FindAnimalToEat();
        }
    }

    Vector3 GetRandomRoamPoint()
    {
        Vector2 r = Random.insideUnitCircle * roamRadius;
        return spawnPoint.position + new Vector3(r.x, 0, r.y);
    }

    void FindAnimalToEat()
    {
        AnimalBrain[] animals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
        
        if (animals.Length > 0)
        {
            AnimalBrain closest = null;
            float minDist = Mathf.Infinity;

            foreach (var a in animals)
            {
                if (a == null || a.IsGone || !(a.Catchable || a.state == AnimalBrain.State.Following)) continue;   // only animals loose on the farm (fences keep the wolf out of pens)
                float d = Vector3.Distance(transform.position, a.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = a;
                }
            }

            if (closest != null)
            {
                currentTargetAnimal = closest.transform;
                CurrentState = State.Eating; chaseTime = 0f;
            }
            else nextEatTime = Time.time + 5f;   // nobody loose: look again in a bit
        }
    }

    void DoEating()
    {
        var prey = currentTargetAnimal != null ? currentTargetAnimal.GetComponent<AnimalBrain>() : null;
        chaseTime += Time.deltaTime;
        if (prey == null || prey.IsGone || chaseTime > 20f || !(prey.Catchable || prey.state == AnimalBrain.State.Following))
        {   // gone, safe back home, or too long a chase
            currentTargetAnimal = null; CurrentState = State.Roaming; nextEatTime = Time.time + 5f;
            return;
        }

        MoveTowards(currentTargetAnimal.position, moveSpeed);

        if (Vector3.Distance(transform.position, currentTargetAnimal.position) < 1.5f)
        {
            prey.HostEaten();                                   // vanishes for everyone (synced), not destroyed
            NetPlayer.WolfAte(prey.kind.ToString());            // every player loses 10 coins + sees why
            currentTargetAnimal = null;

            nextEatTime = Time.time + eatCooldown; 
            CurrentState = State.Roaming;
            roamTarget = GetRandomRoamPoint();
        }
    }

    void DoFleeing()
    {
        MoveTowards(fleeTarget, moveSpeed * 2f);

        if (Vector3.Distance(transform.position, fleeTarget) < 1f)
        {
            hitCount = 0; 
            CurrentState = State.Roaming;
            nextEatTime = Time.time + eatCooldown;
            roamTarget = GetRandomRoamPoint();

            WolfWarningUI warningUI = FindObjectOfType<WolfWarningUI>();
            if (warningUI != null)
            {
                warningUI.ShowVictoryMessage();
                warningUI.ResetWarning();
            }
        }
    }

    /// <summary>Farm Chaos: true while it's running away (scared off). Used by StickAttack for points.</summary>
    public bool IsFleeing => CurrentState == State.Fleeing;

    public void GetHit()
    {
        hitCount++;

        if (hitCount >= maxHitsBeforeFlee)
        {
            CurrentState = State.Fleeing;
            fleeTarget = spawnPoint.position;
            currentTargetAnimal = null; 
        }
    }

    public void ResetWolf()
    {
        hitCount = 0;
        CurrentState = State.Roaming;
        nextEatTime = Time.time + eatCooldown;
        roamTarget = GetRandomRoamPoint();
    }

    void MoveTowards(Vector3 targetPos, float speed)
    {
        Vector3 dir = (targetPos - transform.position).normalized;
        dir.y = 0;
        
        if (rb != null && !rb.isKinematic)
            rb.linearVelocity = new Vector3(dir.x * speed, rb.linearVelocity.y, dir.z * speed);   // real physics: fences and buildings block it
        else
            transform.position += dir * speed * Time.deltaTime;

        if (dir.sqrMagnitude > 0.01f) 
            transform.rotation = Quaternion.LookRotation(dir);
    }

    public void DistractedByBone(Transform bone)
    {
        if (CurrentState == State.Fleeing) return;

        currentTargetAnimal = bone; 
        CurrentState = State.Distracted;
    }

    void DoDistracted()
    {
        if (currentTargetAnimal == null)
        {
            CurrentState = State.Roaming;
            return;
        }

        MoveTowards(currentTargetAnimal.position, moveSpeed * 1.5f); 

        if (Vector3.Distance(transform.position, currentTargetAnimal.position) < 1.5f)
        {
            Destroy(currentTargetAnimal.gameObject); 
            nextEatTime = Time.time + eatCooldown;
            CurrentState = State.Roaming;
            roamTarget = GetRandomRoamPoint();
        }
    }
}