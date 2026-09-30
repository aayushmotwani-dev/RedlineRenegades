# Redline Renegades

Redline Renegades is an original 3D arcade motorcycle-combat racing vertical slice built in Unity 6000.5.3f1. Race five rivals up a 2.1 km mountain pass, trade directional strikes, dodge civilian traffic and preserve enough rider health and bike integrity to reach the summit.

The second presentation pass adds a procedural late-afternoon sky, aerial perspective, balanced three-tone ambient lighting, restrained color grading, micro-normal road detail, reflective roadside markers, curve chevrons, refined motorcycle construction and correctly animated traffic wheels.

## Play the Windows build

1. Open `Build/Windows`.
2. Double-click `RedlineRenegades.exe`.
3. If Windows SmartScreen appears, choose **More info**, then **Run anyway**. This can happen with unsigned portfolio builds.
4. Leave the game at 1280×720 or resize the window. The HUD scales with the window.
5. Press **Enter** on the title screen.

No installation, Unity account or VR headset is required for the Windows build.

## Controls

| Action | Keys |
|---|---|
| Throttle / brake | W / S or Up / Down |
| Steer | A / D or Left / Right |
| Strike left / right | Q / E |
| Overtake boost | Space |
| Recover to the route | R |
| Pause / resume | Escape |
| Restart after the result | R |

## Open the source in Unity

1. Install Unity Hub and Unity Editor **6000.5.3f1** with **Windows Build Support (Mono)**.
2. In Unity Hub choose **Add > Add project from disk**.
3. Select this `RedlineRenegades` folder, not the `Build` folder.
4. Let Unity import the project. Open `Assets/Redline/Scenes/RedlineRenegades.unity` if it is not already open.
5. Press the Play button in the Unity toolbar.

To make a fresh Windows build, use **Redline Renegades > Build Windows Demo** in Unity’s top menu. The executable is written to `Build/Windows`.

## What to demonstrate in an interview

- The point-to-point race uses Catmull–Rom route sampling for road generation, rider progress, lane positions, traffic and safe recovery.
- The motorcycle is an actively stabilized Rigidbody tuned for arcade readability, with speed-sensitive steering, lean, boost and continuous collision detection.
- Melee is a directional state machine with wind-up, a single active hit window, recovery, side validation and readable feedback.
- Rivals make local lane, overtake and attack decisions with restrained catch-up logic rather than teleporting or receiving hidden invulnerability.
- The chase camera changes framing with speed, anticipates the road and corrects for occlusion.
- The UI covers title, countdown, race HUD, pause, victory, defeat and replay at multiple window sizes.
- The project includes a command-line gameplay smoke test and automated screenshot capture used to verify physics, route progress and presentation.

## Art and audio

The road, terrain meshes, bike/rider models, materials, effects and audio are constructed by the project at runtime from deliberately authored systems and palettes. Selected race-event props come from Kenney’s CC0 Racing Kit. The display font is Oxanium under the SIL Open Font License. See `THIRD_PARTY_NOTICES.md`.

The game is an original genre exercise. It does not include Road Rash names, characters, locations, UI, music or art.

See `DESIGN_PLAN.md` for the design rationale, architecture and quality gates.
