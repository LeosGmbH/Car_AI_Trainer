# Unity Scene Setup Guide - 30 Ghost Agents

Diese Anleitung zeigt dir, wie du deine Unity-Szene umstrukturierst, um 30 Agenten in einem Level zu haben.

## Schritt 1: Physics Layers erstellen

1. Öffne `Edit > Project Settings > Tags and Layers`
2. Erstelle folgende Layer (Layer 8-37):
   - Layer 8: `Agent_0`
   - Layer 9: `Agent_1`
   - Layer 10: `Agent_2`
   - ... (bis)
   - Layer 37: `Agent_29`
3. Erstelle auch:
   - Layer 6: `Walls` (falls noch nicht vorhanden)

**Tipp:** Das ist mühsam. Du kannst auch ein Script nutzen, das die Layer automatisch benennt (siehe unten).

---

## Schritt 2: Collision Matrix konfigurieren

1. Öffne `Edit > Project Settings > Physics`
2. Scrolle runter zur **Layer Collision Matrix**
3. Für **jeden** Agent-Layer (Agent_0 bis Agent_29):
   - ✅ Aktiviere Collision mit **sich selbst** (z.B. Agent_0 ↔ Agent_0)
   - ✅ Aktiviere Collision mit **Walls**
   - ❌ Deaktiviere Collision mit **allen anderen Agent-Layern**

**Beispiel für Agent_0:**
```
Agent_0 collides with:
✅ Agent_0
✅ Walls
❌ Agent_1, Agent_2, ..., Agent_29
```

---

## Schritt 3: Scene Hierarchy aufbauen

### Alte Struktur (30 separate Levels):
```
Scene
├── Level_0
│   ├── Agent
│   ├── Pallets
│   └── EvolutionManager
├── Level_1
│   └── ...
```

### Neue Struktur (1 Level, 30 Agenten):
```
Scene
├── EvolutionManager (nur 1x!)
├── Walls (Layer: Walls)
├── DropZone
│
├── Agent_0 (Layer: Agent_0)
│   ├── MLAgentController (agentIndex = 0)
│   ├── FitnessTracker
│   ├── EnviromentController (palletParent = Pallets_Agent_0)
│   └── ... (Forklift Modell)
│
├── Pallets_Agent_0 (Layer: Agent_0)
│   ├── Pallet_1
│   ├── Pallet_2
│   └── Pallet_3
│
├── Agent_1 (Layer: Agent_1)
│   ├── MLAgentController (agentIndex = 1)
│   └── ...
│
├── Pallets_Agent_1 (Layer: Agent_1)
│   └── ...
│
└── ... (bis Agent_29)
```

---

## Schritt 4: Agent GameObjects konfigurieren

Für **jeden** Agenten (0-29):

1. **Agent GameObject:**
   - Name: `Agent_0` (oder `Agent_1`, etc.)
   - Layer: `Agent_0` (wird automatisch vom Script gesetzt)
   - Position: Verteile sie in der Szene (z.B. Grid-Layout)

2. **MLAgentController Component:**
   - `Agent Index`: Setze auf `0` (für Agent_0), `1` (für Agent_1), etc.
   - `Pallet Parent`: Leer lassen (wird automatisch gefunden als `Pallets_Agent_0`)
   - `Drop Zone Manager`: Referenz zur DropZone
   - `Drop Zone Transform`: Referenz zur DropZone Transform

3. **EnviromentController Component:**
   - `Pallet Parent`: Referenz zu `Pallets_Agent_0` (für Agent_0)

4. **FitnessTracker Component:**
   - `Renderers To Color`: Ziehe die MeshRenderer des Autos rein

---

## Schritt 5: Pallet GameObjects erstellen

Für **jeden** Agenten:

1. Erstelle ein leeres GameObject: `Pallets_Agent_0`
2. Layer: `Agent_0` (wichtig!)
3. Füge 3 Pallet-Prefabs als Kinder hinzu
4. Setze Layer **aller** Paletten auf `Agent_0`

**Wichtig:** Jeder Agent braucht seine eigenen Paletten-Instanzen!

---

## Schritt 6: Walls konfigurieren

1. Wähle alle Wand-GameObjects
2. Setze Layer auf `Walls`

---

## Schritt 7: EvolutionManager

1. Erstelle **ein** leeres GameObject: `EvolutionManager`
2. Füge das `EvolutionManager.cs` Script hinzu
3. Einstellungen:
   - `Generation Mode`: `Survival` (oder `TimeBased`)
   - `Survival Rate`: `0.2` (Top 20%)
   - `Show GUI`: `true`

---

## Schritt 8: Testen

1. Starte die Szene
2. Prüfe Console:
   - `[EvolutionManager] Found 30 agents.`
   - Keine Fehler über fehlende Layer
3. Beobachte:
   - Agenten fahren durch einander (keine Collision)
   - Jeder Agent interagiert nur mit seinen Paletten
   - GUI zeigt "Survivors: X / 30"

---

## Bonus: Script zum automatischen Layer-Setup

Falls du nicht alle Layer manuell erstellen willst, kannst du dieses Script nutzen:

```csharp
// LayerSetupHelper.cs
using UnityEngine;
using UnityEditor;

public class LayerSetupHelper : MonoBehaviour
{
    [MenuItem("Tools/Setup Agent Layers")]
    static void SetupLayers()
    {
        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        
        SerializedProperty layers = tagManager.FindProperty("layers");
        
        for (int i = 0; i < 30; i++)
        {
            int layerIndex = 8 + i; // Start at layer 8
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
            layer.stringValue = $"Agent_{i}";
        }
        
        tagManager.ApplyModifiedProperties();
        Debug.Log("Created 30 agent layers (Agent_0 to Agent_29)");
    }
}
```

Speichere das in `Assets/Editor/LayerSetupHelper.cs` und klicke dann auf `Tools > Setup Agent Layers`.

---

## Troubleshooting

**Problem:** "Layer 'Agent_0' not found!"
- **Lösung:** Prüfe, dass du die Layer in `Project Settings > Tags and Layers` erstellt hast.

**Problem:** Agenten colliden miteinander
- **Lösung:** Prüfe die Collision Matrix in `Project Settings > Physics`.

**Problem:** Agent kann keine Paletten finden
- **Lösung:** Prüfe, dass `Pallets_Agent_X` existiert und der richtige Layer gesetzt ist.

**Problem:** "Found 0 agents" im EvolutionManager
- **Lösung:** Stelle sicher, dass jeder Agent ein `FitnessTracker` Component hat.
