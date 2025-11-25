using UnityEngine;

public class CollisionMatrixSetup : MonoBehaviour
{
    [ContextMenu("Setup Collision Matrix")]
    public void SetupMatrix()
    {
        for (int i = 1; i <= 20; i++)
        {
            int agentLayer = LayerMask.NameToLayer($"Agent_{i}");
            if (agentLayer == -1)
            {
                Debug.LogWarning($"[CollisionMatrixSetup] Layer 'Agent_{i}' not found. Please create it in Project Settings > Tags and Layers.");
                continue;
            }
            
            // Get common layers
            int groundLayer = LayerMask.NameToLayer("Ground");
            int wallLayer = LayerMask.NameToLayer("Wall");
            int dropZoneLayer = LayerMask.NameToLayer("DropZone");
            
            // Enable collision with self, ground, walls, dropzone
            Physics.IgnoreLayerCollision(agentLayer, agentLayer, false);
            if (groundLayer != -1) Physics.IgnoreLayerCollision(agentLayer, groundLayer, false);
            if (wallLayer != -1) Physics.IgnoreLayerCollision(agentLayer, wallLayer, false);
            if (dropZoneLayer != -1) Physics.IgnoreLayerCollision(agentLayer, dropZoneLayer, false);
            
            // Disable collision with other agents
            for (int j = 1; j <= 20; j++)
            {
                if (i == j) continue;
                int otherLayer = LayerMask.NameToLayer($"Agent_{j}");
                if (otherLayer != -1)
                {
                    Physics.IgnoreLayerCollision(agentLayer, otherLayer, true);
                }
            }
        }
        
        Debug.Log("[CollisionMatrixSetup] Collision matrix configured for 20 agent layers!");
    }
}
