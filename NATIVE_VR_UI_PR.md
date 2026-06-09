# Native VR Menu UI

## Summary

Adds an experimental native VR menu UI path for Nuclear Option's non-flight menus. The new path builds VR-first world-space UI instead of trying to render the game's existing screen-space menu hierarchy directly in VR.

This is gated behind the `Enable Native Menu UI` config value and keeps the existing menu UI alive for state ownership and action dispatch.

This PR was created with the help of AI-assisted coding. The implementation was built, reviewed, and manually tested locally before submission.

## Included

- Experimental native VR menu root under `NOVR/VrUi/Native`.
- Mouse-first VR pointer support for seated headset users.
- Native main menu shell with existing game actions.
- Native single player mission picker.
- Native multiplayer server browser and lobby creation flow.
- Multiplayer server search, sortable columns, password/mod/server type flags, hide full/empty filters, and ping/distance filters.
- Native password prompt for joining passworded multiplayer lobbies.
- Native Workshop browser for missions and aircraft liveries.
- Workshop search, ordering, details, preview image, Steam page, local files, subscribe/unsubscribe, and update-all actions.
- Native settings UI for audio, graphics, gameplay, controls, bindings, HUD, and chat settings.
- Native bindings UI for keyboard, mouse, and assigned controllers.
- Binding filters for device and assigned/unassigned/all actions.
- Binding remap/assign flow through Rewired `InputMapper`.
- Axis invert support for full-axis bindings.
- Original main menu background reuse by cloning the source `Image`/`RawImage` into the native shell.
- Original menu fallback preparation where the native UI should not own the screen.
- SDK compatibility fix for ambiguous `GetValueOrDefault` calls in the mock OpenXR runtime.

## Out Of Scope

- In-flight menus and overlays, including tactical map and exit menu.
- Mission editor replacement.
- Native VR controller laser-pointer input.
- Removing the existing translated UI path.

## Design Notes

The native UI intentionally keeps the game's original menu objects available. The native panels call through `NativeGameActionAdapter`, which invokes existing buttons or menu actions where possible. This avoids duplicating game state logic while still avoiding the current VR rendering problems caused by screen-space-to-world-space conversion, Z offsets, masks, and TextMeshPro clipping behavior.

Mouse remains the primary input target for this PR because most current users are expected to use a headset with mouse and keyboard.

## Testing

Build:

```powershell
dotnet build NOVR.Build\NOVR.Build.csproj -c Release
```

Manual in-game VR testing completed:

- Native menu config appears after launching with the updated plugin.
- Native main menu renders and responds to mouse clicks.
- Exit game action works.
- Single player mission picker opens and launches through existing game flow.
- Settings panel opens from the native menu.
- Multiplayer panel opens from the native menu.
- Multiplayer browser supports search, column sorting, lobby filtering, password prompts, and create-lobby setup.
- Workshop panel opens from the native menu.
- Workshop panel browses missions/liveries, searches current results, cycles ordering, opens Steam pages, and updates/subscribes/unsubscribes through the game's Steam Workshop API.
- Audio, graphics, gameplay, controls, HUD, and chat settings render and update.
- Bindings panel lists keyboard, mouse, and controller bindings.
- Bindings panel can filter by device.
- Bindings panel can filter assigned, unassigned, and all actions.
- Binding remap and unassigned action assignment work.
- Controller/device paging works.

## Review Focus

- Whether the native UI root lifecycle is safe around menu transitions.
- Whether keeping the original UI alive but visually suppressed is acceptable.
- Whether `NativeGameActionAdapter` should stay reflection/button-driven for now or move toward explicit game APIs where available.
- Whether this should stay config-gated for one or more releases before becoming the default VR menu path.
