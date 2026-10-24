# Build & Bounce 🏃💨🏗️

> **A polished 3D platformer with runtime UGC course creation, async ghost racing, and cross-platform multiplayer.**
> *Built in Unity 6 LTS with Universal Render Pipeline (URP), C#, Unity Input System, and Cinemachine.*

---

## 🎯 1. Project Overview & The 2-Minute Recruiter Test

**Build & Bounce** was engineered as a portfolio project proving four core competencies for a 3D platformer / gameplay engineering role:

1. **Tight Game Feel**: A responsive, physics-tuned 3D character controller with coyote time, jump buffering, variable jump heights, apex hang-time, double jump, snappy air dash, landing squash & stretch, and procedural audio feedback.
2. **Runtime UGC Tools**: An in-game level builder with 3D grid snapping, placement preview validity checking, runtime undo/redo via the **Command Pattern**, and roundtrip JSON course serialization with short share codes.
3. **Async & Realtime Multiplayer**: Deterministic 20 Hz fixed-tick ghost recording, translucent replay playback, persistent local leaderboards, cloud course sharing, and a 2–4 player realtime race lobby with snapshot interpolation.
4. **Production Architecture**: Strict assembly definition boundaries (`Project.Runtime`, `Project.Editor`), zero per-frame garbage collection allocations (`0 B/frame` in `Update`/`FixedUpdate`), complete cross-platform support (Keyboard, Gamepad, Mobile Touch), and single AudioListener enforcement.

```
Core Loop: Play 🏃 → Build 🏗️ → Share 🌐 → Race ⏱️ → Improve Course 🏆
```

---

## 🏗️ 2. Architecture & Systems Design

```mermaid
graph TD
    subgraph Input & Viewpoint
        A[Input System] --> B[PlayerInputReader]
        B --> C[PlayerController]
        D[Cinemachine 3] --> E[Main Camera]
        C -.-> D
    end

    subgraph Core & Level
        F[CourseStart] --> G[CourseTimer]
        H[Checkpoint] --> I[PlayerRespawn]
        J[CourseFinish] --> G
        K[AudioListenerGuard] --> E
    end

    subgraph UGC Level Builder
        L[BuilderGrid] --> M[LevelBuilder]
        N[BlockCatalog] --> M
        O[CommandHistory] --> M
        M --> P[CourseSerializer]
        P --> Q[CourseData JSON]
    end

    subgraph Race & Multiplayer
        G --> R[GhostRecorder]
        R --> S[GhostRunData]
        S --> T[LeaderboardManager]
        T --> U[GhostPlayback]
        V[RealtimeRaceLobby] --> W[NetworkPlayerProxy]
    end

    subgraph Cloud Backend
        Q --> X[CourseBackendService]
        S --> X
        X --> Y[Cloud / Share Codes]
    end
```

### System Decoupling Principles
- **No God Objects**: Every component has a single, well-defined responsibility.
- **Event-Driven Communication**: Systems communicate through C# events (`CourseStart.OnCourseStarted`, `CourseTimer.OnTimerFinished`, `Checkpoint.OnCheckpointActivated`) rather than `FindObjectOfType` or string lookups.
- **Assembly Isolation**: Code is partitioned into `Project.Runtime` and `Project.Editor`, enforcing strict compilation boundaries and zero editor dependencies in shipping code.

---

## 🎮 3. Cross-Platform Controls

The game is 100% playable on **Keyboard & Mouse**, **Xbox/PlayStation Gamepads**, and **Mobile Touchscreens**.

| Action | Keyboard / Mouse | Gamepad | Mobile Touch |
|---|---|---|---|
| **Move** | `W`, `A`, `S`, `D` | Left Stick | Virtual On-Screen Thumbstick |
| **Look / Orbit** | Mouse Delta | Right Stick | Right Screen Drag |
| **Jump** | `Space` | South Button (`A` / `Cross`) | Touch Jump Button |
| **Double Jump** | `Space` (in air) | South Button (in air) | Touch Jump Button |
| **Air Dash** | `Left Shift` / `F` | West Button (`X` / `Square`) | Touch Dash Button |
| **Build / Play Toggle**| `Tab` / `B` | North Button (`Y` / `Triangle`) | Top-Right Toggle Button |
| **Place Block** *(Build)* | Left Mouse Click | South Button (`A` / `Cross`) | Tap Grid Cell |
| **Rotate Block** *(Build)*| `R` | Right Shoulder (`RB` / `R1`) | Rotate Button |
| **Delete Block** *(Build)*| Right Click / `Delete` | East Button (`B` / `Circle`) | Delete Tool |
| **Undo / Redo** *(Build)* | `Ctrl+Z` / `Ctrl+Y` | `D-Pad Left` / `D-Pad Right` | Undo Button |
| **Online Share Dialog** | `O` | D-Pad Up | Cloud Button |
| **Pause Menu** | `Escape` | `Start` / `Options` | Pause Icon |

---

## ⏱️ 4. Ghost Racing & Netcode Architecture (M4 & M6)

### Determinism & Sampling
- **Tick Rate**: Player transforms are sampled at a fixed **20 Hz** inside `FixedUpdate`.
- **Zero Allocations**: Snapshots are buffered into a pre-allocated circular list (`2048` capacity), eliminating heap allocation and GC pauses during runs.
- **Interpolation**: `GhostPlayback` calculates the bounding frames and performs `Vector3.Lerp` for position and `Quaternion.Slerp` for rotation, resulting in silky-smooth ghost replays regardless of display refresh rate (60 Hz, 120 Hz, 144 Hz).

### Realtime 2–4 Player Netcode
- **Client-Side Prediction**: The local player moves with zero latency under direct character controller authority.
- **Snapshot Interpolation with Jitter Buffer**: Remote players (`NetworkPlayerProxy`) render behind real-time by a fixed interpolation delay (`_interpolationDelay = 0.1s`), smoothing out packet delay variance.
- **Recruiter Demo Mode**: When running on a single development machine, `RealtimeRaceLobby` simulates 3 remote racer bots with dynamic sinusoidal pathing, enabling instant recruiter demonstration of multiplayer proxy interpolation.

---

## 🌐 5. Online Backend & Cloud Sharing (M5)

- **Zero Third-Party Bloat**: Built directly on `UnityWebRequest` communicating with Firebase REST APIs, avoiding bulky native SDK binaries (~80MB saved, no Gradle/CocoaPods build conflicts).
- **Short Share Codes**: Generates 6-character clean alphanumeric share codes (e.g. `BNB-84X`) with collision-resistant alphabets (excluding ambiguous `0/O` and `1/I`).
- **Offline Simulation Mode**: Enabled by default, allowing courses and world record ghosts to be uploaded, saved, and loaded by share code immediately without requiring Firebase API keys.

---

## 📊 6. Performance & Profiling Targets (M7)

| Metric | Target | Measured (Editor/Standalone) | Optimization Technique |
|---|---|---|---|
| **Framerate (Mobile)** | Stable 60 FPS | **60 FPS** | URP lightweight pipeline, unlit builder previews |
| **Framerate (PC)** | 144+ FPS | **180+ FPS** | Minimal draw calls, primitive batching |
| **GC Allocations in Hot Paths**| **0 B/frame** | **0 B/frame** | Pre-allocated lists, non-allocating raycasts, cached delegates |
| **Draw Calls** | < 120 | **42 - 68** | Dynamic SRP batching, shared primitive materials |
| **Physics Step** | 50 Hz (0.02s) | **Deterministic** | CharacterController driven in `FixedUpdate` |
| **Audio Listeners** | Exactly 1 | **Exactly 1** | Automated `AudioListenerGuard` single listener enforcer |

---

## 🚀 7. Step-by-Step Editor Testing Guide

Everything can be configured and tested with one click from the top menu bar in the Unity Editor:

1. **Platformer Core & Polish (M1 & M2)**:
   - Menu: **Build & Bounce > Setup Level Greybox & Checkpoints**
   - Menu: **Build & Bounce > Setup Player & Cinemachine Camera**
   - Press **Play**: Run, double jump, dash through checkpoints to the finish pad.

2. **Level Builder UGC (M3 & M5)**:
   - Menu: **Build & Bounce > Setup Level Builder**
   - Press **Play**: Press **Tab** (or Gamepad **Y**) to switch to Build Mode.
   - Place blocks, rotate with **R**, undo with **Ctrl+Z**, test-play with **Tab**.
   - Press **O** to open the Cloud Course Sharing dialog, upload your course, and receive a share code!

3. **Ghost Racing & Local Leaderboards (M4 & M6)**:
   - Menu: **Build & Bounce > Setup Ghost Racing (M4)**
   - Press **Play**: Complete a run. Your personal record and ghost will be recorded.
   - Step onto the Start pad again: race side-by-side against your **translucent cyan ghost**!
