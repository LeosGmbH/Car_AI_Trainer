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
        FindAgents();
        StartGeneration();
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
