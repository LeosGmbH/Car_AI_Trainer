# Runtime Agent Spawning - Setup Guide

## Schritt 1: Prefabs erstellen

### Agent Prefab
1. Erstelle **einen** Agenten in der Szene mit allen Components:
   - `MLAgentController`
   - `FitnessTracker`
   - `MovementController`
   - `NewCarController`
   - `ForkController`
   - Alle anderen benötigten Components
2. Ziehe ihn in den Project-Ordner → Prefab erstellen
3. **Lösche** den Agenten aus der Szene

### Pallet Prefab
1. Erstelle **eine** Palette in der Szene
2. Ziehe sie in den Project-Ordner → Prefab erstellen
3. **Lösche** die Palette aus der Szene

---

## Schritt 2: Spawn-Positionen erstellen

1. Erstelle ein leeres GameObject: `AgentSpawnPos`
   - Position: Wo der Agent spawnen soll (z.B. 0, 0, 0)
2. Erstelle ein leeres GameObject: `PalletSpawnPos`
   - Position: Wo das **erste** Pallet spawnen soll (z.B. 2, 0, 0)

---

## Schritt 3: EvolutionManager konfigurieren

Wähle das `EvolutionManager` GameObject aus:

**Agent Spawning:**
- `Agent Prefab`: Ziehe dein Agent-Prefab rein
- `Pallet Prefab`: Ziehe dein Pallet-Prefab rein
- `Agent Count`: 4 (Slider 1-20)
- `Pallets Per Agent`: 3
- `Agent Spawn Pos`: Ziehe `AgentSpawnPos` GameObject rein
- `Pallet Spawn Pos`: Ziehe `PalletSpawnPos` GameObject rein

---

## Schritt 4: Collision Matrix konfigurieren

1. Wähle das `EvolutionManager` GameObject aus
2. Füge das `CollisionMatrixSetup` Component hinzu
3. Rechtsklick auf das Component → `Setup Collision Matrix`
4. Prüfe die Console: "Collision matrix configured for 20 agent layers!"
5. **Entferne** das Component wieder (wird nicht mehr gebraucht)

---

## Schritt 5: Testen

1. **Lösche alle Agenten und Paletten** aus der Szene
2. Stelle sicher, dass nur noch da sind:
   - EvolutionManager
   - EnviromentController
   - Walls
   - Ground
   - DropZone
   - AgentSpawnPos
   - PalletSpawnPos
3. **Starte das Spiel**
4. Prüfe Console:
   - `[EvolutionManager] Spawned 4 ghost agents with 3 pallets each`
   - `[EvolutionManager] Found 4 agents.`

---

## Scene Hierarchy (Final)

```
Scene
├── EvolutionManager
├── EnviromentController
├── Walls (Layer: Wall)
├── Ground (Layer: Ground)
├── DropZone (Layer: DropZone)
├── AgentSpawnPos
└── PalletSpawnPos
```

**Zur Laufzeit spawnen:**
```
Scene
├── ... (wie oben)
├── Agent_1 (Layer: Agent_1)
├── Agent_2 (Layer: Agent_2)
├── Agent_3 (Layer: Agent_3)
├── Agent_4 (Layer: Agent_4)
├── Pallets (1)
├── Pallets (2)
├── Pallets (3)
└── Pallets (4)
```

---

## Troubleshooting

**Problem:** "Agent or Pallet prefab is null!"
- **Lösung:** Stelle sicher, dass du die Prefabs im EvolutionManager zugewiesen hast

**Problem:** Agenten colliden miteinander
- **Lösung:** Führe `Setup Collision Matrix` aus (Schritt 4)

**Problem:** "Could not find 'Pallets (1)'"
- **Lösung:** Das ist normal beim ersten Frame. Wird automatisch gefunden.

**Problem:** Agenten spawnen nicht
- **Lösung:** Prüfe, dass `agentSpawnPos` und `palletSpawnPos` zugewiesen sind

---

## Vorteile

✅ **Einfach skalierbar**: Slider von 1-20 Agenten
✅ **Keine manuellen Layer**: Automatisch zugewiesen
✅ **Gleiche Positionen**: Alle Agenten haben identische Pallet-Positionen
✅ **Saubere Szene**: Keine 30 Agenten in der Hierarchy vor dem Start
