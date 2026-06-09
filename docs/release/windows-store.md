# Microsoft Store Readiness

RunHold should use MSIX for Microsoft Store submission.

## Why MSIX

Microsoft's current guidance for WPF and WinForms apps is to add a Windows Application Packaging Project when targeting Store submission. The Store handles signing and updates for submitted MSIX packages.

## Decisions for RunHold

- Startup with Windows remains opt-in.
- The packaged startup task should be declared with `Enabled="false"`.
- The Store package must keep the same identity across updates.
- The Store listing should explain that RunHold is intended for games and simple Windows tasks on an individual PC.
- The listing and README should not imply anti-cheat bypassing, stealth behavior, or multiplayer advantage.

## Work still needed

- Reserve the app name in Partner Center.
- Add a Windows Application Packaging Project in Visual Studio.
- Assign the Store identity from Partner Center.
- Add the startup task extension shown in `packaging/msix/Package.appxmanifest.template.xml`.
- Update the app startup service to use the packaged startup task API when running as MSIX.
- Generate Store assets from the approved RunHold logo.
- Run WACK before submission.
- Test a local MSIX install, upgrade, uninstall, and reinstall.

## Store update test

Before public release, test this locally with two package versions:

1. Install package `1.1.0.0`.
2. Configure the toggle trigger, theme, startup setting, and stop-on-any-key setting.
3. Upgrade to package `1.1.1.0`.
4. Confirm settings survived.
5. Confirm startup remains off unless the user opted in.
6. Confirm hooks and held-key behavior still work after upgrade.
