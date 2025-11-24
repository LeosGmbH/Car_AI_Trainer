using System.Collections.Generic;
using UnityEngine;

public class EnviromentController : MonoBehaviour
{
    private enum LevelSettings
    {
        None,
        Level03
    }
    [SerializeField] private LevelSettings levelSettings;

    [Header("Multi-Agent Setup")]
    [Tooltip("Pallet parent objects for each agent (auto-found if empty)")]
    public GameObject[] palletParents;

    private Dictionary<int, Dictionary<Transform, Vector3>> originalPositionsByAgent = new Dictionary<int, Dictionary<Transform, Vector3>>();

    void Start()
    {
        // Get agent count from EvolutionManager
        var evolutionManager = FindFirstObjectByType<EvolutionManager>();
        int agentCount = 4; // Default
        if (evolutionManager != null)
        {
            agentCount = evolutionManager.GetAgentCount();
        }
        else
        {
            Debug.LogWarning("[EnviromentController] EvolutionManager not found, using default agent count of 4");
        }

        // Auto-find pallet parents if not assigned
        if (palletParents == null || palletParents.Length == 0)
        {
            palletParents = new GameObject[agentCount];
            for (int i = 0; i < agentCount; i++)
            {
                palletParents[i] = GameObject.Find($"Pallets ({i + 1})");
                if (palletParents[i] == null)
                {
                    Debug.LogWarning($"[EnviromentController] Could not find 'Pallets ({i + 1})'");
                }
            }
        }
        
        StoreOriginalPositions();
    }

    private void StoreOriginalPositions()
    {
        originalPositionsByAgent.Clear();

        for (int agentIndex = 0; agentIndex < palletParents.Length; agentIndex++)
        {
            GameObject palletParent = palletParents[agentIndex];
            if (palletParent == null) continue;

            Dictionary<Transform, Vector3> positions = new Dictionary<Transform, Vector3>();
            
            foreach (Transform child in palletParent.transform)
            {
                positions.Add(child, child.localPosition);
            }

            originalPositionsByAgent[agentIndex] = positions;
            Debug.Log($"[EnviromentController] Agent {agentIndex + 1}: {positions.Count} pallet positions stored.");
        }
    }

    public void ResetObjectPositions(Transform dropZoneTransform = null, int agentIndex = -1)
    {
        // If agentIndex is specified, reset only that agent's pallets
        if (agentIndex >= 0 && agentIndex < palletParents.Length)
        {
            ResetAgentPallets(agentIndex, dropZoneTransform);
        }
        else
        {
            // Reset all agents
            for (int i = 0; i < palletParents.Length; i++)
            {
                ResetAgentPallets(i, dropZoneTransform);
            }
        }
    }

    private void ResetAgentPallets(int agentIndex, Transform dropZoneTransform)
    {
        if (!originalPositionsByAgent.ContainsKey(agentIndex))
        {
            Debug.LogWarning($"[EnviromentController] No stored positions for agent {agentIndex + 1}");
            return;
        }

        if (levelSettings == LevelSettings.Level03)
        {
            ResetLevel03(dropZoneTransform, agentIndex);
            return;
        }

        var positions = originalPositionsByAgent[agentIndex];
        int resetCount = 0;

        foreach (var entry in positions)
        {
            Transform childTransform = entry.Key;
            Vector3 originalLocalPosition = entry.Value;

            if (childTransform != null)
            {
                childTransform.localPosition = originalLocalPosition;
                childTransform.localRotation = Quaternion.Euler(0f, 0f, 0f);

                Rigidbody rb = childTransform.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                resetCount++;
            }
        }
    }

    private void ResetLevel03(Transform dropZoneTransform, int agentIndex)
    {
        if (dropZoneTransform == null)
        {
            Debug.LogError("[EnviromentController] DropZoneTransform is null in Level03!");
            return;
        }

        if (!originalPositionsByAgent.ContainsKey(agentIndex))
        {
            return;
        }

        // 1. Dropzone platzieren
        float dropX = GetRandomCoordinate(5f, 13f);
        float dropZ = GetRandomCoordinate(5f, 13f);
        dropZoneTransform.localPosition = new Vector3(dropX, dropZoneTransform.localPosition.y, dropZ);

        // 2. Paletten platzieren
        List<Vector3> placedPositions = new List<Vector3>();
        var positions = originalPositionsByAgent[agentIndex];
        
        foreach (var entry in positions)
        {
            Transform pallet = entry.Key;
            if (pallet == null) continue;

            Vector3 newPos = Vector3.zero;
            bool validPositionFound = false;
            int attempts = 0;
            int maxAttempts = 100;

            while (!validPositionFound && attempts < maxAttempts)
            {
                attempts++;
                float pX = GetRandomCoordinate(4f, 14f);
                float pZ = GetRandomCoordinate(4f, 14f);
                newPos = new Vector3(pX, entry.Value.y, pZ);

                // Constraint 1: Abstand zur Dropzone
                bool insideDropZoneArea = (Mathf.Abs(newPos.x - dropZoneTransform.localPosition.x) < 6f) && 
                                          (Mathf.Abs(newPos.z - dropZoneTransform.localPosition.z) < 6f);

                if (insideDropZoneArea) continue;

                // Constraint 2: Paletten untereinander Mindestabstand 5
                bool tooCloseToOtherPallet = false;
                foreach (Vector3 existingPos in placedPositions)
                {
                    if (Vector3.Distance(newPos, existingPos) < 5f)
                    {
                        tooCloseToOtherPallet = true;
                        break;
                    }
                }

                if (tooCloseToOtherPallet) continue;

                validPositionFound = true;
            }

            if (validPositionFound)
            {
                pallet.localPosition = newPos;
                pallet.localRotation = Quaternion.Euler(0f, 0f, 0f);
                Rigidbody rb = pallet.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                placedPositions.Add(newPos);
            }
            else
            {
                Debug.LogWarning($"[EnviromentController] Could not find valid position for pallet {pallet.name} after {maxAttempts} attempts.");
                pallet.localPosition = entry.Value;
            }
        }
    }

    private float GetRandomCoordinate(float minAbs, float maxAbs)
    {
        float val = Random.Range(minAbs, maxAbs);
        return Random.value > 0.5f ? val : -val;
    }
}
