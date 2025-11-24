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

    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        propBlock = new MaterialPropertyBlock();
        if (renderersToColor == null || renderersToColor.Length == 0)
        {
            renderersToColor = GetComponentsInChildren<Renderer>();
        }
    }

    public void AddFitness(float amount)
    {
        if (!isAlive) return;
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
        SetColor(normalColor);
        gameObject.SetActive(true);
    }

    public void Die()
    {
        isAlive = false;
        SetColor(deadColor);
        // Optional: Disable GameObject or just mark as dead
        // gameObject.SetActive(false); 
        // For now, we keep them active but visually "dead" so they don't interfere physically if we wanted, 
        // but usually we want to stop them. Let's rely on the Agent to handle "EndEpisode" or similar if needed.
        // But for pure evolution logic, "Die" might just mean "Excluded from next gen selection".
        // If we want them to stop moving, the Agent script needs to know.
        // For this implementation, we'll just visualy mark them. The EvolutionManager might reset positions anyway.
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
