# Future Recording And Keep-Awake Scope

Macro recording and playback are intentionally outside the current goal.

If keeping the PC awake is the real need, use PowerToys Awake or Windows power APIs such as `SetThreadExecutionState`. RunHold should not fake activity with repeated keyboard playback unless a future use case specifically needs macro behavior.

If recording is added later:

- Show a clear recording state.
- Require explicit save.
- Keep storage local-only.
- Provide an always-visible stop action.
- Avoid logging raw typed text where possible.
- Avoid credential-field capture where detectable.
- Keep the feature disabled by default.
