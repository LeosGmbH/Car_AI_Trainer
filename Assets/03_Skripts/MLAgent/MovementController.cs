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

        private NewCarController carController;
        private ForkController forkController;

        // In Ihrem ForkliftController.cs
        public float MoveInput { get; private set; }
        public float SteerInput { get; private set; }
        public float ForkInput { get; private set; }

        private void Awake()
        {
            carController = GetComponent<NewCarController>();
            forkController = GetComponent<ForkController>();
        }

        private void FixedUpdate()
        {
            ApplyCarInput();
            ApplyForkInput();
        }

        public void SetInput(float move, float steer, float fork)
        {
            MoveInput = Mathf.Clamp(move, -1f, 1f);
            SteerInput = Mathf.Clamp(steer, -1f, 1f);
            ForkInput = Mathf.Clamp(fork, -1f, 1f);
        }

        private void ApplyCarInput()
        {
            float accel = Mathf.Max(0f, MoveInput);
            float footbrake = Mathf.Min(0f, MoveInput);

            carController.Move(SteerInput, accel, footbrake, 0f);
        }

        private void ApplyForkInput()
        {
            forkController.SetForkInput(ForkInput);
        }
    }
}