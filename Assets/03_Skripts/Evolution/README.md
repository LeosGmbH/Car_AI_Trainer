# PPO + Evolution System Setup

Dieses System kombiniert Standard ML-Agents PPO Training mit einer evolutionären Selektion.

## Setup Anleitung

### 1. Evolution Manager erstellen
1. Erstelle ein **leeres GameObject** in deiner Szene und nenne es `EvolutionManager`.
2. Füge das Script `EvolutionManager.cs` hinzu.
3. **Einstellungen**:
   - `Generation Duration`: Dauer einer Generation in Sekunden (z.B. 30 - 60).
   - `Survival Rate`: Anteil der Agenten, die überleben (z.B. 0.2 für Top 20%).
   - `Respawn At Elite`: Wenn aktiv, werden "tote" Agenten bei den Eliten neu gespawnt.

### 2. Agenten vorbereiten
1. Wähle dein Agenten-Prefab (oder alle Agenten in der Szene).
2. Füge das Script `FitnessTracker.cs` hinzu.
3. **Einstellungen**:
   - `Renderers To Color`: Ziehe hier die MeshRenderer deines Autos rein (z.B. Karosserie), damit sie eingefärbt werden können.
   - `Elite Color`: Farbe für die besten Agenten (Grün).
   - `Normal Color`: Standardfarbe.
   - `Dead Color`: Farbe für ausgeschiedene Agenten.
4. **WICHTIG**: Das `MLAgentController` Script sucht automatisch nach dem `FitnessTracker`, wenn er auf dem gleichen Objekt liegt.

### 3. Testen
1. Starte die Szene in Unity.
2. Du solltest im Console-Log sehen: `[EvolutionManager] Generation 1 Started`.
3. Beobachte die Agenten. Wenn sie Rewards sammeln, steigt ihre Fitness (sichtbar im Inspector beim `FitnessTracker`).
4. Nach Ablauf der Zeit (z.B. 30s) sollte im Log stehen: `Generation 1 Ended. Best: ...`.
5. Die schlechten Agenten sollten schwarz werden (oder resetten) und ggf. zu den Positionen der grünen (Elite) Agenten springen.

## Funktionsweise
- **PPO**: Läuft ganz normal weiter. `mlagents-learn` bekommt alle Rewards wie gewohnt.
- **Evolution**:
  - Parallel dazu sammelt der `FitnessTracker` alle Rewards als "Fitness".
  - Der `EvolutionManager` greift periodisch ein, tötet schwache Agenten und verteilt sie neu um die erfolgreichen Agenten.
  - Das hilft dem PPO-Algorithmus, da Agenten öfter in "guten" Situationen starten (bei den Eliten).

## Architektur
- **FitnessTracker**: Component auf dem Agent. Sammelt Punkte.
- **EvolutionManager**: Globaler Manager. Sortiert und selektiert.
- **MLAgentController**: Leitet Rewards an den FitnessTracker weiter (`AddAgentReward`).
