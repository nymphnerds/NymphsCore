# Nymphs Top Down Controller

Reusable Unity package for a top-down player controller and follow camera.

## Includes

- `Runtime/Scripts/PlayerController.cs`
- `Runtime/Scripts/CameraController.cs`
- `Runtime/Scripts/ClickIndicatorRuntime.cs`
- `Editor/PlayerControllerEditor.cs`
- `Runtime/Prefabs/NTDC_Camera.prefab`
- `Runtime/Prefabs/NTDC_Player_Template.prefab`
- `Runtime/Settings/GameInput.inputactions`
- `Runtime/Characters/Oak Tree Ent/*`

## Features

- Camera-relative movement with `CharacterController`
- Sprinting and animation state switching
- Click-to-move with optional queued destinations
- Optional `NavMeshAgent` click-to-move pathing
- Mouse and gamepad camera rotation/zoom
- Auto-targeting camera follow
- Custom inspector for player controller settings

## Requirements

- Unity `6000.0+`
- Input System package
- A ground collider for mouse raycasts

## Install From Git

Add this repository to `Packages/manifest.json`:

```json
"com.nymphs.topdown-controller": "https://github.com/Babyjawz/Nymphs-TDC-Unity.git"
```

## Setup

1. Add `NTDC_Camera.prefab` to your scene.
2. Either drag in `NTDC_Player_Template.prefab` for the temporary tree character, or add `CharacterController`, `PlayerInput`, and `PlayerController` to your own player object.
3. Assign `Runtime/Settings/GameInput.inputactions` to `PlayerInput`.
4. Set the camera's target to your player, or tag the player as `Player`.
5. Update the player animation state names to match your animator.

## Notes

- The package currently includes the temporary `OakTreeEnt_PBR` character assets so `NTDC_Player_Template.prefab` works out of the box.
- Replace that temporary character later with your own prefab when you're ready.
- The included camera prefab was stripped of URP-only camera data so it imports cleanly in non-URP projects.
