using Assets.Skripts;
using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class MLAgentController : Agent
{
    [Header("Settings")]
    [SerializeField] public string levelName;

    [Header("Multi-Agent Setup")]
    [Tooltip("Agent index for Physics Layer isolation (0-29)")]
    public int agentIndex = 0;

    private FitnessTracker fitnessTracker;

    [Header("Referenzen")]
    public DropZoneManager dropZoneManager;
    public Transform dropZoneTransform;
    public GameObject palletParent;  // Wo die Paletten im Hierarchiebaum liegen (zum Suchen)
    public Transform forkTransform; // Transform der Gabel-Plattform
    public Transform mastTransform; 

    private MovementController playerMovement;
    public EnviromentController enviromentController;

    [Header("Input")]
    public InputActionReference moveActionRef;
    public InputActionReference forkActionRef;
    public InputActionReference handbrakeActionRef;


    // Limits für Normalisierung (müssen mit Ihrem Controller übereinstimmen) 
    public float minY = 0.0f;
    public float maxY = 2.0f;


    [HideInInspector]
    public bool IsPalletTouched = false;
    [HideInInspector]
    public bool IsPalletLifted = false;
    private Rigidbody rb;
    private Vector3 startPos;
    private Vector3 forkStartPos;
    private Vector3 mastStartPos;

    private int totalPalletsInScene;
    private bool isFirstEpisode = true;
    [HideInInspector]
    public bool wasRespawned = false;
    
    // Helper classes
    private MLAgentPerceptionHelper perceptionHelper;
    private MLAgentRewardHandler rewardHandler;

    [Header("Debugging")]
    [Tooltip("Enables verbose logging for this agent instance")]
    [SerializeField] private bool enableDebugLogs = false;
    [Tooltip("Angular velocity threshold that triggers debug logs")] 
    [SerializeField] private float angularVelocityDebugThreshold = 10f;
    [Tooltip("Linear velocity threshold that triggers debug logs")] 
    [SerializeField] private float linearVelocityDebugThreshold = 15f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();

        // Force-enable debug logs for the last agent (index 4) to trace the reported issue.
        if (agentIndex == 4)
        {
            enableDebugLogs = true;
            LogDebug("Verbose logging auto-enabled for debugging last agent behaviour.");
        }

        startPos = transform.localPosition;
        forkStartPos = forkTransform.localPosition;
        mastStartPos = mastTransform.localPosition;
        LogDebug($"Awake: startPos={startPos}, forkStartPos={forkStartPos}, mastStartPos={mastStartPos}");

        dropZoneManager = FindAnyObjectByType<DropZoneManager>();
        if (dropZoneManager != null)
        {
            dropZoneTransform = dropZoneManager.transform;
            LogDebug($"Found DropZoneManager '{dropZoneManager.name}'");
        }
        else
        {
            Debug.LogError("[MLAgentController] DropZoneManager not found in scene!");
        }

        enviromentController = FindAnyObjectByType<EnviromentController>();
        if (enviromentController == null)
        {
            Debug.LogError("[MLAgentController] EnviromentController not found in scene!");
        }
        else
        {
            LogDebug($"Found EnviromentController '{enviromentController.name}'");
        }

        // Find agent-specific pallet parent.
        if (palletParent == null)
        {
            palletParent = GameObject.Find($"Pallets ({agentIndex + 1})");
            if (palletParent == null)
            {
                Debug.LogError($"[MLAgentController] Could not find 'Pallets ({agentIndex + 1})'!");
                return;
            }
            else
            {
                LogDebug($"Found pallet parent '{palletParent.name}'");
            }
        }

        // Initialize helpers AFTER all dependencies are found
        if (dropZoneManager != null && palletParent != null)
        {
            perceptionHelper = new MLAgentPerceptionHelper(transform, palletParent, dropZoneManager);
            rewardHandler = new MLAgentRewardHandler(this, dropZoneManager, rb, forkTransform, perceptionHelper, minY, maxY);
            totalPalletsInScene = perceptionHelper.GetTotalPalletCount();
            Debug.Log($"[MLAgentController] Agent {agentIndex + 1} initialized with {totalPalletsInScene} pallets");
        }
        else
        {
            Debug.LogError($"[MLAgentController] Cannot initialize Agent {agentIndex + 1}!");
        }

        // Try to find FitnessTracker if not assigned
        if (fitnessTracker == null)
        {
            fitnessTracker = GetComponent<FitnessTracker>();
            if (fitnessTracker != null)
            {
                LogDebug("Attached FitnessTracker component located.");
            }
            else
            {
                Debug.LogWarning($"[MLAgentController] Agent {agentIndex + 1} missing FitnessTracker component.");
            }
        }

        // Set Physics Layer for ghost agent isolation
        int targetLayer = ResolveAgentLayer(agentIndex);

        if (targetLayer != -1)
        {
            SetLayerRecursively(gameObject, targetLayer);
            LogDebug($"Assigned physics layer {targetLayer}.");
        }
        else
        {
            Debug.LogWarning($"[MLAgentController] Layer 'Agent_{agentIndex + 1}' or 'Agent_{agentIndex + 1:D2}' not found! Please create it in Project Settings > Tags and Layers.");
        }
    }

    private void OnEnable()
    {
        LogDebug("OnEnable invoked.");
    }

    private void OnDisable()
    {
        LogDebug("OnDisable invoked.");
    }

    private void FixedUpdate()
    {
        if (!enableDebugLogs || rb == null) return;

        float angularMag = rb.angularVelocity.magnitude;
        float linearMag = rb.linearVelocity.magnitude;

        if (angularMag > angularVelocityDebugThreshold)
        {
            LogDebug($"High angular velocity detected: {rb.angularVelocity} (mag={angularMag:F2}) at step {StepCount}");
        }

        if (linearMag > linearVelocityDebugThreshold)
        {
            LogDebug($"High linear velocity detected: {rb.linearVelocity} (mag={linearMag:F2}) at step {StepCount}");
        }

        if (transform.position.y < -1f)
        {
            LogWarning($"Agent below expected Y: position={transform.position}, velocity={rb.linearVelocity}, angular={rb.angularVelocity}");
        }
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
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
                LogDebug($"Resolved layer '{layerName}' -> {layer}");
                return layer;
            }
            else
            {
                LogDebug($"Layer '{layerName}' not found.");
            }
        }

        return -1;
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[MLAgentController] Agent {agentIndex + 1}: {message}");
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MLAgentController] Agent {agentIndex + 1}: {message}");
    }

    public bool IsDebugLoggingEnabled => enableDebugLogs;

    public override void OnEpisodeBegin()
    {
        LogDebug($"OnEpisodeBegin invoked. wasRespawned={wasRespawned}, isFirstEpisode={isFirstEpisode}, fitnessDone={(fitnessTracker != null ? fitnessTracker.IsDone : (bool?)null)}");

        // 1. Check if this is a PPO-triggered reset that we didn't catch
        if (!isFirstEpisode && fitnessTracker != null && !fitnessTracker.IsDone)
        {
            var evoManager = Object.FindFirstObjectByType<EvolutionManager>();
            if (evoManager != null && evoManager.generationMode == EvolutionManager.GenerationMode.Survival)
            {
                LogDebug("PPO reset detected while agent still active. Marking as done.");
                fitnessTracker.MarkAsDone();
            }
        }
        isFirstEpisode = false;

        // 2. SURVIVAL MODE CHECK: If Done, stay frozen
        if (fitnessTracker != null && fitnessTracker.IsDone)
        {
            LogDebug("FitnessTracker indicates done. Forcing kinematic state and aborting episode setup.");
            // Set kinematic FIRST, then velocity (order matters!)
            if (!rb.isKinematic) rb.isKinematic = true;
            return; 
        }

        enviromentController.ResetObjectPositions(dropZoneTransform, agentIndex);
        Academy.Instance.StatsRecorder.Add($"Lvls/{levelName}/EpisodesCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/EpisodesCount", 1, StatAggregationMethod.Sum);
        rb.isKinematic = false; // Ensure physics is on FIRST
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        LogDebug("Reset velocities and disabled kinematic mode.");

        // If we were just respawned by the evolution manager, don't reset position.
        if (wasRespawned)
        {
            LogDebug($"Respawn flag true. Maintaining current transform (pos={transform.position}, rot={transform.rotation.eulerAngles}).");
            wasRespawned = false; // Consume the flag
        }
        else
        {
            transform.localPosition = startPos;
            transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            LogDebug($"Reset transform to start position {startPos}.");
        }

        // Reset fork and mast regardless
        forkTransform.localPosition = forkStartPos;
        mastTransform.localPosition = mastStartPos;
        LogDebug("Fork and mast positions reset to defaults.");
        // transform.Rotate(0f, Random.Range(0f, 360f), 0f); // Optional für Variation.

        // NEU: DropZone leeren und Zähler zurücksetzen
        if (dropZoneManager != null) dropZoneManager.palletsInZone.Clear();
        
        rewardHandler.Reset();
        rewardHandler.hasEverTouchedPallet = false;
        perceptionHelper.FindClosestPallets();
        LogDebug("Perception helper updated target pallets after reset.");

    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (fitnessTracker != null && fitnessTracker.IsDone)
        {
            LogDebug("Ignoring actions because agent is marked done.");
            return;
        }

        // 1. Aktionen extrahieren
        float moveInput = actions.ContinuousActions[0];
        float rotateInput = actions.ContinuousActions[1];

        int forkDecision = actions.DiscreteActions[0];
        int handbrakeDecision = actions.DiscreteActions[1];

        float forkInput = forkDecision == 1 ? 1f : forkDecision == 2 ? -1f : 0f;
        float handbrakeInput = handbrakeDecision == 1 ? 1f : 0f;

        // 2. Aktionen ausführen
        playerMovement.SetInput(moveInput, rotateInput, forkInput, handbrakeInput);

        rewardHandler.UpdateActions(actions.ContinuousActions);

        // 3. Belohnungslogik anwenden (ausgelagert)
        if (IsPalletTouched)
        {
            rewardHandler.hasEverTouchedPallet = true;
        }
        rewardHandler.ApplyRewardLogic(forkInput, StepCount, MaxStep);

        if (enableDebugLogs)
        {
            if (StepCount % 25 == 0 || Mathf.Abs(rb.angularVelocity.magnitude) > angularVelocityDebugThreshold)
            {
                LogDebug($"ActionReceived step={StepCount}, move={moveInput:F2}, steer={rotateInput:F2}, fork={forkInput}, handbrake={handbrakeInput}, vel={rb.linearVelocity}, angVel={rb.angularVelocity}");
            }
        }

        // 4. Episoden-Ende prüfen
        if (dropZoneManager.IsComplete(totalPalletsInScene))
        {
            rewardHandler.ReachGoal(StepCount, MaxStep);
            if (fitnessTracker != null) fitnessTracker.MarkAsDone();
            LogDebug("Drop zone complete – agent marked as done.");
        }

        // End of episode due to timeout
        if (StepCount >= MaxStep)
        {
            // DO NOT call EndEpisode() here. This creates a race condition with the EvolutionManager.
            // Instead, just mark this agent as 'done' and let the manager handle the generation end in a controlled way.
            if (fitnessTracker != null) fitnessTracker.MarkAsDone();

            // We can still apply a penalty for timing out.
            AddAgentReward(-1f);

            Academy.Instance.StatsRecorder.Add("Result/WallCollision", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/DieMaxStep", 1, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/WallCollision", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/DieMaxStep", 1, StatAggregationMethod.Sum);
            Debug.Log("Died, Steps überschritten");
            LogDebug("MaxStep reached; timeout penalty applied.");
        }

        rewardHandler.SaveLastActions(actions.ContinuousActions);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("wall"))
        {
            Academy.Instance.StatsRecorder.Add("Result/WallCollision", 1, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/DieMaxStep", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/WallCollision", 1, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/DieMaxStep", 0, StatAggregationMethod.Sum);
            
            rewardHandler.Die();
            if (fitnessTracker != null) fitnessTracker.MarkAsDone();
            LogWarning($"Collided with wall '{collision.gameObject.name}'. Marking as done.");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;

        // 1. Move Input (Horizontal/Vertical) - Liest Vektor2 (korrekt)
        Vector2 moveInput = moveActionRef.action.ReadValue<Vector2>();

        // 2. Fork Input - Muss korrekt als float gelesen werden.
        // Wenn 'forkAction' als 1D Axis Composite (Float) eingerichtet ist, ist dies KORREKT:
        float forkInput = forkActionRef.action.ReadValue<float>();
        float handbrakeInput = handbrakeActionRef.action.ReadValue<float>();

        // Zuweisung zu den ML-Agents ActionBuffers:
        continuousActions[0] = moveInput.y; // Vertical (W/S)
        continuousActions[1] = moveInput.x; // Horizontal (A/D)
        var discreteActions = actionsOut.DiscreteActions;
        discreteActions[0] = forkInput > 0.5f ? 1 : forkInput < -0.5f ? 2 : 0;
        discreteActions[1] = handbrakeInput > 0.5f ? 1 : 0;
    }

    public void ReinitializeForRespawn(int newAgentIndex)
    {
        Debug.Log($"[MLAgentController] Re-initializing agent {agentIndex+1} as a copy of {newAgentIndex+1}.");
        LogDebug($"ReinitializeForRespawn called with parent index {newAgentIndex}");

        // Find the pallet parent of the elite agent we are copying
        palletParent = GameObject.Find($"Pallets ({newAgentIndex + 1})");
        if (palletParent == null)
        {
            Debug.LogError($"[MLAgentController] Respawn failed: Could not find 'Pallets ({newAgentIndex + 1})'!");
            return;
        }
        else
        {
            LogDebug($"Respawn pallet parent '{palletParent.name}' found.");
        }

        // Re-create the helpers with the new pallet references
        if (dropZoneManager != null)
        {
            perceptionHelper = new MLAgentPerceptionHelper(transform, palletParent, dropZoneManager);
            rewardHandler = new MLAgentRewardHandler(this, dropZoneManager, rb, forkTransform, perceptionHelper, minY, maxY);
            totalPalletsInScene = perceptionHelper.GetTotalPalletCount();
            Debug.Log($"[MLAgentController] Respawn successful. Now targeting {totalPalletsInScene} pallets.");
            LogDebug("Respawn helpers reinitialized.");
        }

        // Reset internal state for the new episode
        rewardHandler.Reset();
        perceptionHelper.FindClosestPallets();
        LogDebug("Respawn state reset completed.");
    }

    public void AddAgentReward(float ammount)
    {
        AddReward(ammount);
        if (fitnessTracker != null)
        {
            fitnessTracker.AddFitness(ammount);
        }
    }


    public Dictionary<string, string> GetDebugInformations()
    {
        // Limits für Normalisierung (aus der Klasse)
        float minY = 0.0f;
        float maxY = 2.0f;
        float forkNorm = (forkTransform.localPosition.y - minY) / (maxY - minY);
        
        Transform target = perceptionHelper.TargetPallet;
        string targetName = target != null ? target.name : "None";
        string targetDist = target != null ? Vector3.Distance(transform.position, target.position).ToString("F2") : "-";

        return new Dictionary<string, string>
        {
            // --- ALLGEMEIN ---
            { "--- AGENT ZUSTAND ---", "" },
            { "Cum. Reward", GetCumulativeReward().ToString("F4") },
            { "Fitness", fitnessTracker != null ? fitnessTracker.GetFitness().ToString("F4") : "N/A" },
            { "Steps", $"{StepCount} / {MaxStep}" },
            { "Total Paletten", totalPalletsInScene.ToString() },
            { "Gesicherte Paletten", dropZoneManager.GetCount().ToString() },

            // --- FAHRZEUG PHYSIK ---
            { "--- PHYSIK ---", "" },
            { "Velocity (m/s)", rb.linearVelocity.magnitude.ToString("F2") },
            { "Angular Vel. (Y)", rb.angularVelocity.y.ToString("F2") },
            { "Player Pos", transform.position.ToString("F2") },

            // --- GABEL STATUS ---
            { "--- GABEL ---", "" },
            { "Gabel Höhe (Y)", forkTransform.localPosition.y.ToString("F2") },
            { "Gabel Höhe (Norm)", Mathf.Clamp(forkNorm, 0f, 1f).ToString("F2") },
            { "Pal. Berührt (IsPalletTouched)", IsPalletTouched.ToString() },
            { "Pal. Angehoben (IsPalletLifted)", IsPalletLifted.ToString() },
            
            // --- TARGET PALLET ---
            { "--- TARGET ---", "" },
            { "Target Pallet", targetName },
            { "Distance", targetDist },

            // --- BELOHNUNG (OBSERVATION ZUM FORTSCHRITT) ---
            { "--- FORTSCHRITT ---", "" },
            { "Fortschritt (Obs.)", ((float)dropZoneManager.GetCount() / totalPalletsInScene).ToString("F2") }
        };
    }

}