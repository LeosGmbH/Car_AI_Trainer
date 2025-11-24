using UnityEngine;

public interface IEvolutionAgent
{
    float GetFitness();
    void ResetFitness();
    void Die(); // Called when agent is culled by EvolutionManager
    void OnSurvive(); // Called when agent survives as elite
    
    void MarkAsDone(); // Called when agent finishes its run (dies or succeeds) in Survival Mode
    bool IsDone { get; }

    GameObject gameObject { get; } // Access to GameObject
}
