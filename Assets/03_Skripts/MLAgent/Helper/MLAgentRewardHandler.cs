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
    private float lastDistanceToDropZone = float.MaxValue;
    private int lastFramePalletCount = 0;
    
    // Limits matching the controller
    private float minY;
    private float maxY;

    public bool hasEverTouchedPallet = false;

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
        lastDistanceToDropZone = float.MaxValue;
        
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

    public void ApplyRewardLogic(float forkInput, int stepCount, int maxStep)
    {
        // 1. Zeitstrafe (existentiell)
        agent.AddReward(-0.001f);

        // 3. Paletten Logik (Das Herzstück)
        int currentCount = dropZoneManager.GetCount();

        if (currentCount > lastFramePalletCount)
        {
            // ERFOLG: Eine NEUE Palette ist sicher in der Zone!
            agent.AddReward(1.0f);
            Academy.Instance.StatsRecorder.Add("Events/PalletSecured", 1, StatAggregationMethod.Sum);
            Debug.Log("Palette secured!");
        }
        else if (currentCount < lastFramePalletCount)
        {
            // MISSERFOLG: Eine Palette ist aus der Zone gefallen!
            agent.AddReward(-1.0f);
            Academy.Instance.StatsRecorder.Add("Events/PalletLost", 1, StatAggregationMethod.Sum);
            Debug.Log("Palette lost and must be re-collected!");
        }

        lastFramePalletCount = currentCount; // Status für nächsten Frame speichern

        if (rb.linearVelocity.magnitude > 0.1f)
        {
            agent.AddReward(0.003f);
        }

        bool isPalletHeld = agent.IsPalletLifted;

        if (isPalletHeld)
        {
            // --- LOGIK: ZUR DROPZONE FAHREN ---
            // Wenn wir eine Palette haben, wollen wir zur DropZone!
            float currentDistanceToDropZone = Vector3.Distance(agent.transform.position, agent.dropZoneTransform.position);
            
            if (lastDistanceToDropZone != float.MaxValue)
            {
                float distanceDelta = lastDistanceToDropZone - currentDistanceToDropZone;
                // Höherer Multiplikator, damit der Agent den Weg zur Zone priorisiert
                float reward = 0.5f * distanceDelta; 
                agent.AddReward(reward);
                Academy.Instance.StatsRecorder.Add("Reward/DistanceToDropZone", reward, StatAggregationMethod.Sum);
            }
            lastDistanceToDropZone = currentDistanceToDropZone;
            
            // Reset Pallet Distance, damit wir beim Ablegen nicht springen
            lastDistanceToClosestPallet = float.MaxValue;
        }
        else
        {
            // --- LOGIK: ZUR PALETTE FAHREN ---
            // Wenn wir KEINE Palette haben, suchen wir die nächste!
            Transform closestPallet = perceptionHelper.ClosestPallets[0];
            if (closestPallet != agent.transform)
            {
                float currentDistance = Vector3.Distance(agent.transform.position, closestPallet.position);
                if (lastDistanceToClosestPallet != float.MaxValue)
                {
                    float distanceDelta = lastDistanceToClosestPallet - currentDistance;
                    float reward = 0.1f * distanceDelta;
                    agent.AddReward(reward);
                    Academy.Instance.StatsRecorder.Add("Reward/DistanceToPallet", reward, StatAggregationMethod.Sum);
                }
                lastDistanceToClosestPallet = currentDistance;
            }
            else
            {
                lastDistanceToClosestPallet = float.MaxValue;
            }

            // Reset DropZone Distance
            lastDistanceToDropZone = float.MaxValue;
        }

        float forkNorm = Mathf.Clamp01((forkTransform.localPosition.y - minY) / (maxY - minY));
        // bool isPalletHeld = agent.IsPalletLifted; // Bereits oben definiert
        bool isInDropZone = dropZoneManager.IsAgentInDropZone(agent.transform.position);
        float moveInput = currentActions[0];

        if (!isPalletHeld)
        {
            // Penalty if fork UP
            if (forkNorm > 0)
            {
                float penalty = -0.005f * forkNorm;
                agent.AddReward(penalty);
                Academy.Instance.StatsRecorder.Add("Penalty/ForkUp", penalty, StatAggregationMethod.Sum);
            }
            
            // Penalty if Driving Backwards
            if (moveInput < 0)
            {
                float penalty = -0.005f * Mathf.Abs(moveInput);
                agent.AddReward(penalty);
                Academy.Instance.StatsRecorder.Add("Penalty/Backward", penalty, StatAggregationMethod.Sum);
            }
        }
        else // isPalletHeld
        {
            if (!isInDropZone)
            {
                // Penalty if Fork DOWN (we want to carry it high enough? Or just not on floor?)
                // Plan says: Penalty if forkNorm < 0.1 (Fork DOWN)
                if (forkNorm < 0.1f)
                {
                    float penalty = -0.005f * (1.0f - forkNorm);
                    agent.AddReward(penalty);
                    Academy.Instance.StatsRecorder.Add("Penalty/ForkHeldDown", penalty, StatAggregationMethod.Sum);
                }
            }
            else // isInDropZone
            {
                // Penalty if Fork UP (assuming we want to drop it)
                // Plan says: Penalty if forkNorm > 0.1 (Fork UP)
                if (forkNorm > 0.1f)
                {
                    float penalty = -0.005f * forkNorm;
                    agent.AddReward(penalty);
                    Academy.Instance.StatsRecorder.Add("Penalty/ForkHeldUp", penalty, StatAggregationMethod.Sum);
                }
            }
        }

        // General Fork Height Penalty
        if (forkTransform.localPosition.y > 0.4f)
        {
             float penalty = -0.005f * (forkTransform.localPosition.y - 0.4f);
             agent.AddReward(penalty);
             Academy.Instance.StatsRecorder.Add("Penalty/ForkHeightLimit", penalty, StatAggregationMethod.Sum);
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
        float jitterReward = -0.0005f * jitterPenalty;
        agent.AddReward(jitterReward);
        Academy.Instance.StatsRecorder.Add("Penalty/Jitter", jitterReward, StatAggregationMethod.Sum);

        // Timeout Logic
        if (stepCount > maxStep * 0.5f)
        {
            if (!hasEverTouchedPallet)
            {
                Academy.Instance.StatsRecorder.Add("Result/TimeoutDeath", 1, StatAggregationMethod.Sum);
                Die();
            }
        }
    }

    public void Die()
    {
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/DiedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/SurvivedCount", 0, StatAggregationMethod.Sum);
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


        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/DiedCount", 0, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("Agent/timeBonus", timeBonus, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Agent/winReward", agent.GetCumulativeReward(), StatAggregationMethod.Average);
        agent.EndEpisode();
    }
}
