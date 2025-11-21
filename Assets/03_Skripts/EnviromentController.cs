using System.Collections.Generic;
using UnityEngine;

public class EnviromentController : MonoBehaviour
{
    public GameObject palletParent;

    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();

    void Start()
    {
        StoreOriginalPositions();
    }

    private void StoreOriginalPositions()
    {

        originalPositions.Clear(); // Sicherstellen, dass das Dictionary leer ist.

        foreach (Transform child in palletParent.transform)
        {
            originalPositions.Add(child, child.localPosition);

        }

        Debug.Log($"EnvironmentController: {originalPositions.Count} ursprüngliche Positionen gespeichert.");
    }

    public void ResetObjectPositions()
    {
        if (originalPositions.Count == 0)
        {
            Debug.LogWarning("EnvironmentController: Es wurden keine ursprünglichen Positionen gespeichert. Stelle sicher, dass palletParent zugewiesen ist und Kindobjekte hat.");
            return;
        }

        int resetCount = 0;
        foreach (var entry in originalPositions)
        {
            Transform childTransform = entry.Key;
            Vector3 originalLocalPosition = entry.Value;

            if (childTransform != null)
            {
                // Setzt die lokale Position zurück.
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

        Debug.Log($"EnvironmentController: {resetCount} Objektpositionen wurden zurückgesetzt.");
    }
}
