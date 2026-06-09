# Code Security Plan

## Before coding

- Maintain this threat model.
- Keep dependencies minimal.
- Avoid networking, telemetry, credential capture, macro recording, and raw typed-text logging.

## During implementation

- Review all P/Invoke declarations carefully.
- Keep hook callbacks fast and small.
- Ignore RunHold's own injected events.
- Release held keys on all shutdown paths.
- Store only configured virtual-key bindings and preferences.

## CI

- Enable Dependabot alerts.
- Enable CodeQL scanning.
- Run NuGet audit during restore.
- Run tests on every pull request.

## Before Public Release

- Run `dotnet restore`, `dotnet build`, and `dotnet test`.
- Scan release artifacts with Microsoft Defender.
- Publish checksums.
- Review startup behavior and config file permissions.

## Before public release

- Sign builds if practical.
- Add a privacy note.
- Add game compatibility warnings.
- Run Windows App Certification Kit for any Store-targeted package.
