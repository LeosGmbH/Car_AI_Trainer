using Assets.Skripts;
using System.Collections.Generic;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

public class MLAgentController : Agent
{

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
    public bool IsPalletLifted = false;
    private Rigidbody rb;
    private Vector3 startPos;
    private Vector3 forkStartPos;
    private Vector3 mastStartPos;

    private int totalPalletsInScene;
    
    // Helper classes
    private MLAgentPerceptionHelper perceptionHelper;
    private MLAgentRewardHandler rewardHandler;


    void Start()
    {
        startPos = transform.localPosition;
        forkStartPos = forkTransform.localPosition;
        mastStartPos = mastTransform.localPosition;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();
        
        // Initialize helpers
        perceptionHelper = new MLAgentPerceptionHelper(transform, palletParent, dropZoneManager);
        rewardHandler = new MLAgentRewardHandler(this, dropZoneManager, rb, forkTransform, perceptionHelper, minY, maxY);

        // Zähle alle Paletten zu Beginn
        totalPalletsInScene = perceptionHelper.GetTotalPalletCount();
    }

    public override void OnEpisodeBegin()
    {
        enviromentController.ResetObjectPositions();
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/EpisodesCount", 1, StatAggregationMethod.Sum);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = startPos;
        forkTransform.localPosition = forkStartPos;
        mastTransform.localPosition = mastStartPos;
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        // transform.Rotate(0f, Random.Range(0f, 360f), 0f); // Optional für Variation

        // NEU: DropZone leeren und Zähler zurücksetzen
        if (dropZoneManager != null) dropZoneManager.palletsInZone.Clear();
        
        rewardHandler.Reset();
        perceptionHelper.FindClosestPallets();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        perceptionHelper.FindClosestPallets();

        // Vector Observation Size: 6 Floats

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

        Transform[] closestPallets = perceptionHelper.ClosestPallets;
        for (int i = 0; i < closestPallets.Length; i++)
        {
            Transform pallet = closestPallets[i];
            if (pallet == transform)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
            }
            else
            {
                Vector3 relative = pallet.position - transform.position;
                sensor.AddObservation(Mathf.Clamp(relative.x / 10f, -1f, 1f));
                sensor.AddObservation(Mathf.Clamp(relative.z / 10f, -1f, 1f));
                sensor.AddObservation(1f);
            }
        }
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
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
        rewardHandler.ApplyRewardLogic(forkInput);

        // 4. Episoden-Ende prüfen
        if (dropZoneManager.IsComplete(totalPalletsInScene))
        {
            rewardHandler.ReachGoal(StepCount, MaxStep, totalPalletsInScene);
        }

        if (StepCount >= MaxStep)
        {
            rewardHandler.Die();
            Debug.Log("Died, Steps überschritten");
        }

        rewardHandler.SaveLastActions(actions.ContinuousActions);
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
    }


    public Dictionary<string, string> GetDebugInformations()
    {
        // Limits für Normalisierung (aus der Klasse)
        float minY = 0.0f;
        float maxY = 2.0f;
        float forkNorm = (forkTransform.localPosition.y - minY) / (maxY - minY);

        return new Dictionary<string, string>
        {
            // --- ALLGEMEIN ---
            { "--- AGENT ZUSTAND ---", "" },
            { "Cum. Reward", GetCumulativeReward().ToString("F4") },
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

            // --- BELOHNUNG (OBSERVATION ZUM FORTSCHRITT) ---
            { "--- FORTSCHRITT ---", "" },
            { "Fortschritt (Obs.)", ((float)dropZoneManager.GetCount() / totalPalletsInScene).ToString("F2") }
        };
    }

}