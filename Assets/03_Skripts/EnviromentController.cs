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

    public void ResetObjectPositions(Transform dropZoneTransform = null)
    {
        if (originalPositions.Count == 0)
        {
            Debug.LogWarning("EnvironmentController: Es wurden keine ursprünglichen Positionen gespeichert. Stelle sicher, dass palletParent zugewiesen ist und Kindobjekte hat.");
            return;
        }

        if (levelSettings == LevelSettings.Level03)
        {
            ResetLevel03(dropZoneTransform);
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

    private void ResetLevel03(Transform dropZoneTransform)
    {
        if (dropZoneTransform == null)
        {
            Debug.LogError("EnvironmentController: DropZoneTransform ist null in Level03!");
            return;
        }

        // 1. Dropzone platzieren
        // Dropzone wird zufällig zwischen z und x = 5 bis 13 oder -5 bis -13 platziert.
        float dropX = GetRandomCoordinate(5f, 13f);
        float dropZ = GetRandomCoordinate(5f, 13f);
        dropZoneTransform.localPosition = new Vector3(dropX, dropZoneTransform.localPosition.y, dropZ);

        // 2. Paletten platzieren
        List<Vector3> placedPositions = new List<Vector3>();
        
        foreach (var entry in originalPositions)
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
                // Die pallets werden zufällig irgendwo bei 4 bis 14 oder -4 bis -14 xz platziert...
                float pX = GetRandomCoordinate(4f, 14f);
                float pZ = GetRandomCoordinate(4f, 14f);
                newPos = new Vector3(pX, entry.Value.y, pZ); // Y von original behalten

                // Constraint 1: Abstand zur Dropzone (x und z >= 6)
                // "d.h. wenn dropzone bei 0.0.0 ist, dann darf pallet nicht näher ran als 6.0.0"
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
                Debug.LogWarning($"EnvironmentController: Konnte keine valide Position für Palette {pallet.name} finden nach {maxAttempts} Versuchen.");
                pallet.localPosition = entry.Value;
            }
        }
    }

    private float GetRandomCoordinate(float minAbs, float maxAbs)
    {
        // Zufällig positiv oder negativ
        float val = Random.Range(minAbs, maxAbs);
        return Random.value > 0.5f ? val : -val;
    }
}
