using System.Collections.Generic;
using UnityEngine;

public class DropZoneManager : MonoBehaviour
{
    public List<GameObject> palletsInZone = new List<GameObject>();
    public int GetCount() => palletsInZone.Count;

    public bool IsComplete(int totalExpected) => palletsInZone.Count >= totalExpected;

    public bool IsAgentInDropZone(Vector3 position)
    {
        Collider zoneCollider = GetComponent<Collider>();
        return zoneCollider.bounds.Contains(position);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("pallet"))
        {
            GameObject pallet = other.gameObject;
            if (!palletsInZone.Contains(pallet))
            {
                palletsInZone.Add(pallet);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("pallet"))
        {
            GameObject pallet = other.gameObject;
            if (palletsInZone.Contains(pallet))
            {
                palletsInZone.Remove(pallet);
                // WICHTIG: Die Strafe wird NICHT hier, sondern im AgentController gegeben!
            }
        }
    }
}
