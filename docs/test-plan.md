# RunHold Test Plan

## Diagnostics

- Confirm Home and supported mouse buttons can be detected as the toggle trigger.
- Confirm captured names match Windows virtual-key or mouse-button names.
- Turn on `Show diagnostics` in History and confirm trigger and release events appear.
- Turn diagnostics off and confirm History shows only actual held-key combinations.

## Baseline

- Test in a visible key-test surface. Notepad can be useful for clues, but text apps are not the primary target.
- Hold `W`, activate, release physical `W`, and verify continued hold.
- Hold `Shift+W`, activate, release both, and verify continued combo.
- Hold `A+S+W`, activate, release all three physical keys, and verify all three stay logically down until stop.
- Stop with the toggle trigger, physical held-key handoff, and tray `Release All`.
- Confirm the tray icon changes while holding.
- Confirm left-clicking the tray icon opens the UI.

## Games

- The Planet Crafter on Steam: long travel with `W`, `W+Space`, and any sprint/movement combo you actually use.
- Subnautica 2: movement, swim or travel behavior, inventory interruption, and returning to physical control.
- Satisfactory on Steam: movement, sprint/movement, build-mode interruption, and returning to physical control.
- Defense Grid 2: older-game movement/input acceptance and clean release.
- Portal 2: older-game movement/input acceptance and clean release.
- Add one higher-risk game or app if useful, then record whether it accepts held synthetic input.

## Safety

- Alt-tab while active.
- Restart RunHold while active.
- Sleep/wake while active.
- Keyboard disconnect/reconnect.
- Verify no stuck keys after physical held-key handoff, tray `Release All`, or app exit.

## Package Test

- Extract the portable ZIP to a new folder.
- Run `RunHold.exe` from that extracted folder.
- Confirm the Read Me tab shows the expected version.
- Confirm settings are stored in `%LOCALAPPDATA%\RunHold\settings.json`.
- Close RunHold, replace the extracted folder with a fresh copy, and confirm settings still load.
- Verify the ZIP checksum matches the `.sha256.txt` file.
