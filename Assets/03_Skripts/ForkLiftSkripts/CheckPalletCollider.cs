using UnityEngine;

public class CheckPalletCollider : MonoBehaviour
{
    [SerializeField] private float carryingHeightThreshold = 0.3f;

    public MLAgentController mlAgentController; // Im Inspector zuweisen!

    private void Update()
    {
        // 1. Zustand: Wurde die Palette erfolgreich gehoben?
        if (mlAgentController.IsPalletTouched)
        {
            // Setzt IsPalletLifted nur auf true, wenn berührt UND hoch genug
            bool isForkHighEnough = mlAgentController.forkTransform.localPosition.y > carryingHeightThreshold;
            mlAgentController.IsPalletLifted = isForkHighEnough;
        }
        else
        {
            // Wenn nicht berührt, kann auch nicht gehoben werden.
            mlAgentController.IsPalletLifted = false;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Setzt den Berührungsstatus
        if (other.CompareTag("Pallet") || other.CompareTag("pallet"))
        {
            mlAgentController.IsPalletTouched = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Berührungsstatus beendet
        if (other.CompareTag("Pallet") || other.CompareTag("pallet"))
        {
            mlAgentController.IsPalletTouched = false;
        }
    }

}
