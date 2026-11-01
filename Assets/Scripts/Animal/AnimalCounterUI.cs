using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Linq;

public class AnimalCounterUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject counterPanel;   
    public Transform listContainer;      
    public GameObject textPrefab;        

    private Dictionary<AnimalBrain.Kind, int> totalAnimals = new Dictionary<AnimalBrain.Kind, int>();
    
    private Dictionary<AnimalBrain.Kind, TextMeshProUGUI> uiTexts = new Dictionary<AnimalBrain.Kind, TextMeshProUGUI>();

    void Start()
    {
        Invoke(nameof(SetupUI), 0.5f);
    }

    void SetupUI()
    {
        AnimalBrain[] allAnimals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
        
        if (allAnimals.Length == 0) return;

        foreach (var animal in allAnimals)
        {
            if (!totalAnimals.ContainsKey(animal.kind))
                totalAnimals[animal.kind] = 0;
            
            totalAnimals[animal.kind]++;
        }

        foreach (var kvp in totalAnimals)
        {
            AnimalBrain.Kind kind = kvp.Key;
            int total = kvp.Value;

            GameObject textObj = Instantiate(textPrefab, listContainer);
            TextMeshProUGUI textMesh = textObj.GetComponent<TextMeshProUGUI>();
            
            textMesh.text = $"{GetAnimalName(kind)}: {total} | {total}";
            
            uiTexts[kind] = textMesh;
        }

        if (counterPanel != null) counterPanel.SetActive(true);
    }

    void Update()
    {
        if (uiTexts.Count == 0) return;

        AnimalBrain[] currentAnimals = FindObjectsByType<AnimalBrain>(FindObjectsSortMode.None);
        Dictionary<AnimalBrain.Kind, int> currentCounts = new Dictionary<AnimalBrain.Kind, int>();

        foreach (var animal in currentAnimals)
        {
            if (animal.IsGone) continue;   // Farm Chaos: eaten by the wolf (they stay in the scene so online play stays in sync)
            if (!currentCounts.ContainsKey(animal.kind))
                currentCounts[animal.kind] = 0;
            
            currentCounts[animal.kind]++;
        }

        foreach (var kvp in uiTexts)
        {
            AnimalBrain.Kind kind = kvp.Key;
            TextMeshProUGUI uiText = kvp.Value;
            
            int current = currentCounts.ContainsKey(kind) ? currentCounts[kind] : 0;
            int total = totalAnimals[kind];
            uiText.text = $"{GetAnimalName(kind)}: {current} | {total}";

            if (current == 0)
                uiText.color = Color.red;
            else if (current <= total / 2)
                uiText.color = new Color(1f, 0.5f, 0f);
        }
    }

    string GetAnimalName(AnimalBrain.Kind kind)
    {
        switch (kind)
        {
            case AnimalBrain.Kind.Chicken: return "Chicken";
            case AnimalBrain.Kind.Bunny: return "Bunny";
            case AnimalBrain.Kind.Sheep: return "Sheep";
            case AnimalBrain.Kind.Cow: return "Cow";
            default: return "Unknown";
        }
    }
}