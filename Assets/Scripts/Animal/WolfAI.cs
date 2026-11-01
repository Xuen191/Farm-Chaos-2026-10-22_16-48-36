using UnityEngine;
using System.Collections.Generic;

public class WolfAI : MonoBehaviour
{
    public enum State { Roaming, Eating, Fleeing }
    
    [Header("Settings")]
    public float moveSpeed = 3.5f;
    public float roamRadius = 15f;       // 在出生點附近遊蕩的範圍
    public float eatCooldown = 20f;      // 幾秒吃一隻動物
    public int maxHitsBeforeFlee = 3;    // 被打幾次會逃跑

    [Header("References")]
    public Transform spawnPoint;         // 狼的出生點 (逃跑時會回去)
    
    private State currentState = State.Roaming;
    private Vector3 roamTarget;
    private float nextEatTime;
    private int hitCount = 0;
    private Transform currentTargetAnimal;
    private Vector3 fleeTarget;

    public State CurrentState { get; private set; } = State.Roaming;

    void Start()
    {
        if (spawnPoint == null) 
        {
            // 如果沒有設定出生點，就把一開始的位置當作出生點
            GameObject sp = new GameObject("WolfSpawnPoint");
            sp.transform.position = transform.position;
            spawnPoint = sp.transform;
        }

        roamTarget = GetRandomRoamPoint();
        nextEatTime = Time.time + eatCooldown;
    }

    void Update()
    {
        // 如果遊戲已經結束或 UI 打開，就不做事
        if (ProximityInteractor.UIOpen) return;

        switch (currentState)
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
        }
    }

    // ================= 狀態：遊蕩與找食物 =================
    void DoRoaming()
    {
        // 1. 慢慢晃到隨機目標點
        transform.position = Vector3.MoveTowards(transform.position, roamTarget, moveSpeed * 0.5f * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, roamTarget) < 1f)
        {
            roamTarget = GetRandomRoamPoint();
        }

        // 2. 如果冷卻時間到了，就找一隻動物吃
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
        // 找出所有農場動物 (需要有 AnimalBrain 腳本)
        AnimalBrain[] animals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
        
        if (animals.Length > 0)
        {
            // 選最近的一隻
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
                currentState = State.Eating;
                Debug.Log("🐺 狼盯上了一隻動物：" + currentTargetAnimal.name);
            }
        }
    }

    // ================= 狀態：追動物並吃掉 =================
    void DoEating()
    {
        if (currentTargetAnimal == null)
        {
            // 動物不見了（可能被玩家救了），回去遊蕩
            currentState = State.Roaming;
            return;
        }

        // 追向動物
        Vector3 dir = (currentTargetAnimal.position - transform.position).normalized;
        dir.y = 0;
        transform.position += dir * moveSpeed * Time.deltaTime;
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

        // 如果抓到動物了
        if (Vector3.Distance(transform.position, currentTargetAnimal.position) < 1.5f)
        {
            Debug.Log("🐺 狼吃掉了一隻動物！");
            Destroy(currentTargetAnimal.gameObject); // 💡 吃掉動物 (也可以改成讓動物消失幾秒再重生)
            
            nextEatTime = Time.time + eatCooldown; // 重置冷卻
            currentState = State.Roaming;
            roamTarget = GetRandomRoamPoint();
        }
    }

    void DoFleeing()
    {
        Vector3 dir = (fleeTarget - transform.position).normalized;
        dir.y = 0;
        transform.position += dir * (moveSpeed * 2f) * Time.deltaTime; 
        if (dir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(dir);

        // 💡 當狼回到出生點
        if (Vector3.Distance(transform.position, fleeTarget) < 1f)
        {
            hitCount = 0; 
            CurrentState = State.Roaming;
            nextEatTime = Time.time + eatCooldown;
            roamTarget = GetRandomRoamPoint();
            Debug.Log("🐺 狼回到了出生點。");

            // 💡 顯示勝利訊息，並重置警告狀態
            WolfWarningUI warningUI = FindObjectOfType<WolfWarningUI>();
            if (warningUI != null)
            {
                warningUI.ShowVictoryMessage();
                warningUI.ResetWarning();
            }
        }
    }

    // 💡 這個函數會由 StickAttack 呼叫
    /// <summary>Farm Chaos: true while it's running away (scared off).</summary>
    public bool IsFleeing => currentState == State.Fleeing;

    public void GetHit()
    {
        hitCount++;
        Debug.Log($"🐺 狼被打了！({hitCount}/{maxHitsBeforeFlee})");

        if (hitCount >= maxHitsBeforeFlee)
        {
            // 被打滿三次，開始逃跑
            currentState = State.Fleeing;
            fleeTarget = spawnPoint.position;
            currentTargetAnimal = null; // 放棄追動物
            Debug.Log("🐺 狼嚇跑了！正在逃回出生點！");
        }
        else
        {
            // 被打但還沒滿三次，可能會暫停一下或發出叫聲
        }
    }

        // 💡 讓生成器呼叫，重置狼的狀態
    public void ResetWolf()
    {
        hitCount = 0;
        CurrentState = State.Roaming;
        nextEatTime = Time.time + eatCooldown;
        roamTarget = GetRandomRoamPoint();
        Debug.Log("🐺 狼的狀態已重置！");
    }
}