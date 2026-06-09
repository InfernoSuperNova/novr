# Native VR UI Roadmap

## Purpose

This roadmap breaks the native VR UI work into implementation milestones for one larger feature branch. The goal is to move NOVR away from broad screen-space-to-world-space UI conversion for complex pre-flight menus while preserving mouse-first seated VR control.

## Guiding Constraints

- Mouse and keyboard remain first-class controls.
- VR controllers are optional, not required.
- Existing game UI may stay alive when it owns state or side effects.
- Native VR UI should be introduced incrementally behind an experimental config option.
- The first shipping target is the pre-flight menu stack that is most important for headset-plus-mouse play: main menu, settings, single player mission selection, multiplayer browser/hosting, and Workshop browsing.
- Mission editor, encyclopedia replacement, and in-flight menus are not part of the first native UI delivery. Top-level buttons or external links may still delegate to original game actions where a native replacement is not present.
- In-flight menus are out of scope for the first native UI delivery. This includes the tactical map, pause/exit menu while flying, and other gameplay overlays.
- New UI should avoid inherited screen-space Z offsets, clipping masks, and fragile TextMeshPro mask interactions.

## Phase 0: Baseline And Guardrails

Document the current UI conversion path and identify which screens are already patched.

Deliverables:

- Native VR UI proposal document.
- Roadmap document.
- Initial list of target screens and known problem areas.

Acceptance criteria:

- No runtime behavior changes from documentation-only commits.
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

## Phase 2: Main Menu Parent Shell

Build a native parent shell for the main menu to validate layout, input, and lifecycle behavior against the highest-level UI flow. This is the first visible feature slice of the larger non-flight menu replacement.

Initial scope:

- Top-level main menu panel.
- Mouse-first hover/click behavior.
- Native buttons that call existing game actions or delegate to original UI buttons.
- Original main menu kept alive where it owns state or side effects.
- Deep settings, mission selection, and mission editor flows left to later phases.
- In-flight tactical map and pause/exit menu left out of scope.

Deliverables:

- Native main menu parent panel with text and clickable actions.
- Show/hide lifecycle controlled by the native UI root.
- Game action adapter for top-level main menu actions.
- Fallback path to the patched original main menu.

Acceptance criteria:

- Parent main menu renders reliably in VR.
- Mouse click and hover behavior work in headset.
- Original game UI continues to function when native UI is disabled.
- Top-level menu actions either work natively or cleanly delegate to original UI behavior.

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

## Phase 4: Pre-Flight Menu Migration

Move the highest-impact pre-flight/main-menu UI stack from patched original UI to native VR panels.

Implemented order for this branch:

1. Main menu submenus.
2. Settings/options.
3. Single player mission selection.
4. Multiplayer entry flow.
5. Workshop.

Deferred or delegated:

- Mission editor replacement is deferred because it is not a good first-pass VR surface.
- Encyclopedia replacement is deferred; the top-level action can still delegate to the original game action.
- Changelog, development roadmap, and community links remain delegated top-level actions.

Deliverables:

- Native panels for selected menu flows.
- Original UI hidden or bypassed only where the native version is active.
- Config fallback to patched original UI.

Acceptance criteria:

- Users can complete the implemented pre-flight menu flows with headset plus mouse.
- Text rendering does not depend on disabling ancestor masks.
- UI layout does not require per-element Z flattening from the original hierarchy.

Current branch status:

- Native main menu shell implemented.
- Native settings/options implemented, including controls and bindings.
- Native single player mission picker implemented.
- Native multiplayer browser, filters, sorting, hosting, and password join flow implemented.
- Native Workshop browser implemented for missions and aircraft liveries.
- Mission editor, encyclopedia, tactical map, and in-flight pause/exit menus are out of scope.

## Phase 5: In-Flight Menu Research

Treat in-flight menus as a later research milestone because they mix gameplay state, world/map coordinates, cursor math, markers, selection behavior, and pause/exit state.

Deliverables:

- List of in-flight menu surfaces and owners.
- Map interaction requirements.
- Coordinate conversion notes.
- Decision on native map versus RenderTexture-backed map.
- Prototype for cursor-to-map selection.

Acceptance criteria:

- No full tactical map or in-flight pause menu rewrite begins until the interaction model is proven.
- In-flight work remains isolated from pre-flight menu migration.

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

Prefer one cohesive PR for the non-flight native menu stack, with commits organized by milestone:

- Proposal, roadmap, and SDK compatibility fix.
- Native UI scaffolding and input state.
- Main menu parent shell.
- Individual non-flight menu migrations.
- Stabilization and documentation.

Keep in-flight menus, tactical map, and gameplay pause/exit menus out of the first native UI PR.
