# RunHold Threat Model

## Security-sensitive behavior

RunHold monitors keyboard state and supported mouse trigger buttons, and it injects keyboard input. That means it must behave like a visible local utility, not hidden automation.

## Primary risks

- Capturing private typed text.
- Injecting unexpected input into the wrong application.
- Leaving synthetic keys stuck down.
- Tampered config changing hotkeys or startup behavior.
- Distribution files being modified after release.
- Users misunderstanding game compatibility or anti-cheat risk.

## Mitigations

- Track only virtual-key down/up state, not typed text strings.
- Do not log raw typed input.
- Exclude networking and telemetry.
- Provide tray-visible status and `Release All`.
- Release held keys on stop, exit, crash-handled shutdown, and startup cleanup.
- Store minimal JSON config in the user's local app data.
- Keep startup opt-in and user-visible.
- Scan dependencies and builds before release.
- Document single-player scope and compatibility limits.
