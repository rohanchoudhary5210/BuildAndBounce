# Build & Bounce — Project Context for AI Coding Agents

> Put this file at the Unity project root as **both** `CLAUDE.md` (Claude Code) and `AGENTS.md` (Antigravity).
> Every agent must read it fully before making changes.

## 1. Why this project exists

This is a **portfolio project** for a Game Developer role (3D platformer studio, Unity/Unreal, cross-platform PS5/PC/iOS, multiplayer and UGC tools as a plus).
The game must prove four things to a recruiter in under 2 minutes of play:

1. **Game feel**: a tight, polished 3D platformer controller.
2. **UGC tools**: players build their own levels in-game and share them.
3. **Multiplayer**: friends race on shared levels (ghosts first, then realtime).
4. **Production quality**: clean architecture, cross-platform input, profiling, documentation.

The developer (Rohan) must be able to explain every system in an interview. **Code must be readable, simple and well-reasoned, never clever for its own sake.**

## 2. Game concept

**Build & Bounce**: a 3D platformer where players build obstacle courses, share them with a short code, and race friends.

Core loop: **Play → Build → Share → Race → Improve the course.**

- **Play mode**: run, jump, dash through a course from Start to Finish, against a timer.
- **Build mode**: place, rotate and delete blocks on a grid; test-play instantly.
- **Share**: a course saves to JSON and gets a short share code.
- **Race**: race against ghost replays of other players' best runs; later, live 2–4 player races.

Art style: simple, colourful, low-poly or flat-shaded primitives. **Readability over detail.** No paid or third-party art packs.

## 3. Tech stack

| Area | Choice |
|---|---|
| Engine | Unity 6 LTS, URP (Universal 3D template) |
| Language | C# |
| Input | Unity Input System (keyboard + gamepad + touch) |
| Camera | Cinemachine |
| Level greybox | ProBuilder |
| UI | uGUI + TextMeshPro |
| Save format | JSON (JsonUtility or Newtonsoft) |
| Backend (Phase 3) | Firebase (Auth, Firestore/Storage) |
| Multiplayer (Phase 4) | Decide later: Mirror (developer has experience) or Netcode for GameObjects |
| Targets | PC (Windows) + Android/iOS; gamepad support throughout |

**Do not add packages** beyond these without asking the developer first.

## 4. Folder structure

All project code and assets live under `Assets/_Project/`. Never write outside it, except Unity-managed files the editor changes itself.

```
Assets/_Project/
  Scripts/
    Core/        # game state, services, events, save system
    Player/      # controller, animation hooks, respawn
    Level/       # blocks, checkpoints, hazards, moving platforms, start/finish
    Builder/     # build mode: grid, placement, undo/redo, serialization
    Race/        # timer, ghost recording/playback, leaderboard
    Net/         # backend + multiplayer (later phases)
    UI/          # menus, HUD, build-mode UI
    Editor/      # Unity Editor tools (editor-only assembly)
  Prefabs/
  Scenes/        # Boot, MainMenu, Play, Build
  Materials/
  Data/          # ScriptableObjects (block catalog, settings)
  Audio/
```

Use **Assembly Definitions**: `Project.Runtime`, `Project.Editor`, and later `Project.Tests`.

## 5. Coding rules

- Namespace: `BuildAndBounce.<Area>` (for example `BuildAndBounce.Player`).
- One public class per file; file name = class name.
- `[SerializeField] private` fields, not public fields. Expose read-only properties when other code needs data.
- Tunable values (speeds, timings, grid size) go in **ScriptableObjects** or serialized fields with `[Header]` and `[Tooltip]`, never hard-coded magic numbers.
- Prefer **composition**: small components with one job each. No god-objects or "GameManager does everything".
- Communicate between systems with C# events or a small event channel, not `FindObjectOfType` or string lookups.
- No `GameObject.Find`, no `SendMessage`, and no per-frame allocations in hot paths (`Update`, `FixedUpdate`).
- Physics logic in `FixedUpdate` or a deterministic tick when it matters (ghosts and replays need determinism).
- Comment the **why**, not the what. Every non-obvious system gets a short summary comment at the top of the class.
- Input only through the Input System, never `Input.GetKey`. Every action must work on keyboard, gamepad and (where relevant) touch.
- Keep it simple: no extra abstraction "for the future" until a second real use exists.

## 6. Milestones and acceptance criteria

Work **one milestone at a time**. Do not start the next until the developer confirms the current one works in the editor.

### M1: Platformer core
- Character controller: camera-relative movement, accel/decel, jump with height and time-to-apex tuning, coyote time, jump buffer, variable jump height, double jump, dash.
- Cinemachine third-person camera with collision.
- Level pieces: static block, moving platform (player rides it correctly), checkpoint, kill zone/hazard, start and finish.
- Respawn at last checkpoint; run timer from start to finish.
- **Done when**: a ProBuilder test level can be completed start to finish on keyboard and gamepad, and moving platforms carry the player without jitter.

### M2: Game feel and polish
- Squash and stretch, landing dust, jump/land/dash sounds, light camera shake on hard landings.
- HUD: timer, best time, checkpoint indicator.
- Pause menu.
- **Done when**: a 30-second gameplay clip looks and feels polished.

### M3: Level builder (UGC)
- Build mode with a grid and placement preview (valid/invalid colour).
- Place, rotate and delete blocks from a block catalog (ScriptableObject).
- Undo/redo (command pattern).
- Save/load a course to JSON; generate a short share code.
- One button to switch between Build and Play.
- Validation: a course needs exactly one Start and at least one Finish.
- **Done when**: a player can build a course, play it, save it, quit, reload it, and get identical results.

### M4: Ghost racing (async multiplayer)
- Record the player's run as sampled transforms or inputs at a fixed rate.
- Play back the best run as a translucent ghost.
- Local leaderboard per course.
- **Done when**: a player can race their own previous best ghost.

### M5: Online backend
- Firebase auth (anonymous first).
- Upload/download courses by share code; online ghost leaderboard.
- **Done when**: two devices can share a course by code and race each other's ghosts.

### M6: Realtime multiplayer (stretch goal)
- 2–4 players race live on a shared course.
- Document the netcode decisions (authority, interpolation, lag handling) in the README.

### M7: Cross-platform and ship
- Touch controls for mobile; gamepad UI navigation.
- Profile on mobile; target a stable 60 FPS. Record the numbers in the README.
- PC and Android builds on itch.io; gameplay trailer; README with architecture diagram.

## 7. Rules for AI agents

1. **Read this file first.** If a request conflicts with it, say so and ask before proceeding.
2. **Stay in scope.** Only change what the current task needs. No drive-by refactors, renames or reformatting.
3. **One feature per session.** Finish it, explain how to test it in the Unity editor, then stop.
4. **Never touch** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, or `.meta` files of assets you did not create. Do not change `ProjectSettings/` without asking.
5. **Scene and prefab setup**: Unity scenes and prefabs are hard to edit as text. Instead of editing `.unity` or `.prefab` YAML, give clear step-by-step setup instructions for the editor, or write an Editor menu script that builds the setup.
6. **No new packages, no third-party assets, no copied code** from other projects. The developer's own open-source repos may be reused later: Localization-Package and Simple-Analytics (Unity), plus his Firebase experience.
7. **After every change**, report: files created or changed, what each does, how to test it in the editor, and any known limitations.
8. **Explain the design** in 2–4 sentences: why this approach and what the trade-off is. The developer must be able to explain it in an interview.
9. **Remind the developer to commit** before and after each feature.
10. If something is ambiguous, **ask one clear question** instead of guessing.

## 8. Definition of done (every feature)

- Compiles with no errors or warnings.
- Works on keyboard **and** gamepad.
- Tunable values exposed in the Inspector or a ScriptableObject.
- No allocations or `Find` calls in per-frame code.
- Short summary comment on each new class.
- Test steps written down in the agent's report.

## 9. Current status

- [ ] M1 Platformer core ← **start here**
- [ ] M2 Game feel and polish
- [ ] M3 Level builder (UGC)
- [ ] M4 Ghost racing
- [ ] M5 Online backend
- [ ] M6 Realtime multiplayer (stretch)
- [ ] M7 Cross-platform and ship

> Update this checklist as milestones are finished, so every agent knows where the project stands.