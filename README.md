# KeyHold

KeyHold is a small Windows 11 tray utility for games and simple personal automation tasks. It captures the keyboard keys you are physically holding when you activate it, keeps those keys held with synthetic input, and releases them when you stop.

The beta product name is KeyHold. The early brand board keeps KeyLatch as a backup route and parks KeyRun and PressLatch for now.

## Beta Scope

- Keyboard hold only.
- Beta target: stable hold for up to three captured keyboard keys.
- A single keyboard toggle activates and stops a hold.
- No macro recording or playback in 1.0.
- No stealth behavior, multiplayer advantage, anti-cheat bypassing, or hidden automation.
- Dark and light UI themes.
- Launches minimized to the tray by default.

## Initial Game Matrix

- Satisfactory on Steam
- The Planet Crafter on Steam
- Far Cry 6 on Epic

## Build

Install the .NET 10 Desktop SDK, then run:

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/KeyHold
```

## Security Position

KeyHold monitors keyboard state and injects keyboard input, so it is treated as security-sensitive. It does not include networking, telemetry, credential capture, macro recording, or raw typed-text logging.

See [docs/security/threat-model.md](docs/security/threat-model.md) and [docs/security/security-plan.md](docs/security/security-plan.md).
