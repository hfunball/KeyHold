# RunHold User Guide

RunHold keeps selected keyboard keys held until you stop it. It is designed for games and simple personal Windows tasks where holding movement keys gets repetitive.

## Basic Use

1. Start RunHold.
2. Hold the key or keys you want RunHold to keep holding.
3. Press your toggle trigger. The default trigger is `Home`.
4. Release the physical keys.
5. Press the toggle trigger again, press a held key to take control back, or use `Release All`.

Examples:

- Hold `W` for long forward travel.
- Hold `W+Space` for travel that needs forward plus jump, swim, fly, or rise.
- Hold `Shift+W` when a game uses Shift for sprint.
- Hold up to three keys when the game supports that combination.

## What To Expect

- RunHold works best in games and apps that read held keyboard state.
- Text apps, browsers, chat apps, and office apps may behave differently from games.
- Some games, elevated apps, protected processes, anti-cheat tools, mouse software, and existing key bindings may interfere.
- RunHold does not start with Windows by default. You can turn that on in Settings.

## History

History shows only key combinations RunHold actually held, such as `W` or `W + Space bar`.

Turn on `Show diagnostics` only when troubleshooting. Diagnostics can show trigger, release, capture, settings, and hook messages.

## Settings Location

RunHold stores settings here:

```text
%LOCALAPPDATA%\RunHold\settings.json
```

Settings are outside the install folder, so they should survive replacing the portable app folder during upgrades.
