using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;

public class EvolutionManager : MonoBehaviour
{
    public enum GenerationMode { TimeBased, Survival }

    [Header("Settings")]
    public GenerationMode generationMode = GenerationMode.TimeBased;

    [Tooltip("Duration of one generation in seconds (Only for TimeBased)")]
    public float generationDuration = 30f;
    
    [Tooltip("Percentage of top agents to keep (0.0 to 1.0)")]
    [Range(0f, 1f)]
    public float survivalRate = 0.2f;

    [Tooltip("If true, dead agents are respawned at the position of an elite")]
    public bool respawnAtElite = true;

    [Header("Agent Spawning")]
    [Tooltip("Agent prefab to spawn")]
    public GameObject agentPrefab;

    [Tooltip("Pallet prefab (should contain 'Pallets' with one 'PalletEvo (0)' child)")]
    public GameObject palletPrefab;

    [Tooltip("Number of agents to spawn (1-20)")]
    [Range(1, 20)]
    public int agentCount = 4;

    [Tooltip("Number of pallets per agent")]
    public int palletsPerAgent = 3;

    [SerializeField] private GameObject agentSpawnPos;
    [SerializeField] private GameObject palletSpawnPos;
    [SerializeField] private GameObject allAgentsParent;
    [SerializeField] private GameObject allPalletsParent;

    [Header("UI / Debug")]
    public bool showGUI = true;

    // State
    private float timer;
    private int generationCount = 1;
    private List<IEvolutionAgent> agents = new List<IEvolutionAgent>();
    private HashSet<IEvolutionAgent> activeAgents = new HashSet<IEvolutionAgent>();
    
    // Stats
    private float bestFitness;
    private float avgFitness;
    private float worstFitness;
    private int survivorsCount;

    private void Start()
    {
        SpawnAgents();
        FindAgents();
        StartGeneration();
    }

    private void SpawnAgents()
    {
        if (agentPrefab == null || palletPrefab == null)
        {
            Debug.LogError("[EvolutionManager] Agent or Pallet prefab is null! Cannot spawn.");
            return;
        }

        Vector3 spawnPos = agentSpawnPos != null ? agentSpawnPos.transform.position : Vector3.zero;
        Vector3 firstPalletPos = palletSpawnPos != null ? palletSpawnPos.transform.position : Vector3.zero;

        // Generate random pallet positions (same for all agents)
        Vector3[] palletPositions = new Vector3[palletsPerAgent];
        palletPositions[0] = firstPalletPos; // First pallet at fixed position

        // Generate random positions for remaining pallets
        var envController = FindFirstObjectByType<EnviromentController>();
        if (envController != null && palletsPerAgent > 1)
        {
            // Use EnviromentController to generate random positions
            for (int p = 1; p < palletsPerAgent; p++)
            {
                palletPositions[p] = GenerateRandomPalletPosition();
            }
        }

        // Spawn all agents
        for (int i = 0; i < agentCount; i++)
        {
            // 1. Spawn Agent (as child of AllAgents)
            Transform parentTransform = allAgentsParent != null ? allAgentsParent.transform : null;
            GameObject agent = Instantiate(agentPrefab, spawnPos, Quaternion.identity, parentTransform);
            agent.name = $"Agent_{i + 1}";

            // 2. Set Layer
            int layer = LayerMask.NameToLayer($"Agent_{i + 1}");
            if (layer != -1)
            {
                SetLayerRecursively(agent, layer);
            }

            // 3. Configure Components
            var controller = agent.GetComponent<MLAgentController>();
            if (controller != null)
            {
                controller.agentIndex = i;
            }

            // 4. Spawn Pallets (same positions for all agents)
            SpawnPalletsForAgent(i, palletPositions, layer);
        }

        Debug.Log($"[EvolutionManager] Spawned {agentCount} ghost agents with {palletsPerAgent} pallets each");

        // Notify EnviromentController to find the new pallets
        if (envController != null)
        {
            envController.FindPalletParents();
        }
    }

    private void SpawnPalletsForAgent(int agentIndex, Vector3[] positions, int layer)
    {
        // Spawn the pallet prefab (contains "Pallets" with one "PalletEvo (0)")
        Transform parentTransform = allPalletsParent != null ? allPalletsParent.transform : null;
        GameObject palletContainer = Instantiate(palletPrefab, Vector3.zero, Quaternion.identity, parentTransform);
        palletContainer.name = $"Pallets ({agentIndex + 1})";

        // Find the first PalletEvo child to use as template
        Transform firstPallet = palletContainer.transform.Find("PalletEvo (0)");
        if (firstPallet == null)
        {
            Debug.LogError($"[EvolutionManager] Pallet prefab must have a child named 'PalletEvo (0)'!");
            return;
        }

        // Position the first pallet
        firstPallet.position = positions[0];
        SetLayerRecursively(firstPallet.gameObject, layer);

        // Clone the first pallet for remaining positions
        for (int p = 1; p < positions.Length; p++)
        {
            GameObject clonedPallet = Instantiate(firstPallet.gameObject, positions[p], Quaternion.identity, palletContainer.transform);
            clonedPallet.name = $"PalletEvo ({p})";
            SetLayerRecursively(clonedPallet, layer);
        }
    }

    private Vector3 GenerateRandomPalletPosition()
    {
        // Generate random position in a range (you can adjust these values)
        float x = Random.Range(-10f, 10f);
        float z = Random.Range(-10f, 10f);
        return new Vector3(x, 0.5f, z); // Y=0.5 for pallet height
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void Update()
    {
        if (generationMode == GenerationMode.TimeBased)
        {
            timer += Time.deltaTime;
            if (timer >= generationDuration)
            {
                EndGeneration();
            }
        }
        else if (generationMode == GenerationMode.Survival)
        {
            timer += Time.deltaTime;
            // Failsafe: If generation takes too long (e.g. 5 minutes), force end
            if (timer > 300f) 
            {
                EndGeneration();
            }
        }
    }

    public void NotifyAgentDone(IEvolutionAgent agent)
    {
        if (generationMode != GenerationMode.Survival) return;

        if (activeAgents.Contains(agent))
        {
            activeAgents.Remove(agent);
            
            if (activeAgents.Count == 0)
            {
                EndGeneration();
            }
        }
    }

    private void FindAgents()
    {
        agents.Clear();
        var found = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IEvolutionAgent>();
        agents.AddRange(found);
        Debug.Log($"[EvolutionManager] Found {agents.Count} agents.");
    }

    public int GetAgentCount()
    {
        return agents.Count;
    }

    private void StartGeneration()
    {
        timer = 0f;
        Debug.Log($"[EvolutionManager] Generation {generationCount} Started.");
        
        activeAgents.Clear();
        foreach (var agent in agents)
        {
            agent.ResetFitness();
            activeAgents.Add(agent);
        }
    }

    private void EndGeneration()
    {
        if (agents.Count == 0)
        {
            FindAgents();
            if (agents.Count == 0) return;
        }

        // 1. Sort by fitness
        var sortedAgents = agents.OrderByDescending(a => a.GetFitness()).ToList();

        // 2. Calculate Stats
        bestFitness = sortedAgents[0].GetFitness();
        worstFitness = sortedAgents[sortedAgents.Count - 1].GetFitness();
        avgFitness = sortedAgents.Average(a => a.GetFitness());

        // 3. Select Elites
        int keepCount = Mathf.Max(1, Mathf.RoundToInt(agents.Count * survivalRate));
        survivorsCount = keepCount;

        List<IEvolutionAgent> elites = sortedAgents.Take(keepCount).ToList();
        List<IEvolutionAgent> others = sortedAgents.Skip(keepCount).ToList();

        Debug.Log($"[EvolutionManager] Generation {generationCount} Ended. Best: {bestFitness:F2}, Avg: {avgFitness:F2}");

        // Log to TensorBoard
        Academy.Instance.StatsRecorder.Add("Evolution/BestFitness", bestFitness, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Evolution/AvgFitness", avgFitness, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Evolution/WorstFitness", worstFitness, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Evolution/Generation", generationCount, StatAggregationMethod.Average);

        // 4. Handle Elites
        foreach (var elite in elites)
        {
            elite.OnSurvive();
        }

        // 5. Handle Others (Die / Respawn)
        foreach (var other in others)
        {
            other.Die();
            
            if (respawnAtElite && elites.Count > 0)
            {
                // Pick random elite to spawn near
                var parent = elites[Random.Range(0, elites.Count)];
                RespawnAsChild(other, parent);
            }
            else
            {
                // Just reset
            }
        }

        // 6. Next Gen
        generationCount++;
        StartGeneration();
    }

    private void RespawnAsChild(IEvolutionAgent child, IEvolutionAgent parent)
    {
        Transform parentTrans = parent.gameObject.transform;
        Transform childTrans = child.gameObject.transform;

        // Reset Physics if any
        Rigidbody rb = child.gameObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Teleport
        childTrans.position = parentTrans.position;
        childTrans.rotation = parentTrans.rotation;
    }

    private void OnGUI()
    {
        if (!showGUI) return;

        float w = 250;
        float h = 180; // Increased height for more info
        float x = Screen.width - w - 10;
        float y = 10;

        GUI.Box(new Rect(x, y, w, h), "Evolution Stats");

        GUILayout.BeginArea(new Rect(x + 10, y + 25, w - 20, h - 30));
        GUILayout.Label($"Generation: {generationCount}");
        if (generationMode == GenerationMode.TimeBased)
            GUILayout.Label($"Time: {timer:F1} / {generationDuration:F1} s");
        else
            GUILayout.Label($"Time: {timer:F1} s (Survival)");
            
        GUILayout.Space(5);
        
        // Agent info
        int aliveCount = agents.Count - activeAgents.Count(a => a.IsDone);
        GUILayout.Label($"Agents: {agents.Count} total, {aliveCount} alive");
        
        GUILayout.Space(3);
        GUILayout.Label($"Best Fitness: {bestFitness:F2}");
        GUILayout.Label($"Avg Fitness: {avgFitness:F2}");
        GUILayout.Label($"Survivors: {survivorsCount} / {agents.Count}");
        if (generationMode == GenerationMode.Survival)
            GUILayout.Label($"Active: {activeAgents.Count}");
        GUILayout.EndArea();
    }
}
