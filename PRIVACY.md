# RunHold Privacy Policy

Last updated: June 10, 2026

RunHold runs locally on your Windows PC. It does not include networking code, telemetry, ads, analytics, or user accounts.

## Information RunHold accesses

RunHold accesses keyboard state and supported mouse trigger button state while it is running. It uses that access only to detect the toggle trigger, capture the keys you are physically holding when you activate RunHold, and release keys when you stop a hold.

RunHold stores app settings locally at:

```text
%LOCALAPPDATA%\RunHold\settings.json
```

Settings can include your toggle trigger, theme, startup preference, notification preference, and stop behavior. RunHold also shows in-app history for the current session, including the key combinations RunHold actually held and timestamps. If you turn on diagnostics, the History tab can show trigger, capture, release, settings, and hook messages for troubleshooting. RunHold does not store that history as a separate log file.

## Information RunHold does not collect

RunHold does not collect, transmit, sell, or share personal data.

RunHold does not record raw typed text, passwords, chat messages, documents, browser activity, game content, or screen contents. RunHold does not use cookies, advertising identifiers, analytics services, telemetry services, or cloud services.

## How information is used

RunHold uses keyboard and supported mouse button state only to provide its held-key function. RunHold uses local settings only to remember your preferences between launches.

## Storage and security

RunHold settings stay on your Windows PC in your local app data folder. Those settings are protected by your normal Windows account and file-system permissions. RunHold does not upload settings or history to RunHold, HappyFunBall, GitHub, Microsoft, or any other service.

## Sharing and disclosure

RunHold does not disclose app data to third parties. Microsoft Store, GitHub, and other distribution services may process download, account, crash, or store-listing data under their own privacy policies. RunHold does not receive that data from them.

## User controls

You can change RunHold settings in the app. You can turn diagnostics off, choose whether RunHold starts with Windows, and choose whether notifications are shown.

To remove RunHold's local settings, close RunHold and delete:

```text
%LOCALAPPDATA%\RunHold\settings.json
```

Uninstalling RunHold removes the app. Depending on the install method and Windows behavior, local settings may remain until you delete the settings file.

## Changes to this policy

This policy will be updated if RunHold adds features that change what information it accesses, stores, or transmits.

## Contact

For privacy questions, open an issue at:

https://github.com/hfunball/RunHold/issues
