# S-400 Triumf Air Defence System — Unity Simulation

## Overview

This project is a real-time 3D simulation of the S-400 Triumf surface-to-air missile system built in Unity. It models the full engagement cycle of a modern air defence battery: radar search and detection, automated threat classification, fire control decision-making, interceptor missile launch, and terminal guidance. All systems operate at **1:40 scale** relative to real-world values — distances, speeds, and altitudes are divided by 40.

The simulation is not a game in the traditional sense. It is a behavioural and systems model intended to demonstrate how each component of the S-400 works and interacts. There is no player input for the defence side — the system operates autonomously. The only player interaction is spawning targets via keyboard shortcuts.

---

## Scale Convention

All numeric values in this project follow a strict **1:40 scale factor**:

| Parameter | Real World | In Simulation |
|---|---|---|
| S-400 max intercept range | 400 km | 10,000 m |
| Radar detection range | 600 km | 15,000 m |
| Interceptor top speed (9M96E2) | ~1,800 m/s | 45 m/s |
| Interceptor launch speed | ~800 m/s | 20 m/s |
| Stealth fighter cruise speed | ~600 m/s | 15 m/s |
| Stealth fighter altitude | ~12,000 m | 300 m AGL |
| Strategic bomber altitude | ~12,000 m | 300 m AGL |
| Cruise missile terrain altitude | ~50 m | ~2 m |
| Ballistic missile apogee | ~50,000 m | 1,250 m |
| Ballistic missile peak speed | ~2,100 m/s | 52 m/s |
| UAV cruise speed | ~180 m/s | 4.5 m/s |
| Proximity fuze radius | ~320 m | 8 m |

Config altitudes in `TargetConfig` are expressed as **height above ground level (AGL)**, not absolute world Y. The spawner and each target script add the terrain elevation at the relevant position before using these values.

---

## Project Structure

```
Assets/
├── Scripts/
│   ├── Targets/
│   │   ├── AerialTarget.cs          Base class for all flying objects
│   │   ├── StealthFighter.cs
│   │   ├── StrategicBomber.cs
│   │   ├── UAVDrone.cs
│   │   ├── CruiseMissile.cs
│   │   ├── BallisticMissile.cs
│   │   ├── Bird.cs
│   │   └── TargetSpawner.cs
│   ├── Radar/
│   │   ├── RadarAntenna.cs
│   │   ├── RadarContact.cs
│   │   ├── ThreatClassifier.cs
│   │   └── RadarHitHandler.cs
│   ├── FireControl/
│   │   └── FireControlSystem.cs
│   ├── Missile/
│   │   └── MissileController.cs
│   └── UI/
│       ├── ThreatPanel.cs
│       └── HUDController.cs
├── ScriptableObjects/
│   ├── TargetConfig.cs              Data definition
│   ├── StealthFighterConfig.asset
│   ├── StrategicBomberConfig.asset
│   ├── UAVDroneConfig.asset
│   ├── CruiseMissileConfig.asset
│   ├── BallisticMissileConfig.asset
│   └── BirdConfig.asset
└── Editor/
    ├── MatchTerrainToSkybox.cs
    └── DebugMissileSpawner.cs
```

---

## Keyboard Controls (Play Mode)

| Key | Action |
|---|---|
| `F` | Spawn Stealth Fighter |
| `R` | Spawn Strategic Bomber |
| `D` | Spawn 5× UAV Drones |
| `C` | Spawn Cruise Missile |
| `B` | Spawn Ballistic Missile |
| `K` | Spawn 8× Birds |
| `T` | Debug spawn (Stealth Fighter close to radar) |

Auto-spawn is also available: set `autoSpawnEnabled = true` on the `TargetSpawner` component.

---

## System Architecture — Data Flow

The simulation operates as a pipeline that runs every frame:

```
TargetSpawner
     │
     ▼
AerialTarget (6 subclasses) ──► Physics / Rigidbody
     │
     ▼
RadarAntenna.SweepForTargets()
     │  OverlapSphere → cone angle filter → contact list
     ▼
ThreatClassifier.Classify()
     │  Pure function: speed + altitude + RCS → ThreatLevel
     ▼
FireControlSystem.Update()
     │  Range gate → reload gate → catchability check → LaunchSalvo()
     ▼
MissileController (per interceptor)
     │  Vertical launch → Pure Pursuit → Proportional Navigation → Detonate()
     ▼
HUDController / ThreatPanel (UI feedback)
```

Each stage is described in detail below.

---

## 1. Target Spawner (`TargetSpawner.cs`)

Targets are spawned on the circumference of a circle centred on the radar, at `spawnRadius` (default 3,000 m). The spawner:

1. Picks a random angle on the circle to determine the XZ spawn position.
2. Raycasts downward from Y=5,000 to find the terrain height at that XZ position.
3. Reads `TargetConfig.minAltitude` and `maxAltitude` and adds the terrain height to get a world Y spawn position. This ensures all altitude values in configs are AGL, not absolute.
4. Instantiates the prefab, faces it toward the radar, and calls `target.SetRadarTarget(centerPoint)` before `Start()` runs — this is critical because `InitializeTarget()` runs in `Start()` and needs the radar reference to set initial velocity direction and altitude targets.

The `SetRadarTarget` / `Start()` ordering is deliberate: `Awake()` only sets up the Rigidbody, `Start()` calls `InitializeTarget()` one frame later, by which point the spawner has already set `radarTarget`.

---

## 2. Aerial Target Base Class (`AerialTarget.cs`)

All six target types inherit from this abstract class. It provides:

### Physics Setup
- `useGravity = false` for all targets except `BallisticMissile` during coast/reentry.
- `linearDamping = 0` — no automatic drag, all deceleration is explicit.
- Layer mask built in `Awake()` that excludes the object's own layer from terrain raycasts, preventing the common bug where a downward ray hits the object's own collider and returns a false ground height.

### Radar State
Three public fields written by the radar and read by subclasses and the FCS:
- `isDetected` — set true when the radar beam sweeps over the target.
- `isTracked` — set true simultaneously with `isDetected` (in this simulation they are equivalent; a more advanced version would separate initial detection from sustained track).
- `threatLevel` — written by `ThreatClassifier` after each contact is built.

### `HoldAltitude(float desiredWorldY, float strength)`
A proportional controller (P-controller) for vertical position. It computes the altitude error `desiredWorldY - position.y`, multiplies by `strength` to get a target vertical velocity, clamps it to ±60 m/s, then lerps `rb.linearVelocity.y` toward that target. This drives altitude purely through velocity without teleporting position, which avoids fighting the physics engine.

**Critical design note**: All subclasses that use `HoldAltitude` must track `currentSpeed` as the horizontal magnitude only (`rb.linearVelocity` with `y=0`). If `currentSpeed` includes the vertical component from `HoldAltitude`, there is a feedback loop: HoldAltitude adds vel.y → total speed increases → currentSpeed reads higher → next tick the XZ steering targets a higher horizontal speed → total speed grows unboundedly. All subclasses explicitly do `hv.y = 0; currentSpeed = hv.magnitude` at the end of `UpdateMotion()`.

### `GetTerrainYBelow()`
Raycasts downward from `position + 500m` using the layer mask that excludes the object's own layer. Returns the terrain Y at that XZ position. Falls back to `Terrain.SampleHeight()` if the raycast misses (e.g., outside terrain bounds). Used by `HoldAltitude` callers and by `CruiseMissile` for terrain following.

### `EnforceTerrainFloor()`
Runs after every `UpdateMotion()` call. Gets the terrain Y below, adds `minTerrainClearance` (default 15 m), and if `position.y` is below that floor, snaps the object up and zeroes any downward velocity. This is a safety net — it catches edge cases where the movement logic doesn't react fast enough to rising terrain.

### Terrain and Radar Collision
- `OnCollisionEnter`: if the colliding object has a `TerrainCollider`, the target destroys itself.
- `RadarHitHandler` on the Radar GameObject handles the reverse: if a target or missile collides with the radar, it is destroyed with a CRITICAL HIT log message.

---

## 3. Individual Target Behaviours

### 3.1 Stealth Fighter (`StealthFighter.cs`)

Three-state machine: **Cruise → Evade → Dash**. The Dash state is terminal — once entered it is never exited.

**Cruise**: Steers XZ toward the radar using a lerp on the x and z components of `rb.linearVelocity` separately (never setting the full vector, which would zero out the Y component that `HoldAltitude` owns). Calls `HoldAltitude` every tick to maintain cruise altitude. Checks `isTracked` each tick and transitions to Evade the instant it becomes true.

**Evade**: At state entry, the current flat (Y=0) velocity is rotated 90° around the world-up axis to produce `_evadeDir`. This direction is locked — it is never recalculated from `transform.forward`, which would cause the break direction to chase itself as the aircraft rotates. The XZ velocity is aggressively lerped toward `_evadeDir * currentSpeed * 1.15` with a high rate (12×dt ≈ 0.24/tick), giving a sharp visible break manoeuvre. `HoldAltitude` keeps the aircraft level during the break. After 3 seconds the current flat velocity is locked as `_dashDir` and the state transitions to Dash.

**Dash**: Lerps XZ velocity toward `_dashDir * currentSpeed` while simultaneously accelerating `currentSpeed` toward `config.maxSpeed`. `HoldAltitude` keeps altitude stable. Because `_dashDir` was locked from the flat velocity at Evade exit, the dash is always horizontal — no climb component is ever introduced.

**Speed tracking**: `currentSpeed = clamp(flatVelocity.magnitude, 0, config.maxSpeed)`. The Y component of velocity is explicitly excluded.

---

### 3.2 Strategic Bomber (`StrategicBomber.cs`)

No state machine. Flies straight and level from spawn to radar indefinitely. Never reacts to `isTracked`. The only active behaviour is calling `HoldAltitude` each tick. XZ velocity is maintained by lerping the x and z components toward their current normalised direction — this preserves heading without zeroing Y. `currentSpeed` is set at init and never changed.

---

### 3.3 UAV Drone (`UAVDrone.cs`)

Two-state machine: **Loiter → Dive**. Dive is permanent once triggered.

**Loiter**: Uses trigonometric circle calculation — the orbit angle is advanced by `currentSpeed / loiterRadius` radians per second, and the target XZ position is `loiterCenter + (cos(angle), 0, sin(angle)) * loiterRadius`. The orbit centre is the spawn position, not the radar. `HoldAltitude` maintains loiter altitude. Transitions to Dive when `isTracked`.

**Dive**: Constructs a dive direction from the current horizontal forward vector combined with `Vector3.down` at a 60° angle (`cos(60°)` forward + `sin(60°)` down, normalised). Accelerates toward `config.maxSpeed`. No return to Loiter — the dive is a one-way commitment.

---

### 3.4 Cruise Missile (`CruiseMissile.cs`)

Two simultaneous controllers on separate axes every `FixedUpdate`:

**Y-axis (terrain following)**: `GetTerrainYBelow()` returns the terrain height at current XZ. The missile's Y position is lerped toward `groundY + terrainFollowHeight` (default 2 m). This is done by direct position assignment (teleporting Y smoothly), and `rb.linearVelocity.y` is set to 0 after each frame. The terrain follower completely owns the vertical axis — physics has no role in Y movement.

**XZ-axis (waypoint navigation)**: A sequential waypoint list defines the attack corridor. The missile steers toward `waypoints[wpIndex]`, advancing when within `waypointRadius` (100 m). Falls back to the radar position when no waypoints are assigned or all are exhausted. Steering is a lerp on the full XZ velocity vector at rate 3×dt.

The two controllers are independent — terrain following adjusts Y, waypoint navigation adjusts XZ — producing a nap-of-earth flight path.

---

### 3.5 Ballistic Missile (`BallisticMissile.cs`)

Three physical phases driven by explicit Rigidbody state changes:

**Boost** (`rb.useGravity = false`): A fixed boost direction `_boostDir` is locked at launch as `(transform.forward + Vector3.up * 2).normalized`. This is computed once and never recalculated — the mistake of recalculating from `transform.forward` each tick would cause the direction to rotate with the missile and produce a vertical feedback loop. `rb.AddForce(_boostDir * accel, ForceMode.Acceleration)` is applied each tick. Velocity is clamped to `config.maxSpeed`. Boost ends when either `boostDuration` seconds have elapsed or `transform.position.y >= apogeeAltitude` (1,250 m).

**Coast** (`rb.useGravity = true`): No code intervention. Unity's physics engine drives the ballistic arc. The missile continues climbing under its own momentum, peaks, and begins descending. The phase ends when `rb.linearVelocity.y < 0` (descending).

**Reentry**: `rb.linearDamping = 0.08` is applied to simulate atmospheric drag. No thrust, no steering. Gravity accelerates the warhead downward and the drag prevents unbounded speed growth. `currentSpeed` increases dramatically during this phase as intended.

---

### 3.6 Bird (`Bird.cs`)

Boid flocking with two rules blended at 1.5:0.5 (separation:cohesion):

**Separation**: For each other bird within `separationDist` (8 m), adds a vector pointing away from that bird to the separation accumulator.

**Cohesion**: Accumulates all other birds' positions, averages them, subtracts current position to get a vector toward the flock centre.

The blend `separation * 1.5 + cohesion * 0.5` is normalised and used as the steering target direction. A single bird with no flock mates produces zero steer and flies straight indefinitely — this is expected and documented behaviour.

Altitude is maintained via `HoldAltitude(groundY + flockAltitude)` where `groundY` is terrain height from `GetTerrainYBelow()`. Birds must stay below ~10 m AGL (12 m world Y in practice) to be classified as `ThreatLevel.None` by the classifier — if a bird somehow climbs higher it risks being misidentified and engaged.

---

## 4. Radar System (`RadarAntenna.cs`, `RadarContact.cs`, `ThreatClassifier.cs`)

### 4.1 Antenna Rotation and Sweep

The antenna model (`antennaModel` Transform) rotates at `rotationSpeed` degrees/second (36°/s = 6 RPM). The sweep direction is derived from the **world Y rotation angle** of the antenna model, not from `transform.forward`, because the model's local forward axis may not align with world forward depending on how the mesh was imported.

Each frame, `SweepForTargets()`:
1. Clears the contacts list.
2. Computes `sweepForward` as `(sin(angle), 0, cos(angle))` — a horizontal unit vector in the antenna's current facing direction.
3. Calls `Physics.OverlapSphere` with radius `range` and `targetMask` to find all colliders within range on the target layer.
4. For each hit, computes the flat angle between `sweepForward` and the flat direction to the target. If that angle ≤ `coneAngle` (6°), the target is inside the beam.
5. Reads the `AerialTarget` component, sets `isDetected = true` and `isTracked = true`, builds a `RadarContact`, runs `ThreatClassifier.Classify()`, and adds the contact to the list.

**Implications of the sweep model**: A target is only in the contact list during the frames when the antenna beam physically overlaps it — roughly once per 10-second rotation cycle. Between sweeps the contacts list is empty for that target. The FCS reads the contacts list every frame, so it acts immediately on each sweep detection. The `isTracked` flag is set to true when detected but is never cleared — this means after first detection `isTracked` remains true permanently, which is what triggers evasive manoeuvres on affected targets.

### 4.2 RadarContact Data

Each contact is a snapshot containing:
- `target` — direct reference to the `AerialTarget` component
- `position` — world position at detection time
- `velocity` — `Rigidbody.linearVelocity` at detection time (used for intercept prediction)
- `rcs` — read from `TargetConfig.rcs`
- `threatLevel` — assigned by the classifier

### 4.3 Threat Classifier (`ThreatClassifier.cs`)

A pure static function with no state. Decision tree evaluated in strict order — first match wins. All thresholds are at 1:40 scale:

| Rule | Condition | Result |
|---|---|---|
| 1 (Bird) | speed < 0.7 m/s AND alt < 12 m AND rcs < 0.01 | None |
| 2 (Ballistic) | speed > 20 m/s | Critical |
| 3 (Cruise) | alt < 5 m AND speed > 3.75 m/s AND rcs < 0.5 | Critical |
| 4 (Stealth) | rcs < 0.01 AND speed > 7.5 m/s | Critical |
| 5 (Bomber) | rcs > 50 AND alt > 125 m | High |
| 6 (UAV) | speed < 3.75 m/s AND alt < 150 m | Medium |
| 7 (Fallback) | anything else | Low |

Bird check is first because birds share RCS < 0.01 with stealth fighters — without the bird check, a slow low bird would fall through to Rule 4 and be misidentified as a stealth fighter. The speed gate (< 0.7 m/s) is what separates them.

---

## 5. Fire Control System (`FireControlSystem.cs`)

Runs in `Update()` every frame. Iterates the radar contact list and for each contact applies three gates:

**Gate 1 — Threat**: `threatLevel == None` skips the contact. Birds are never engaged.

**Gate 2 — Range**: Distance from FCS to contact > `engagementRange` (5,000 m). Out-of-range contacts are logged but not engaged.

**Gate 3 — Catchability**: Computes `closingSpeed = missileMaxSpeed - targetRadialSpeed` where `targetRadialSpeed = dot(contact.velocity, directionToTarget)`. If closing speed < 5 m/s the target is moving away from the launcher nearly as fast as the missile — engagement is aborted. At 1:40 scale with missile top speed 45 m/s, this threshold is meaningful.

**Gate 4 — Reload**: `Time.time - lastLaunchTime < reloadTime`. Prevents full magazine dump in a single burst.

If all gates pass, `LaunchSalvo()` fires 1 or 2 interceptors (based on threat level) via coroutines staggered 0.5 seconds apart. The target is added to `engagedTargets` (a `HashSet`) to prevent duplicate engagement on subsequent radar sweeps.

`missileMaxSpeed` is read from the missile prefab's `MissileController.maxSpeed` at `Start()` — it is `[HideInInspector]` to prevent Inspector overrides from silently breaking the catchability check.

**Salvo size**:
- Critical → 2 interceptors
- High → 2 interceptors
- Medium → 1 interceptor
- Low → 1 interceptor

**Launcher selection**: The nearest launcher Transform (by Euclidean distance) to the target's contact position is selected. All launchers are always assumed available.

---

## 6. Interceptor Missile — Guidance System (`MissileController.cs`)

This is the most technically detailed component in the project. The interceptor models the S-400's 9M96E2 missile at 1:40 scale.

### 6.1 Launch

The missile is spawned pointing straight up (`Quaternion.LookRotation(Vector3.up)`) with initial velocity `Vector3.up * launchSpeed` (20 m/s upward). This models the S-400's vertical cold-launch ejection system, where the missile is ejected from the canister vertically before the motor fires and pitchover guidance begins.

Angular physics are disabled: `rb.constraints = RigidbodyConstraints.FreezeRotation`. Rotation is driven entirely by the guidance code, not by physics torques. This is the key design decision that makes the guidance stable — earlier attempts to use `AddTorque` with a PID controller caused uncontrollable spinning because the PID integral accumulated faster than the missile could respond at simulation scale.

### 6.2 Acceleration

Each `FixedUpdate`, `currentSpeed` is advanced toward `maxSpeed` via `Mathf.MoveTowards` at rate `acceleration` m/s². The velocity is then set directly to `transform.forward * _speed` — velocity always follows the nose direction. There is no separate force model for thrust; the motor is implicitly modelled by the speed ramp.

### 6.3 Guidance — Phase Selection

Distance to target is checked each tick:
- Distance > `terminalRange` (80 m): **Pure Pursuit with lead** (`ComputePursuit()`)
- Distance ≤ `terminalRange`: **Proportional Navigation** (`ComputePN()`)

### 6.4 Pure Pursuit with Lead (`ComputePursuit()`)

Pure pursuit means pointing directly at the target's current position. Naive pure pursuit causes the missile to chase the target from behind and requires the missile to be significantly faster than the target. The implementation adds a **lead correction** to reduce this chase behaviour:

```
tof = distance / missileSpeed
aimPoint = target.position + target.velocity * tof * 0.5
aimDir = (aimPoint - missile.position).normalized
```

The time-of-flight estimate `tof` is the distance divided by current missile speed. The target's velocity is projected forward by half that time, placing the aim point ahead of where the target currently is. The 0.5 factor is deliberately conservative — projecting by the full TOF tends to overshoot on slow targets or when the target is turning.

This is stable at all ranges and handles all target types including the ballistic missile during reentry (where the target is moving very fast but mostly downward, and the lead correction still converges).

### 6.5 Proportional Navigation (`ComputePN()`)

Proportional Navigation (PN) is the guidance law used by virtually all modern guided missiles. It is based on the principle that if the line-of-sight (LOS) from missile to target is not rotating, the missile is on a collision course. The guidance command is proportional to the LOS rotation rate.

**LOS and LOS rate**:
```
LOS = (target.position - missile.position).normalized
LOSrate = (LOS - lastLOS) / deltaTime
```
`lastLOS` is the LOS direction from the previous physics tick. The difference divided by `deltaTime` gives the angular rate of change of the LOS vector — how fast the target is "drifting" relative to the missile's nose.

**Closing velocity**:
```
closingVel = dot(missile.velocity - target.velocity, -LOS)
```
The component of relative velocity along the LOS direction toward the target. Clamped to a minimum of 1 m/s to avoid division problems when the missile is nearly stationary relative to the target.

**Acceleration command**:
```
accel = N * closingVel * LOSrate
```
where N = `navConstant` (3 by default). This is the standard PN formula: `a_c = N * V_c * ω` where V_c is closing velocity and ω is LOS rate.

**Desired direction**:
```
desiredDir = (transform.forward + accel * deltaTime).normalized
```
The acceleration command is added to the current forward direction and renormalised. This converts the acceleration command into a new heading direction that the rotation controller can track.

**Why PN is only used in terminal phase**: PN is numerically sensitive. When the missile is far from the target, small errors in `lastLOS` (from floating point or large timesteps) produce large `LOSrate` values and erratic commands. At close range the geometry is well-conditioned and PN's advantage over pure pursuit — that it doesn't require the missile to be faster than the target — matters most.

### 6.6 Rotation Control

Both guidance laws return a unit vector `aimDir` representing the desired nose direction. The rotation controller:

```csharp
Quaternion targetRot = Quaternion.LookRotation(aimDir);
transform.rotation = Quaternion.RotateTowards(
    transform.rotation, targetRot, maxTurnRate * Time.fixedDeltaTime);
```

`Quaternion.RotateTowards` rotates the current orientation toward `targetRot` by at most `maxTurnRate * dt` degrees. With `maxTurnRate = 180°/s`, the missile can rotate at most ~3° per physics tick (at 60 Hz fixed timestep). This is the physical manoeuvrability limit of the missile.

The velocity is then set to `transform.forward * _speed` — velocity always follows the nose. This means the missile does not "slide" — it always moves in the direction it is pointing. This is a simplification of real missile aerodynamics but produces correct interception behaviour at simulation scale.

### 6.7 Proximity Fuze

Each tick, `Vector3.Distance(position, target.position)` is compared against `fuzeRadius` (8 m). When the missile enters the fuze radius, `Detonate()` is called. This models a proximity-fuzed warhead — the missile does not need to make direct contact, just pass within the lethal radius.

`Detonate()`:
1. Loads and instantiates `Resources/Effects/Explosion` if it exists.
2. Destroys the target GameObject.
3. Notifies `FireControlSystem.OnMissileTerminated(true)` so the FCS updates its active missile count and the HUD registers a kill.
4. Destroys the missile GameObject.

A `_terminated` flag prevents double-notification if `OnDestroy` fires after `Detonate` has already run.

### 6.8 Self-Destruct (Fuel Exhaustion)

If `_elapsed > lifetime` (30 seconds) the missile calls `Terminate(false)`, notifying FCS of a miss. This models fuel exhaustion — the motor burns out and the missile falls. In the current implementation it simply destroys the missile rather than simulating a ballistic fall, but the miss is correctly recorded.

### 6.9 Terrain Impact

`OnCollisionEnter` checks for `TerrainCollider` on the colliding object. A terrain collision calls `Detonate()` — treating ground impact as a detonation (which is physically reasonable; a missile hitting the ground at 45 m/s would detonate, though without a valid target the `hit` flag sent to FCS is false).

---

## 7. UI Systems

### 7.1 Threat Panel (`ThreatPanel.cs`)

A scrolling list that mirrors the radar contacts list in real time. Each row shows target name, threat level, distance in km, and speed in m/s. Rows are pooled — excess rows are hidden rather than destroyed. Colours: red = Critical, orange = High, yellow = Medium, cyan = Low, green = None.

### 7.2 HUD Controller (`HUDController.cs`)

Tracks kills and misses via `RegisterKill()` and `RegisterMiss()` called by the FCS after each missile termination.

---

## 8. Terrain and Environment

The terrain is a Unity Terrain with a custom heightmap sculpted to produce a realistic mountain ring around a central valley. The terrain uses four TerrainLayer textures from the `TerrainDemoScene_URP` package: `Grass_A` (valley floor), `Grass_Moss_A` (slopes), `Cliff_Mossy_E` (steep rock faces), and `Heather_A` (high ridgelines). The splatmap is painted procedurally by slope angle and normalised altitude.

A `SkyboxMountains` prefab ring of mesh geometry is positioned at terrain centre to provide a continuous mountain horizon that matches the terrain edges.

The `MatchTerrainToSkybox` editor tool (`Tools > Match Terrain To Skybox`) applies the terrain material, layers, and environment settings in a single click.

---

## 9. Known Design Decisions and Simplifications

- **No radar memory**: The contacts list is cleared and rebuilt every frame. Track loss occurs implicitly — if a target leaves the beam, it simply disappears from the list. There is no dead-reckoning or track-before-detect logic.
- **`isTracked` is never cleared**: Once a target is detected for the first time, `isTracked` remains true permanently. A more realistic model would clear it between radar sweeps and only set it to true on confirmed sustained track.
- **No electronic warfare**: The radar cannot be jammed. RCS values are constants, not aspect-angle-dependent.
- **No magazine limit**: The FCS can fire indefinitely. The reload timer prevents burst firing but there is no total missile count.
- **No multi-target tracking priority**: The FCS engages contacts in the order the radar returns them, not sorted by threat or time-to-impact.
- **Missile velocity follows rotation exactly**: The missile does not have aerodynamic slip or angle-of-attack. Velocity is always `transform.forward * speed`, which is a simplification that produces stable guidance but is not physically accurate for a real aerodynamic body.
- **Terrain collision is terminal**: Both targets and missiles are destroyed on terrain contact. Targets do not break apart, there are no debris effects.

---

## 10. Editor Tools

### `Tools > Match Terrain To Skybox`
Applies the TerrainLit material, terrain layers, lighting settings, and fog to match the terrain to the skybox mountains.

### `Tools > Debug > Spawn Missiles at Launchers`
Places static (disabled) missile prefab instances at each launcher position for visual alignment without entering play mode.

### `Tools > Debug > Remove Debug Missiles`
Cleans up debug missile instances.

---

## 11. TargetConfig ScriptableObject

Each target type has a dedicated asset in `Assets/ScriptableObjects/`. Fields:

| Field | Description |
|---|---|
| `targetTypeName` | Display name |
| `minSpeed / maxSpeed` | Speed range in m/s (1:40 scale) |
| `minAltitude / maxAltitude` | Altitude range in metres **above ground level** |
| `rcs` | Radar cross-section in m² |
| `maxManeuverG` | Max manoeuvrability (currently informational only) |
| `defaultThreat` | Expected threat level (for reference) |

All altitude values are AGL. The spawner and target scripts add terrain elevation before using them.

---

*Content was written from direct source code analysis. All values reflect the current state of the implementation.*
