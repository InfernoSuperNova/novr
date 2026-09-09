# AGENTS.md

## Project Overview

NOVR (Nuclear Option Virtual Reality Mod) is a reworked version of UUVR (Universal Unity VR) that adds full VR support to the flight combat game [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/).

### Architecture

The mod integrates at multiple levels of the Unity engine:

- **BepInEx plugin** (`NOVR.dll`) — core VR logic: stereo camera, VR UI, input, headset data. Applied via `BepInEx/plugins/NOVR/`.
- **BepInEx preloader patcher** (`NOVR.Patcher.dll`) — replaces Unity XR assemblies and patches `globalgamemanagers` before the game loads. Applied via `BepInEx/patchers/NOVR/`.
- **XR plugin assemblies** — replacements for `Unity.XR.OpenXR.dll`, `Unity.XR.OpenVR.dll`, `Unity.XR.Management.dll` that load as native Unity plugins.

### Key technologies

- **C# 10** (.NET Framework 4.8) — main mod code, XR plugins, patcher, installer (Avalonia), build orchestration
- **C++** (MSVC v143) — XInput proxy DLL (`Uuvr.XInput`)
- **BepInEx 5.4.16** — mod loader with Harmony 2.x IL patching (`Harmony.CreateAndPatchAll`)
- **Mono.Cecil 0.10.4** — assembly rewriting in patcher
- **AssetsTools.NET 2.0.9** — Unity asset file (`globalgamemanagers`) manipulation
- **Avalonia 11.2.6** — GUI installer framework
- **MSBuild** SDK-style projects — build system, invoked via `dotnet build`

### Dependencies

- Nuclear Option game installed (Steam) — game assemblies referenced at build time (`NuclearOption_Data/Managed/`)
- BepInEx 5.x installed in the game directory (`BepInEx/core/`)
- .NET SDK 8.0+ (for net48 builds) / 9.0 (for installer projects)
- Visual Studio 2022 or Build Tools with .NET Framework 4.8 targeting pack

### NuGet feeds (`NuGet.Config`)

```xml
<packageSources>
  <clear />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  <add key="BepInEx" value="https://nuget.bepinex.dev/v3/index.json" protocolVersion="3" />
</packageSources>
```

Use `dotnet restore` before building.

## Project Architecture

Multi-project solution (`NuclearOptionVirtualRealityMod.sln`) with 9 subprojects:

| Project | Target | Description |
|---|---|---|
| **NOVR** | net48 | Main mod plugin — core VR logic, camera, UI, configuration, Harmony patches. Output: `BepInEx/plugins/NOVR/NOVR.dll` |
| **NOVR.Patcher** | net48 | BepInEx preloader patcher — copies XR DLLs to game data, patches `globalgamemanagers`. Output: `BepInEx/patchers/NOVR/NOVR.Patcher.dll` |
| **NOVR.XR.OpenXR** | net48 | OpenXR plugin implementation (replaces `Unity.XR.OpenXR`). Assembly: `Unity.XR.OpenXR.dll` |
| **NOVR.XR.OpenVR** | net48 | OpenVR plugin implementation. Assembly: `Unity.XR.OpenVR.dll` |
| **NOVR.XR.Management** | net48 | XR Management SDK (replaces `Unity.XR.Management`). Assembly: `Unity.XR.Management.dll` |
| **NOVR.Installer** | net9.0 | GUI installer (Avalonia 11.2.6) — detects game, downloads/installs NOVR and BepInEx. Produces self-extracting single-file executables |
| **NOVR.Installer.Sfx** | net9.0 | Windows self-extracting stub for the installer |
| **NOVR.Build** | net9.0 | Build orchestration project (no source code) — defines MSBuild props/targets, references all projects, creates release ZIP |
| **Uuvr.XInput** | C++ (v143) | XInput 1.3 proxy DLL — hooks XInput for VR controller input |
| **NOVR.SteamVR** | net48 | SteamVR integration (stub — `.csproj` exists, no source files yet) |

### Dependency graph

```
NOVR ──> NOVR.XR.OpenXR (private ref)
NOVR.Patcher ──> NOVR.XR.OpenXR, NOVR.XR.Management (ref only, no output copy)
NOVR.XR.OpenXR ──> NOVR.XR.Management
NOVR.XR.OpenVR ──> NOVR.XR.Management
NOVR.SteamVR ──> NOVR.XR.OpenVR, NOVR.XR.OpenXR
NOVR.Build ──> NOVR, NOVR.Patcher (ref only)
NOVR.Installer ──> (standalone)
NOVR.Installer.Sfx ──> (standalone)
Uuvr.XInput ──> (standalone C++ DLL)
```

### MSBuild architecture

- `Directory.Build.props` → imports `NOVR.Build/NOVR.Build.props`
- `Directory.Build.targets` → imports `NOVR.Build/NOVR.Build.targets`
- `NOVR.Build.props` — defines `BuildOutputDir`, `GameLayoutOutputDir`, `NuclearOptionFallbackLibDir`, imports `NOVR.Sources.props`
- `NOVR.Build.targets` — defines three key targets:
  - `BuildProjectReferencesBeforeResolveReferences` — ensures project refs build first
  - `StageGameLayout` — copies build output (excluding `Unity.XR.*.dll`) to `build-output/game/`
  - `DeployToGame` — copies staged layout to the game's BepInEx directory
- `NOVR.Sources.props` — game path auto-detection (Windows + Linux paths), resolves `NuclearOptionManagedDir` and `NuclearOptionBepInExCoreDir`; excludes XR plugin assemblies from game reference list

### Game path auto-detection

Auto-detection passes check `NuclearOption_Data/Managed` exists at each path. Order (first match wins):
1. MSBuild property `NuclearOptionGameDir`
2. Env var `NUCLEAR_OPTION_GAME_DIR`
3. Hardcoded Windows paths (3 locations)
4. Hardcoded Linux paths (3 locations + `$HOME/Locations/NuclearOption/Install`)
5. Falls back to `NUCLEAR_OPTION_NOT_FOUND` — game assemblies unavailable, patcher skips deploy

## Key Source Directories

| Area | Location | Files |
|---|---|---|
| **Main plugin entry** | `NOVR/NOVRPlugin.cs` | BepInEx plugin entry, Harmony patch registration |
| **Core runtime** | `NOVR/Core.cs` | `MonoBehaviour` root — spawns VrCameraManager, NOUIManager; manages physics rate, aircraft tracking |
| **Mod config** | `NOVR/ModConfiguration.cs` | BepInEx config entries |
| **VR Camera system** | `NOVR/VrCamera/` | **10 files** — `VrCamera`, `StereoCamera`, `VrCameraManager`, offset management, state patches (`CameraCockpitStatePatch`, `CameraOrbitStatePatch`, `CameraSelectionStatePatch`, `CameraStateManagerMainCameraPatch`, `TurretVrCameraPatch`), `AdditionalCameraData` |
| **VR UI system** | `NOVR/VrUi/` | `NOUIManager`, `VrUiCursor`, `VrControllerLaser`, `VrControllerInput`, `VrCanvasHitTester`, `UIBehaviorPatcher`, + `UiTranslation/` (2 files: `UITranslationWorldSpace`, `UITranslationBackend`) |
| **XR togglers** | `NOVR/VrTogglers/` | **5 files** — `VrTogglerManager`, `VrToggler` base, `XrPluginToggler`, `XrPluginOpenXrToggler`, `LegacyOpenVrToggler` |
| **Harmony patches** | `NOVR/Patches.cs` | All Harmony postfix/transpiler patches for game classes |
| **Other NOVR** | `NOVR/` | `APIBus`, `NOVRBehaviour`, `NOVRHeadsetData`, `NOVRPoseDriver`, `FollowTarget`, `LayerHelper`, `TypeExtensions`, `UuvrInput`, `KeyboardKey` |
| **OpenXR plugin** | `NOVR.XR.OpenXR/` | **18 files** — `OpenXRLoader`, `OpenXRLoaderBase`, `OpenXRLoaderNoPreInit`, `OpenXRUtility`, `OpenXRRestarter`, `OpenXRRuntime`, input/ features, composition layers, API layers |
| **OpenVR plugin** | `NOVR.XR.OpenVR/` | `OpenVRLoader`, `OpenVRSettings`, `OpenVREvents`, `OpenVRHelpers`, `openvr_api.cs` |
| **XR Management** | `NOVR.XR.Management/` | `XRManagerSettings`, `XRGeneralSettings`, `XRLoader`, `XRLoaderHelper`, `XRConfigurationData`, `XRManagementAnalytics`, `IXRLoaderPreInit` |
| **Patcher** | `NOVR.Patcher/UuvrPatcher.cs` | Assembly rewriting, `globalgamemanagers` patching, XR DLL copy logic |
| **Installer** | `NOVR.Installer/` | Avalonia views, view models, services (game detection, download, install) |
| **XInput DLL** | `Uuvr.XInput/main.cpp` | XInput 1.3 proxy hook |
| **Build orchestrator** | `NOVR.Build/` | `.props`, `.targets`, `.csproj` only — no C# source |

## Reverse Engineering the Game

The game is a closed-source Unity title — NOVR integrates with it via Harmony patches and reflection but has no access to its source. When you need to understand how a game system actually works (UI hierarchy, camera states, input bindings, scene structure), decompile the relevant assemblies.

### Game assemblies

```
<Steam>/steamapps/common/Nuclear Option/NuclearOption_Data/Managed/
├── Assembly-CSharp.dll          # Main game code (gameplay, UI, networking)
├── Assembly-CSharp-firstpass.dll # First-pass code (early Unity callbacks)
├── UnityEngine.*.dll             # Unity engine (don't decompile, just reference)
└── ... (other 3rd-party deps)
```

`<Steam>` is wherever the user installed Steam (e.g. `C:\Program Files (x86)\Steam`, `D:\SteamLibrary`, etc.) — the `steamapps/common/Nuclear Option/` portion is fixed. Resolve from Steam app manifest `libraryfolders.vdf` if unsure.

### Decompiler

Use **ilspycmd** (ICSharpCode.Decompiler CLI). Install once globally:

```powershell
dotnet tool install -g ilspycmd
```

ilspycmd 8.x targets .NET 6. Set `DOTNET_ROLL_FORWARD=Major` to use it on machines that only have .NET 8/9 runtimes (common setup):

```powershell
$env:DOTNET_ROLL_FORWARD = "Major"
ilspycmd <args>
```

Tool lives at `C:\Users\<user>\.dotnet\tools\ilspycmd.exe`. Add `$env:USERPROFILE\.dotnet\tools` to `$env:PATH` to invoke as `ilspycmd`.

### Common invocations

```powershell
# List every class in an assembly (filter with grep for the area you care about)
ilspycmd -l c "D:\...\Assembly-CSharp.dll" | Select-String "Pause|Menu|UI"

# Decompile a single type to stdout — quickest path to understand one class
ilspycmd -t GameplayUI "D:\...\Assembly-CSharp.dll"

# Full decompile to directory (one file per type) for offline searching
ilspycmd -p -o C:\temp\decompiled "D:\...\Assembly-CSharp.dll"
```

The `-t` flag takes the **unqualified type name**. Use `-t "Namespace.TypeName"` for namespaced types. Pair with `| Out-File path.cs` for long output.

### Where to look first

When investigating a feature area, these entry points cover most of what NOVR integrates with:

| Area | Type to decompile |
|---|---|
| In-game UI (pause menu, HUD, settings) | `GameplayUI`, `MessageUI`, `StatusDisplay` |
| Pause menu buttons + flow | `QuitMissionButton`, `RestartMissionButton`, `ButtonController` (`JamesFrowen.ScriptableVariables.UI.ButtonController`) |
| Camera state machine | `CameraStateManager`, `CameraCockpitState`, `CameraSelectionState`, `CameraOrbitState` |
| Network/multiplayer pause | `NuclearOption.Networking.NetworkPause` |
| Cockpit / flight scene | `CombatHUD`, `FlightHud` |
| UI primitives | `JamesFrowen.ScriptableVariables.UI.ButtonController`, `NuclearOption.UI.BaseToggle`, `NuclearOption.UI.SliderText` |

After decompiling, search the output for the GameObject / field / panel name that you need to target from NOVR.

### Tips

- `ilspycmd -t` is faster than full-project decompile and produces readable output for one-off investigation.
- Button-related game UI almost always extends `JamesFrowen.ScriptableVariables.UI.ButtonController` (which `[RequireComponent(typeof(Button))]`), not `UnityEngine.UI.Button` directly — search for `ButtonController` to find click handlers, then follow the parent's `Transform` chain to find the containing panel.
- For UI hierarchy at runtime (when GameObject names are not visible in source), use `Debug.Log` from a temporary MonoBehaviour or add a one-shot hierarchy dump to a Harmony postfix (see `NOVR/Patches/UI/GameplayUIPauseMenuPatch.cs` for the pattern).
- Decompiled output goes stale as the game updates — re-decompile after Steam updates Nuclear Option.

## Setup Commands

All commands run from the repository root. Use PowerShell (Windows) or bash (Linux).

```powershell
# Restore NuGet packages
dotnet restore NuclearOptionVirtualRealityMod.sln

# Build full solution (Release) — produces dist/NOVR.zip + dist/NOVR.Installer-Linux + dist/NOVR.Installer-Win.exe
dotnet build NuclearOptionVirtualRealityMod.sln -c Release

# Build full solution (Debug) — deploys to game BepInEx directory if game found
dotnet build NuclearOptionVirtualRealityMod.sln -c Debug

# Build installer only (includes single-file publish step)
dotnet build NOVR.Installer/NOVR.Installer.csproj -c Release

# Override game path if auto-detection fails
dotnet build -p:NuclearOptionGameDir="path\to\Nuclear Option"

# Override game path via environment variable
$env:NUCLEAR_OPTION_GAME_DIR = "path\to\Nuclear Option"
```

### Build outputs

| Path | Contents |
|---|---|
| `build-output/plugins/` | `NOVR.dll`, `Unity.XR.OpenXR.dll`, `Unity.XR.Management.dll`, etc. |
| `build-output/patchers/` | `NOVR.Patcher.dll`, `CopyToGame/` payload |
| `build-output/game/` | Staged game layout (BepInEx plugin/patcher dirs) — ready for direct copy |
| `dist/NOVR.zip` | Release ZIP (Release config only) |
| `dist/NOVR.Installer-Linux` | Linux self-extracting installer (Release config) |
| `dist/NOVR.Installer-Win.exe` | Windows installer stub (Release config) |

## Development Workflow

1. Edit C# files in any project
2. Build:
   ```bash
   dotnet build -c Debug
   ```
3. Build output is auto-deployed to `Nuclear Option/BepInEx/` (if game directory is detected)
4. Close **Nuclear Option** before building — Windows locks files the patcher needs to replace
5. Launch Nuclear Option from Steam to test

### Testing

**No test projects exist** in this repository. Testing is manual:

1. Build in Debug or Release
2. Launch Nuclear Option from Steam
3. Check log: `Nuclear Option/BepInEx/LogOutput.log`
4. Verify VR camera behavior, UI interaction, controller input in-game
5. For patcher issues, check the BepInEx preloader log at game startup

Key behaviors to verify after changes:
- Stereo camera rendering (both eyes)
- VR UI cursor/laser interaction
- XR runtime detection (OpenVR vs OpenXR toggling)
- Headset position tracking and calibration
- Controller input via XInput proxy

## Code Style Guidelines

### C# conventions

- **C# 10** — file-scoped namespaces (`namespace NOVR;`), `global using` compatible
- **Nullable enabled** (`<Nullable>enable</Nullable>`) across all projects
- **`AllowUnsafeBlocks=true`** in most projects
- **`GenerateAssemblyInfo=false`** — custom `AssemblyInfo.cs` files
- **No debug symbols in Release** — `DebugType=none`
- **`AppendTargetFrameworkToOutputPath=false`** — clean output paths
- **`LangVersion=10`** for net48 projects, `latest` for net9.0 projects
- **No StyleCop or EditorConfig** — no automated formatting enforcement
- **Unity serializable class member ordering**: event functions (`Awake`, `Start`, `Update`) before normal methods, serialized fields in order

### Project patterns

- **Harmony patching**: use `Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly())` in plugin constructor (see `NOVRPlugin.cs:35`). Place all patches in `Patches.cs`.
- **XR assembly replacement**: XR plugin projects output with Unity assembly names (`Unity.XR.OpenXR.dll`, `Unity.XR.Management.dll`) so Unity's engine loads them as native plugins.
- **XR assembly exclusion**: `Unity.XR.*.dll` are excluded from staged game layout and from game managed directory references — they are only produced by the NOVR XR projects.
- **`NOVRBehaviour`** base class: custom `MonoBehaviour`-like pattern used for NOVR component lifecycle.
- **`APIBus`**: central event/message bus pattern.
- **`#if CPP`** preprocessor guard in `NOVRPlugin.cs` for IL2CPP compatibility — normally not active.
- **Assembly assets**: `NOVR/Assets/MainMenuLogo.png` copied to output via `<Content>` in csproj.

### Dictionary entries (add to spell-checker)

`novr`, `Smushi`, `unityexplorer`, `universelib`, `Uuvr`, `BepInEx`, `Togglers`, `Toggler`, `preloader`

## Build and Deployment

### Creating a release

```powershell
dotnet build NuclearOptionVirtualRealityMod.sln -c Release
```

This produces:
- `dist/NOVR.zip` — mod release archive (plugins + patchers)
- `dist/NOVR.Installer-Linux` — Linux self-extracting installer
- `dist/NOVR.Installer-Win.exe` — Windows installer

The installer downloads the latest NOVR release ZIP and installs alongside BepInEx 5.x.

### Installer build process (Release only)

The `NOVR.Installer.csproj` `PublishInstallersToDist` target (after Build):
1. Publishes single-file Linux and Windows Avalonia apps
2. Publishes the SFX stub
3. Packages Windows build as ZIP payload, Linux build as tar.gz payload
4. Concatenates SFX stub + marker + payload for each platform

## NuGet Dependencies

| Package | Version | Used By |
|---|---|---|
| `BepInEx.Core` | 5.4.16 | NOVR.XR.OpenXR, NOVR.XR.Management, NOVR.XR.OpenVR |
| `AssetsTools.NET` | 2.0.9 | NOVR.Patcher |
| `Mono.Cecil` | 0.10.4 | NOVR.Patcher |
| `Avalonia` | 11.2.6 | NOVR.Installer |
| `Avalonia.Desktop` | 11.2.6 | NOVR.Installer |
| `Avalonia.Themes.Fluent` | 11.2.6 | NOVR.Installer |

## PR Guidelines

- Title format: `[Area] Brief description`
- Build the solution in Release before submitting
- Close the game before building or deploying
- No CI/CD workflows exist — `.github/` only has `FUNDING.yml`
- Manual verification required

## Troubleshooting

| Symptom | Cause / Fix |
|---|---|
| **Build fails with game not found** | Set `NuclearOptionGameDir` MSBuild property or `NUCLEAR_OPTION_GAME_DIR` env var |
| **Patcher fails to copy files** | Game is running — close Nuclear Option entirely |
| **BepInEx doesn't load on Linux/Proton** | Configure `winhttp` Wine override to `native,builtin` |
| **XR runtime not detected** | Check which VR runtime is active (OpenVR/OpenXR); verify the corresponding plugin `.dll` is in `BepInEx/plugins/NOVR/` |
| **Mod not loaded** | Check `BepInEx/LogOutput.log` for exceptions; verify `NOVR.dll` exists in `BepInEx/plugins/NOVR/` |
| **`Unity.XR.*.dll` not found** | These are excluded from staged layout — they are produced by NOVR XR projects and copied by the patcher's `CopyXrAssembliesToPatcherPayload` target |
| **Camera not stereoscopic** | Check `VrCamera/VrCamera.cs` and the state patches — each camera state (cockpit, orbit, selection, turret) has its own patch file |

## Important Notes

- **BepInEx 5.x only** — do not use BepInEx 6.x
- The patcher copies XR support files into `NuclearOption_Data` on every game startup
- `lib/mono/modern/` — fallback Unity DLLs used when game directory can't be found
- `lib/Valve.Newtonsoft.Json.dll` — used by NOVR.SteamVR (stub project, no source yet)
- XR plugin assemblies must use Unity's expected filenames (`Unity.XR.OpenXR.dll`, etc.) for Unity to load them as native plugins
- The `NOVR` project references `NOVR.XR.OpenXR` with `Private="false"` — reference only, no output copy
- `NOVR.Patcher` references `NOVR.XR.Management` and `NOVR.XR.OpenXR` with `ReferenceOutputAssembly="false"` — used only for assembly metadata during patching
- The `DeployToGame` target is gated on `NuclearOptionGameDirResolved != NUCLEAR_OPTION_NOT_FOUND`
- `Version.txt` is written by the installer (not the build) to track installed version
