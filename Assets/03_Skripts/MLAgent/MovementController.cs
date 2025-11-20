using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Skripts
{
    public class MovementController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 10f;
        [SerializeField] private float runSpeed = 15f;
        [SerializeField] private float rotationSpeed = 20f;
        [SerializeField] private float jumpForce = 30f;
        [SerializeField] private float fallMultiplier = 10f;
        [SerializeField] private float groundCheckDistance = 0.1f;
        [SerializeField] private LayerMask groundLayer;


        [Header("Map Edge Settings")]
        [SerializeField] private string wall = "wall";
        [SerializeField] private string killPlayerTag = "killPlayer";



        // Component references
        private Rigidbody rb;
        private SphereCollider sphereCollider;
        private MLAgentController movementMLAgent;

        [Header("State variables")]
        private bool isRunning;
        private float horizontalInput;
        private float verticalInput;
        private bool jumpInput;
        private bool manualControl = true;

        public bool isGrounded;
        public Material playerColor;
        public float h3dMoveReward;
        public float wallReward;

        private void Awake()
        {
            h3dMoveReward = 0f;
            wallReward = 0f;
            playerColor = GetComponent<MeshRenderer>().material;
            movementMLAgent = GetComponent<MLAgentController>();
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();

            
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        private void FixedUpdate()
        {
            CheckGrounded();
            Handle3DMovement();
            HandleJump();
            ApplyGravityModifier();

            jumpInput = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.CompareTag(wall))
            {
                float impactForce = collision.impulse.magnitude;
                movementMLAgent.AddAgentReward(-1f * Mathf.Clamp(impactForce, 0.5f, 3f));
                wallReward += -5f * Mathf.Clamp(impactForce, 0.5f, 3f);
                Debug.Log("WallHit");
                playerColor.color = Color.yellow;
            }
            if (collision.gameObject.CompareTag(killPlayerTag))
            {
                Debug.Log("Died");
                movementMLAgent.Die();
            }
        }

        private void CheckGrounded()
        {
            Bounds bounds = sphereCollider.bounds;
            float extents = bounds.extents.y;
            Vector3 castOrigin = bounds.center;

            // Check isGrounded mit Ray
            isGrounded = Physics.Raycast(castOrigin, Vector3.down, extents + groundCheckDistance, groundLayer);
        }

        private void Handle3DMovement()
        {
            // movementspeed �ndern je nach dem ob man rennt oder nicht
            float currentSpeed = isRunning ? runSpeed : walkSpeed;

            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                float rotationAmount = horizontalInput * currentSpeed * Time.fixedDeltaTime * rotationSpeed;
                transform.Rotate(0f, rotationAmount, 0f);
            }

            if (Mathf.Abs(verticalInput) > 0.1f)
            {
                Vector3 moveDirection = transform.forward * verticalInput;
                Vector3 targetVelocity = moveDirection * currentSpeed;

                targetVelocity.y = rb.linearVelocity.y;

                rb.linearVelocity = targetVelocity;
            }
            else
            {
                //kein vertical input, aber verticale velocity beibehalten
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            if (Mathf.Abs(verticalInput) > 0.1f && isGrounded)
            {
                //forward movement wird mehr belohnt als backward movement
                float movementReward = 0.02f * Mathf.Clamp(verticalInput, 0f, 1f);
                movementMLAgent.AddAgentReward(movementReward);
                h3dMoveReward += movementReward; // f�r tensorboard
            }
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                movementMLAgent.AddAgentReward(-0.025f); // Kleine Strafe f�r Drehung
                h3dMoveReward += -0.025f; // f�r tensorboard
            }

        }

        private void HandleJump()
        {
            if (!jumpInput) return;

            if (isGrounded)
            {
                movementMLAgent.AddAgentReward(-2.5f);
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            }
        }

        private void ApplyGravityModifier()
        {
            if (rb.linearVelocity.y < 0)
            {
                rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
            }
        }

        public void SetMovement(float horizontalMovement, float verticalMovement, bool shouldRun, bool shouldJump) //ML Agent Movement Methode
        {
            manualControl = false;
            horizontalInput = Mathf.Clamp(horizontalMovement, -1f, 1f); //left/right movement
            verticalInput = Mathf.Clamp(verticalMovement, -1f, 1f); //forward/backward movement
            isRunning = shouldRun; 
            jumpInput = shouldJump;
        }
       
        public void SetControlMode(bool useManualControl)
        {
            manualControl = useManualControl;

            //reset inputs if switching to manual 
            if (manualControl)
            {
                horizontalInput = 0f;
                verticalInput = 0f;
                isRunning = false;
                jumpInput = false;
            }
        }
        public bool IsGrounded()
        {
            return isGrounded;
        }
        public Vector3 GetVelocity()
        {
            return rb.linearVelocity;
        }
    }
}