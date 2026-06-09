# Pre-Publish Checklist

Use this before publishing a GitHub Release or submitting to the Microsoft Store.

## Build And Package

- [ ] Confirm `RunHold.slnx` is the current solution.
- [ ] Run `.\.tools\dotnet\dotnet.exe restore RunHold.slnx`.
- [ ] Run `.\.tools\dotnet\dotnet.exe build RunHold.slnx --configuration Release --no-restore`.
- [ ] Run `.\.tools\dotnet\dotnet.exe test RunHold.slnx --configuration Release --no-build`.
- [ ] Run `.\.tools\dotnet\dotnet.exe restore src\RunHold\RunHold.csproj --runtime win-x64`.
- [ ] Run `.\scripts\publish-github.ps1 -Version 1.1`.
- [ ] Confirm `artifacts\release\RunHold-1.1-win-x64-portable.zip` exists.
- [ ] Confirm the SHA-256 file exists and matches the ZIP.

## Portable ZIP Test

- [ ] Extract the ZIP to a clean folder, not the build output folder.
- [ ] Run `RunHold.exe`.
- [ ] Confirm the app icon and tray icon show the RunHold logo.
- [ ] Confirm the Read Me tab shows `Version 1.1`.
- [ ] Confirm the app starts minimized to tray after first-run behavior is handled.
- [ ] Confirm left-clicking the tray icon opens the UI.
- [ ] Confirm Settings changes do not crash the app.
- [ ] Confirm settings persist after closing and reopening.
- [ ] Confirm settings are written to `%LOCALAPPDATA%\RunHold\settings.json`.

## Core Input Tests

- [ ] Hold `W`, press the toggle trigger, release `W`, and confirm `W` stays held.
- [ ] Press the toggle trigger again and confirm `W` releases.
- [ ] Hold `W+Space`, press the toggle trigger, release both, and confirm both stay held.
- [ ] Press the toggle trigger while physically holding `W+Space` again and confirm physical control continues.
- [ ] Hold `Shift+W`, activate, release both, and confirm both stay held.
- [ ] Hold three keys, such as `A+S+W`, activate, release all three, and confirm all three stay held.
- [ ] Press one held key physically and confirm control returns cleanly.
- [ ] Use tray `Release All` and confirm no keys remain stuck.
- [ ] Enable `Stop key hold with any keyboard button press` and confirm another keyboard press stops the hold.
- [ ] Confirm supported mouse buttons can be set as the toggle trigger.

## Game Tests

- [ ] The Planet Crafter: test `W`, `W+Space`, long travel, stop, and physical handoff.
- [ ] Subnautica 2: test movement, swim or travel behavior, inventory interruption, stop, and physical handoff.
- [ ] Satisfactory: test movement, sprint/movement, build-mode interruption, stop, and physical handoff.
- [ ] Record any game where synthetic held keys are ignored or behave differently.

## Safety Tests

- [ ] Alt-tab while RunHold is holding keys, then release.
- [ ] Exit RunHold from the tray while holding keys and confirm keys release.
- [ ] Restart RunHold after a hold and confirm no stale held state.
- [ ] Sleep/wake while holding if you are comfortable testing it.
- [ ] Disconnect/reconnect keyboard if you are comfortable testing it.
- [ ] Confirm History shows only held combos by default.
- [ ] Confirm diagnostics mode is off by default and useful when enabled.

## GitHub Release

- [ ] Confirm the repo is named `RunHold`.
- [ ] Confirm README links point to `https://github.com/hfunball/RunHold`.
- [ ] Create tag `v1.1`.
- [ ] Upload the ZIP and SHA-256 file.
- [ ] Include a short release note with tested games and known limitations.
- [ ] Add GitHub topics such as `windows`, `wpf`, `keyboard`, `tray-app`, `gaming-utility`, `key-hold`, `movement-keys`, and `run-key`.

## Microsoft Store

- [ ] Reserve `RunHold` in Partner Center.
- [ ] Add the Store identity to the MSIX packaging project.
- [ ] Confirm startup behavior for packaged apps, especially the opt-in startup task.
- [ ] Run the Windows App Certification Kit.
- [ ] Confirm keyboard hooks and synthetic input are acceptable for the Store listing and app behavior.
- [ ] Use the listing notes in `docs/release/store-and-github-listing-notes.md`.
