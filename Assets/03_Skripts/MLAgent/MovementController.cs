using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Skripts
{
    public class MovementController : MonoBehaviour
    {
        [SerializeField] private string pallet = "pallet";
        [SerializeField] private string dropZone = "dropZone";



        // Component references
        private Rigidbody rb;
        private MLAgentController movementMLAgent;


        // In Ihrem ForkliftController.cs
        public float MoveInput { get; private set; }
        public float SteerInput { get; private set; }
        public float ForkInput { get; private set; }
        private void Awake()
        {
            movementMLAgent = GetComponent<MLAgentController>();
            rb = GetComponent<Rigidbody>();

            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }


        public void SetInput(float move, float steer, float fork)
        {
            MoveInput = move;
            SteerInput = steer;
            ForkInput = fork;
        }


    }
}