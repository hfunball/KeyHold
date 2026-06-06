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
    public void StableHold_NonHeldKeyPressCancelsAndReleasesHeldKeys()
    {
        var sender = new RecordingInputSender();
        using var engine = CreateEngine(new AppSettings(), sender);

        engine.HandleKeyboardEvent(Down(W));
        engine.HandleKeyboardEvent(Down(Home));

        Assert.IsTrue(engine.HandleKeyboardEvent(Up(W)));
        Assert.AreEqual(2, sender.DownCount(W));

        Assert.IsFalse(engine.HandleKeyboardEvent(Down(A)));

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
