# README

This is the current version of the Unity project, without the development history.

See the earlier stages and the full documentation on the main repository here:

<https://github.com/orangespy-arts/head-md-care-the-yellow-building/>

## Technical Overview
- **Engine:** Unity **6000.3.15f1** (Unity 6), URP, new Input System, Unity Toon Shader.
- **Two projects:** the state-machine **core** (GameManager, CameraController, Cat, FloatingText, per-room controllers) was authored in `GreyBoxing/` and is being migrated into `FinalYellowBuilding/`.
**Architecture highlights:**
- `GameManager` — owns the `GameState` enum (`Screensaver / Interactive / Dissolving / Ending`), the idle timeout, completion tracking with deduplication, the `rooms[9]` dissolve array, and the fade-in/fade-out of the UI.
- `CameraController` — screensaver cat-follow push-in, smooth zoom-out on exit, and the dual position/rotation lerp into the ending shot.
- `FloatingText` — world-space TextMeshPro labels with `ShowLine` / `ShowSequence` / `Hide`; dialogue is exposed as `[TextArea] string[]` so French copy can be entered without touching code.
- Rooms implement a shared resettable interface so the whole installation can return cleanly to State 1 on every loop.
### Opening the project
1. Install **Unity 6000.3.15f1** (via Unity Hub).
2. Open `unity/FinalYellowBuilding/` for the exhibition build, or `unity/GreyBoxing/` for the prototype.
3. The main scene lives under `Assets/Scenes/`.
> `Library/`, `Temp/`, `Logs/`, and other generated Unity folders are local build artifacts and should not be relied on across machines.
### Exhibition checklist (before showing)
- Turn **off** `debugSkipScreensaver`; confirm the idle threshold is **45s**.
- Test a **Windows full-screen build** (not just the editor).
- Verify clicks on the on-site touch screen / trackpad.
- Configure auto-launch on boot and rehearse power-loss recovery.
- Run an unattended 30-minute loop and confirm no accumulated errors or memory growth.
