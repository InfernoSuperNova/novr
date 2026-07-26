# George's Nuclear Option settings

Snapshot taken on July 26, 2026 from Steam app `2168680` while Nuclear Option
was running under Proton Experimental.

## Files

- `playerprefs-wine-section.reg` is the exact
  `Software\Shockfront\NuclearOption` section from Proton's `user.reg`. It
  contains every Nuclear Option PlayerPrefs value: Rewired keyboard, mouse,
  gamepad, and HOTAS maps; controller assignments and calibration; graphics;
  audio; HUD; camera; sensitivity; and gameplay preferences.
- `rewired-keybinds.reg` is a portable registry import containing only the 43
  Rewired values. This is the convenient keybind backup.
- `deltawing.novr.cfg` contains the active NOVR settings.
- `deltawing.novr.mcpbridge.cfg` contains the active NOVR MCP bridge settings.

Saves, missions, logs, caches, and Steam Workshop content are deliberately not
included because they are not settings.

## Restore the keybinds on this machine

Close Nuclear Option first, then run this from the repository root:

```bash
WINEPREFIX="$HOME/.local/share/Steam/steamapps/compatdata/2168680/pfx" \
  "$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/files/bin/wine" \
  reg import \
  "Z:\\home\\george\\Documents\\GitHub\\novr\\UserSettings\\NuclearOption\\rewired-keybinds.reg"
```

Launch the game afterward. This restores keyboard, mouse, controller, and HOTAS
bindings without changing graphics or gameplay preferences.

## Restore NOVR settings

With Nuclear Option closed, copy the two `deltawing.*.cfg` files to:

```text
~/.local/share/Steam/steamapps/common/Nuclear Option/BepInEx/config/
```

## Full PlayerPrefs recovery

`playerprefs-wine-section.reg` preserves the exact native Wine representation,
including Nuclear Option's unusual eight-byte numeric PlayerPrefs values.
Wine's normal `reg export` truncates some of those values, so do not convert
this file with `reg export`.

For a full recovery, close Nuclear Option and all processes using its Proton
prefix, back up the prefix's `user.reg`, and replace only its
`[Software\\Shockfront\\NuclearOption]` section with the section in this file.
The active prefix registry is:

```text
~/.local/share/Steam/steamapps/compatdata/2168680/pfx/user.reg
```
