using RunHold.Models;
using RunHold.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RunHold.Tests;

[TestClass]
public sealed class RunHoldEngineTests
{
    private const int A = 0x41;
    private const int S = 0x53;
    private const int W = 0x57;
    private const int Space = 0x20;
    private const int PageDown = 0x22;
    private const int Home = 0x24;

    [TestMethod]
    public void Toggle_CapturesHeldKeysAndReassertsAfterPhysicalRelease()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(W)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Down(Space)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(Home)));

        CollectionAssert.AreEquivalent(new[] { Space, W }, sender.DownKeys);
        CollectionAssert.AreEquivalent(new[] { Space, W }, engine.Status.HeldKeys.ToArray());
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Up(Space)));

        Assert.AreEqual(2, sender.DownCount(W));
        Assert.AreEqual(2, sender.DownCount(Space));
        Assert.AreEqual(0, sender.UpCount(W));
        Assert.AreEqual(0, sender.UpCount(Space));
    }

    [TestMethod]
    public void Toggle_PressesReleaseHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));
        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        engine.HandleKeyboardEvent(Up(Home));

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(Home)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void Toggle_IgnoresActivationWhenNoKeysAreHeld()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(Home)));

        Assert.IsFalse(engine.Status.IsActive);
        Assert.IsEmpty(sender.DownKeys);
        Assert.IsEmpty(sender.UpKeys);
    }

    [TestMethod]
    public void Toggle_CanHoldKeysThatUsedToBeStopKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(PageDown));
        engine.HandleKeyboardEvent(Down(Home));

        CollectionAssert.Contains(sender.DownKeys, PageDown);
        Assert.IsTrue(engine.Status.IsActive);
    }

    [TestMethod]
    public void MouseToggle_CapturesAndReleasesHeldKeys()
    {
        var sender = new RecordingInputSender();
        var settings = new AppSettings { ToggleBinding = InputBinding.Mouse(MouseTriggerCode.XButton1) };
        using var engine = CreateEngine(settings, sender);

        engine.HandleKeyboardEvent(Down(W));
        Assert.IsTrue(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false)));
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(2, sender.DownCount(W));
        Assert.IsTrue(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void MouseToggle_IgnoresOtherMouseButtons()
    {
        var sender = new RecordingInputSender();
        var settings = new AppSettings { ToggleBinding = InputBinding.Mouse(MouseTriggerCode.XButton1) };
        using var engine = CreateEngine(settings, sender);

        engine.HandleKeyboardEvent(Down(W));

        Assert.IsFalse(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton2, true, false)));

        Assert.IsFalse(engine.Status.IsActive);
        Assert.IsEmpty(sender.DownKeys);
    }

    [TestMethod]
    public void StableHold_CapturesThreeKeysAndReassertsAfterPhysicalRelease()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(A));
        engine.HandleKeyboardEvent(Down(S));
        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        CollectionAssert.AreEquivalent(new[] { A, S, W }, sender.DownKeys);
        CollectionAssert.AreEquivalent(new[] { A, S, W }, engine.Status.HeldKeys.ToArray());

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(A)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Up(S)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));

        Assert.AreEqual(2, sender.DownCount(A));
        Assert.AreEqual(2, sender.DownCount(S));
        Assert.AreEqual(2, sender.DownCount(W));
        Assert.AreEqual(0, sender.UpCount(W));
    }

    [TestMethod]
    public void StableHold_HeldKeyTapCancelsWithPhysicalHandoff()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(2, sender.DownCount(W));

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(W)));

        Assert.AreEqual(3, sender.DownCount(W));
        Assert.AreEqual(0, sender.UpCount(W));
        Assert.IsFalse(engine.Status.IsActive);

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(0, sender.UpCount(W));
    }

    [TestMethod]
    public void StableHold_NonHeldKeyPressDoesNotCancelByDefault()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(2, sender.DownCount(W));

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(A)));

        Assert.AreEqual(0, sender.UpCount(W));
        Assert.IsTrue(engine.Status.IsActive);
    }

    [TestMethod]
    public void StableHold_NonHeldKeyPressCanCancelWhenEnabled()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings { StopOnAnyKeyboardPress = true }, sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(2, sender.DownCount(W));

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(A)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void TriggerCapture_CapturesKeyboardTrigger()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);
        InputBinding? captured = null;
        engine.ToggleTriggerCaptured += (_, binding) => captured = binding;

        engine.BeginToggleTriggerCapture();

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(A)));

        Assert.IsNotNull(captured);
        Assert.AreEqual(InputDeviceKind.Keyboard, captured.Device);
        Assert.AreEqual(A, captured.Code);
    }

    [TestMethod]
    public void TriggerCapture_CapturesMouseTrigger()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);
        InputBinding? captured = null;
        engine.ToggleTriggerCaptured += (_, binding) => captured = binding;

        engine.BeginToggleTriggerCapture();

        Assert.IsTrue(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false)));

        Assert.IsNotNull(captured);
        Assert.AreEqual(InputDeviceKind.Mouse, captured.Device);
        Assert.AreEqual((int)MouseTriggerCode.XButton1, captured.Code);
    }

    [TestMethod]
    public void History_RecordsHeldCombosAndDiagnosticsSkipOrdinaryKeyEvents()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);
        var diagnostics = new List<string>();
        var holds = new List<HoldHistoryEntry>();
        engine.DiagnosticLogged += (_, entry) => diagnostics.Add(entry.Message);
        engine.HoldRecorded += (_, entry) => holds.Add(entry);

        engine.HandleKeyboardEvent(Down(A));
        engine.HandleKeyboardEvent(Up(A));

        Assert.IsEmpty(diagnostics);
        Assert.IsEmpty(holds);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));
        engine.HandleKeyboardEvent(Up(W));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.AreEqual(1, holds.Count);
        CollectionAssert.AreEquivalent(new[] { W }, holds[0].HeldKeys.ToArray());
        CollectionAssert.Contains(diagnostics, "Toggle trigger pressed: Home.");
        Assert.IsTrue(diagnostics.Any(message => message.StartsWith("Released all keys: Toggle release", StringComparison.Ordinal)));
        Assert.IsFalse(diagnostics.Any(message => message.StartsWith("Down:", StringComparison.Ordinal)));
        Assert.IsFalse(diagnostics.Any(message => message.StartsWith("Up:", StringComparison.Ordinal)));
    }

    private static KeyboardInputEvent Down(int key)
    {
        return new KeyboardInputEvent(key, true, false, false);
    }

    private static KeyboardInputEvent Up(int key)
    {
        return new KeyboardInputEvent(key, false, false, false);
    }

    private static RunHoldEngine CreateEngine(AppSettings settings, RecordingInputSender sender)
    {
        return new RunHoldEngine(settings, sender);
    }

    private sealed class RecordingInputSender : IInputSender
    {
        private readonly object gate = new();
        private readonly List<int> downKeys = [];
        private readonly List<int> upKeys = [];

        public int[] DownKeys
        {
            get
            {
                lock (gate)
                {
                    return [.. downKeys];
                }
            }
        }

        public int[] UpKeys
        {
            get
            {
                lock (gate)
                {
                    return [.. upKeys];
                }
            }
        }

        public void SendKeyDown(int virtualKey)
        {
            lock (gate)
            {
                downKeys.Add(virtualKey);
            }
        }

        public void SendKeyUp(int virtualKey)
        {
            lock (gate)
            {
                upKeys.Add(virtualKey);
            }
        }

        public int DownCount(int virtualKey)
        {
            lock (gate)
            {
                return downKeys.Count(key => key == virtualKey);
            }
        }

        public int UpCount(int virtualKey)
        {
            lock (gate)
            {
                return upKeys.Count(key => key == virtualKey);
            }
        }
    }
}
