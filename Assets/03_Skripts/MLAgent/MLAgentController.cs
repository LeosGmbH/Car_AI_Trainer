using Assets.Skripts;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MLAgentController : Agent
{
    private enum SearchState
    {
        Pallet, //sucht nach pallet
        DropZone, //sucht nach DropZone
    }

    [Header("Referenzen")]
    public Transform dropZoneTransform;
    public Transform forkTransform; // Referenz zur Gabel
    public Transform palletParent;  // Wo die Paletten im Hierarchiebaum liegen (zum Suchen)
    private MovementController playerMovement;


    // Limits für Normalisierung (müssen mit Ihrem Controller übereinstimmen)
    public float minY = 0.0f;
    public float maxY = 2.0f;


    [Header("Einstellungen")]
    public float moveSpeed = 5f;
    public float rotateSpeed = 200f;

    [Header("RL Settings")]
    public float distanceRewardFactor = 0.01f; // Dein "K" Faktor


    private SearchState currentSearchState;

    private bool IsCarryingPallet = false;
    private GameObject currentTargetPallet;
    private float previousDistanceToTarget;




    #region old
    private Rigidbody rb;
    private RayPerceptionSensorComponent3D[] raySensors;
    private Vector3 startPos;
    #endregion

    void Start()
    {
        startPos = transform.localPosition;
    }

    private void Awake()
    {
        raySensors = GetComponents<RayPerceptionSensorComponent3D>(); // Holt ALLE Ray Perception Sensoren im GameObject
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.x / 8f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.z / 8f, -1f, 1f));
        sensor.AddObservation(rb.linearVelocity.y);




        //new:
        // 1. Vektor zum Ziel (3)
        Vector3 targetPos = GetCurrentTargetPosition();
        sensor.AddObservation((targetPos - transform.position).normalized);
        sensor.AddObservation((targetPos - transform.position).magnitude);

        // 2. Distanz zum Ziel (1)
        sensor.AddObservation(Vector3.Distance(transform.position, targetPos));

        // 3. Status: Trägt Palette? (1)
        sensor.AddObservation(IsCarryingPallet);

        // 4. Gabel-Höhe (1) - Wichtig für das Netz zu wissen, wann Aufnahme möglich ist
        float currentH = forkTransform.localPosition.y; // Oder aus Controller lesen
        sensor.AddObservation((currentH - minY) / (maxY - minY));
        sensor.AddObservation((int)currentSearchState);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (StepCount >= MaxStep)
        {
            Die();
            Debug.Log("Died, Steps �berschritten");
        }
        //new

        // --- Aktionen an Ihr Controller-Skript weiterleiten ---
        float moveInput = actions.ContinuousActions[0];   // -1 bis 1
        float rotateInput = actions.ContinuousActions[1]; // -1 bis 1
        float forkInput = actions.ContinuousActions[2];   // -1 bis 1

        // Rufen Sie hier Ihre Methoden auf. Beispiel:
        playerMovement.SetInput(moveInput, rotateInput, forkInput);

        // --- Belohnungen ---
        AddReward(-0.001f); // Time Penalty

        // Distanz-Belohnung
        float currentDistance = Vector3.Distance(transform.position, GetCurrentTargetPosition());
        float distanceChange = previousDistanceToTarget - currentDistance;
        if (distanceChange > 0) AddReward(distanceChange * distanceRewardFactor);

        previousDistanceToTarget = currentDistance;


    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Manuelle Steuerung für Testzwecke (Mapping auf Ihre Tasten)
        var continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Vertical");   // W/S
        continuousActions[1] = Input.GetAxis("Horizontal"); // A/D

        float fork = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) fork = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) fork = -1f;
        continuousActions[2] = fork;
    }

    public void AddAgentReward(float reward)
    {
        AddReward(reward);
    }
    public void EndAgentEpisode()
    {
        Die();

    }

    public void Die()
    {
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/DiedCount", 1, StatAggregationMethod.Sum);
        AddReward(-20f);
        EndEpisode();

    }

    public void ReachGoal()
    {
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/SurvivedCount", 1, StatAggregationMethod.Sum);

        Debug.Log("Survived");
        float timeBonus = Mathf.Clamp(1f - ((float)StepCount / MaxStep), 0f, 1f) * 20f;
        Academy.Instance.StatsRecorder.Add("Agent/timeBonus", timeBonus, StatAggregationMethod.Average);
        Academy.Instance.StatsRecorder.Add("Agent/winReward", GetCumulativeReward(), StatAggregationMethod.Average);
        AddReward(timeBonus);
        AddReward(10f);
        EndEpisode();

    }


    public override void OnEpisodeBegin()
    {
        Academy.Instance.StatsRecorder.Add("WinDeathRatio/EpisodesCount", 1, StatAggregationMethod.Sum);
        transform.localPosition = startPos;
        // transform.Rotate(0f, Random.Range(0f, 360f), 0f);



        //new
        IsCarryingPallet = false;
        currentSearchState = SearchState.Pallet;
        FindClosestPallet();
    }







    private void OnTriggerEnter(Collider other)
    {
        // Prüfen: Ist Gabel unten? (Toleranzbereich z.B. 0.1 Einheiten über Min)
        bool isForkDown = forkTransform.localPosition.y <= (minY + 0.1f);

        // AUFNAHME
        if (other.CompareTag("Pallet") && !IsCarryingPallet && isForkDown)
        {
            IsCarryingPallet = true;

            // Physik-Logik zum "Anheften" der Palette hier oder im Controller aufrufen:
            //playerMovement.AttachPallet(other.gameObject);

            AddReward(1.0f);
            previousDistanceToTarget = Vector3.Distance(transform.position, dropZoneTransform.position);
        }

        // ABLAGE
        else if (other.CompareTag("DropZone") && IsCarryingPallet)
        {
            IsCarryingPallet = false;

            // Palette lösen/zerstören
           // forkliftController.DetachAndDestroyPallet();

            AddReward(5.0f);
            FindClosestPallet();
        }
    }

    // Hilfsfunktionen
    private Vector3 GetCurrentTargetPosition()
    {
        if (IsCarryingPallet) return dropZoneTransform.position;
        if (currentTargetPallet == null) FindClosestPallet();
        return currentTargetPallet != null ? currentTargetPallet.transform.position : transform.position;
    }

    private void FindClosestPallet()
    {
        // (Hier gleiche Suchlogik wie zuvor)
        GameObject[] allPallets = GameObject.FindGameObjectsWithTag("Pallet");
        GameObject closest = null;
        float minDst = Mathf.Infinity;
        foreach (GameObject p in allPallets)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < minDst) { minDst = d; closest = p; }
        }
        currentTargetPallet = closest;
        if (currentTargetPallet != null)
            previousDistanceToTarget = Vector3.Distance(transform.position, currentTargetPallet.transform.position);
    }


}
