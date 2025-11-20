using Assets.Skripts;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class MLAgentController : Agent
{
    private enum ForkLiftState
    {
        Searching,
        Holding,
    }
    private MovementController playerMovement;
    private Rigidbody rb;
    private RayPerceptionSensorComponent3D[] raySensors;

    [Header("Level Settings")]
    private Transform targetTransform;


    private Vector3 startPos;

   void Start()
    {
        startPos = transform.localPosition;
    }
    
    private void Awake()
    {
        raySensors = GetComponents<RayPerceptionSensorComponent3D>(); // Holt ALLE Ray Perception Sensoren im GameObject
        rb = GetComponent<Rigidbody>();
        playerMovement = GetComponent<MovementController>();
        //levelManager = FindAnyObjectByType<LevelManager>();
        playerMovement.SetControlMode(false);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 toTarget = targetTransform.localPosition - transform.localPosition;
        sensor.AddObservation(toTarget.normalized);  // Richtung (3)
        sensor.AddObservation(Mathf.Clamp(toTarget.magnitude / 50f, 0f, 1f));  // Skalierte Entfernung (1)
        sensor.AddObservation(playerMovement.isGrounded ? 1f : 0f);  // More reliable than single raycast (1 value)
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.x / 8f, -1f, 1f));
        sensor.AddObservation(Mathf.Clamp(rb.linearVelocity.z / 8f, -1f, 1f));
        sensor.AddObservation(rb.linearVelocity.y);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        bool jump = actions.DiscreteActions[0] == 1; // 1 for jump, 0 for no jump
        bool run = actions.DiscreteActions[1] == 1; // 1 for jump, 0 for no jump
        playerMovement.SetMovement(moveX, moveZ, run, jump);

        if (StepCount >= MaxStep)
        {
            Die();
            Debug.Log("Died, Steps �berschritten");
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        ActionSegment<int> discreteActions = actionsOut.DiscreteActions;

        continuousActions[0] = Input.GetAxisRaw("Horizontal");
        continuousActions[1] = Input.GetAxisRaw("Vertical");
        discreteActions[0] = Input.GetKey(KeyCode.Space) ? 1 : 0;
        discreteActions[1] = Input.GetKey(KeyCode.LeftShift) ? 1 : 0;

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
        playerMovement.playerColor.color = Color.green;

    }


    public override void OnEpisodeBegin()
    {

        Academy.Instance.StatsRecorder.Add("WinDeathRatio/EpisodesCount", 1, StatAggregationMethod.Sum);
       
        transform.localPosition = startPos;
       // transform.Rotate(0f, Random.Range(0f, 360f), 0f);
        playerMovement.h3dMoveReward = 0f;
        playerMovement.wallReward = 0f;
    }

    public void SetTargetTransformObject(Transform targetTransformObject)
    {
        targetTransform = targetTransformObject;
    }

  

}
