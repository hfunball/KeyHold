using KeyHold.Models;

namespace KeyHold.Services;

public sealed class KeyHoldEngine : IDisposable
{
    private readonly object gate = new();
    private readonly IInputSender inputSender;
    private readonly HashSet<int> physicalKeysDown = [];
    private readonly HashSet<int> heldKeys = [];
    private readonly HashSet<int> releasedHeldKeys = [];
    private readonly HashSet<int> handoffReadyKeys = [];
    private AppSettings settings;
    private bool disposed;
    private bool toggleTriggerCaptureActive;

    public KeyHoldEngine(AppSettings settings, IInputSender inputSender)
    {
        this.settings = settings;
        this.inputSender = inputSender;
        Status = new HoldStatus(false, Array.Empty<int>(), "Idle");
    }

    public HoldStatus Status { get; private set; }

    public event EventHandler<HoldStatus>? StatusChanged;

    public event EventHandler<DiagnosticEntry>? DiagnosticLogged;

    public event EventHandler<InputBinding>? ToggleTriggerCaptured;

    public void UpdateSettings(AppSettings newSettings)
    {
        lock (gate)
        {
            settings = newSettings;
        }

        Log("Settings updated.");
    }

    public void BeginToggleTriggerCapture()
    {
        lock (gate)
        {
            toggleTriggerCaptureActive = true;
        }
    }

    public void CancelToggleTriggerCapture()
    {
        lock (gate)
        {
            toggleTriggerCaptureActive = false;
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
        InputBinding? capturedBinding = null;

        lock (gate)
        {
            if (toggleTriggerCaptureActive)
            {
                suppress = true;
                if (input.IsDown)
                {
                    capturedBinding = InputBinding.Keyboard(input.VirtualKey);
                    toggleTriggerCaptureActive = false;
                }
            }
            else if (input.IsDown)
            {
                if (IsToggleTrigger(input.VirtualKey))
                {
                    ActivateOrReleaseLocked();
                    suppress = true;
                }
                else
                {
                    physicalKeysDown.Add(input.VirtualKey);
                    suppress = HandleActiveKeyDownLocked(input.VirtualKey);
                }
            }
            else
            {
                if (IsToggleTrigger(input.VirtualKey))
                {
                    suppress = true;
                }
                else
                {
                    physicalKeysDown.Remove(input.VirtualKey);
                }

                if (heldKeys.Contains(input.VirtualKey))
                {
                    releasedHeldKeys.Add(input.VirtualKey);
                    handoffReadyKeys.Remove(input.VirtualKey);
                    suppress = true;
                    inputSender.SendKeyDown(input.VirtualKey);
                }
            }

            LogLocked($"{(input.IsDown ? "Down" : "Up")}: {VirtualKeyNames.GetName(input.VirtualKey)}{(suppress ? " (suppressed)" : string.Empty)}");
        }

        if (capturedBinding is not null)
        {
            ToggleTriggerCaptured?.Invoke(this, capturedBinding);
        }

        return suppress;
    }

    public bool HandleMouseEvent(MouseInputEvent input)
    {
        if (input.IsInjected)
        {
            return false;
        }

        var suppress = false;
        InputBinding? capturedBinding = null;

        lock (gate)
        {
            if (toggleTriggerCaptureActive)
            {
                suppress = true;
                if (input.IsDown)
                {
                    capturedBinding = InputBinding.Mouse(input.Button);
                    toggleTriggerCaptureActive = false;
                }
            }
            else if (settings.ToggleBinding.MatchesMouse(input.Button))
            {
                if (input.IsDown)
                {
                    ActivateOrReleaseLocked();
                }

                suppress = true;
            }

            if (suppress)
            {
                LogLocked($"{(input.IsDown ? "Mouse down" : "Mouse up")}: {InputBinding.Mouse(input.Button).DisplayName} (suppressed)");
            }
        }

        if (capturedBinding is not null)
        {
            ToggleTriggerCaptured?.Invoke(this, capturedBinding);
        }

        return suppress;
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
            disposed = true;
        }
    }

    private void ActivateOrReleaseLocked()
    {
        if (heldKeys.Count > 0)
        {
            ReleaseAllLocked("Toggle release", allowPhysicalHandoff: true);
            return;
        }

        var snapshot = physicalKeysDown
            .Where(key => !IsToggleTrigger(key))
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

        PublishStatusLocked("Hold active");
        LogLocked($"Holding {string.Join(", ", snapshot.Select(VirtualKeyNames.GetName))}.");
    }

    private bool HandleActiveKeyDownLocked(int virtualKey)
    {
        if (heldKeys.Count == 0)
        {
            return false;
        }

        if (!heldKeys.Contains(virtualKey))
        {
            if (settings.StopOnAnyKeyboardPress)
            {
                ReleaseAllLocked($"Canceled by {VirtualKeyNames.GetName(virtualKey)}", allowPhysicalHandoff: false);
            }

            return false;
        }

        if (!releasedHeldKeys.Contains(virtualKey))
        {
            return true;
        }

        handoffReadyKeys.Add(virtualKey);
        ReleaseAllLocked($"Physical key takeover: {VirtualKeyNames.GetName(virtualKey)}", allowPhysicalHandoff: true);
        return false;
    }

    private void ReleaseAllLocked(string reason, bool allowPhysicalHandoff)
    {
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

    private bool IsToggleTrigger(int virtualKey)
    {
        return settings.ToggleBinding.MatchesKeyboard(virtualKey);
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
