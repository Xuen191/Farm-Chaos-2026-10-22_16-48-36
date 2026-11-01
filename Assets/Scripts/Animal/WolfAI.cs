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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
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
        if (ProximityInteractor.UIOpen) return;

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
                CurrentState = State.Eating;
            }
        }
    }

    void DoEating()
    {
        if (currentTargetAnimal == null)
        {
            CurrentState = State.Roaming;
            return;
        }

        MoveTowards(currentTargetAnimal.position, moveSpeed);

        if (Vector3.Distance(transform.position, currentTargetAnimal.position) < 1.5f)
        {
            
            Destroy(currentTargetAnimal.gameObject); 
            
            if (CoinWallet.Exists)
            {
                CoinWallet.Add(-10, "Wolf ate an animal");
            }

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
        
        Vector3 nextPos = transform.position + dir * speed * Time.deltaTime;

        if (rb != null)
            rb.MovePosition(nextPos);
        else
            transform.position = nextPos;

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