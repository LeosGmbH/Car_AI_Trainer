# Agent_4 fliegt aus der Map - Diagnose

## Mögliche Ursachen:

### 1. **Physics Layer Collision Matrix** ⚠️ WAHRSCHEINLICHSTE URSACHE
Agent_4 (Layer `Agent_4`) kollidiert möglicherweise mit sich selbst oder anderen Objekten falsch.

**Zu prüfen in Unity:**
1. Edit → Project Settings → Physics
2. Scrolle runter zur "Layer Collision Matrix"
3. Prüfe für `Agent_4`:
   - ✅ Sollte kollidieren mit: `Default`, `Wall`, `Ground`
   - ❌ Sollte NICHT kollidieren mit: `Agent_1`, `Agent_2`, `Agent_3`, `Agent_4` (sich selbst!)
   
**Symptom:** Wenn Agent_4 mit sich selbst kollidiert, können die Wheel Colliders "explodieren" und den Agent wegschleudern.

### 2. **Rigidbody Constraints**
Prüfe im Agent_4 Prefab:
- Rigidbody → Constraints → Freeze Rotation: X ✓, Z ✓ (Y sollte frei sein)
- Mass: Sollte gleich sein wie bei anderen Agents
- Drag: Sollte gleich sein
- Angular Drag: Sollte gleich sein

### 3. **Wheel Collider Setup**
Prüfe alle 4 Wheel Colliders von Agent_4:
- Sind alle Wheel Colliders korrekt zugewiesen?
- Haben alle die gleichen Werte wie Agent_1, Agent_2, Agent_3?
- Spring/Damper/Target Position korrekt?

### 4. **NewCarController Handbrake Issue**
Die NullReferenceException in `NewCarController.ApplyHandbrake` (Line 211) könnte Agent_4 destabilisieren.

**Zu prüfen:**
```csharp
// In NewCarController.cs Line 211
// Wahrscheinlich fehlt ein Null-Check für ein Wheel Collider Array
```

## Schnelltest:

1. **Deaktiviere Agent_4 temporär** im EvolutionManager (setze `agentCount = 3`)
2. Wenn das Problem verschwindet → Es ist ein Agent_4-spezifisches Problem
3. Wenn es zu einem anderen Agent wechselt → Es ist ein allgemeines Physics-Problem

## Fix für OnEpisodeBegin NullReference:

Füge am Anfang von `OnEpisodeBegin()` hinzu:
```csharp
public override void OnEpisodeBegin()
{
    // Safety check
    if (perceptionHelper == null || rewardHandler == null || enviromentController == null)
    {
        return; // Skip if not initialized yet
    }
    
    // ... rest of code
}
```

## Empfohlene Reihenfolge zum Debuggen:

1. ✅ Prüfe Physics Layer Collision Matrix für Agent_4
2. ✅ Vergleiche Rigidbody Settings von Agent_1 vs Agent_4
3. ✅ Prüfe Wheel Collider Assignments
4. ✅ Füge Debug.Log in NewCarController.ApplyHandbrake hinzu um zu sehen welcher Agent das Problem hat
