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
        private FitnessTracker fitnessTracker;

        // In Ihrem ForkliftController.cs
        public float MoveInput { get; private set; }
        public float SteerInput { get; private set; }
        public float ForkInput { get; private set; }

        public float HandbrakeInput { get; private set; }

        private void Awake()
        {
            carController = GetComponent<NewCarController>();
            forkController = GetComponent<ForkController>();
            fitnessTracker = GetComponent<FitnessTracker>();
        }

        private void FixedUpdate()
        {
            ApplyCarInput();
            ApplyForkInput();
        }

        public void SetInput(float move, float steer, float fork, float handbrake)
        {
            MoveInput = Mathf.Clamp(move, -1f, 1f);
            SteerInput = Mathf.Clamp(-steer, -1f, 1f);
            ForkInput = Mathf.Clamp(fork, -1f, 1f);
            HandbrakeInput = Mathf.Clamp(handbrake, 0f, 1f);
        }

        private void ApplyCarInput()
        {
            // Don't move if agent is done
            if (fitnessTracker != null && fitnessTracker.IsDone) return;

            float accel = Mathf.Max(0f, MoveInput);
            float footbrake = Mathf.Min(0f, MoveInput);

            carController.Move(SteerInput, accel, footbrake, HandbrakeInput);
        }

        public Dictionary<string, string> GetDebugInformations()
        {
            float accel = Mathf.Max(0f, MoveInput);
            float footbrake = Mathf.Abs(Mathf.Min(0f, MoveInput)); // Absoluter Wert

            return new Dictionary<string, string>
            {
                // --- ML AGENT INPUTS ---
                { "--- ML INPUTS ---", "" },
                { "Move Input", MoveInput.ToString("F2") },
                { "Steer Input", SteerInput.ToString("F2") },
                { "Fork Input", ForkInput.ToString("F2") },
                { "Handbrake Input", HandbrakeInput.ToString("F2") },
                
                // --- BERECHNETE AKTIONEN ---
                { "--- AUTO AKTIONEN ---", "" },
                { "Accel", accel.ToString("F2") },
                { "Foot Brake", footbrake.ToString("F2") },
                { "Hand Brake (An)", (HandbrakeInput > 0.5f).ToString() }
            };
        }

        private void ApplyForkInput()
        {
            // Don't move fork if agent is done
            if (fitnessTracker != null && fitnessTracker.IsDone) return;

            forkController.SetForkInput(ForkInput);
        }
    }
}