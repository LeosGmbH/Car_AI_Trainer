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
    private Transform[] closestPallets = new Transform[5];
    private float lastDistanceToClosestPallet = float.MaxValue;
    private float[] lastActions = new float[4];
    private float[] currentActions = new float[4];


    private int totalPalletsInScene;
    private int lastFramePalletCount = 0;


    void Start()
    {
        startPos = transform.localPosition;
        forkStartPos = forkTransform.localPosition;
    }

    private void Awake()
    {

        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();
        // Zähle alle Paletten zu Beginn
        totalPalletsInScene = palletParent.transform.childCount;
    }

    private void FindClosestPallet()
    {
        for (int i = 0; i < closestPallets.Length; i++)
        {
            closestPallets[i] = transform;
        }

        List<Transform> unsecuredPallets = new List<Transform>();
        int childCount = palletParent.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = palletParent.transform.GetChild(i);
            if (!dropZoneManager.palletsInZone.Contains(child.gameObject))
            {
                unsecuredPallets.Add(child);
            }
        }

        unsecuredPallets.Sort((a, b) =>
            ((a.position - transform.position).sqrMagnitude)
                .CompareTo((b.position - transform.position).sqrMagnitude));

        int limit = Mathf.Min(closestPallets.Length, unsecuredPallets.Count);
        for (int i = 0; i < limit; i++)
        {
            closestPallets[i] = unsecuredPallets[i];
        }
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
        lastDistanceToClosestPallet = float.MaxValue;
        for (int i = 0; i < lastActions.Length; i++)
        {
            lastActions[i] = 0f;
        }
        for (int i = 0; i < currentActions.Length; i++)
        {
            currentActions[i] = 0f;
        }
        FindClosestPallet();
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

        FindClosestPallet();
        for (int i = 0; i < closestPallets.Length; i++)
        {
            Transform pallet = closestPallets[i];
            Vector3 relative = pallet.position - transform.position;
            sensor.AddObservation(Mathf.Clamp(relative.x / 10f, -1f, 1f));
            sensor.AddObservation(Mathf.Clamp(relative.z / 10f, -1f, 1f));
            float state = pallet == transform ? 0f : 1f;
            sensor.AddObservation(state);
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

        int actionCount = Mathf.Min(actions.ContinuousActions.Length, currentActions.Length);
        for (int i = 0; i < actionCount; i++)
        {
            currentActions[i] = actions.ContinuousActions[i];
        }
        for (int i = actionCount; i < currentActions.Length; i++)
        {
            currentActions[i] = 0f;
        }

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

        for (int i = 0; i < actionCount; i++)
        {
            lastActions[i] = actions.ContinuousActions[i];
        }
        for (int i = actionCount; i < lastActions.Length; i++)
        {
            lastActions[i] = 0f;
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


    private void ApplyRewardLogic(float forkInput)
    {
        // 1. Zeitstrafe (existentiell)
        AddReward(-0.001f);

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

        if (rb.linearVelocity.magnitude > 0.1f)
        {
            AddReward(0.003f);
        }

        if (closestPallets[0] != transform)
        {
            float currentDistance = Vector3.Distance(transform.position, closestPallets[0].position);
            if (lastDistanceToClosestPallet != float.MaxValue)
            {
                float distanceDelta = lastDistanceToClosestPallet - currentDistance;
                AddReward(0.01f * distanceDelta);
            }
            lastDistanceToClosestPallet = currentDistance;
        }
        else
        {
            lastDistanceToClosestPallet = float.MaxValue;
        }

        float forkNorm = Mathf.Clamp01((forkTransform.localPosition.y - minY) / (maxY - minY));
        bool isPalletHeld = IsPalletLifted;
        bool isInDropZone = dropZoneManager.IsAgentInDropZone(transform.position);

        if (!isPalletHeld)
        {
            AddReward(0.002f * (1.0f - forkNorm));
            AddReward(-0.001f * forkNorm);
        }
        else if (!isInDropZone)
        {
            AddReward(0.002f * forkNorm);
            AddReward(-0.001f * (1.0f - forkNorm));
        }
        else
        {
            AddReward(0.005f * (1.0f - forkNorm));
            AddReward(-0.005f * forkNorm);
        }

        if (rb.linearVelocity.magnitude < 0.1f)
        {
            AddReward(-0.005f);
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
        AddReward(-0.002f * jitterPenalty);
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