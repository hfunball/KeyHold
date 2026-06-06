# Beta Test Plan

## Diagnostics

- Confirm Home and any configured toggle key are detected.
- Confirm captured names match Windows virtual-key names.

## Baseline

- Test in Notepad or a visible key-test surface.
- Hold `W`, activate, release physical `W`, and verify continued hold.
- Hold `Shift+W`, activate, release both, and verify continued combo.
- Hold `A+S+W`, activate, release all three physical keys, and verify all three stay logically down until stop.
- Stop with the toggle key, physical held-key handoff, and tray `Release All`.

## Games

- Satisfactory on Steam: movement, sprint/movement, and building use.
- The Planet Crafter on Steam: movement, sprint/movement, and repeated travel/building use.
- Far Cry 6 on Epic: movement and sprint/movement, with higher compatibility risk.

## Safety

- Alt-tab while active.
- Restart KeyHold while active.
- Sleep/wake while active.
- Keyboard disconnect/reconnect.
- Verify no stuck keys after physical held-key handoff, tray `Release All`, or app exit.
