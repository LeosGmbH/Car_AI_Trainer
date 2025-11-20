using System;
using UnityEngine;
using UnityEngine.InputSystem;

    [RequireComponent(typeof (NewCarController))]
public class NewCarUserControl : MonoBehaviour
    {
        private NewCarController m_Car; // the car controller we want to use
        private InputAction moveAction;
        private InputAction handbrakeAction;

        private void Awake()
        {
            // get the car controller
            m_Car = GetComponent<NewCarController>();
            EnsureInputActions();
        }

        private void EnsureInputActions()
        {
            if (moveAction != null)
            {
                return;
            }

            moveAction = new InputAction("CarMove", InputActionType.Value);
            moveAction.AddBinding("<Gamepad>/leftStick");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            handbrakeAction = new InputAction("Handbrake", InputActionType.Value);
            handbrakeAction.AddBinding("<Keyboard>/space");
            handbrakeAction.AddBinding("<Gamepad>/buttonSouth");
            handbrakeAction.AddBinding("<Keyboard>/leftShift");
        }

        private void OnEnable()
        {
            EnsureInputActions();
            moveAction?.Enable();
            handbrakeAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            handbrakeAction?.Disable();
        }

        private void OnDestroy()
        {
            moveAction?.Disable();
            moveAction?.Dispose();
            moveAction = null;

            handbrakeAction?.Disable();
            handbrakeAction?.Dispose();
            handbrakeAction = null;
        }

        private void FixedUpdate()
        {
            var moveValue = moveAction?.ReadValue<Vector2>() ?? Vector2.zero;
            float h = moveValue.x;
            float v = moveValue.y;
            float handbrake = handbrakeAction != null ? handbrakeAction.ReadValue<float>() : 0f;

            m_Car.Move(h, v, v, handbrake);
        }
    }

