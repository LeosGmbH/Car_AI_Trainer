# Implementation Plan - Reward Logic Update

## Goal
Optimize the ML-Agent's training by refining rewards/penalties to encourage approaching pallets, picking them up, and driving forward. Add fail conditions for wall collisions and inactivity (not touching a pallet).

## User Review Required
> [!IMPORTANT]
> **Wall Tag**: I will assume the tag is named "wall" (lowercase). Please ensure your wall objects have this tag.
> **Fork Height Limit**: The penalty for fork height > 0.4 will apply regardless of whether a pallet is held or not (as "everything above is unnecessary").

## Proposed Changes

### Helper Classes

#### [MODIFY] [MLAgentRewardHandler.cs](file:///e:/LEO_X9/Projekte/Unity/Car_AI_Trainer/Assets/03_Skripts/MLAgent/Helper/MLAgentRewardHandler.cs)
- **New Variables**:
    - `bool hasEverTouchedPallet`: To track if the agent has made contact at least once.
- **Update `ApplyRewardLogic`**:
    - **Distance Reward**: Increase multiplier (currently `0.05f`).
    - **Fork Logic**:
        - If `!isPalletHeld`:
            - Penalty if `forkNorm > 0` (Fork UP).
            - Penalty if `moveInput < 0` (Driving Backwards).
        - If `isPalletHeld`:
            - If `!isInDropZone`: Penalty if `forkNorm < 0.1` (Fork DOWN).
            - If `isInDropZone`: Penalty if `forkNorm > 0.1` (Fork UP - assuming we want to drop it). *Wait, user said "Pallet & dropzone = strafe für fork oben". usually you want to lower it there. Correct.*
        - **General Fork Height**: Penalty if `forkTransform.localPosition.y > 0.4f`.
    - **Timeout Logic**:
        - Check `stepCount > maxStep * 0.5`.
        - If `!hasEverTouchedPallet`, call `Die()` with penalty.

### Controller

#### [MODIFY] [MLAgentController.cs](file:///e:/LEO_X9/Projekte/Unity/Car_AI_Trainer/Assets/03_Skripts/MLAgent/MLAgentController.cs)
- **New Method**: `OnCollisionEnter(Collision collision)`
    - Check `collision.gameObject.CompareTag("wall")`.
    - Call `rewardHandler.Die()` or specific wall penalty method.
- **Update `OnEpisodeBegin`**:
    - Reset `hasEverTouchedPallet` in handler.
- **Update `OnActionReceived`**:
    - Update `hasEverTouchedPallet` if `IsPalletTouched` is true.
    - Pass `StepCount` and `MaxStep` to `ApplyRewardLogic`.

## Verification Plan
### Automated Tests
- None (Unity logic).
### Manual Verification
- **Compilation**: Ensure no errors.
- **Logic Check**: Review the code to ensure all user constraints are met.
