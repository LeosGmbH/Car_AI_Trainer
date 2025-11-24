using UnityEngine;
using Unity.MLAgents;
using Assets.Skripts;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

public class MLAgentRewardHandler
{
    private MLAgentController agent;
    private DropZoneManager dropZoneManager;
    private Rigidbody rb;
    private Transform forkTransform;
    private MLAgentPerceptionHelper perceptionHelper;

    // Limits matching the controller
    private float minY;
    private float maxY;

    public bool hasEverTouchedPallet = false;

    // Action tracking
    private float[] lastActions = new float[4];
    private float[] currentActions = new float[4];

    // State tracking for rewards
    private float lastTimeTouched = -100f;
    private float lastTimeLifted = -100f;
    private bool lastIsPalletTouched = false;
    private bool lastIsPalletLifted = false;
    
    // Movement & Position tracking
    private float forwardMoveTimer = 0f;
    private float backwardMoveTimer = 0f;
    
    // Rolling window for position check (Time, Position)
    private Queue<(float time, Vector3 pos)> positionHistory = new Queue<(float, Vector3)>();
    
    // Zone tracking
    private int maxPalletsInZone = 0;
    private float agentOutsideZoneTimer = 0f;

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
        hasEverTouchedPallet = false;
        lastIsPalletTouched = false;
        lastIsPalletLifted = false;
        lastTimeTouched = -100f;
        lastTimeLifted = -100f;
        
        forwardMoveTimer = 0f;
        backwardMoveTimer = 0f;
        
        positionHistory.Clear();
        positionHistory.Enqueue((Time.time, agent.transform.position));
        
        maxPalletsInZone = 0;
        agentOutsideZoneTimer = 0f;

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
        // --- Generelle Rewards/Penalties ---

        // 1. Zeitstrafe
        agent.AddReward(-0.001f);

        // 2. Zittrige Steuerung
        float jitterPenalty = 0f;
        if (lastActions[0] != 0f)
        {
            int limit = Mathf.Min(currentActions.Length, lastActions.Length);
            for (int i = 0; i < limit; i++)
            {
                jitterPenalty += Mathf.Abs(currentActions[i] - lastActions[i]);
            }
        }
        float jitterReward = -0.005f * jitterPenalty;
        agent.AddReward(jitterReward);
        Academy.Instance.StatsRecorder.Add("Penalty/Jitter", jitterReward, StatAggregationMethod.Average);

        // 3. Stillstand
        if (rb.linearVelocity.magnitude < 0.1f)
        {
            agent.AddReward(-0.1f);
            Academy.Instance.StatsRecorder.Add("Penalty/Standstill", -0.1f, StatAggregationMethod.Average);
        }

        // 4. Bewegung (Vorwärts/Rückwärts)
        float moveInput = currentActions[0];
        
        // Vorwärts
        if (moveInput > 0.1f) // Threshold for "moving" input
        {
            forwardMoveTimer += Time.fixedDeltaTime;
            if (forwardMoveTimer > 0.2f)
            {
                agent.AddReward(0.05f);
                Academy.Instance.StatsRecorder.Add("Reward/ForwardMovement", 0.05f, StatAggregationMethod.Average);
            }
        }
        else
        {
            forwardMoveTimer = 0f;
        }

        // Rückwärts
        if (moveInput < -0.1f)
        {
            backwardMoveTimer += Time.fixedDeltaTime;
            if (backwardMoveTimer > 0.2f)
            {
                agent.AddReward(0.015f);
                Academy.Instance.StatsRecorder.Add("Reward/BackwardMovement", 0.015f, StatAggregationMethod.Average);
            }
        }
        else
        {
            backwardMoveTimer = 0f;
        }

        // 5. Positionsänderung Check (0.5s Rolling Window)
        // Add current position
        positionHistory.Enqueue((Time.time, agent.transform.position));
        
        // Remove old positions (> 0.5s) BUT keep at least one that is >= 0.5s ago if possible to compare
        // Actually, we want to compare with the position ~0.5s ago.
        // So we peek.
        while (positionHistory.Count > 0 && Time.time - positionHistory.Peek().time > 0.6f) // Keep a bit more buffer
        {
            positionHistory.Dequeue();
        }
        
        // Find a point that is at least 0.5s ago
        if (positionHistory.Count > 0)
        {
            var oldPoint = positionHistory.Peek();
            if (Time.time - oldPoint.time >= 0.5f)
            {
                float dx = Mathf.Abs(agent.transform.position.x - oldPoint.pos.x);
                float dz = Mathf.Abs(agent.transform.position.z - oldPoint.pos.z);

                if (dx < 1.0f && dz < 1.0f)
                {
                    agent.AddReward(-0.1f);
                    Academy.Instance.StatsRecorder.Add("Penalty/NoPositionChange", -0.1f, StatAggregationMethod.Average);
                }
            }
        }

        // --- Status Variablen ---
        bool isTouched = agent.IsPalletTouched;
        bool isLifted = agent.IsPalletLifted;
        bool isInDropZone = dropZoneManager.IsAgentInDropZone(agent.transform.position);
        float forkY = forkTransform.localPosition.y;
        
        // --- Phase 4: Palette in DropZone (Check first if any pallet is in zone logic applies) ---
        
        int currentPalletCount = dropZoneManager.GetCount();
        
        // "Palette betritt die Zone : Einmalig +10"
        if (currentPalletCount > maxPalletsInZone)
        {
            agent.AddReward(10f);
            Academy.Instance.StatsRecorder.Add("Reward/PalletEnterZoneCount", 1f, StatAggregationMethod.Sum);
            maxPalletsInZone = currentPalletCount;
        }

        // --- Phasen Logik ---

        if (!isTouched && !isLifted)
        {
            // --- Phase 1: Search ---
            
            // Annäherung an nächste Palette
            // Annäherung an nächste Palette
            Transform closestPallet = perceptionHelper.TargetPallet;
            if (closestPallet != null && closestPallet != agent.transform)
            {
                // Distance tracking handled in HandleDistanceRewards
            }
            
            // Gabel zu hoch (y > 0.15)
            if (forkY > 0.15f)
            {
                agent.AddReward(-0.01f);
                Academy.Instance.StatsRecorder.Add("Penalty/Phase1_ForkHigh", -0.01f, StatAggregationMethod.Average);
            }

            // Agent in DropZone
            if (isInDropZone)
            {
                agent.AddReward(-0.5f);
                Academy.Instance.StatsRecorder.Add("Penalty/Phase1_InDropZone", -0.5f, StatAggregationMethod.Average);
            }
        }
        else if (isTouched && !isLifted)
        {
            // --- Phase 2: Touched, Not Lifted ---
            
            // Belohnung einmalig pro Berühren (Cooldown 5s)
            if (!lastIsPalletTouched && isTouched)
            {
                if (Time.time - lastTimeTouched >= 5.0f)
                {
                    agent.AddReward(5f);
                    Academy.Instance.StatsRecorder.Add("Reward/Phase2_TouchCount", 1f, StatAggregationMethod.Sum);
                    lastTimeTouched = Time.time;
                }
            }

            // Palette schleift (Gabel y < 0.2)
            if (forkY < 0.2f)
            {
                agent.AddReward(-0.05f);
                Academy.Instance.StatsRecorder.Add("Penalty/Phase2_Dragging", -0.05f, StatAggregationMethod.Average);
            }
        }
        else if (isTouched && isLifted)
        {
            // --- Phase 3: Lifted ---
            
            // Belohnung einmalig pro Aufheben (Cooldown 5s)
            if (!lastIsPalletLifted && isLifted)
            {
                if (Time.time - lastTimeLifted >= 5.0f)
                {
                    agent.AddReward(5f);
                    Academy.Instance.StatsRecorder.Add("Reward/Phase3_LiftCount", 1f, StatAggregationMethod.Sum);
                    lastTimeLifted = Time.time;
                }
            }

            // Annäherung an Dropzone handled in HandleDistanceRewards
            
            // Palette zu hoch (Gabel y > 1.0)
            if (forkY > 1.0f)
            {
                agent.AddReward(-0.05f);
                Academy.Instance.StatsRecorder.Add("Penalty/Phase3_ForkHigh", -0.05f, StatAggregationMethod.Average);
            }
        }

        // --- Distance Tracking (Phase 1 & 3) ---
        HandleDistanceRewards(isTouched, isLifted);


        // --- Phase 2/3 Common Penalties (Drop Logic) ---
        
        // Touched -> False (Lost contact)
        if (lastIsPalletTouched && !isTouched)
        {
            if (!isInDropZone)
            {
                agent.AddReward(-10f);
                Academy.Instance.StatsRecorder.Add("Penalty/DropOutsideZone_TouchCount", -1f, StatAggregationMethod.Sum);
            }
            else
            {
                // "Palette ist in der Zone und IsPalletTouched wird = false = Einmalige belohung +10"
                agent.AddReward(10f);
                Academy.Instance.StatsRecorder.Add("Reward/DropInZoneCount", 1f, StatAggregationMethod.Sum);
            }
        }

        // Lifted -> False (Dropped)
        if (lastIsPalletLifted && !isLifted)
        {
            if (!isInDropZone)
            {
                agent.AddReward(-10f);
                Academy.Instance.StatsRecorder.Add("Penalty/DropOutsideZone_LiftCount", -1f, StatAggregationMethod.Sum);
            }
        }


        // --- Phase 4: In DropZone Specifics ---
        if (isInDropZone)
        {
            if (isTouched)
            {
                // Gabel zu hoch (y > 0.2)
                if (forkY > 0.2f)
                {
                    agent.AddReward(-0.5f);
                    Academy.Instance.StatsRecorder.Add("Penalty/Phase4_ForkHigh", -0.5f, StatAggregationMethod.Average);
                }

                // Gabel unten (y < 0.2) und vorwärts
                if (forkY < 0.2f && moveInput > 0.1f)
                {
                    agent.AddReward(-1.0f);
                    Academy.Instance.StatsRecorder.Add("Penalty/Phase4_DriveThrough", -1.0f, StatAggregationMethod.Average);
                }

                // Gabel unten (y < 0.2) und rückwärts
                if (forkY < 0.2f && moveInput < -0.1f)
                {
                    agent.AddReward(1.0f);
                    Academy.Instance.StatsRecorder.Add("Reward/Phase4_BackOut", 1.0f, StatAggregationMethod.Average);
                }
            }
        }


        // --- Update Last States ---
        lastIsPalletTouched = isTouched;
        lastIsPalletLifted = isLifted;


        // --- Episoden Ende Bedingungen ---
        
        // 2. Timeout (50% Steps & No Touch)
        if (stepCount > maxStep * 0.5f)
        {
            if (!hasEverTouchedPallet)
            {
                Academy.Instance.StatsRecorder.Add("Result/WallCollision", 0, StatAggregationMethod.Sum);
                Academy.Instance.StatsRecorder.Add("Result/TimeoutNoTouch", 1, StatAggregationMethod.Sum);
                Academy.Instance.StatsRecorder.Add("Result/DieMaxStep", 0, StatAggregationMethod.Sum);

                Academy.Instance.StatsRecorder.Add($"Result/{agent.levelName}/WallCollision", 0, StatAggregationMethod.Sum);
                Academy.Instance.StatsRecorder.Add($"Result/{agent.levelName}/TimeoutNoTouch", 1, StatAggregationMethod.Sum);
                Academy.Instance.StatsRecorder.Add($"Result/{agent.levelName}/DieMaxStep", 0, StatAggregationMethod.Sum);
                Die();
            }
        }

        // 3. Success (Alle Paletten in Zone & Agent 2s draußen)
        if (dropZoneManager.IsComplete(perceptionHelper.GetTotalPalletCount()))
        {
            if (!isInDropZone)
            {
                agentOutsideZoneTimer += Time.fixedDeltaTime;
                if (agentOutsideZoneTimer > 2.0f)
                {
                    ReachGoal(stepCount, maxStep);
                }
            }
            else
            {
                agentOutsideZoneTimer = 0f;
            }
        }
    }

    // Helper for Distance Rewards
    private float lastDistToPallet = -1f;
    private float lastDistToZone = -1f;

    private void HandleDistanceRewards(bool isTouched, bool isLifted)
    {
        // Phase 1: Approach Pallet
        if (!isTouched && !isLifted)
        {
            Transform closest = perceptionHelper.TargetPallet;
            if (closest != null && closest != agent.transform)
            {
                float dist = Vector3.Distance(agent.transform.position, closest.position);
                if (lastDistToPallet != -1f)
                {
                    float diff = lastDistToPallet - dist;
                    // Reward: +0.1 * Distanzänderung
                    if (diff > 0) 
                    {
                        agent.AddReward(0.1f * diff);
                        Academy.Instance.StatsRecorder.Add("Reward/Phase1_Approach", 0.1f * diff, StatAggregationMethod.Average);
                    }
                }
                lastDistToPallet = dist;
            }
            else
            {
                lastDistToPallet = -1f;
            }
            lastDistToZone = -1f; 
        }
        // Phase 3: Approach DropZone
        else if (isTouched && isLifted)
        {
            float dist = Vector3.Distance(agent.transform.position, agent.dropZoneTransform.position);
            if (lastDistToZone != -1f)
            {
                float diff = lastDistToZone - dist;
                // Reward: +0.5 * Distanzänderung
                agent.AddReward(0.5f * diff);
                Academy.Instance.StatsRecorder.Add("Reward/Phase3_ApproachZone", 0.5f * diff, StatAggregationMethod.Average);
            }
            lastDistToZone = dist;
            lastDistToPallet = -1f; 
        }
        else
        {
            lastDistToPallet = -1f;
            lastDistToZone = -1f;
        }
    }

    public void Die()
    {
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/DiedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/DiedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/SurvivedCount", 0, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 0, StatAggregationMethod.Sum);

        // "Tod: -20 Strafe & Episode Ende"
        agent.AddReward(-20f);
        agent.EndEpisode();
    }
    public void ReachGoal(int stepCount, int maxStep)
    {
        Debug.Log("All Pallets Delivered!");
        
        // "+40 reward und Reachgole"
        agent.AddReward(40f);

        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add($"Lvls/{agent.levelName}/DiedCount", 0, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/DiedCount", 0, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("Agent/winReward", agent.GetCumulativeReward(), StatAggregationMethod.Average);
        
        agent.EndEpisode();
    }
}
