using UnityEngine;

public interface IEvolutionAgent
{
    float GetFitness();
    void ResetFitness();
    void Die(); // Called when agent is culled
    void OnSurvive(); // Called when agent survives as elite
    GameObject gameObject { get; } // Access to GameObject
}
