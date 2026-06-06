using KeyHold.Models;
using KeyHold.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyHold.Tests;

[TestClass]
public sealed class KeyHoldEngineTests
{
    private const int A = 0x41;
    private const int S = 0x53;
    private const int W = 0x57;
    private const int Space = 0x20;
    private const int Shift = 0x10;
    private const int PageUp = 0x21;
    private const int PageDown = 0x22;
    private const int Home = 0x24;
    private const int F12 = 0x7B;

    [TestMethod]
    public void SeparateKeys_CapturesHeldKeysAndReassertsAfterRelease()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(CreateSeparateKeySettings(), sender);

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(W)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Down(Shift)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(PageUp)));

        CollectionAssert.AreEquivalent(new[] { Shift, W }, sender.DownKeys);
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        Assert.IsEmpty(sender.UpKeys);
    }

    [TestMethod]
    public void SeparateKeys_StopKeyReleasesHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(CreateSeparateKeySettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(PageUp));
        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(PageDown)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void SeparateKeys_StopKeyReleasesHeldKeysWithoutPhysicalRegrab()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(CreateSeparateKeySettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(PageUp));
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(PageDown)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void ToggleMode_DefaultHomeActivationReleasesHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        engine.HandleKeyboardEvent(Up(Home));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(Home)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void ToggleMode_CanHoldSeparateModeStopKey()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(PageDown));
        engine.HandleKeyboardEvent(Down(Home));

        CollectionAssert.Contains(sender.DownKeys, PageDown);
        Assert.IsTrue(engine.Status.IsActive);
    }

    [TestMethod]
    public void MouseTriggerMode_MouseButtonStartsAndStopsHold()
    {
        var sender = new RecordingInputSender();
        var settings = new AppSettings { ActivationMode = ActivationMode.MouseTrigger };
        using var engine = CreateEngine(settings, sender);

        engine.HandleKeyboardEvent(Down(W));
        Assert.IsTrue(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false)));
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        Assert.IsTrue(engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void MouseTriggerMode_CanHoldKeyboardTriggerDefaults()
    {
        var sender = new RecordingInputSender();
        var settings = new AppSettings { ActivationMode = ActivationMode.MouseTrigger };
        using var engine = CreateEngine(settings, sender);

        engine.HandleKeyboardEvent(Down(PageUp));
        engine.HandleKeyboardEvent(Down(PageDown));
        engine.HandleMouseEvent(new MouseInputEvent(MouseTriggerCode.XButton1, true, false));

        CollectionAssert.Contains(sender.DownKeys, PageUp);
        CollectionAssert.Contains(sender.DownKeys, PageDown);
        Assert.IsTrue(engine.Status.IsActive);
    }

    [TestMethod]
    public void EmergencyHotkeyReleasesHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));
        Assert.IsTrue(engine.Status.IsActive);

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(F12)));

        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void StableHold_DefaultDoesNotPulseHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        Thread.Sleep(60);

        Assert.AreEqual(1, sender.DownCount(W));
        Assert.AreEqual(0, sender.UpCount(W));
        Assert.IsTrue(engine.Status.IsActive);
    }

    [TestMethod]
    public void StableHold_CapturesThreeKeysAndReassertsAfterPhysicalRelease()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(CreateSeparateKeySettings(), sender);

        engine.HandleKeyboardEvent(Down(A));
        engine.HandleKeyboardEvent(Down(S));
        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(PageUp));

        CollectionAssert.AreEquivalent(new[] { A, S, W }, sender.DownKeys);
        CollectionAssert.AreEquivalent(new[] { A, S, W }, engine.Status.HeldKeys.ToArray());

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(A)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Up(S)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(A) >= 2));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(S) >= 2));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        Assert.AreEqual(2, sender.DownCount(W));
        Assert.AreEqual(0, sender.UpCount(W));

        Assert.IsTrue(engine.HandleKeyboardEvent(Down(PageDown)));

        CollectionAssert.AreEquivalent(new[] { W, S, A }, sender.UpKeys);
        Assert.IsFalse(engine.Status.IsActive);
    }

    [TestMethod]
    public void StableHold_StopTransfersKeysBackToPhysicalHold()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Space));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Up(Space)));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 2));
        Assert.IsTrue(WaitUntil(() => sender.DownCount(Space) >= 2));
        Assert.AreEqual(2, sender.DownCount(W));
        Assert.AreEqual(2, sender.DownCount(Space));

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(W)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Down(Space)));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(Home)));

        Assert.AreEqual(3, sender.DownCount(W));
        Assert.AreEqual(3, sender.DownCount(Space));
        Assert.AreEqual(0, sender.UpCount(W));
        Assert.AreEqual(0, sender.UpCount(Space));
        Assert.IsFalse(engine.Status.IsActive);

        Assert.IsFalse(engine.HandleKeyboardEvent(Up(W)));
        Assert.IsFalse(engine.HandleKeyboardEvent(Up(Space)));
        Assert.AreEqual(0, sender.UpCount(W));
        Assert.AreEqual(0, sender.UpCount(Space));
    }

    [TestMethod]
    public void RepeatedPressMode_PulsesHeldKeysUntilStop()
    {
        var sender = new RecordingInputSender();
        var settings = new AppSettings
        {
            ActivationMode = ActivationMode.SeparateKeys,
            EnableBinding = InputBinding.Keyboard(PageUp),
            StopBinding = InputBinding.Keyboard(PageDown),
            KeyEmulationMode = KeyEmulationMode.RepeatedPress,
            RepeatedPressIntervalMilliseconds = 10
        };
        using var engine = CreateEngine(settings, sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(PageUp));

        Assert.IsTrue(WaitUntil(() => sender.DownCount(W) >= 3));
        Assert.IsTrue(sender.UpCount(W) >= 1);

        engine.HandleKeyboardEvent(Up(W));
        Assert.IsTrue(engine.HandleKeyboardEvent(Down(PageDown)));

        var downCountAfterStop = sender.DownCount(W);
        Thread.Sleep(40);
        Assert.AreEqual(downCountAfterStop, sender.DownCount(W));
        CollectionAssert.Contains(sender.UpKeys, W);
        Assert.IsFalse(engine.Status.IsActive);
    }

    private static KeyboardInputEvent Down(int key)
    {
        return new KeyboardInputEvent(key, true, false, false);
    }

    private static KeyboardInputEvent Up(int key)
    {
        return new KeyboardInputEvent(key, false, false, false);
    }

    private static KeyHoldEngine CreateEngine(AppSettings settings, RecordingInputSender sender)
    {
        return new KeyHoldEngine(settings, sender);
    }

    private static AppSettings CreateSeparateKeySettings()
    {
        return new AppSettings
        {
            ActivationMode = ActivationMode.SeparateKeys,
            EnableBinding = InputBinding.Keyboard(PageUp),
            StopBinding = InputBinding.Keyboard(PageDown)
        };
    }

    private static bool WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return false;
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
