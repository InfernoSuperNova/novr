# Native VR UI Roadmap

## Purpose

This roadmap breaks the native VR UI work into small, reviewable milestones. The goal is to move NOVR away from broad screen-space-to-world-space UI conversion for complex menus while preserving mouse-first seated VR control.

## Guiding Constraints

- Mouse and keyboard remain first-class controls.
- VR controllers are optional, not required.
- Existing game UI may stay alive when it owns state or side effects.
- Native VR UI should be introduced incrementally behind an experimental config option.
- The tactical map should not be the first screen migrated.
- New UI should avoid inherited screen-space Z offsets, clipping masks, and fragile TextMeshPro mask interactions.

## Phase 0: Baseline And Guardrails

Document the current UI conversion path and identify which screens are already patched.

Deliverables:

- Native VR UI proposal document.
- Roadmap document.
- Initial list of target screens and known problem areas.

Acceptance criteria:

- No runtime behavior changes.
- Reviewers can evaluate the intended direction before implementation starts.

## Phase 1: Native UI Scaffolding

Add the minimum runtime structure needed for native VR UI without replacing any existing screens.

Deliverables:

- `NOVR/VrUi/Native` namespace/folder.
- `VrPointerState` abstraction for mouse-driven VR UI input.
- `NativeVrUiRoot` managed by `NOUIManager`.
- Experimental config flag for native UI.
- Basic panel placement in front of the headset or cockpit HUD reference.

Acceptance criteria:

- Existing patched UI remains the default.
- Native UI can be enabled experimentally.
- Mouse click, scroll, and cursor/ray state can be read from one place.
- No existing gameplay UI is replaced yet.

## Phase 2: First Native Panel

Build one small, bounded native panel to validate layout, input, and lifecycle behavior.

Candidate screens:

- Pause/resume panel.
- Simple debug/status panel.
- Small in-flight utility panel.

Deliverables:

- One native panel with text and clickable actions.
- Show/hide lifecycle controlled by the native UI root.
- Game action adapter for the selected screen.

Acceptance criteria:

- Panel renders reliably in VR.
- Mouse click and hover behavior work in headset.
- Original game UI continues to function when native UI is disabled.

## Phase 3: Game State And Action Adapters

Create thin adapters between NOVR UI and Nuclear Option game systems.

Deliverables:

- Game state adapter for commonly displayed state.
- Game action adapter for commonly invoked UI actions.
- Reflection/Harmony access isolated inside adapters where direct calls are not available.

Acceptance criteria:

- Native UI code does not directly scatter reflection calls across widgets.
- Each adapter has a narrow responsibility.
- Missing game references fail gracefully with useful logs.

## Phase 4: Menu Migration

Move selected non-map menus from patched original UI to native VR panels.

Candidate order:

1. Pause menu.
2. Main menu navigation.
3. Settings/options.
4. Aircraft/loadout screens.

Deliverables:

- Native panels for selected menu flows.
- Original UI hidden or bypassed only where the native version is active.
- Config fallback to patched original UI.

Acceptance criteria:

- Users can complete the selected flow with headset plus mouse.
- Text rendering does not depend on disabling ancestor masks.
- UI layout does not require per-element Z flattening from the original hierarchy.

## Phase 5: Tactical Map Research

Treat the tactical map as a separate research milestone because it mixes UI, world/map coordinates, cursor math, markers, and selection behavior.

Deliverables:

- Map interaction requirements.
- Coordinate conversion notes.
- Decision on native map versus RenderTexture-backed map.
- Prototype for cursor-to-map selection.

Acceptance criteria:

- No full tactical map rewrite begins until the interaction model is proven.
- Map work remains isolated from simpler menu migration.

## Phase 6: Stabilization

Harden native UI for repeated play sessions and different headset/mouse setups.

Deliverables:

- Config cleanup.
- Error logging cleanup.
- Input edge case handling.
- Documentation for native UI mode.

Acceptance criteria:

- Native UI can be left enabled for normal play.
- Fallback to patched original UI remains available.
- Known limitations are documented.

## PR Strategy

Prefer small PRs:

- PR 1: proposal and roadmap.
- PR 2: native UI scaffolding and input state.
- PR 3: first native panel.
- PR 4+: individual menu migrations.

Avoid combining scaffolding, multiple menu rewrites, and tactical map work in one PR.
