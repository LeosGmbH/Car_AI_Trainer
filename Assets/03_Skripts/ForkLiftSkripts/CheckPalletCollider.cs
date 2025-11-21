using UnityEngine;

public class CheckPalletCollider : MonoBehaviour
{
    private bool isTouchingPallet = false;
    public MLAgentController mlAgentController;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("pallet"))
        {
            mlAgentController.IsCarryingPallet = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("pallet"))
        {
            mlAgentController.FindClosestPallet();
            mlAgentController.IsCarryingPallet = false;
        }

    }

    
}
