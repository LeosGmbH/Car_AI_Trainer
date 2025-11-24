using System.Collections.Generic;
using UnityEngine;
using Assets.Skripts;

public class MLAgentPerceptionHelper
{
    private Transform agentTransform;
    private GameObject palletParent;
    private DropZoneManager dropZoneManager;

    public Transform TargetPallet { get; private set; }

    public MLAgentPerceptionHelper(Transform agentTransform, GameObject palletParent, DropZoneManager dropZoneManager)
    {
        this.agentTransform = agentTransform;
        this.palletParent = palletParent;
        this.dropZoneManager = dropZoneManager;
    }

    public void FindClosestPallets()
    {
        TargetPallet = null;
        float closestDistSqr = float.MaxValue;

        int childCount = palletParent.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = palletParent.transform.GetChild(i);
            
            // Ignore pallets already in the drop zone
            if (dropZoneManager.palletsInZone.Contains(child.gameObject))
                continue;

            float distSqr = (child.position - agentTransform.position).sqrMagnitude;
            if (distSqr < closestDistSqr)
            {
                closestDistSqr = distSqr;
                TargetPallet = child;
            }
        }
    }

    public int GetTotalPalletCount()
    {
        return palletParent.transform.childCount;
    }
}
