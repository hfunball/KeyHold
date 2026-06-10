# RunHold

RunHold is a small Windows tray utility for games and simple Windows tasks where holding movement keys gets repetitive. It captures the keyboard keys you are physically holding when you activate it, keeps those keys held with synthetic input, and releases them when you stop.

It is meant for cases like holding `W`, `Shift+W`, `W+Space`, or other movement-key combinations during long single-player travel. It can also help with simple personal Windows tasks where a held key is useful. RunHold is not a macro recorder, auto-clicker, competitive advantage tool, or anti-cheat bypass.

Repository: https://github.com/hfunball/RunHold

User guide: [docs/user-guide.md](docs/user-guide.md)

## Current Scope

- Keyboard hold only.
- Target: stable hold for up to three captured keyboard keys.
- A single keyboard key or supported mouse button activates and stops a hold. Primary left and right mouse buttons are not used as triggers.
- No macro recording or playback.
- No stealth behavior, multiplayer advantage, anti-cheat bypassing, or hidden automation.
- Uses the Windows theme setting by default, with dark and light theme options.
- Launches minimized to the tray by default.
- Relaunching RunHold while it is already running shows the running splash instead of opening another copy.
- Start with Windows is optional and off by default. If enabled, your toggle trigger may be bound to RunHold and may not function normally in other applications while RunHold is active.

## How It Works

1. Hold the key or keys you want RunHold to take over.
2. Press your toggle trigger. The default trigger is `Home` and can be customized.
3. Release the physical keys.
4. Press the toggle trigger again, press a held key to take control back, or use `Release All`.

## Install

After Microsoft Store publication, the Store package is the recommended install path. Store installs receive updates through the Microsoft Store.

Direct downloads are available from GitHub Releases:

https://github.com/hfunball/RunHold/releases

For the portable ZIP, extract the ZIP and run `RunHold.exe`. ZIP installs update manually by downloading a newer ZIP and replacing the app folder. Settings are stored outside the app folder in `%LOCALAPPDATA%\RunHold\settings.json`, so they should survive folder replacement.

## Build

Install the .NET 10 Desktop SDK, then run:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/RunHold
```

## Release

Release planning lives in [docs/release/release-plan.md](docs/release/release-plan.md).

- Microsoft Store submission uses MSIX packaging after the Store identity is assigned in Partner Center. Store users receive updates through the Microsoft Store.
- GitHub Releases can use `scripts/publish-github.ps1` to produce a self-contained Windows ZIP and checksum. ZIP users update manually.
- Pre-publish testing lives in [docs/release/pre-publish-checklist.md](docs/release/pre-publish-checklist.md).
- Store and GitHub listing notes live in [docs/release/store-and-github-listing-notes.md](docs/release/store-and-github-listing-notes.md).

## License

RunHold is licensed under the [MIT License](LICENSE). Copyright (c) 2026 Hfunball and RunHold contributors.

## Privacy

Privacy policy: [PRIVACY.md](PRIVACY.md)

Public Store URL: https://github.com/hfunball/RunHold/blob/main/PRIVACY.md

## Security Position

RunHold monitors keyboard state and supported mouse trigger buttons, and it injects keyboard input, so it is treated as security-sensitive. It does not include networking, telemetry, credential capture, macro recording, or raw typed-text logging.

See [SECURITY.md](SECURITY.md), [docs/security/threat-model.md](docs/security/threat-model.md), and [docs/security/security-plan.md](docs/security/security-plan.md).

## Contributing

Issues and focused pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).
