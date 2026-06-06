using KeyHold.Models;

namespace KeyHold.Services;

public sealed class KeyHoldEngine : IDisposable
{
    private const int DefaultRepeatedPressIntervalMilliseconds = 45;
    private const int StableHoldReassertDelayMilliseconds = 1;

    private readonly object gate = new();
    private readonly IInputSender inputSender;
    private readonly HashSet<int> physicalKeysDown = [];
    private readonly HashSet<int> heldKeys = [];
    private readonly HashSet<int> releasedHeldKeys = [];
    private readonly HashSet<int> handoffReadyKeys = [];
    private AppSettings settings;
    private System.Threading.Timer? repeatTimer;
    private bool disposed;
    private bool uiCaptureActive;

    public KeyHoldEngine(AppSettings settings, IInputSender inputSender)
    {
        this.settings = settings;
        this.inputSender = inputSender;
        Status = new HoldStatus(false, Array.Empty<int>(), "Idle");
    }

    public HoldStatus Status { get; private set; }

    public event EventHandler<HoldStatus>? StatusChanged;

    public event EventHandler<DiagnosticEntry>? DiagnosticLogged;

    public void UpdateSettings(AppSettings newSettings)
    {
        lock (gate)
        {
            settings = newSettings;
            UpdateRepeatTimerLocked();
        }

        Log("Settings updated.");
    }

    public void SetUiCaptureActive(bool isActive)
    {
        lock (gate)
        {
            uiCaptureActive = isActive;
        }
    }

    public void LogDiagnostic(string message)
    {
        Log(message);
    }

    public bool HandleKeyboardEvent(KeyboardInputEvent input)
    {
        if (input.IsInjected)
        {
            return false;
        }

        var suppress = false;

        lock (gate)
        {
            if (uiCaptureActive)
            {
                return false;
            }

            if (input.IsDown)
            {
                if (settings.EmergencyBinding.MatchesKeyboard(input.VirtualKey))
                {
                    ReleaseAllLocked("Emergency hotkey", allowPhysicalHandoff: false);
                    LogLocked($"Emergency release: {VirtualKeyNames.GetName(input.VirtualKey)}");
                    return settings.SuppressTriggerInput;
                }

                if (IsEnableTrigger(input.VirtualKey))
                {
                    ActivateOrToggleLocked();
                    suppress = settings.SuppressTriggerInput;
                }
                else if (IsStopTrigger(input.VirtualKey))
                {
                    ReleaseAllLocked("Stop hotkey", allowPhysicalHandoff: true);
                    suppress = settings.SuppressTriggerInput;
                }
                else
                {
                    physicalKeysDown.Add(input.VirtualKey);
                    if (heldKeys.Contains(input.VirtualKey))
                    {
                        if (releasedHeldKeys.Contains(input.VirtualKey))
                        {
                            handoffReadyKeys.Add(input.VirtualKey);
                            suppress = false;
                        }
                        else
                        {
                            suppress = true;
                        }
                    }
                }
            }
            else
            {
                if (IsAnyKeyboardTrigger(input.VirtualKey))
                {
                    suppress = settings.SuppressTriggerInput;
                }
                else
                {
                    physicalKeysDown.Remove(input.VirtualKey);
                }

                if (heldKeys.Contains(input.VirtualKey))
                {
                    releasedHeldKeys.Add(input.VirtualKey);
                    handoffReadyKeys.Remove(input.VirtualKey);
                    if (settings.KeyEmulationMode == KeyEmulationMode.StableHold)
                    {
                        QueueStableHoldReassert(input.VirtualKey);
                    }
                }
            }

            LogLocked($"{(input.IsDown ? "Down" : "Up")}: {VirtualKeyNames.GetName(input.VirtualKey)}{(suppress ? " (suppressed)" : string.Empty)}");
        }

        return suppress;
    }

    public bool HandleMouseEvent(MouseInputEvent input)
    {
        if (input.IsInjected || !settings.MouseTrigger.MatchesMouse(input.Button))
        {
            return false;
        }

        lock (gate)
        {
            if (uiCaptureActive)
            {
                return false;
            }

            if (input.IsDown && settings.ActivationMode == ActivationMode.MouseTrigger)
            {
                ActivateOrToggleLocked();
            }

            LogLocked($"{(input.IsDown ? "Mouse down" : "Mouse up")}: {input.Button}");
            return settings.SuppressTriggerInput;
        }
    }

    public void ReleaseAll(string reason)
    {
        lock (gate)
        {
            ReleaseAllLocked(reason, allowPhysicalHandoff: false);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            ReleaseAllLocked("Engine dispose", allowPhysicalHandoff: false);
            StopRepeatTimerLocked();
            disposed = true;
        }
    }

    private void ActivateOrToggleLocked()
    {
        if ((settings.ActivationMode == ActivationMode.Toggle || settings.ActivationMode == ActivationMode.MouseTrigger) && heldKeys.Count > 0)
        {
            ReleaseAllLocked("Toggle release", allowPhysicalHandoff: true);
            return;
        }

        if (heldKeys.Count > 0)
        {
            ReleaseAllLocked("New hold started", allowPhysicalHandoff: true);
        }

        var snapshot = physicalKeysDown
            .Where(key => !IsAnyKeyboardTrigger(key))
            .OrderBy(key => key)
            .ToArray();

        if (snapshot.Length == 0)
        {
            LogLocked("Activation ignored: no non-trigger keys are currently held.");
            PublishStatusLocked("No keys captured");
            return;
        }

        releasedHeldKeys.Clear();
        handoffReadyKeys.Clear();
        foreach (var key in snapshot)
        {
            inputSender.SendKeyDown(key);
            heldKeys.Add(key);
        }

        UpdateRepeatTimerLocked();
        PublishStatusLocked("Hold active");
        LogLocked($"Holding {string.Join(", ", snapshot.Select(VirtualKeyNames.GetName))}.");
    }

    private void ReleaseAllLocked(string reason, bool allowPhysicalHandoff)
    {
        StopRepeatTimerLocked();
        if (heldKeys.Count == 0)
        {
            releasedHeldKeys.Clear();
            handoffReadyKeys.Clear();
            PublishStatusLocked(reason);
            return;
        }

        var transferredKeys = new List<int>();
        foreach (var key in heldKeys.OrderByDescending(key => key).ToArray())
        {
            if (allowPhysicalHandoff && physicalKeysDown.Contains(key) && handoffReadyKeys.Contains(key))
            {
                inputSender.SendKeyDown(key);
                transferredKeys.Add(key);
                continue;
            }

            inputSender.SendKeyUp(key);
        }

        heldKeys.Clear();
        releasedHeldKeys.Clear();
        handoffReadyKeys.Clear();
        PublishStatusLocked(reason);
        if (transferredKeys.Count > 0)
        {
            LogLocked($"Released all keys: {reason}. Physical hold continued for {string.Join(", ", transferredKeys.Select(VirtualKeyNames.GetName))}.");
        }
        else
        {
            LogLocked($"Released all keys: {reason}.");
        }
    }

    private void UpdateRepeatTimerLocked()
    {
        StopRepeatTimerLocked();
        if (heldKeys.Count > 0 && settings.KeyEmulationMode == KeyEmulationMode.RepeatedPress)
        {
            StartRepeatTimerLocked();
        }
    }

    private void StartRepeatTimerLocked()
    {
        var milliseconds = settings.RepeatedPressIntervalMilliseconds > 0
            ? settings.RepeatedPressIntervalMilliseconds
            : DefaultRepeatedPressIntervalMilliseconds;
        var interval = TimeSpan.FromMilliseconds(milliseconds);
        repeatTimer ??= new System.Threading.Timer(_ => RepeatHeldKeys(), null, interval, interval);
    }

    private void StopRepeatTimerLocked()
    {
        repeatTimer?.Dispose();
        repeatTimer = null;
    }

    private void RepeatHeldKeys()
    {
        lock (gate)
        {
            if (disposed || heldKeys.Count == 0 || settings.KeyEmulationMode != KeyEmulationMode.RepeatedPress)
            {
                return;
            }

            foreach (var key in heldKeys.OrderBy(key => key))
            {
                inputSender.SendKeyUp(key);
                inputSender.SendKeyDown(key);
            }
        }
    }

    private void QueueStableHoldReassert(int virtualKey)
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Thread.Sleep(StableHoldReassertDelayMilliseconds);
            lock (gate)
            {
                if (disposed || !heldKeys.Contains(virtualKey) || settings.KeyEmulationMode != KeyEmulationMode.StableHold)
                {
                    return;
                }

                inputSender.SendKeyDown(virtualKey);
            }
        });
    }

    private bool IsEnableTrigger(int virtualKey)
    {
        return settings.ActivationMode switch
        {
            ActivationMode.SeparateKeys => settings.EnableBinding.MatchesKeyboard(virtualKey),
            ActivationMode.Toggle => settings.EnableBinding.MatchesKeyboard(virtualKey),
            _ => false
        };
    }

    private bool IsStopTrigger(int virtualKey)
    {
        return settings.ActivationMode == ActivationMode.SeparateKeys && settings.StopBinding.MatchesKeyboard(virtualKey);
    }

    private bool IsAnyKeyboardTrigger(int virtualKey)
    {
        if (settings.EmergencyBinding.MatchesKeyboard(virtualKey))
        {
            return true;
        }

        return settings.ActivationMode switch
        {
            ActivationMode.SeparateKeys => settings.EnableBinding.MatchesKeyboard(virtualKey)
                || settings.StopBinding.MatchesKeyboard(virtualKey),
            ActivationMode.Toggle => settings.EnableBinding.MatchesKeyboard(virtualKey),
            _ => false
        };
    }

    private void PublishStatusLocked(string reason)
    {
        Status = new HoldStatus(heldKeys.Count > 0, heldKeys.ToArray(), reason);
        StatusChanged?.Invoke(this, Status);
    }

    private void Log(string message)
    {
        DiagnosticLogged?.Invoke(this, new DiagnosticEntry(DateTime.Now, message));
    }

    private void LogLocked(string message)
    {
        DiagnosticLogged?.Invoke(this, new DiagnosticEntry(DateTime.Now, message));
    }
}
