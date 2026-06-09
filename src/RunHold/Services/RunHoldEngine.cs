using RunHold.Models;

namespace RunHold.Services;

public sealed class RunHoldEngine : IDisposable
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

    public RunHoldEngine(AppSettings settings, IInputSender inputSender)
    {
        this.settings = settings;
        this.inputSender = inputSender;
        Status = new HoldStatus(false, Array.Empty<int>(), "Idle");
    }

    public HoldStatus Status { get; private set; }

    public event EventHandler<HoldStatus>? StatusChanged;

    public event EventHandler<DiagnosticEntry>? DiagnosticLogged;

    public event EventHandler<HoldHistoryEntry>? HoldRecorded;

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
        var events = new EngineEvents();

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
                    events.AddDiagnostic($"Toggle trigger pressed: {settings.ToggleBinding.DisplayName}.");
                    ActivateOrReleaseLocked(events);
                    suppress = true;
                }
                else
                {
                    physicalKeysDown.Add(input.VirtualKey);
                    suppress = HandleActiveKeyDownLocked(input.VirtualKey, events);
                }
            }
            else
            {
                if (IsToggleTrigger(input.VirtualKey))
                {
                    events.AddDiagnostic($"Toggle trigger released: {settings.ToggleBinding.DisplayName}.");
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
        }

        Publish(events);
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
        var events = new EngineEvents();

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
                    events.AddDiagnostic($"Toggle trigger pressed: {settings.ToggleBinding.DisplayName}.");
                    ActivateOrReleaseLocked(events);
                }
                else
                {
                    events.AddDiagnostic($"Toggle trigger released: {settings.ToggleBinding.DisplayName}.");
                }

                suppress = true;
            }
        }

        Publish(events);
        if (capturedBinding is not null)
        {
            ToggleTriggerCaptured?.Invoke(this, capturedBinding);
        }

        return suppress;
    }

    public void ReleaseAll(string reason)
    {
        var events = new EngineEvents();
        lock (gate)
        {
            ReleaseAllLocked(reason, allowPhysicalHandoff: false, events);
        }

        Publish(events);
    }

    public void Dispose()
    {
        var events = new EngineEvents();
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            ReleaseAllLocked("Engine dispose", allowPhysicalHandoff: false, events);
            disposed = true;
        }

        Publish(events);
    }

    private void ActivateOrReleaseLocked(EngineEvents events)
    {
        if (heldKeys.Count > 0)
        {
            ReleaseAllLocked("Toggle release", allowPhysicalHandoff: true, events);
            return;
        }

        var snapshot = physicalKeysDown
            .Where(key => !IsToggleTrigger(key))
            .OrderBy(key => key)
            .ToArray();

        if (snapshot.Length == 0)
        {
            events.AddDiagnostic("Activation ignored: no non-trigger keys are currently held.");
            QueueStatusLocked("No keys captured", events);
            return;
        }

        releasedHeldKeys.Clear();
        handoffReadyKeys.Clear();
        foreach (var key in snapshot)
        {
            inputSender.SendKeyDown(key);
            heldKeys.Add(key);
        }

        QueueStatusLocked("Hold active", events);
        events.AddHoldHistory(snapshot);
    }

    private bool HandleActiveKeyDownLocked(int virtualKey, EngineEvents events)
    {
        if (heldKeys.Count == 0)
        {
            return false;
        }

        if (!heldKeys.Contains(virtualKey))
        {
            if (settings.StopOnAnyKeyboardPress)
            {
                ReleaseAllLocked("Canceled by keyboard input", allowPhysicalHandoff: false, events);
            }

            return false;
        }

        if (!releasedHeldKeys.Contains(virtualKey))
        {
            return true;
        }

        handoffReadyKeys.Add(virtualKey);
        ReleaseAllLocked($"Physical key takeover: {VirtualKeyNames.GetName(virtualKey)}", allowPhysicalHandoff: true, events);
        return false;
    }

    private void ReleaseAllLocked(string reason, bool allowPhysicalHandoff, EngineEvents events)
    {
        if (heldKeys.Count == 0)
        {
            releasedHeldKeys.Clear();
            handoffReadyKeys.Clear();
            QueueStatusLocked(reason, events);
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
        QueueStatusLocked(reason, events);
        if (transferredKeys.Count > 0)
        {
            events.AddDiagnostic($"Released all keys: {reason}. Physical hold continued for {string.Join(", ", transferredKeys.Select(VirtualKeyNames.GetName))}.");
        }
        else
        {
            events.AddDiagnostic($"Released all keys: {reason}.");
        }
    }

    private bool IsToggleTrigger(int virtualKey)
    {
        return settings.ToggleBinding.MatchesKeyboard(virtualKey);
    }

    private void QueueStatusLocked(string reason, EngineEvents events)
    {
        Status = new HoldStatus(heldKeys.Count > 0, heldKeys.ToArray(), reason);
        events.Status = Status;
    }

    private void Log(string message)
    {
        DiagnosticLogged?.Invoke(this, new DiagnosticEntry(DateTime.Now, message));
    }

    private void Publish(EngineEvents events)
    {
        if (events.Status is not null)
        {
            StatusChanged?.Invoke(this, events.Status);
        }

        foreach (var diagnostic in events.Diagnostics)
        {
            DiagnosticLogged?.Invoke(this, diagnostic);
        }

        foreach (var hold in events.Holds)
        {
            HoldRecorded?.Invoke(this, hold);
        }
    }

    private sealed class EngineEvents
    {
        private readonly List<DiagnosticEntry> diagnostics = [];
        private readonly List<HoldHistoryEntry> holds = [];

        public HoldStatus? Status { get; set; }

        public IReadOnlyList<DiagnosticEntry> Diagnostics => diagnostics;

        public IReadOnlyList<HoldHistoryEntry> Holds => holds;

        public void AddDiagnostic(string message)
        {
            diagnostics.Add(new DiagnosticEntry(DateTime.Now, message));
        }

        public void AddHoldHistory(IReadOnlyCollection<int> heldKeys)
        {
            holds.Add(new HoldHistoryEntry(DateTime.Now, heldKeys.ToArray()));
        }
    }
}
