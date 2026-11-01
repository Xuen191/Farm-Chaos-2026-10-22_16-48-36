// Farm Chaos - the spy's secret moves (only the spy can do these; farmers just see the results):
//   1. Overload the power box ........ PowerBox (E at a working box)            +40, no alert (looks like any outage)
//   2. Open a pen gate ................ GateSwitch (USE at the switch box)       +30, alert: "The SHEEP gate was opened!"
//   3. Stinky bait .................... AnimalBait (the spy's bait scares animals away instead)   +10, no alert
//   4. Fake helper - LET GO ........... CatchController (LET GO button / G while leading animals)  +10 each, alert: "Sheep got loose near the cow pen!"
// Alerts say WHERE, never WHO - farmers have to think about who was nearby.
using UnityEngine;

public static class SpySabotage
{
    public const float GateCooldown = 30f;
    static float gateReadyAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { gateReadyAt = 0f; }

    public static float GateCooldownLeft => Mathf.Max(0f, gateReadyAt - Time.time);
    public static void UsedGate() { gateReadyAt = Time.time + GateCooldown; }

    /// <summary>Name of the pen closest to a spot ("cow", "sheep"...), from the gate switch boxes.</summary>
    public static string PenNear(Vector3 pos)
    {
        string best = ""; float bd = float.MaxValue;
        foreach (var s in Object.FindObjectsByType<GateSwitch>(FindObjectsSortMode.None))
        {
            float d = (s.transform.position - pos).sqrMagnitude;
            if (d < bd) { bd = d; best = s.penName; }
        }
        return best;
    }

    static string Plural(AnimalBrain.Kind k, int n)
    {
        switch (k)
        {
            case AnimalBrain.Kind.Chicken: return n == 1 ? "A chicken" : "Some chickens";
            case AnimalBrain.Kind.Bunny: return n == 1 ? "A bunny" : "Some bunnies";
            case AnimalBrain.Kind.Sheep: return n == 1 ? "A sheep" : "Some sheep";
            default: return n == 1 ? "A cow" : "Some cows";
        }
    }

    public static string LooseMessage(AnimalBrain.Kind kind, int n, Vector3 where)
    {
        string pen = PenNear(where);
        return pen == "" ? $"{Plural(kind, n)} got loose!" : $"{Plural(kind, n)} got loose near the {pen.ToUpper()} pen!";
    }

    public static bool AnimalNear(Vector3 pos, float range)
    {
        foreach (var a in AnimalBrain.All)
        {
            if (a == null || !a.Catchable) continue;
            Vector3 d = a.transform.position - pos; d.y = 0f;
            if (d.magnitude < range) return true;
        }
        return false;
    }
}
