using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.MLAgents;

public class EvolutionManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Duration of one generation in seconds")]
    public float generationDuration = 30f;
    
    [Tooltip("Percentage of top agents to keep (0.0 to 1.0)")]
    [Range(0f, 1f)]
    public float survivalRate = 0.2f;

    [Tooltip("If true, dead agents are respawned at the position of an elite")]
    public bool respawnAtElite = true;

    [Header("UI / Debug")]
    public bool showGUI = true;

    // State
    private float timer;
    private int generationCount = 1;
    private List<IEvolutionAgent> agents = new List<IEvolutionAgent>();
    
    // Stats
    private float bestFitness;
    private float avgFitness;
    private float worstFitness;
    private int survivorsCount;

    private void Start()
    {
        // Find all agents
        // Note: This finds components implementing the interface.
        // If agents are instantiated at runtime, this needs to be called again or agents need to register themselves.
        FindAgents();
        StartGeneration();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= generationDuration)
        {
            EndGeneration();
        }
    }

    private void FindAgents()
    {
        // Find all MonoBehaviours that implement IEvolutionAgent
        // This is a bit expensive, so do it only on Start or when needed
        agents.Clear();
        var found = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IEvolutionAgent>();
        agents.AddRange(found);
        Debug.Log($"[EvolutionManager] Found {agents.Count} agents.");
    }

    private void StartGeneration()
    {
        timer = 0f;
        Debug.Log($"[EvolutionManager] Generation {generationCount} Started.");
        
        // Reset all agents
        foreach (var agent in agents)
        {
            agent.ResetFitness();
            // Note: We don't force Agent.EndEpisode() here because PPO might be running its own episodes.
            // But for "Evolution", we usually want a synchronized start.
            // If we want to sync with ML-Agents episodes, we might need to force a reset.
            // For now, we just reset fitness tracking.
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
                // other.ResetFitness(); // Will be done in StartGeneration anyway
            }
        }

        // 6. Next Gen
        generationCount++;
        StartGeneration();
    }

    private void RespawnAsChild(IEvolutionAgent child, IEvolutionAgent parent)
    {
        // Simple respawn logic: Move child to parent's position + slight offset
        // We need to access the Transform. IEvolutionAgent has .gameObject property.
        
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
        
        // Optional: Mutate? 
        // In PPO + Evolution, mutation is usually handled by the Policy update (PPO).
        // Here we just "select" the starting state or keep the agent alive.
        // If we wanted to copy Neural Network weights, we would need to access the Policy.
        // But the user asked for "PPO + Evolution" where PPO learns. 
        // Usually this means: PPO updates weights. Evolution just selects WHO continues or resets positions.
        // If we just move the "bad" agents to the "good" agents' positions, we are essentially 
        // doing "Go-Explore" or "Imitation" by position.
    }

    private void OnGUI()
    {
        if (!showGUI) return;

        float w = 250;
        float h = 150;
        float x = Screen.width - w - 10;
        float y = 10;

        GUI.Box(new Rect(x, y, w, h), "Evolution Stats");

        GUILayout.BeginArea(new Rect(x + 10, y + 25, w - 20, h - 30));
        GUILayout.Label($"Generation: {generationCount}");
        GUILayout.Label($"Time: {timer:F1} / {generationDuration:F1} s");
        GUILayout.Space(5);
        GUILayout.Label($"Best Fitness: {bestFitness:F2}");
        GUILayout.Label($"Avg Fitness: {avgFitness:F2}");
        GUILayout.Label($"Survivors: {survivorsCount} / {agents.Count}");
        GUILayout.EndArea();
    }
}
