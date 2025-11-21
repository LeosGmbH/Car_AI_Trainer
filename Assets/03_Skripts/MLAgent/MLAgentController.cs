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


    private int totalPalletsInScene;
    private int lastFramePalletCount = 0;


    void Start()
    {
        startPos = transform.localPosition;
        forkStartPos= forkTransform.localPosition;
    }

    private void Awake()
    {

        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();
        // Zähle alle Paletten zu Beginn
        totalPalletsInScene = palletParent.transform.childCount;
    }

    public override void OnEpisodeBegin()
    {
        enviromentController.ResetObjectPositions();
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/EpisodesCount", 1, StatAggregationMethod.Sum);
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.localPosition = startPos;
        forkTransform.localPosition = forkStartPos;
        transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        // transform.Rotate(0f, Random.Range(0f, 360f), 0f); // Optional für Variation

        // NEU: DropZone leeren und Zähler zurücksetzen
        if (dropZoneManager != null) dropZoneManager.palletsInZone.Clear();
        lastFramePalletCount = 0;

    }

    public override void CollectObservations(VectorSensor sensor)
    {
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
    }


    public override void OnActionReceived(ActionBuffers actions)
    {
        // 1. Aktionen extrahieren
        float moveInput = actions.ContinuousActions[0];
        float rotateInput = actions.ContinuousActions[1];
        float forkInput = actions.ContinuousActions[2];
        float handbrakeInput = actions.ContinuousActions[3];

        // 2. Aktionen ausführen
        playerMovement.SetInput(moveInput, rotateInput, forkInput, handbrakeInput);

        // 3. Belohnungslogik anwenden (ausgelagert)
        ApplyRewardLogic(forkInput);

        // 4. Episoden-Ende prüfen
        if (dropZoneManager.IsComplete(totalPalletsInScene))
        {
            Debug.LogError($"EPISODE ENDE ZU FRÜH ERKANNT! " +
                           $"Erwartet: {totalPalletsInScene}. " +
                           $"Aktuell gezählt: {dropZoneManager.GetCount()}");
            ReachGoal();
        }

        if (StepCount >= MaxStep)
        {
            Die();
            Debug.Log("Died, Steps überschritten");
        }
    }

    private void ApplyRewardLogic(float forkInput)
    {
        // 1. Zeitstrafe (existentiell)
        AddReward(-0.001f);

        // 2. Energie-Strafe für unnötige Gabelbewegung (Anti-Jitter)
        if (Mathf.Abs(forkInput) > 0.1f)
        {
            AddReward(-0.0005f * Mathf.Abs(forkInput));
        }

        // 3. Paletten Logik (Das Herzstück)
        int currentCount = dropZoneManager.GetCount();

        if (currentCount > lastFramePalletCount)
        {
            // ERFOLG: Eine NEUE Palette ist sicher in der Zone!
            AddReward(1.0f);
            Debug.Log("Palette secured!");
        }
        else if (currentCount < lastFramePalletCount)
        {
            // MISSERFOLG: Eine Palette ist aus der Zone gefallen!
            AddReward(-1.0f);
            Debug.Log("Palette lost and must be re-collected!");
        }

        lastFramePalletCount = currentCount; // Status für nächsten Frame speichern
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
        continuousActions[2] = forkInput;    // Fork (Up/Down)
        continuousActions[3] = handbrakeInput;
    }


    public void Die()
    {
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/DiedCount", 1, StatAggregationMethod.Sum);
        AddReward(-20f);
        EndEpisode();

    }

    public void ReachGoal()
    {
        Debug.Log("Survived");
        float timeBonus = Mathf.Clamp(1f - ((float)StepCount / MaxStep), 0f, 1f) * 20f;

        AddReward(timeBonus);
        AddReward(10f);

        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 1, StatAggregationMethod.Sum);
        Academy.Instance.StatsRecorder.Add("Agent/timeBonus", timeBonus, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Agent/winReward", GetCumulativeReward(), StatAggregationMethod.Average);
        EndEpisode();
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