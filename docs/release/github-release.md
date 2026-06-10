# GitHub Release Path

This path is for early public builds and source users. It does not replace the Microsoft Store path.

## Manual local publish

From the repo root:

```powershell
.\.tools\dotnet\dotnet.exe restore
.\.tools\dotnet\dotnet.exe build --configuration Release --no-restore
.\.tools\dotnet\dotnet.exe test --configuration Release --no-build
.\.tools\dotnet\dotnet.exe restore src\RunHold\RunHold.csproj --runtime win-x64
.\scripts\publish-github.ps1 -Version 1.11
```

Outputs:

- `artifacts\release\RunHold-1.11-win-x64-portable.zip`
- `artifacts\release\RunHold-1.11-win-x64-portable.zip.sha256.txt`

The ZIP is self-contained and includes the .NET runtime. Users should extract the whole ZIP and run `RunHold.exe`.

If you are on a connected machine and want the script to restore as part of publish, use:

```powershell
.\scripts\publish-github.ps1 -Version 1.11 -Restore
```

## GitHub Actions

The `Release Artifacts` workflow can run two ways:

- Manually through `workflow_dispatch`, using a version like `1.11`.
- Automatically when a GitHub Release is published.

When it runs on a published GitHub Release, it builds, tests, creates the portable ZIP, creates a checksum, and uploads both files to the Release.

## User upgrade behavior

For the portable ZIP:

- Close RunHold.
- Download the new ZIP.
- Extract it over the old folder or to a new folder.
- Run `RunHold.exe`.

Settings are stored outside the app folder in `%LOCALAPPDATA%\RunHold\settings.json`.

## Later GitHub improvements

- Sign release binaries.
- Add an installer for Start menu shortcuts and uninstall support.
- Add MSIX plus `.appinstaller` for update checks, if direct-download auto-update becomes important.
- Submit a WinGet manifest after the GitHub release asset URL and package identifier are stable.
