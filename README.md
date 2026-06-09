# RunHold

RunHold is a small Windows 11 tray utility for games and simple personal automation tasks. It captures the keyboard keys you are physically holding when you activate it, keeps those keys held with synthetic input, and releases them when you stop.

It is meant for cases like holding `W`, `Shift+W`, `W+Space`, or other movement-key combinations during long single-player travel. It can also help with simple personal Windows tasks where a held key is useful. RunHold is not a macro recorder, auto-clicker, competitive advantage tool, or anti-cheat bypass.

Repository: https://github.com/hfunball/RunHold

User guide: [docs/user-guide.md](docs/user-guide.md)

## Current Scope

- Keyboard hold only.
- Target: stable hold for up to three captured keyboard keys.
- A single keyboard or supported mouse-button toggle activates and stops a hold.
- No macro recording or playback.
- No stealth behavior, multiplayer advantage, anti-cheat bypassing, or hidden automation.
- Dark and light UI themes.
- Launches minimized to the tray by default. Start with Windows is optional.

## How It Works

1. Hold the key or keys you want RunHold to take over.
2. Press your toggle trigger. The default trigger is `Home`.
3. Release the physical keys.
4. Press the toggle trigger again, press a held key to take control back, or use `Release All`.

## How It Works

1. Hold the key or keys you want RunHold to take over.
2. Press your toggle trigger. The default trigger is `Home`.
3. Release the physical keys.
4. Press the toggle trigger again, press a held key to take control back, or use `Release All`.

## Tested Game Matrix

- The Planet Crafter on Steam
- Subnautica 2
- Additional games are still being tested.

## Install

For early builds, download the portable ZIP from GitHub Releases:

https://github.com/hfunball/RunHold/releases

Extract the ZIP and run `RunHold.exe`. Settings are stored outside the app folder in `%LOCALAPPDATA%\RunHold\settings.json`, so they should survive replacing the app folder during upgrades.

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

- GitHub Releases can use `scripts/publish-github.ps1` to produce a self-contained Windows ZIP and checksum.
- Microsoft Store submission should use an MSIX package after the Store identity is reserved.
- Pre-publish testing lives in [docs/release/pre-publish-checklist.md](docs/release/pre-publish-checklist.md).
- Store and GitHub listing notes live in [docs/release/store-and-github-listing-notes.md](docs/release/store-and-github-listing-notes.md).

## License

RunHold is licensed under the [MIT License](LICENSE).

## Security Position

RunHold monitors keyboard state and supported mouse trigger buttons, and it injects keyboard input, so it is treated as security-sensitive. It does not include networking, telemetry, credential capture, macro recording, or raw typed-text logging.

See [SECURITY.md](SECURITY.md), [docs/security/threat-model.md](docs/security/threat-model.md), and [docs/security/security-plan.md](docs/security/security-plan.md).

## Contributing

Issues and focused pull requests are welcome once the repository is public. See [CONTRIBUTING.md](CONTRIBUTING.md).
