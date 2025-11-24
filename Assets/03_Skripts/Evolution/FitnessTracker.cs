using UnityEngine;

public class FitnessTracker : MonoBehaviour, IEvolutionAgent
{
    [Header("Settings")]
    [SerializeField] private Renderer[] renderersToColor;
    [SerializeField] private Color eliteColor = Color.green;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color deadColor = Color.black;

    [Header("Debug")]
    [SerializeField] private float currentFitness = 0f;
    [SerializeField] private bool isAlive = true;
    public bool IsDone { get; private set; } = false;

    private MaterialPropertyBlock propBlock;
    private Rigidbody rb;
    private Collider[] colliders;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();

        if (renderersToColor == null || renderersToColor.Length == 0)
        {
            renderersToColor = GetComponentsInChildren<Renderer>();
        }
    }

    public void AddFitness(float amount)
    {
        if (!isAlive || IsDone) return;
        currentFitness += amount;
    }

    // --- IEvolutionAgent Implementation ---

    public float GetFitness()
    {
        return currentFitness;
    }

    public void ResetFitness()
    {
        currentFitness = 0f;
        isAlive = true;
        IsDone = false;
        SetColor(normalColor);
        
        // Re-enable physics/visuals
        SetVisualsAndPhysics(true);
        gameObject.SetActive(true);
    }

    public void MarkAsDone()
    {
        if (IsDone) return;
        IsDone = true;
        
        // Notify Manager
        var manager = FindFirstObjectByType<EvolutionManager>();
        if (manager != null) manager.NotifyAgentDone(this);

        // Disable physics/visuals to "hide" agent until next gen
        SetVisualsAndPhysics(false);
    }

    private void SetVisualsAndPhysics(bool state)
    {
        // Visuals
        foreach (var r in renderersToColor) r.enabled = state;
        
        // Physics
        if (rb != null)
        {
            if (!state && !rb.isKinematic) 
            {
                rb.linearVelocity = Vector3.zero;
            }
            rb.isKinematic = !state;
        }
        
        // Colliders - but NOT WheelColliders (they are needed by NewCarController)
        foreach (var c in colliders)
        {
            if (c is WheelCollider) continue; // Skip WheelColliders
            c.enabled = state;
        }
    }

    public void Die()
    {
        isAlive = false;
        SetColor(deadColor);
    }

    public void OnSurvive()
    {
        SetColor(eliteColor);
    }

    private void SetColor(Color c)
    {
        if (renderersToColor == null) return;

        foreach (var r in renderersToColor)
        {
            r.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", c);
            r.SetPropertyBlock(propBlock);
        }
    }
}
