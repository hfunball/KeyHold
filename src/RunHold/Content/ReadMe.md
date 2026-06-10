# RunHold

## Things to know

- RunHold is for games and simple Windows tasks where holding movement keys gets repetitive.
- Hold the keys you want RunHold to take over, then press the toggle trigger. The default trigger is Home.
- The toggle trigger can be a keyboard key, middle mouse button, Mouse Button 4, or Mouse Button 5. Primary left and right mouse buttons are not used as triggers.
- Press the toggle trigger to start and again to stop. In some games, pressing and releasing a held key is the most reliable way to take control back.
- RunHold uses the Windows theme setting by default and launches minimized to the tray by default.
- RunHold does not start with Windows by default in case it conflicts with anti-cheat tools or other software on your PC. You can turn this on in Settings.
- RunHold may not behave like a normal text tool in Notepad, office apps, chat apps, or browsers. Games and Windows apps look at keyboard input in different ways.

## Privacy and security

- RunHold runs locally on your Windows PC.
- RunHold does not collect, transmit, sell, or upload data. It has no networking code and no telemetry.
- RunHold monitors keyboard state and supported mouse trigger buttons only so it can detect the configured toggle and held keys.
- RunHold stores settings locally at %LOCALAPPDATA%\RunHold\settings.json.
- RunHold does not store raw typed text, passwords, chat messages, or game input history.
- History shows only key combinations RunHold actually held. Diagnostics can show trigger, capture, release, settings, and hook messages when you turn it on.

## Open source and license

- Source code and releases: https://github.com/hfunball/RunHold
- RunHold is licensed under the MIT License.
- Copyright (c) 2026 RunHold contributors.
- The full license text is included with the release package and in the source repository.
- To report a bug or security issue, include the RunHold version, Windows version, install method, trigger key or button, held keys, and the game or app you tested.
