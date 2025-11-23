using UnityEngine;
using Unity.MLAgents;
using Assets.Skripts;
using Unity.MLAgents.Actuators;

public class MLAgentRewardHandler
{
    private MLAgentController agent;
    private DropZoneManager dropZoneManager;
    private Rigidbody rb;
    private Transform forkTransform;
    private MLAgentPerceptionHelper perceptionHelper;

    private float lastDistanceToClosestPallet = float.MaxValue;
    private int lastFramePalletCount = 0;
    
    // Limits matching the controller
    private float minY;
    private float maxY;

    // Action tracking for jitter penalty
    private float[] lastActions = new float[4];
    private float[] currentActions = new float[4];

    public MLAgentRewardHandler(MLAgentController agent, DropZoneManager dropZoneManager, Rigidbody rb, Transform forkTransform, MLAgentPerceptionHelper perceptionHelper, float minY, float maxY)
    {
        this.agent = agent;
        this.dropZoneManager = dropZoneManager;
        this.rb = rb;
        this.forkTransform = forkTransform;
        this.perceptionHelper = perceptionHelper;
        this.minY = minY;
        this.maxY = maxY;
    }

    public void Reset()
    {
        lastFramePalletCount = 0;
        lastDistanceToClosestPallet = float.MaxValue;
        
        for (int i = 0; i < lastActions.Length; i++) lastActions[i] = 0f;
        for (int i = 0; i < currentActions.Length; i++) currentActions[i] = 0f;
    }

    public void UpdateActions(ActionSegment<float> continuousActions)
    {
        int actionCount = Mathf.Min(continuousActions.Length, currentActions.Length);
        for (int i = 0; i < actionCount; i++)
        {
            currentActions[i] = continuousActions[i];
        }
        for (int i = actionCount; i < currentActions.Length; i++)
        {
            currentActions[i] = 0f;
        }
    }

    public void SaveLastActions(ActionSegment<float> continuousActions)
    {
        int actionCount = Mathf.Min(continuousActions.Length, lastActions.Length);
        for (int i = 0; i < actionCount; i++)
        {
            lastActions[i] = continuousActions[i];
        }
        for (int i = actionCount; i < lastActions.Length; i++)
        {
            lastActions[i] = 0f;
        }
    }

    public void ApplyRewardLogic(float forkInput)
    {
        // 1. Zeitstrafe (existentiell)
        agent.AddReward(-0.001f);

        // 3. Paletten Logik (Das Herzstück)
        int currentCount = dropZoneManager.GetCount();

        if (currentCount > lastFramePalletCount)
        {
            // ERFOLG: Eine NEUE Palette ist sicher in der Zone!
            agent.AddReward(1.0f);
            Debug.Log("Palette secured!");
        }
        else if (currentCount < lastFramePalletCount)
        {
            // MISSERFOLG: Eine Palette ist aus der Zone gefallen!
            agent.AddReward(-1.0f);
            Debug.Log("Palette lost and must be re-collected!");
        }

        lastFramePalletCount = currentCount; // Status für nächsten Frame speichern

        if (rb.linearVelocity.magnitude > 0.1f)
        {
            agent.AddReward(0.003f);
        }

        Transform closestPallet = perceptionHelper.ClosestPallets[0];
        if (closestPallet != agent.transform)
        {
            float currentDistance = Vector3.Distance(agent.transform.position, closestPallet.position);
            if (lastDistanceToClosestPallet != float.MaxValue)
            {
                float distanceDelta = lastDistanceToClosestPallet - currentDistance;
                agent.AddReward(0.05f * distanceDelta);
            }
            lastDistanceToClosestPallet = currentDistance;
        }
        else
        {
            lastDistanceToClosestPallet = float.MaxValue;
        }

        float forkNorm = Mathf.Clamp01((forkTransform.localPosition.y - minY) / (maxY - minY));
        bool isPalletHeld = agent.IsPalletLifted;
        bool isInDropZone = dropZoneManager.IsAgentInDropZone(agent.transform.position);

        if (!isPalletHeld)
        {
            agent.AddReward(0.005f * (1.0f - forkNorm));
            agent.AddReward(-0.005f * forkNorm);
        }
        else if (!isInDropZone)
        {
            agent.AddReward(0.005f * forkNorm);
            agent.AddReward(-0.005f * (1.0f - forkNorm));
        }
        else
        {
            agent.AddReward(0.005f * (1.0f - forkNorm));
            agent.AddReward(-0.005f * forkNorm);
        }

        if (rb.linearVelocity.magnitude < 0.1f)
        {
            agent.AddReward(-0.005f);
        }

        float jitterPenalty = 0f;
        if (lastActions[0] != 0f)
        {
            int limit = Mathf.Min(currentActions.Length, lastActions.Length);
            for (int i = 0; i < limit; i++)
            {
                jitterPenalty += Mathf.Abs(currentActions[i] - lastActions[i]);
            }
        }
        agent.AddReward(-0.0005f * jitterPenalty);
    }

    public void Die()
    {
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/DiedCount", 1, StatAggregationMethod.Sum);
        agent.AddReward(-20f);
        agent.EndEpisode();
    }

    public void ReachGoal(int stepCount, int maxStep, int totalPallets)
    {
        Debug.Log("Survived");
        float timeBonus = Mathf.Clamp(1f - ((float)stepCount / maxStep), 0f, 1f) * 20f;

        agent.AddReward(timeBonus);
        agent.AddReward(10f);

        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("Agent/timeBonus", timeBonus, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Agent/winReward", agent.GetCumulativeReward(), StatAggregationMethod.Average);
        agent.EndEpisode();
    }
}
