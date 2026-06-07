# Dynamic Map VR Cursor Fix

Branch: `vr-map-input-fix`

This branch targets the Nuclear Option map issue where NOVR's VR cursor can hover/highlight map aircraft, but clicking them is rejected or behaves inconsistently.

## Next Codex Session Handoff

Start here on the Windows/headset machine.

1. Open a shell in the repo or clone the fork:

   ```powershell
   git clone https://github.com/georgeciobanu/novr.git
   cd novr
   git switch vr-map-input-fix
   ```

   If the repo already exists:

   ```powershell
   git fetch george
   git switch vr-map-input-fix
   git pull
   ```

2. Build:

   ```powershell
   dotnet restore
   dotnet build
   ```

3. Install the built plugin:

   ```text
   build-output/plugins/NOVR.dll
   ```

   Replace:

   ```text
   <Nuclear Option install>/BepInEx/plugins/NOVR.dll
   ```

4. Launch Nuclear Option in VR and collect:

   ```text
   <Nuclear Option install>/BepInEx/LogOutput.log
   ```

   Search for:

   ```text
   NOVR DynamicMap cursor:
   ```

5. First test with default branch settings:

   ```ini
   [UI]
   Dynamic Map VR Cursor Fix Enabled = true

   [Diagnostics]
   Dynamic Map VR Cursor Diagnostics Enabled = true
   ```

6. If map clicks still fail, keep diagnostics enabled and capture logs for:
   - hover aircraft, click aircraft;
   - click `Select Aircraft`;
   - one normal 16:9 resolution;
   - one ultrawide/native monitor resolution.

The Mac session committed and pushed this branch but did not produce a reliable DLL because the Mac-side local reference set was missing Unity's JSON serialization module for the vendored OpenXR project. Build on the Windows/game machine is the source of truth.

## What Changed

- `NOVR/VrUi/VrUiPointerState.cs`
  - Stores NOVR's current VR UI screen point and the real mouse point each frame.
- `NOVR/VrUi/VrUiCursor.cs`
  - Publishes the screen point it already sends to the `VirtualMouse`.
- `NOVR/VrUi/HarmonyPatches/DynamicMapVrCursorPatch.cs`
  - Patches `DynamicMap.IsCursorInMapRectangle()` to use NOVR's VR pointer instead of legacy `Input.mousePosition`.
  - Patches `DynamicMap.GetCursorCoordinates()` to convert the VR pointer through the world-space map image.
  - Patches the private `DynamicMap.SelectFromMap()` path so controller-style select uses the VR pointer and does not accidentally select from stale real mouse coordinates.
  - Logs map click diagnostics.
- `NOVR/ModConfiguration.cs`
  - Adds config flags for the fix and diagnostics.

## Config Flags

After first launch, BepInEx should create or update:

`BepInEx/config/deltawing.novr.cfg`

Relevant keys:

```ini
[UI]
Dynamic Map VR Cursor Fix Enabled = true

[Diagnostics]
Dynamic Map VR Cursor Diagnostics Enabled = true
```

Diagnostics default to `true` on this branch for testing. Turn them off after collecting logs.

## Expected Logs

Look in:

`BepInEx/LogOutput.log`

Search for:

```text
NOVR DynamicMap cursor:
```

Useful cases to capture:

- Hover an aircraft, click it, and note whether selection succeeds.
- Click the `Select Aircraft` button if it is still finicky.
- Try one normal 16:9 game/window resolution and one ultrawide/native monitor mode.

Important fields in the log:

- `vrPoint`: NOVR's virtual UI pointer.
- `realMouse`: Unity legacy mouse position.
- `vrInMap`: whether the patched VR pointer is inside the map rectangle.
- `realInMap`: whether the original legacy mouse position is inside the map rectangle.
- `hit`: selected or clicked map icon name if found.
- `screen`: Unity `Screen.width` x `Screen.height`.

If `vrInMap=true` and `realInMap=false`, this branch is fixing the exact mixed-input bug.

## Windows Build

From the repo root:

```powershell
git fetch
git switch vr-map-input-fix
dotnet restore
dotnet build
```

Expected plugin output is under:

```text
build-output/plugins/
```

The main file to install/test is:

```text
build-output/plugins/NOVR.dll
```

If the build fails on `JsonUtility`, make sure the game/Unity managed reference set includes:

```text
NuclearOption_Data/Managed/UnityEngine.JSONSerializeModule.dll
```

The Mac-side repo did not have that module in `lib/mono/modern`, so local build validation stopped at the vendored OpenXR project before producing a DLL.

## Manual Install

Back up the currently installed NOVR DLL, then replace it with the built `NOVR.dll`:

```text
<Nuclear Option install>/BepInEx/plugins/NOVR.dll
```

Keep the same plugin ID (`deltawing.novr`) so BepInEx uses the same config file.

## Test Matrix

1. Start with both flags enabled.
2. Open the map in VR.
3. Hover an aircraft until it highlights or shows tooltip.
4. Left click it.
5. Press the relevant select/control input if you normally use one.
6. Try the `Select Aircraft` button.
7. Repeat at a known-good 16:9 resolution and at the ultrawide resolution.

Then test fallback:

```ini
[UI]
Dynamic Map VR Cursor Fix Enabled = false
```

That should restore original NOVR behavior while keeping diagnostics available if the diagnostics flag remains true.
