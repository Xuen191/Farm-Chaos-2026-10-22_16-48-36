// Farm Chaos - bait on the ground. Animals that like it walk over and eat (and are calm while they eat).
//   Chicken Feed -> chickens      Fresh Hay -> sheep + cows      Pig Slop -> bunnies (they love veggie slop) + cows
//   Magic Whistle -> every animal nearby trots over to you for a moment
// Online: only the host runs the animals, so a friend's bait is sent to the host (NetPlayer.SendBait).
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class AnimalBait
{
    struct Bait { public Vector3 pos; public int mask; public float until; public bool stinky; }
    const string Stinky = "Stinky:";
    static readonly List<Bait> baits = new List<Bait>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { baits.Clear(); SceneManager.sceneLoaded -= Clear; SceneManager.sceneLoaded += Clear; }
    static void Clear(Scene s, LoadSceneMode m) => baits.Clear();

    static int Bit(AnimalBrain.Kind k) => 1 << (int)k;
    public static int MaskFor(string item)
    {
        if (item != null && item.StartsWith(Stinky)) item = item.Substring(Stinky.Length);
        switch (item)
        {
            case "Chicken Feed": return Bit(AnimalBrain.Kind.Chicken);
            case "Fresh Hay": return Bit(AnimalBrain.Kind.Sheep) | Bit(AnimalBrain.Kind.Cow);
            case "Pig Slop": return Bit(AnimalBrain.Kind.Bunny) | Bit(AnimalBrain.Kind.Cow);
            case "Carrot": return Bit(AnimalBrain.Kind.Bunny);
            case "Magic Whistle": return ~0;
            default: return 0;
        }
    }

    /// <summary>Someone dropped food (or blew the whistle) here.</summary>
    public static void Drop(Vector3 pos, string item)
    {
        if (MaskFor(item) == 0) return;
        if (NetPlayer.IAmSpy)
        {   // the spy's bait looks the same... but stinks. Animals run from it.
            item = Stinky + item;
            if (SpySabotage.AnimalNear(pos, 8f)) ScoreKeeper.Add(10, "Stinky bait!");
        }
        if (NetSession.Online && !Unity.Netcode.NetworkManager.Singleton.IsServer)
        {   // the host runs the animals - tell it
            if (NetPlayer.Local != null) NetPlayer.Local.SendBait(pos, item);
            return;
        }
        Add(pos, item);
    }

    /// <summary>Host / solo: remember the bait.</summary>
    public static void Add(Vector3 pos, string item)
    {
        bool stinky = item.StartsWith(Stinky);
        float life = item.EndsWith("Magic Whistle") ? 4f : stinky ? 15f : 25f;
        baits.Add(new Bait { pos = pos, mask = MaskFor(item), until = Time.time + life, stinky = stinky });
    }

    public static bool Nearest(AnimalBrain.Kind kind, Vector3 from, float range, out Vector3 spot)
    {
        spot = default; float best = range; bool found = false;
        for (int i = baits.Count - 1; i >= 0; i--)
        {
            if (Time.time > baits[i].until) { baits.RemoveAt(i); continue; }
            if (baits[i].stinky || (baits[i].mask & Bit(kind)) == 0) continue;
            Vector3 d = baits[i].pos - from; d.y = 0f;
            if (d.magnitude < best) { best = d.magnitude; spot = baits[i].pos; found = true; }
        }
        return found;
    }

    /// <summary>Is there stinky (spy) bait near here? Every animal runs from it.</summary>
    public static bool NearestStink(Vector3 from, float range, out Vector3 spot)
    {
        spot = default; float best = range; bool found = false;
        for (int i = baits.Count - 1; i >= 0; i--)
        {
            if (Time.time > baits[i].until) { baits.RemoveAt(i); continue; }
            if (!baits[i].stinky) continue;
            Vector3 d = baits[i].pos - from; d.y = 0f;
            if (d.magnitude < best) { best = d.magnitude; spot = baits[i].pos; found = true; }
        }
        return found;
    }
}
