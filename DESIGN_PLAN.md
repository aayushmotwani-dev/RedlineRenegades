# Redline Renegades — Production Plan

## Product statement

Redline Renegades is an original, single-player 3D arcade motorcycle-combat race. The player fights through five rival riders and civilian traffic on a point-to-point mountain highway. The project takes inspiration from the broad “racing plus close-range combat” genre but does not use Road Rash names, characters, locations, interface, audio or artwork.

## Player promise

Within ten seconds the player should feel speed, understand the road and see a rival close enough to fight. Steering is forgiving, crashes cost time without ending every run, and attacks are readable because they have anticipation, a directional arc and recovery.

## Complete vertical-slice scope

- One authored 2.1 km mountain highway with curves, elevation, guardrails, shoulders, trees, rocks, signs and a tunnel gate
- Six riders total with distinct colours, lane preferences, aggression and catch-up limits
- Civilian traffic that follows the route as a readable moving hazard
- Keyboard controls: W/S throttle/brake, A/D steer, Q/E attack left/right, Space boost, R recover, Esc pause
- Arcade Rigidbody bike model with active balance, road adhesion, speed-sensitive steering and continuous collision detection
- Directional melee with wind-up, active and recovery states
- Rider health, bike integrity, boost, impact stun, knockdown and safe recovery
- Race progress, checkpoint validation, placement, distance-to-finish and final results
- Title/calibration screen, HUD, pause, victory/defeat and restart
- Chase camera with speed-dependent FOV, lean framing, impact shake and obstruction correction
- Reproducible Windows build and source package

## Technical architecture

| System | Responsibility |
|---|---|
| `RoadSpline` | Catmull-Rom route sampling, nearest progress, lane positions and procedural road mesh |
| `WorldBuilder` | Terrain ribbon, guardrails, scenery, tunnel, lighting, signs and material palette |
| `BikeMotor` | Rigidbody motion, acceleration, braking, steering, lean, adhesion and recovery |
| `RiderCombat` | Directional attack state machine, hit queries, damage and knockdowns |
| `RaceAI` | Route following, lane choice, overtaking, limited rubber-banding and attack decisions |
| `TrafficVehicle` | Lightweight route-following hazards with lane changes |
| `RedlineGame` | Countdown, checkpoints, standings, finish rules and game states |
| `ChaseCamera` | Follow smoothing, velocity look-ahead, FOV, shake and collision correction |
| `GamePresentation` | Resolution-aware menus, HUD, feedback and results |

## Physics decision

The game uses an actively stabilized Rigidbody instead of a literal two-wheel `WheelCollider` rig. Unity’s Wheel Colliders use raycast contacts, suspension and slip-based friction curves. They are valuable for car simulation, but an enjoyable motorcycle additionally needs active balance and careful single-track tire tuning. A guided arcade model is less physically literal but more controllable, easier to test and more appropriate for a short combat-racing vertical slice.

Critical fast-moving bodies use continuous collision detection. Static scenery remains discrete for performance. Recovery always queries the route and places the bike at a valid lane point facing forward.

## Art direction

“Late-afternoon outlaw hill climb”: warm sandstone, desaturated pine, charcoal asphalt, cream lane paint and strong rider colour blocking. Shapes are low-poly and intentionally authored. Cyan/purple generic-neon styling is avoided.

## Quality gates

- No rider can spawn below, outside or facing away from the road.
- Race progress never decreases by more than the recovery allowance.
- Position ranking uses route progress rather than world distance.
- Attacks cannot hit through the opposite side of the bike.
- A rider cannot take repeated damage during one attack active window.
- Recovery cannot place a bike inside traffic or another rider.
- All game states can restart without leaving time scale paused.
- HUD has no clipping at 1280×720, 1920×1080 or 16:10.
- Standalone player log contains no exceptions or missing shaders.

## Research basis

- Unity Wheel Collider documentation: https://docs.unity3d.com/6000.0/Documentation/Manual/class-WheelCollider.html
- Unity collision-mode guidance: https://docs.unity3d.com/current/Manual/physics-optimization-cpu-rigidbody-collision-modes.html
- Unity Cinemachine documentation: https://docs.unity3d.com/Manual/com.unity.cinemachine.html
- Unity Input System overview: https://docs.unity3d.com/Manual/com.unity.inputsystem.html
- Classic motorcycle-combat structure research: point-to-point racing, nearby directional combat, health, crashes and progression
