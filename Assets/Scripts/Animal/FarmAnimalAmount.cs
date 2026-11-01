// Farm Chaos - "Animals: Few / Normal / Lots" from the lobby's settings board.
//   Few    -> every other animal stays home (about half)
//   Normal -> the farm as built
//   Lots   -> extra copies join each pen (about 1.6x)
// Every player does exactly the same thing (same names, same spots), so online the animals still line up.
// Nothing to set up: runs by itself when the farm loads.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FarmAnimalAmount
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook() { SceneManager.sceneLoaded -= OnScene; SceneManager.sceneLoaded += OnScene; OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }

    static void OnScene(Scene s, LoadSceneMode m)
    {
        // big parties (5-6 players) get about a third more animals, so nobody is left with nothing to chase
        float bigParty = PartySettings.Players >= 5 ? 0.35f : 0f;
        if (PartySettings.Animals == 1 && bigParty == 0f) return;
        var animals = Object.FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None)
            .Where(a => a.gameObject.scene == s && !a.name.Contains("_extra")).OrderBy(a => a.name, System.StringComparer.Ordinal).ToList();
        if (animals.Count == 0) return;

        if (PartySettings.Animals == 0)
        {
            // keep the odd-numbered ones (Chicken_1, Chicken_3...) - at least one of each kind
            foreach (var g in animals.GroupBy(a => a.kind))
            {
                var list = g.ToList();
                for (int i = 1; i < list.Count; i += 2) list[i].gameObject.SetActive(false);
            }
            return;
        }

        // normal/lots: copy some of each kind (lots = ~60%, plus extra for a big party)
        float frac = (PartySettings.Animals == 2 ? 0.6f : 0f) + bigParty;   // copies are placed a little way from the original (same spot on every device)
        foreach (var g in animals.GroupBy(a => a.kind))
        {
            var list = g.ToList();
            int extra = Mathf.CeilToInt(list.Count * frac);
            for (int i = 0; i < extra; i++)
            {
                var src = list[i % list.Count];
                var copy = Object.Instantiate(src.gameObject, src.transform.parent);
                copy.name = $"{src.name}_extra{i + 1}";
                Vector3 off = Quaternion.Euler(0f, 137f * (i + 1), 0f) * new Vector3(0.9f, 0f, 0f);
                Vector3 p = src.transform.position + off;
                // stay inside the pen
                Vector3 toCentre = src.homeCentre - p; toCentre.y = 0f;
                float maxR = Mathf.Max(0.5f, src.homeRadius * 0.8f);
                Vector3 fromC = -toCentre; if (fromC.magnitude > maxR) p = src.homeCentre + fromC.normalized * maxR + Vector3.up * (p.y - src.homeCentre.y);
                copy.transform.SetPositionAndRotation(p, src.transform.rotation * Quaternion.Euler(0f, 90f * (i + 1), 0f));
            }
        }
    }
}
