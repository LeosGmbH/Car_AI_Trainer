using Assets.Skripts;
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
    
    // Helper classes
    private MLAgentPerceptionHelper perceptionHelper;
    private MLAgentRewardHandler rewardHandler;



    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();

        startPos = transform.localPosition;
        forkStartPos = forkTransform.localPosition;
        mastStartPos = mastTransform.localPosition;

        dropZoneManager = FindAnyObjectByType<DropZoneManager>();
        if (dropZoneManager != null)
        {
            dropZoneTransform = dropZoneManager.transform;
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

        // Find agent-specific pallet parent.
        if (palletParent == null)
        {
            palletParent = GameObject.Find($"Pallets ({agentIndex + 1})");
            if (palletParent == null)
            {
                Debug.LogError($"[MLAgentController] Could not find 'Pallets ({agentIndex + 1})'!");
                return;
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
        }

        // Set Physics Layer for ghost agent isolation
        int targetLayer = LayerMask.NameToLayer($"Agent_{agentIndex+1}"); // D2 = 2 digits with leading zero
        if (targetLayer != -1)
        {
            SetLayerRecursively(gameObject, targetLayer);
        }
        else
        {
            Debug.LogWarning($"[MLAgentController] Layer 'Agent_{agentIndex+1:D2}' not found! Please create it in Project Settings > Tags and Layers.");
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

    public override void OnEpisodeBegin()
    {
        // 1. Check if this is a PPO-triggered reset that we didn't catch
        if (!isFirstEpisode && fitnessTracker != null && !fitnessTracker.IsDone)
        {
            var evoManager = Object.FindFirstObjectByType<EvolutionManager>();
            if (evoManager != null && evoManager.generationMode == EvolutionManager.GenerationMode.Survival)
            {
                fitnessTracker.MarkAsDone();
            }
        }
        isFirstEpisode = false;

        // 2. SURVIVAL MODE CHECK: If Done, stay frozen
        if (fitnessTracker != null && fitnessTracker.IsDone)
        {
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
        transform.localPosition = startPos;
        forkTransform.localPosition = forkStartPos;
        mastTransform.localPosition = mastStartPos;
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        // transform.Rotate(0f, Random.Range(0f, 360f), 0f); // Optional für Variation.

        // NEU: DropZone leeren und Zähler zurücksetzen
        if (dropZoneManager != null) dropZoneManager.palletsInZone.Clear();
        
        rewardHandler.Reset();
        rewardHandler.hasEverTouchedPallet = false;
        perceptionHelper.FindClosestPallets();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // If done, send zeros or last state? Zeros is probably safer to avoid noise.
        if (fitnessTracker != null && fitnessTracker.IsDone)
        {
            for(int i=0; i<10; i++) sensor.AddObservation(0f);
            return;
        }

        perceptionHelper.FindClosestPallets();

        // Vector Observation Size: 10 Floats

        // 1. Eigene Geschwindigkeit (2) - Nur XZ-Ebene, normalisiert
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.x / 8f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.z / 8f, -1f, 1f));

        // 2. Gabel Status (2)
        float forkNorm = (forkTransform.localPosition.y - minY) / (maxY - minY);
        sensor.AddObservation(forkNorm);     // Normalisierte Gabelhöhe [0, 1]
        sensor.AddObservation(IsPalletTouched); // NEU: Signal zum Anheben!
        sensor.AddObservation(IsPalletLifted);

        // 3. Fortschritt (1)
        sensor.AddObservation((float)dropZoneManager.GetCount() / totalPalletsInScene);

        // 4. Steuerung (1)
        sensor.AddObservation(rb.angularVelocity.y / 10f); // Normalisierte Drehgeschwindigkeit

        // 5. Target Pallet (3)
        Transform targetPallet = perceptionHelper.TargetPallet;
        if (targetPallet != null)
        {
            Vector3 relative = targetPallet.position - transform.position;
            sensor.AddObservation(Mathf.Clamp(relative.x / 10f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(relative.z / 10f, -1f, 1f));
            sensor.AddObservation(1f); // Target exists
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f); // Target does not exist
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        if (fitnessTracker != null && fitnessTracker.IsDone) return;

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

        // 4. Episoden-Ende prüfen
        if (dropZoneManager.IsComplete(totalPalletsInScene))
        {
            rewardHandler.ReachGoal(StepCount, MaxStep);
            if (fitnessTracker != null) fitnessTracker.MarkAsDone();
        }

        if (StepCount >= MaxStep)
        {
            rewardHandler.Die();
            if (fitnessTracker != null) fitnessTracker.MarkAsDone();

            Academy.Instance.StatsRecorder.Add("Result/WallCollision", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add("Result/DieMaxStep", 1, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/WallCollision", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/TimeoutNoTouch", 0, StatAggregationMethod.Sum);
            Academy.Instance.StatsRecorder.Add($"Result/{levelName}/DieMaxStep", 1, StatAggregationMethod.Sum);
            Debug.Log("Died, Steps überschritten");
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