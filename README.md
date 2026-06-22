# S-400 Triumf — Air Defence Simulation

A Unity (URP) simulation of the Russian S-400 surface-to-air missile system engaging a variety of aerial targets. All distances, speeds, and altitudes are modelled at **1:40 scale** (real-world values divided by 40). Time and gravity are not scaled.

---

## Table of Contents
1. [Project Structure](#project-structure)
2. [Scale Reference](#scale-reference)
3. [Keybindings](#keybindings)
4. [Target Types](#target-types)
5. [Interceptor Missiles](#interceptor-missiles)
6. [Radar System](#radar-system)
7. [Fire Control System](#fire-control-system)
8. [Guidance & Avoidance](#guidance--avoidance)
9. [Camera Displays](#camera-displays)
10. [HUD & UI](#hud--ui)
11. [Scene Setup Checklist](#scene-setup-checklist)
12. [Inspector Reference](#inspector-reference)

---

## Project Structure

```
Assets/
├── Editor/
│   └── DebugMissileSpawner.cs       # Tools > Debug menu items
├── Models/                          # FBX models + noise.mp4
├── Prefabs/
│   ├── Missiles/
│   │   ├── 9M96E.prefab             # Short-range agile interceptor
│   │   ├── 48N6DM.prefab            # Long-range interceptor
│   │   └── S400_Interceptor.prefab  # Legacy (unused at runtime)
│   └── Targets/
│       ├── BallisticMissile.prefab
│       ├── Bird.prefab
│       ├── CruiseMissile.prefab
│       ├── StealthFighter.prefab
│       ├── StrategicBomber.prefab
│       └── UAVDrone.prefab
├── ScriptableObjects/
│   ├── BallisticMissileConfig.asset
│   ├── BirdConfig.asset
│   ├── CruiseMissileConfig.asset
│   ├── StealthFighterConfig.asset
│   ├── StrategicBomberConfig.asset
│   ├── UAVDroneConfig.asset
│   ├── 9M96E_Config.asset           # InterceptorConfig (not yet wired to MissileController)
│   └── 48N6DM_Config.asset          # InterceptorConfig (not yet wired to MissileController)
├── Scripts/
│   ├── Camera/
│   │   ├── MissileCameraDisplay.cs  # 1st-person split-view on Display 4
│   │   ├── StaticNoiseDisplay.cs    # Shader graph static on idle displays
│   │   └── TargetCameraDisplay.cs   # 3rd-person target tracking on Display 3
│   ├── FireControl/
│   │   └── FireControlSystem.cs     # Engagement logic + missile routing
│   ├── Missile/
│   │   ├── MissileController.cs     # Guidance, avoidance, detonation
│   │   ├── MissileType.cs           # Enum: Standard_48N6DM / Agile_9M96E
│   │   └── PIDController.cs        # Utility class (not currently wired)
│   ├── Radar/
│   │   ├── RadarAntenna.cs          # Sweep, persistent track table
│   │   ├── RadarContact.cs          # Data class: position/velocity/rcs/threatLevel
│   │   ├── RadarHitHandler.cs       # Destroy on physical radar collision
│   │   └── ThreatClassifier.cs      # Pure function: RCS + speed → ThreatLevel
│   ├── Targets/
│   │   ├── AerialTarget.cs          # Abstract base: terrain floor, altitude hold
│   │   ├── BallisticMissile.cs      # Boost → Coast → Reentry phases
│   │   ├── Bird.cs                  # V-formation flock, auto-despawn
│   │   ├── CruiseMissile.cs         # Terrain-following + waypoint nav
│   │   ├── StealthFighter.cs        # Cruise → Dash + missile proximity evasion
│   │   ├── StrategicBomber.cs       # Straight-and-level, no evasion
│   │   ├── TargetSpawner.cs         # Keyboard spawn + V-flock spawn
│   │   └── UAVDrone.cs              # Loiter orbit → 60° dive on detection
│   └── UI/
│       ├── HUDController.cs         # UI Toolkit HUD driver
│       ├── RadarDisplay.cs          # Texture2D phosphor radar scope
│       └── ThreatPanel.cs           # Legacy UGUI threat list (TMPro)
└── UI/
    └── S400_HUD.uxml                # HUD layout
```

---

## Scale Reference

| Quantity | Real World | 1:40 Scale (in-game) |
|---|---|---|
| S-400 engagement range | 400 km | 10,000 m |
| Radar range | 400 km | 10,000 m |
| Spawn radius | — | 12,000 m |
| Ballistic missile apogee | 50 km | 1,250 m |
| Cruise missile terrain follow | 50 m | ~2 m (visibility tweak) |
| Stealth fighter altitude | 10,000–18,000 m | 1,000–1,500 m |
| Bomber altitude | 15,000–16,000 m | 900–1,200 m |
| UAV altitude | 3,000–7,000 m | 350–400 m |

All speeds are intentionally inflated from strict 1:40 for playability.

---

## Keybindings

| Key | Action |
|---|---|
| **B** | Spawn ballistic missile |
| **C** | Spawn cruise missile |
| **F** | Spawn stealth fighter |
| **R** | Spawn strategic bomber |
| **D** | Spawn 5 UAV drones |
| **K** | Spawn 1 V-formation bird flock (8 birds) |
| **T** | Cycle target camera (Display 3) to next active target |
| **ESC** | Pause / unpause |

---

## Target Types

All targets extend `AerialTarget` and share:
- `TargetConfig` ScriptableObject (speeds, altitudes, RCS, max-G)
- `SetRadarTarget(Transform)` — called by `TargetSpawner` after instantiation
- `HoldAltitude(float)` — P-controller on vertical velocity, clamped ±60 m/s
- `GetTerrainYBelow()` — downward raycast + Unity Terrain API fallback
- `EnforceTerrainFloor()` — hard position clamp + velocity cancel if below `minTerrainClearance` (15 m)
- `engagementRadius = 14000 m` — if target travels beyond this it steers back toward radar

### Ballistic Missile
**Config:** `rcs=1`, `spd=45–52 m/s`, `alt=50–1250 m`  
Three physics phases:
1. **Boost** (18 s): gravity off, constant force along locked upward-forward direction, clamps to `maxSpeed`. Ends at 18 s or `apogeeAltitude=1250 m`.
2. **Coast**: gravity on, physics drives the arc.
3. **Reentry**: triggered when vertical velocity goes negative; `linearDamping=0.08` simulates atmospheric drag. Speed increases under gravity.

### Strategic Bomber
**Config:** `rcs=100`, `spd=30–33 m/s`, `alt=900–1200 m`  
No state machine. Flies straight toward radar at constant speed and altitude forever. Never evades. Only active behaviour: gentle `HoldAltitude` correction.

### UAV Drone
**Config:** `rcs=0.01`, `spd=7–10 m/s`, `alt=350–400 m`  
Two states:
1. **Loiter**: perfect circular orbit (radius 1200 m) around spawn XZ point at fixed altitude.
2. **Dive**: triggered when `isTracked=true`. Accelerates to `maxSpeed`. Executes a true 60° nose-down dive toward the ground.

Never returns to Loiter.

### Cruise Missile
**Config:** `rcs=0.05`, `spd=17–20 m/s`, `alt=8–10 m`  
Simultaneous independent axes:
- **Y-axis**: smoothly Lerps world-Y toward `terrainHeight + 2 m` each tick; zero vertical velocity afterward.
- **XZ-axis**: steers toward waypoints in sequence (advance within 100 m radius), falls back to radar position when no waypoints assigned.

### Stealth Fighter
**Config:** `rcs=0.001`, `spd=26–30 m/s`, `alt=1000–1500 m`  
Two states:
1. **Cruise**: flies straight toward radar at `minSpeed`. Does **not** evade on radar detection.
2. **Dash**: accelerates to `maxSpeed` on locked heading after completing evasion.

**Missile evasion**: every cooldown window (`missileEvadeCooldown=3 s`), scans for any `MissileController` within `missileEvadeRadius=300 m`. If found, performs a smooth banking break perpendicular to the missile's flight path — side chosen to maximise lateral separation. Turn rate is clamped to `evasionTurnRate=60 °/s` so it sweeps a realistic curve rather than snapping. After `missileEvadeCooldown × 0.8 s` the break ends and the fighter transitions to Dash.

### Bird (Flock)
**Config:** `rcs=0.0005`, `spd=2–5 m/s`  
Spawned in a V-formation of up to 8 birds by `TargetSpawner.SpawnFlock()`. Each bird is assigned a fixed slot offset in the leader's local space:
```
        [0] Leader (tip)
     [1]   [2]  Wing row 1
  [3]         [4]  Wing row 2
[5]             [6]  Wing row 3
[7]                  Wing row 4 (left only)
```
Slot spacing: `lat = flockSpreadRadius × 0.5`, `depth = flockSpreadRadius × 0.55`.  
Each follower steers toward its world-space slot (`leader.position + leaderRotation × slotOffset`) with `slotTracking=3` lerp rate. Leader flies straight in `flockDir`.  
Altitude held at `flockAltitude=15 m` above terrain. Auto-despawns after `lifetime=30 s`.  
Classified as `ThreatLevel.None` (RCS below 0.0008 threshold).

---

## Interceptor Missiles

### 9M96E — Short-range, High-agility
Targets: UAV drones, cruise missiles  
**Key prefab values (set directly on MissileController component):**
| Parameter | Value |
|---|---|
| launchSpeed | 30 m/s |
| maxSpeed | 80 m/s |
| acceleration | 100 m/s² |
| maxTurnRate | 500 °/s |
| boostDuration | 0.3 s |
| terminalRange | 500 m |
| navConstant | 5 |
| fuzeRadius | 8 m |
| lifetime | 180 s |
| pursuitLeadTime | 3 s |
| avoidanceLookAhead | 80 m |

### 48N6DM — Long-range
Targets: ballistic missiles, stealth fighters, strategic bombers  
**Key prefab values:**
| Parameter | Value |
|---|---|
| launchSpeed | 40 m/s |
| maxSpeed | 150 m/s |
| acceleration | 200 m/s² |
| maxTurnRate | 180 °/s |
| boostDuration | 2 s |
| terminalRange | 800 m |
| navConstant | 5 |
| fuzeRadius | 8 m |
| lifetime | 180 s |
| pursuitLeadTime | 8 s |
| avoidanceLookAhead | 200 m |

> **Note:** `InterceptorConfig` ScriptableObject assets (`9M96E_Config.asset`, `48N6DM_Config.asset`) exist but are **not read at runtime**. All values are configured directly on the prefab's `MissileController` component in the Inspector.

---

## Radar System

### RadarAntenna
- **Range**: 10,000 m (default; override in Inspector per scene)
- **Rotation**: 36 °/s (6 RPM)
- **Cone angle**: 6° half-angle sweep beam
- Maintains **two contact lists**:
  - `contacts` — only targets inside the beam **this frame** (consumed by FCS)
  - `trackedContacts` — persistent `Dictionary<AerialTarget, TrackedContact>` — all ever-detected targets, updated on each sweep hit, never cleared until the target is destroyed
- On sweep hit: creates/updates a `TrackedContact` entry with current position, velocity, RCS, and `lastSeenAngle`
- `NotifyTargetNeutralized(target)` — called by `MissileController` before destroying a target; marks the track `neutralized=true` so HUD and radar scope show NEUTRALIZED state
- `PurgeDestroyedTargets()` — removes null keys each frame (Unity null check detects destroyed objects)

### ThreatClassifier
Pure static function, no memory. First matching rule wins.

| Rule | Condition | Result |
|---|---|---|
| 1 — Bird | `rcs < 0.0008` | None |
| 2 — Ballistic | `spd > 40 m/s` | Critical |
| 3 — Stealth | `rcs < 0.01 && spd > 20 m/s` | Critical |
| 4 — Cruise | `rcs 0.01–0.5 && spd > 14 m/s` | Critical |
| 5 — Bomber | `rcs > 50` | High |
| 6 — Drone | `rcs ≤ 0.01 && spd ≤ 20 m/s` | Medium |
| 7 — Fallback | — | Low |

### RadarHitHandler
Attach to the Radar GameObject. Destroys any `AerialTarget` or `MissileController` that physically collides with it and registers a miss on the HUD.

---

## Fire Control System

`FireControlSystem` drives engagement decisions each `Update()` tick:

1. **Contact source**: reads `radar.trackedContacts` (persistent, always available) rather than the transient sweep list, so targets are engaged between radar sweeps.
2. **Filters**: skips `ThreatLevel.None`, null targets, already-engaged targets, targets beyond `engagementRange=10,000 m`, and targets with insufficient closing speed (< 5 m/s advantage).
3. **Reload gate**: minimum `reloadTime=3 s` between launches.
4. **Missile selection** (`SelectPrefabForContact`):
   - `rcs ≤ 0.002 && Critical` → **48N6DM** (stealth fighter — identified by RCS, not speed, to avoid misrouting during evasive manoeuvres)
   - `Medium` or `Low` → **9M96E** (drone)
   - `High` → **48N6DM** (bomber)
   - `Critical && spd ≤ 22 m/s` → **9M96E** (cruise missile)
   - `Critical && spd > 22 m/s` → **48N6DM** (ballistic)
5. **Single-shot policy**: always fires exactly **1 missile** first. If it misses and no other interceptor is tracking the target, the target is re-queued and a follow-up is fired automatically.
6. **Vertical cold-launch**: missiles spawn pointing straight up (`Quaternion.LookRotation(Vector3.up)`) and rely on `boostDuration` to pitch over.
7. Notifies `MissileCameraDisplay` on launch and `HUDController` on termination (hit/miss).

---

## Guidance & Avoidance

### MissileController — Guidance Phases

**Boost phase** (`elapsed < boostDuration`):  
Nose rotated toward `Vector3.up` at `maxTurnRate`. No guidance. Pure vertical climb.

**Mid-course** (`dist ≥ terminalRange`):  
Pure pursuit with lead — aims at `target.position + target.velocity × min(tof, pursuitLeadTime)`.  
- `pursuitLeadTime` is per-prefab: 9M96E=3 s (avoids phantom-chasing circling drones), 48N6DM=8 s (aims well ahead of fast straight targets).

**Terminal phase** (`dist < terminalRange`):  
Proportional Navigation (PN): steers to zero the line-of-sight rate.  
`accel = N' × max(closingVel, 1) × LOSrate`  
`navConstant` (N') = 5 on both prefabs.

**Terrain/obstacle avoidance** (mid-course only, skipped in terminal):  
7-ray fan cast forward in the missile's hemisphere:
- Forward, ±27° left/right, ±27° up/down, ±45° up-left/up-right
- Dynamic look-ahead: `max(avoidanceLookAhead, speed × 1.5)`
- Reacts only to `TerrainCollider` or objects tagged **"Radar"**
- Urgency = `1 - (hitDist / lookAhead)` — closer obstacle = stronger push
- Avoidance direction = reflected hit normal + 0.5× upward bias
- **Low-pass smoothed** at `avoidanceSmoothing=4` rate to prevent per-frame jitter
- Final blend: `Slerp(guidanceDir, smoothedAvoidDir, avoidanceWeight=0.85)`

**Detonation**:
- Proximity fuze: detonates when `dist < fuzeRadius=8 m`
- Contact kill: `OnCollisionEnter` detonates on any physical contact
- Fuel exhaustion: `Terminate(false)` after `lifetime` seconds

> **Radar tag requirement**: objects that should be avoided must be tagged **"Radar"** in the Unity Editor. Set this in Edit → Tags & Layers.

---

## Camera Displays

### Display 3 — Target Tracking (`TargetCameraDisplay`)
- 3rd-person follow camera, offset `(0, 2, -6)` in target local space
- Camera depth: `1` — renders over static noise
- Smoothed position: `Lerp` at `followSmoothing=5`
- Looks at `target.position + Vector3.up × 2`
- **T** key cycles `_activeIndex` through registered targets
- When active target is destroyed, automatically shifts to next in queue
- Camera hidden when no targets remain

### Display 4 — Missile POV (`MissileCameraDisplay`)
- One `Camera` component per active interceptor, all on Display 4
- Camera depth: `1`
- **Smart viewport layout**: iterates all column counts, picks the `cols × rows` grid minimising deviation from 16:9 per cell. Examples: 1 missile = full screen, 4 missiles = 2×2, 6 missiles = 3×2.
- Cameras are created on `RegisterMissile()` and destroyed on `UnregisterMissile()` or null-purged in `LateUpdate`

### Static Noise (`StaticNoiseDisplay`)
- Depth -99 orthographic camera — renders behind all real cameras
- Fullscreen quad (scale = `aspect × 1`) driven by shader graph material (`Assets/StaticNoise.mat` from `Assets/Static.shadergraph`)
- Auto-loads `StaticNoise.mat` by path at runtime; falls back to magenta with a warning if not found
- One instance per display: set `displayIndex=2` for Display 3, `displayIndex=3` for Display 4
- Hidden automatically when a real camera (depth ≥ 0) renders on the same display

---

## HUD & UI

Built with **UI Toolkit** (`S400_HUD.uxml`). Driven by `HUDController`.

### Left Panel — Engagement Status
| Label | UXML name | Content |
|---|---|---|
| Active interceptors | `active-missiles-val` | `fireControl.ActiveMissileCount` |
| Kills confirmed | `kills-val` | Incremented by `RegisterKill()` |
| Misses | `misses-val` | Incremented by `RegisterMiss()` |
| Radar contacts | `contacts-val` | `radar.contacts.Count` (current sweep) |
| Antenna bearing | `antenna-angle-val` | `radar.AntennaAngle:000°` |
| Clock | `clock-label` | Elapsed time HH:MM:SS |

### Keybindings section (UXML, static)
`[F] STEALTH`, `[R] BOMBER`, `[B] BALLISTIC`, `[C] CRUISE`, `[D] UAV x5`, `[K] BIRDS x8`, `[ESC] PAUSE`, `[T] CYCLE TARGET CAM`

### Right Panel — Threat Tracks
Persistent per-target rows driven from `radar.trackedContacts`:
- Row created on first radar detection; stays alive until target is destroyed
- Live updates: distance (km), speed (m/s), altitude (m)
- On neutralization: badge changes to **NEUTRALIZED**, row shows **TARGET DESTROYED**, then auto-removes after `neutralizedDisplayTime=4 s`

### Radar Scope
The `radar-scope` UI element receives `RadarDisplay.GetRadarTexture()` as a `backgroundImage` each frame. The `RadarDisplay` script renders a 512×512 `Texture2D` with:
- Green phosphor background
- 3 concentric range rings at ¼, ½, ¾ radius
- Rotating sweep line with brightness fade
- Persistent blips from `trackedContacts` (fade with antenna angle delta; neutralized = small grey dot)
- Blip colours: Critical=red, High=orange, Medium=yellow, Low=green, None=dim green

---

## Scene Setup Checklist

1. **S-400 / Launcher GameObject**
   - Add `FireControlSystem` → assign `radar`, `launcherPositions[]`, `missilePrefab9M96E`, `missilePrefab48N6DM`, `missileCameraDisplay`
   - Add `RadarAntenna` (or child) → assign `antennaModel`, set `targetMask` to your target layer
   - Add `RadarHitHandler` to the radar mesh

2. **Spawner GameObject**
   - Add `TargetSpawner` → assign all 6 prefab slots + `centerPoint` (radar Transform) + `targetCameraDisplay`

3. **HUD GameObject**
   - Add `UIDocument` + `HUDController` → assign `fireControl`, `radar`, `radarDisplay`
   - Add `RadarDisplay` → assign `radar`

4. **Camera Manager GameObject**
   - Add `TargetCameraDisplay` → `displayIndex=2`
   - Add `MissileCameraDisplay` → `missileDisplay=3`
   - Add `StaticNoiseDisplay` ×2 → `displayIndex=2` and `displayIndex=3`, assign `StaticNoise.mat`

5. **Tags & Layers**
   - Create tag **"Radar"** and apply to radar collider GameObjects (for missile avoidance)
   - Assign target GameObjects to the layer used by `RadarAntenna.targetMask`

6. **Missile Prefab Trail**
   - Both 9M96E and 48N6DM have a `TrailRenderer`: `Time=2`, `MinVertexDistance=5`, `StartWidth=3`, `EndWidth=0.1`, gradient `#FF6600 → transparent`
   - Assign a particle/unlit material to the TrailRenderer's `Materials[0]` slot (e.g. `Default-Particle`)

---

## Inspector Reference

### TargetConfig (ScriptableObject)
| Field | Description |
|---|---|
| targetTypeName | Display name |
| minSpeed / maxSpeed | m/s at 1:40 scale |
| minAltitude / maxAltitude | metres AGL at 1:40 scale |
| rcs | Radar cross-section m² |
| maxManeuverG | Max manoeuver force (not yet enforced in code) |
| defaultThreat | Default ThreatLevel (informational) |

### MissileController (per-prefab)
All parameters are set directly on the prefab — `InterceptorConfig` assets are not read at runtime.

| Header | Field | Description |
|---|---|---|
| Speed | launchSpeed | Initial velocity off the rail (m/s) |
| Speed | maxSpeed | Terminal velocity (m/s) |
| Speed | acceleration | Rate of speed increase (m/s²) |
| Maneuverability | maxTurnRate | Max nose rotation rate (°/s) |
| Maneuverability | terminalRange | Switch to PN guidance inside this distance (m) |
| PN | navConstant | N' gain for proportional navigation |
| Warhead | fuzeRadius | Proximity kill radius (m) |
| Self-destruct | lifetime | Seconds before fuel exhaustion |
| Boost | boostDuration | Seconds of vertical climb before homing |
| Guidance | pursuitLeadTime | Max TOF cap for pursuit lead (s) |
| Avoidance | avoidanceLookAhead | Minimum ray cast distance (m) |
| Avoidance | avoidanceFanAngle | Half-angle of ray fan (°) |
| Avoidance | avoidanceWeight | Avoidance blend strength (0–1) |
| Avoidance | avoidanceSmoothing | Low-pass filter rate (3–6 recommended) |
| Avoidance | avoidanceMask | LayerMask for obstacle rays |
| Runtime | currentSpeed | Read-only live speed display (m/s) |
