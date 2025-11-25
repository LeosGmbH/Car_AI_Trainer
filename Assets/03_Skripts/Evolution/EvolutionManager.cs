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

    [Tooltip("Pallet prefab (single pallet GameObject that will be cloned)")]
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
            bool shouldLogAgent = ShouldLogAgent(i);
            if (shouldLogAgent)
            {
                Debug.Log($"[EvolutionManager] Preparing spawn for Agent index={i} (name will be Agent_{i + 1})");
            }

            int layer = ResolveAgentLayer(i);
            if (layer == -1)
            {
                Debug.LogWarning($"[EvolutionManager] Layer 'Agent_{i + 1}' or 'Agent_{i + 1:D2}' not found. Agent {i + 1} will use default collisions.");
            }

            SpawnPalletsForAgent(i, palletPositions, layer);
            // 1. Spawn Agent (as child of AllAgents)
            Transform parentTransform = allAgentsParent != null ? allAgentsParent.transform : null;
            GameObject agent = Instantiate(agentPrefab, spawnPos, Quaternion.identity, parentTransform);
            agent.name = $"Agent_{i + 1}";
            if (shouldLogAgent)
            {
                Debug.Log($"[EvolutionManager] Spawned GameObject '{agent.name}' at {spawnPos} with layer {layer}");
            }

            // 2. Set Layer
            if (layer != -1)
            {
                SetLayerRecursively(agent, layer);
                if (shouldLogAgent)
                {
                    Debug.Log($"[EvolutionManager] Applied physics layer {layer} to '{agent.name}' hierarchy");
                }
            }

            // 3. Configure Components
            var controller = agent.GetComponent<MLAgentController>();
            if (controller != null)
            {
                controller.agentIndex = i;
                if (shouldLogAgent)
                {
                    Debug.Log($"[EvolutionManager] Assigned agentIndex={i} to '{agent.name}', debugLogging={controller.IsDebugLoggingEnabled}");
                }
            }

            // 4. Spawn Pallets (same positions for all agents)
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
        bool shouldLogAgent = ShouldLogAgent(agentIndex);
        // Create a container for this agent's pallets
        Transform parentTransform = allPalletsParent != null ? allPalletsParent.transform : null;
        GameObject palletContainer = new GameObject($"Pallets ({agentIndex + 1})");
        palletContainer.transform.SetParent(parentTransform);
        palletContainer.transform.position = Vector3.zero;
        palletContainer.transform.rotation = Quaternion.identity;

        if (layer != -1)
        {
            SetLayerRecursively(palletContainer, layer);
            if (shouldLogAgent)
            {
                Debug.Log($"[EvolutionManager] Pallet container '{palletContainer.name}' assigned layer {layer}");
            }
        }

        // Spawn individual pallets at each position
        for (int p = 0; p < positions.Length; p++)
        {
            GameObject pallet = Instantiate(palletPrefab, positions[p], Quaternion.identity, palletContainer.transform);
            pallet.name = $"PalletEvo ({p})";
            if (layer != -1)
            {
                SetLayerRecursively(pallet, layer);
            }
            if (shouldLogAgent)
            {
                Debug.Log($"[EvolutionManager] Spawned pallet '{pallet.name}' for Agent_{agentIndex + 1} at {positions[p]}");
            }
        }
    }

    private int ResolveAgentLayer(int index)
    {
        string[] candidates =
        {
            $"Agent_{index + 1}",
            $"Agent_{index + 1:D2}"
        };

        foreach (string layerName in candidates)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer != -1)
            {
                return layer;
            }
        }

        return -1;
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
        Debug.Log($"[EvolutionManager] Generation {generationCount} started (mode: {generationMode}, agents: {agents.Count})");

        activeAgents.Clear();
        foreach (var agent in agents)
        {
            agent.ResetFitness();
            Debug.Log($"[EvolutionManager] Generation {generationCount}: activated agent '{agent.gameObject.name}'");
            activeAgents.Add(agent);
            if (ShouldLogAgent(agent))
            {
                var ctrl = agent.gameObject.GetComponent<MLAgentController>();
                Debug.Log($"[EvolutionManager] Agent '{agent.gameObject.name}' reset -> IsDone={agent.IsDone}, ctrlDebug={(ctrl != null ? ctrl.IsDebugLoggingEnabled : (bool?)null)}");
            }
        }
    }

    private void EndGeneration()
    {

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
            Debug.Log($"[EvolutionManager] Elite survivor '{elite.gameObject.name}' (fitness {elite.GetFitness():F2})");
            elite.OnSurvive();
            if (ShouldLogAgent(elite))
            {
                Debug.Log($"[EvolutionManager] -> Elite '{elite.gameObject.name}' belongs to tracked agent (index 5)");
            }
        }

        // 5. Handle Others (Die / Respawn)
        foreach (var other in others)
        {
            Debug.Log($"[EvolutionManager] Culling agent '{other.gameObject.name}' (fitness {other.GetFitness():F2})");
            other.Die();

            if (respawnAtElite && elites.Count > 0)
            {
                // Pick random elite to spawn near
                var parent = elites[Random.Range(0, elites.Count)];
                Debug.Log($"[EvolutionManager] Respawning '{other.gameObject.name}' from elite '{parent.gameObject.name}'");
                RespawnAsChild(other, parent);
                if (ShouldLogAgent(other))
                {
                    Debug.Log($"[EvolutionManager] -> '{other.gameObject.name}' (Agent 5) selected for respawn from '{parent.gameObject.name}'");
                }
            }
            else
            {
                // Just reset
            }
        }

        // 6. Next Gen
        generationCount++;
        Debug.Log($"[EvolutionManager] Preparing generation {generationCount}");
        StartGeneration();
    }

    private void RespawnAsChild(IEvolutionAgent child, IEvolutionAgent parent)
    {
        Transform parentTrans = parent.gameObject.transform;
        Transform childTrans = child.gameObject.transform;

        // Teleport with an offset to prevent physics explosions from ground penetration.
        Vector3 verticalOffset = Vector3.up * 0.5f; // Spawn 0.5m above the parent to avoid ground collision.
        Vector3 randomHorizontalOffset = Random.insideUnitSphere * 0.2f; // Small horizontal jitter.
        randomHorizontalOffset.y = 0;

        Vector3 targetPos = parentTrans.position + verticalOffset + randomHorizontalOffset;
        childTrans.position = targetPos;
        childTrans.rotation = parentTrans.rotation;
        Debug.Log($"[EvolutionManager] Respawn '{child.gameObject.name}' near '{parent.gameObject.name}' at {targetPos}");

        // HARD RESET PHYSICS STATE: This is the definitive fix.
        // Deactivating and reactivating the GameObject forces Unity to discard the old,
        // corrupted physics state and create a fresh one, preventing the explosion.
        Debug.Log($"[EvolutionManager] Respawn '{child.gameObject.name}': toggling active state to reset physics");
        child.gameObject.SetActive(false);
        child.gameObject.SetActive(true);

        // Re-initialize the agent's logic to find the new pallets
        var childController = child.gameObject.GetComponent<MLAgentController>();
        var parentController = parent.gameObject.GetComponent<MLAgentController>();
        if (childController != null && parentController != null)
        {
            Debug.Log($"[EvolutionManager] Respawn '{child.gameObject.name}': reinit with parent index {parentController.agentIndex}");
            childController.ReinitializeForRespawn(parentController.agentIndex);
            childController.wasRespawned = true; // Set flag to prevent position reset
            if (ShouldLogAgent(child))
            {
                Debug.Log($"[EvolutionManager] -> '{child.gameObject.name}' (Agent 5) marked as respawned; parent index {parentController.agentIndex}");
            }
        }
        else
        {
            Debug.LogWarning($"[EvolutionManager] Respawn '{child.gameObject.name}': missing MLAgentController (child: {childController != null}, parent: {parentController != null})");
        }
    }

    private bool ShouldLogAgent(int agentIndex)
    {
        return agentIndex == 4; // Agent 5 (0-based index)
    }

    private bool ShouldLogAgent(IEvolutionAgent agent)
    {
        if (agent == null || agent.gameObject == null) return false;
        return agent.gameObject.name.EndsWith("5") || agent.gameObject.name.Contains("5");
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
