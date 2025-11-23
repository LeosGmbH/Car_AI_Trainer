using System.Collections.Generic;
using UnityEngine;
using Assets.Skripts;

public class MLAgentPerceptionHelper
{
    private Transform agentTransform;
    private GameObject palletParent;
    private DropZoneManager dropZoneManager;

    public Transform[] ClosestPallets { get; private set; } = new Transform[5];

    public MLAgentPerceptionHelper(Transform agentTransform, GameObject palletParent, DropZoneManager dropZoneManager)
    {
        this.agentTransform = agentTransform;
        this.palletParent = palletParent;
        this.dropZoneManager = dropZoneManager;
    }

    public void FindClosestPallets()
    {
        // Reset array
        for (int i = 0; i < ClosestPallets.Length; i++)
        {
            ClosestPallets[i] = agentTransform;
        }

        List<Transform> unsecuredPallets = new List<Transform>();
        int childCount = palletParent.transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = palletParent.transform.GetChild(i);
            if (!dropZoneManager.palletsInZone.Contains(child.gameObject))
            {
                unsecuredPallets.Add(child);
            }
        }

        unsecuredPallets.Sort((a, b) =>
            ((a.position - agentTransform.position).sqrMagnitude)
                .CompareTo((b.position - agentTransform.position).sqrMagnitude));

        int limit = Mathf.Min(ClosestPallets.Length, unsecuredPallets.Count);
        for (int i = 0; i < limit; i++)
        {
            ClosestPallets[i] = unsecuredPallets[i];
        }
    }

    public int GetTotalPalletCount()
    {
        return palletParent.transform.childCount;
    }
}
