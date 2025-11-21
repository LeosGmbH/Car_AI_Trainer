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
    public GameObject palletParent;  // Wo die Paletten im Hierarchiebaum liegen (zum Suchen)
    public Transform forkTransform; // Transform der Gabel-Plattform
    private MovementController playerMovement;
    public CheckPalletCollider palletColliderChecker;


    // Limits für Normalisierung (müssen mit Ihrem Controller übereinstimmen)
    public float minY = 0.0f;
    public float maxY = 2.0f;


    public float distanceRewardFactor = 0.01f; // Dein "K" Faktor


    private SearchState currentSearchState;
    [HideInInspector]
    public bool IsCarryingPallet = false;
    private GameObject currentTargetPallet;




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
        float currentH = forkTransform.position.y;// Oder aus Controller lesen
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


    }

    //public override void Heuristic(in ActionBuffers actionsOut)
    //{
    //    // Manuelle Steuerung für Testzwecke (Mapping auf Ihre Tasten)
    //    var continuousActions = actionsOut.ContinuousActions;
    //    continuousActions[0] = Input.GetAxis("Vertical");   // W/S
    //    continuousActions[1] = Input.GetAxis("Horizontal"); // A/D

    //    float fork = 0f;
    //    if (Input.GetKey(KeyCode.UpArrow)) fork = 1f;
    //    else if (Input.GetKey(KeyCode.DownArrow)) fork = -1f;
    //    continuousActions[2] = fork;
    //}

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
        currentSearchState = SearchState.Pallet;
        FindClosestPallet();
    }


    



    // Hilfsfunktionen
    private Vector3 GetCurrentTargetPosition()
    {
        if (IsCarryingPallet) return dropZoneTransform.position;
        if (currentTargetPallet == null) FindClosestPallet();
        return currentTargetPallet != null ? currentTargetPallet.transform.position : transform.position;
    }

    public void FindClosestPallet()
    {

        List<GameObject> allPallets = new List<GameObject>();

        foreach (Transform child in palletParent.transform)
        {
            allPallets.Add(child.gameObject);
        }

        GameObject closest = null;
        float minDst = Mathf.Infinity;
        foreach (GameObject p in allPallets)
        {
            float d = Vector3.Distance(transform.position, p.transform.position);
            if (d < minDst) { minDst = d; closest = p; }
        }
        currentTargetPallet = closest;
    }


}
