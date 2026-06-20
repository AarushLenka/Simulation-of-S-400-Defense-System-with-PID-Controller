# S-400 Triumf Unity Simulation — Component Behaviour Reference

## Purpose of This Document

This document describes **what every system does and how it behaves at runtime**, not how to code it. Read this before touching scripts — understanding the intended behaviour first makes the implementation decisions in the main guide make sense. Every section maps directly to a script or group of scripts in the main implementation guide.

---

## 1. Aerial Targets — Shared Behaviour (AerialTarget Base)

Every flying object in the simulation — whether a stealth fighter, a ballistic missile, or a bird — shares the following baseline behaviour derived from the `AerialTarget` base class.

**Physics**: Targets do not use Unity's built-in gravity by default (except the ballistic missile in its coast and reentry phases). All movement is driven explicitly through velocity assignments and force application in code, giving each target type full manual control over its flight path.

**State exposed to other systems**: Every target constantly broadcasts three pieces of state to the rest of the simulation: whether it has been detected by the radar (`isDetected`), whether it is currently being actively tracked (`isTracked`), and what threat level the radar has classified it as (`threatLevel`). These fields are written by the radar and read by the fire control system — the target itself never writes to `threatLevel`.

**Speed and altitude**: Each target's current speed and altitude are tracked as live floats, updated every physics tick. These values are what the radar reads when it builds a `RadarContact`.

**Config-driven tuning**: Speed range, altitude range, radar cross-section (RCS), and maneuver G-limit all come from a `TargetConfig` ScriptableObject asset assigned in the Inspector, not hardcoded into the script. This means you can change a target's behaviour without touching code.

---

## 2. Target Behaviours — Individual

### 2.1 Stealth Fighter

**Personality**: Aggressive, fast, evasive. Behaves like a 4th or 5th generation multirole fighter conducting a strike mission.

**State machine — three phases**:

- **Cruise**: Default entry state. The fighter flies straight toward the radar/S-400 position at its minimum speed. It does not react to anything in this state — it is simply inbound on a heading.

- **Evade**: Triggered the moment `isTracked` becomes true (i.e., the radar has achieved a sustained lock on it). The fighter immediately breaks from its straight-line course and executes a high-G spiral turn — direction is fixed at 90° yaw and 45° roll relative to its current heading, modelling a classic defensive break manoeuvre. It holds this evasive turn for approximately 3 seconds before transitioning to Dash.

- **Dash**: After the evasive break, the fighter accelerates toward its maximum speed (a supersonic sprint) and maintains that speed and heading indefinitely. It does not return to Cruise or Evade once in this state — the intent is that it has committed to a high-speed egress or continued attack run.

**Detection signature**: Extremely low RCS (~0.001 m²), making it the hardest target for the radar to classify confidently alongside birds. The classifier distinguishes it from birds via speed: the fighter is always supersonic (300+ m/s) while birds are never above 25 m/s.

**Threat level**: CRITICAL. Always gets two interceptors.

---

### 2.2 Strategic Bomber

**Personality**: Slow, predictable, massive. Behaves like a long-range strategic bomber (B-52/Tu-95 class) on a straight bombing run.

**Behaviour**: The bomber has no state machine. It enters the scene flying straight and level and never deviates. It does not react to being tracked, does not evade, and does not change speed. The only active behaviour is a gentle altitude self-correction — if the physics timestep nudges it slightly off its cruise altitude, it smoothly returns to level flight. This makes it the easiest target to intercept geometrically, but its very high altitude (8,000–16,000 m) means the interceptor has significant climb distance to cover.

**Detection signature**: Enormous RCS (50–100 m²), making it the easiest target for the radar to detect and classify at maximum range. The classifier identifies it immediately by the combination of very large RCS and high altitude.

**Threat level**: HIGH. Two interceptors assigned.

---

### 2.3 UAV / Drone

**Personality**: Patient, then sudden. Behaves like a loitering munition or reconnaissance drone that commits to an attack dive when threatened.

**State machine — two phases**:

- **Loiter**: The drone flies a circular holding pattern around its spawn-point position, maintaining a fixed altitude and moderate speed. It calculates its position on the circle mathematically each tick (trigonometric circle based on accumulated angle), so the circle is perfectly smooth regardless of physics timestep. It will hold this pattern indefinitely until it is tracked by the radar.

- **Dive**: Triggered when `isTracked` becomes true. The drone immediately abandons its circular pattern and commits to a steep angled descent — 60% downward component, 100% forward component, normalised. It gradually accelerates toward its maximum speed during the dive. Once in Dive state it never returns to Loiter — it is committed.

**Detection signature**: Small-to-medium RCS (0.01–1 m²) and low-to-moderate speed (30–120 m/s loitering, up to 120 m/s in the dive). The classifier identifies it by speed and altitude — too slow for a cruise missile, too high and purposeful for a bird.

**Threat level**: MEDIUM. One interceptor assigned.

---

### 2.4 Cruise Missile

**Personality**: Low, fast, sneaky. Behaves like a terrain-following cruise missile (Tomahawk class) navigating to a target via programmed waypoints.

**Behaviour**: The cruise missile does two things simultaneously on every physics tick:

- **Terrain following**: It fires a downward raycast and reads the terrain height directly below it. It then smoothly adjusts its own altitude to maintain a fixed height above that terrain point (50 m by default). On flat ground this keeps it at 50 m. Over a hill it climbs to stay 50 m above the hill crest. In a valley it descends. This makes it extremely difficult to detect with a flat-horizon radar geometry.

- **Waypoint navigation**: It steers toward a sequence of waypoints in order. When it comes within 100 m of a waypoint it advances to the next one. The waypoints define its overall route through the terrain — they must be set up in the scene by the user as empty GameObjects placed along a plausible attack corridor.

Both behaviours run simultaneously: terrain following adjusts the Y-axis (vertical), waypoint navigation adjusts the XZ-axis (horizontal heading). Together they produce a realistic nap-of-earth flight path.

**Detection difficulty**: The combination of very low altitude (10–100 m), moderate RCS (~0.05 m²), and moderate speed (220–280 m/s) makes the cruise missile the hardest target for a high-mounted radar to detect, since it spends most of its flight below the radar's cone angle.

**Threat level**: CRITICAL. Two interceptors assigned.

---

### 2.5 Ballistic Missile

**Personality**: Powerful, parabolic, fast reentry. Behaves like a short-range ballistic missile (SRBM) following a burn-coast-reentry trajectory.

**Three physical phases**:

- **Boost phase** (first 60 seconds): A thrust force is applied upward and forward every physics tick, accelerating the missile along a steep climb. Unity's gravity is OFF during this phase — the script drives all motion via force application. The missile climbs rapidly to high altitude.

- **Coast/midcourse phase** (after boost ends): Thrust stops. Unity's gravity is switched ON. The missile follows a natural ballistic arc — it has enough velocity from the boost to continue climbing for a while before gravity pulls it over the top of the parabola.

- **Reentry/terminal phase** (when vertical velocity becomes negative, i.e., it is descending): The missile adds a small aerodynamic drag value to simulate atmospheric resistance during reentry. Speed increases dramatically as gravity accelerates the now-descending warhead. This is when the interceptor must work hardest — the target is fastest and the engagement window is shortest.

**Detection**: Medium RCS (~1 m²), very high speed during reentry (1,000–3,000 m/s). The classifier identifies it solely by speed — nothing else in the simulation goes that fast.

**Threat level**: CRITICAL. Two interceptors assigned.

---

### 2.6 Bird (Non-Threat Control)

**Personality**: Natural, unpredictable, harmless. Behaves as a realistic bird flock using Boids flocking rules.

**Boids behaviour — three simultaneous forces**:

- **Separation**: If another bird is closer than the separation distance threshold, the bird steers away from it. This prevents the flock from collapsing into a single point.

- **Cohesion**: Each bird steers gently toward the average position of all other birds in the flock. This keeps the group together over time.

Both forces are blended (separation weighted 1.5x, cohesion weighted 0.5x) and the resulting direction is normalised and used as the steering target. The bird then smoothly interpolates its current velocity toward this target direction at its configured max speed.

**Important**: A single bird placed alone in the scene will fly in a straight line indefinitely, since there are no other birds for the separation/cohesion forces to act on. Spawn at least 5–8 birds clustered within 50 m of each other for flocking behaviour to emerge.

**Why it matters for the simulation**: Birds have very similar RCS to stealth fighters (~0.001 m²). The radar will detect them. The classifier must distinguish them from stealth fighters — it does this via speed (birds never exceed 25 m/s) and altitude (birds stay below 400 m). The fire control system must then observe that birds receive a `ThreatLevel.None` classification and not launch.

---

## 3. Radar System

### 3.1 Physical Sweep (RadarAntenna)

The radar antenna is a Unity GameObject that rotates continuously at 36 degrees per second (one full rotation every 10 seconds, equivalent to 6 RPM — realistic for a phased-array system operating in search mode).

On every frame, the antenna runs a **detection sweep**:

1. It casts a `Physics.OverlapSphere` of 400 km radius centred on itself — this finds every collider within range.
2. For each hit, it calculates the angle between the antenna's current forward direction and the vector toward the target.
3. If that angle is within the cone half-angle (6°), the target is inside the radar beam at this moment in its rotation.
4. Targets inside the beam are marked `isDetected = true` and `isTracked = true`, and added to the live contacts list.

**Implication for gameplay**: Because the radar sweeps rather than instantly knowing all targets at all times, a target that was inside the beam at 0° will not be inside the beam again until the antenna has completed a full rotation (~10 seconds). During that 10-second gap, the fire control system still uses the last known position and velocity from the previous contact (dead reckoning is implicit — the FCS uses the contact data from the last sweep). A fast-moving target like a ballistic missile may have moved significantly between sweeps, which is why the interceptor uses a time-of-flight prediction rather than the current position.

**Cone angle and its effect**: The 6° half-angle means the radar beam is 12° wide. At 400 km range, this translates to a beam width of approximately 84 km at the edges. This is intentionally generous for a simulation — real S-400 radar beams are much narrower but electronically steered and much faster in practice. The wide cone ensures the simulation doesn't become frustratingly dependent on exact rotation timing.

---

### 3.2 Radar Contact Data

Every target the antenna detects during a sweep produces one `RadarContact` — a data packet containing:

- A reference to the target's script component (so other systems can directly query its state)
- The target's **position** at the moment of detection (a snapshot, not a live feed)
- The target's **velocity** at the moment of detection (used by the FCS for intercept prediction)
- The target's **RCS** value (read from the target's `TargetConfig` asset)
- The **threat level** assigned by the classifier (filled in immediately after the contact is created)

The contacts list is cleared and rebuilt from scratch on every sweep. This means if a target leaves the radar's range or is destroyed between sweeps, it simply won't appear in the next contact list — there is no "track loss" state or memory; the FCS will just stop launching at it.

---

### 3.3 Threat Classifier

The classifier runs immediately after each contact is built, before the contacts list is passed to the fire control system. It is a pure function: the same inputs always produce the same output, with no memory of previous states.

**Classification decision tree** (evaluated in strict order — the first matching rule wins):

1. **Bird check first**: Speed below 25 m/s AND altitude below 400 m AND RCS below 0.01 m² → `ThreatLevel.None`. This gate is checked before anything else to keep birds from accidentally matching any other category.

2. **Ballistic missile**: Speed above 800 m/s → `ThreatLevel.Critical`. Speed alone is sufficient — nothing else in the simulation goes this fast.

3. **Cruise missile**: Altitude below 200 m AND speed above 150 m/s AND RCS below 0.5 m² → `ThreatLevel.Critical`. The altitude floor is the key discriminator — cruise missiles hug the terrain.

4. **Stealth fighter**: RCS below 0.01 m² AND speed above 300 m/s → `ThreatLevel.Critical`. The RCS-speed combination is unique to the fighter (birds share the low RCS but are never fast).

5. **Strategic bomber**: RCS above 50 m² AND altitude above 5,000 m → `ThreatLevel.High`. The enormous RCS makes it trivially identifiable.

6. **UAV/Drone**: Speed below 150 m/s AND altitude below 6,000 m → `ThreatLevel.Medium`. Catch-all for slow, low, small targets.

7. **Fallback**: Anything not matched by the above rules → `ThreatLevel.Low`.

**What the radar does NOT do**: The radar in this simulation cannot be jammed or deceived. It has no false-positive rate, no probability-of-detection curve, and no multi-path effects. RCS values are read directly from the config asset rather than being modelled from aspect angle, frequency, or surface treatment. These are simplifications appropriate for a simulation focused on guidance and control rather than signal processing.

---

## 5. Fire Control System

The fire control system is the decision-making brain between the radar and the missile launchers. It runs continuously in `Update()` (every frame), checking the current contacts list every frame even though that list only updates once per radar sweep.

### 5.1 Engagement Decision

For every contact in the radar's list, the FCS evaluates two conditions:

1. **Threat gate**: Is the threat level anything other than `ThreatLevel.None`? Birds (None) are always skipped regardless of any other condition.

2. **Range gate**: Is the target within 200 km of the S-400 launcher position? Contacts beyond this range are noted but not engaged — they are informational until they close to within engagement range.

3. **Reload gate**: Has enough time elapsed since the last salvo? The default reload interval is 4 seconds. The FCS will not launch a new salvo until this timer has expired, even if a critical threat is inside range. This prevents the system from emptying its magazine in a single burst.

If all three conditions are met, the FCS immediately initiates a salvo against that contact.

### 5.2 Salvo Size

The number of interceptors launched per threat is:

- **CRITICAL** → 2 interceptors (staggered 0.5 seconds apart)
- **HIGH** → 2 interceptors (staggered 0.5 seconds apart)
- **MEDIUM** → 1 interceptor
- **LOW** → 1 interceptor

### 5.3 Launcher Selection

The FCS selects the physically nearest launcher position (from its list of TEL positions) to the target's last known position. This is a simple Euclidean distance comparison across all registered launchers — no line-of-sight check, no reload state per launcher. Every launcher is always considered available.

### 5.4 Launch Execution

Each interceptor in a salvo is spawned at its assigned launcher's position, oriented straight up (vertical launch, as the real S-400 uses cold-launch ejection before the motor fires and the missile pitches over toward the target). The `MissileController` on each spawned interceptor is immediately given a reference to the target's `AerialTarget` component — this is the only information the missile has about its target at launch.

---

## 6. Interceptor Missile — Guidance Behaviour

The interceptor missile's guidance moves through automatically based on the distance to its target.



Immediately after spawning, the missile is oriented straight up. The velocity is set to `transform.forward * currentSpeed`, which at spawn means it is going straight up. This initial vertical orientation is intentional — it models the S-400's vertical cold-launch ejection, after which the guidance pitchover begins.

Once a target is assigned to an interceptor, Proportional Navigation (PN) acts as the guidance system and determines where the missile should fly in order to intercept the target. Rather than pointing directly at the target's current position, PN uses the missile's position and velocity together with the target's position and velocity to predict the interception path and generate a desired turn rate (or desired heading). This allows the missile to lead the target and cut it off rather than chasing behind it. The PID controller then acts as the flight-control system and determines how the missile should turn to achieve the turn rate commanded by PN. It compares the desired turn rate from PN with the missile's actual turn rate and computes the torque required to reduce the error, applying that torque through AddTorque() on the Rigidbody. In other words, PN is responsible for deciding the interception course, while PID is responsible for physically steering the missile along that course. The complete control chain becomes: Radar → Threat Assessment → Target Assignment → PN Guidance → Desired Turn Rate → PID Controller → Torque Command → Rigidbody → Missile Motion, where PN serves as the strategist deciding where to go and PID serves as the pilot ensuring the missile actually follows that command accurately and smoothly.

## 7. Threat Panel (UI)

The threat panel is a scrolling list that reflects the radar's contacts list in real time. 

**Each row shows**: Target name | Threat level | Distance in km | Speed in m/s

**Row pool behaviour**: The panel keeps a pool of text rows. If the contacts list grows (new targets detected), it instantiates new rows into the pool. If the contacts list shrinks (targets destroyed or out of range), excess rows are hidden but not destroyed — they are reused the next time the list grows again. This avoids instantiation overhead each frame.

**Colours**: Each row's text colour matches the threat level colour scheme used on the radar scope — red for CRITICAL, orange for HIGH, yellow for MEDIUM, cyan for LOW, green for NONE. A single glance at the panel tells the operator the overall threat picture without reading individual labels.

**What it does not show**: Track history, predicted intercept time, which targets already have interceptors assigned, or how many interceptors are in flight toward each contact. These would be natural next additions.

---

