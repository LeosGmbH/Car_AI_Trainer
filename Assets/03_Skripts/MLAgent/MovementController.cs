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
        private MLAgentController movementMLAgent;


        // In Ihrem ForkliftController.cs
        public float MoveInput { get; private set; }
        public float SteerInput { get; private set; }
        public float ForkInput { get; private set; }
        private void Awake()
        {
            movementMLAgent = GetComponent<MLAgentController>();
        }


        public void SetInput(float move, float steer, float fork)
        {
            MoveInput = move;
            SteerInput = steer;
            ForkInput = fork;
        }


    }
}