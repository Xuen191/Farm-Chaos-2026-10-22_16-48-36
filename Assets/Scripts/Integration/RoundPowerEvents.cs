// Farm Chaos - power events during the round:
//  * a moment after the round starts the power cuts out -> electric gates pop open (the chaos begins)
//  * at fuseHour a fuse appears at one of the spawn points (needed for the power box puzzle)
using System.Collections;
using UnityEngine;

public class RoundPowerEvents : MonoBehaviour
{
    [Header("Power cut")]
    public float powerCutDelay = 2.5f;

    [Header("Fuse")]
    public GameObject fuse;
    public Transform[] fuseSpawnPoints;
    [Tooltip("In-game hour the fuse appears (3 = 3 AM). Set to 23 to have it from the start.")]
    [Range(0, 23)] public int fuseHour = 3;

    bool fuseShown;

    void Start()
    {
        FusePickup.playerHasFuse = false;   // fresh round
        if (fuse != null) fuse.SetActive(false);
        var clock = RoundClock.Instance;
        if (clock != null)
        {
            clock.HourChanged += OnHour;
            if (clock.Running) OnHour(clock.Hour24);
        }
        StartCoroutine(CutPower());
    }

    IEnumerator CutPower()
    {
        yield return new WaitForSeconds(powerCutDelay);
        ElectricGate.SetPower(false);
    }

    void OnHour(int hour)
    {
        if (fuseShown || fuse == null || hour != fuseHour) return;
        fuseShown = true;
        if (fuseSpawnPoints != null && fuseSpawnPoints.Length > 0)
        {
            var p = fuseSpawnPoints[Random.Range(0, fuseSpawnPoints.Length)];
            fuse.transform.position = p.position;
        }
        fuse.SetActive(true);
        Debug.Log("[Farm Chaos] A fuse has appeared on the farm!");
    }
}
