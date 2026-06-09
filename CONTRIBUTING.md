# Contributing

RunHold is a small Windows utility, so contributions should stay focused on reliability, clear UI behavior, and safe release paths.

## Good first contributions

- Bug reports with the app version, Windows version, game or app tested, trigger key or button, held keys, and exact steps.
- Focused fixes for held-key reliability, release behavior, tray behavior, settings, packaging, and documentation.
- Small UI refinements that keep the app quiet, readable, and easy to test.

## Pull requests

- Keep changes scoped.
- Run `dotnet build --configuration Release --no-restore`.
- Run `dotnet test --configuration Release --no-build`.
- Avoid adding networking, telemetry, macro recording, stealth behavior, or anti-cheat bypass behavior.

## Local paths

Settings are stored in `%LOCALAPPDATA%\RunHold\settings.json`, outside the install folder.
