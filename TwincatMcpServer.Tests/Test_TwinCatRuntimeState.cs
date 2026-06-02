using TwincatMcpServer.TwinCat;
using TwinCAT.Ads;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinCatRuntimeState
{
    [TestCase("Config", "Config")]
    [TestCase("config", "Config")]
    [TestCase("Run", "Run")]
    [TestCase("run", "Run")]
    public void Test_Parse_AcceptsSupportedStates(string value, string expected)
    {
        Assert.That(TwinCatRuntimeStateParser.Parse(value).ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Test_Parse_RejectsUnsupportedState()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            TwinCatRuntimeStateParser.Parse("FreeRun"))!;

        Assert.That(exception.Message, Does.Contain("Config"));
        Assert.That(exception.Message, Does.Contain("Run"));
    }

    [Test]
    public void Test_ToAdsState_MapsSupportedStates()
    {
        Assert.That(TwinCatRuntimeStateParser.ToAdsState(TwinCatRuntimeState.Config), Is.EqualTo(AdsState.Config));
        Assert.That(TwinCatRuntimeStateParser.ToAdsState(TwinCatRuntimeState.Run), Is.EqualTo(AdsState.Run));
    }

    [Test]
    public void Test_DialogPolicy_ConfirmsConfigRestartOnlyForConfigRequests()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "TwinCAT XAE",
            "Restart TwinCAT System in Config Mode?",
            []);

        TwinCatModalDialogDecision configDecision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToConfig);
        TwinCatModalDialogDecision runDecision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToRun);

        Assert.That(configDecision.ShouldConfirm, Is.True);
        Assert.That(runDecision.ShouldConfirm, Is.False);
        Assert.That(runDecision.BlockReason, Does.Contain("Run mode"));
    }

    [Test]
    public void Test_DialogPolicy_ConfirmsLoadIoDevicesForRuntimeSwitches()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "Load I/O Devices",
            "Load I/O Devices?",
            []);

        TwinCatModalDialogDecision runDecision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToRun);
        TwinCatModalDialogDecision configDecision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToConfig);

        Assert.That(runDecision.ShouldConfirm, Is.True);
        Assert.That(configDecision.ShouldConfirm, Is.True);
        Assert.That(configDecision.BlockReason, Is.Null);
    }

    [Test]
    public void Test_DialogPolicy_ConfirmsExternalFileReloadPrompt()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "TcXaeShell",
            "C:\\Projects\\Machine\\MAIN.TcPOU File has been changed outside the environment. Reload the new file?",
            [
                new TwinCatModalDialogButton(IntPtr.Zero, "Yes"),
                new TwinCatModalDialogButton(IntPtr.Zero, "No"),
                new TwinCatModalDialogButton(IntPtr.Zero, "Cancel")
            ]);

        TwinCatModalDialogDecision generalDecision = TwinCatModalDialogPolicy.Decide(dialog);
        TwinCatModalDialogDecision runtimeDecision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToRun);

        Assert.That(generalDecision.ShouldConfirm, Is.True);
        Assert.That(generalDecision.ShouldBlock, Is.False);
        Assert.That(runtimeDecision.ShouldConfirm, Is.True);
    }

    [Test]
    public void Test_DialogPolicy_BlocksRuntimeDialogsOutsideRuntimeRequest()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "TwinCAT XAE",
            "Restart TwinCAT System in Config Mode?",
            []);

        TwinCatModalDialogDecision decision = TwinCatModalDialogPolicy.Decide(dialog);

        Assert.That(decision.ShouldConfirm, Is.False);
        Assert.That(decision.ShouldBlock, Is.True);
        Assert.That(decision.BlockReason, Does.Contain("outside a runtime-state request"));
    }

    [Test]
    public void Test_DialogPolicy_DeclinesActivateFreeRunForConfigRequests()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "Activate Free Run",
            "Activate Free Run?",
            []);

        TwinCatModalDialogDecision decision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToConfig);

        Assert.That(decision.ShouldConfirm, Is.False);
        Assert.That(decision.ShouldDecline, Is.True);
        Assert.That(decision.ShouldBlock, Is.False);
        Assert.That(decision.BlockReason, Is.Null);
    }

    [Test]
    public void Test_DialogPolicy_DoesNotTreatFreeRunAsRun()
    {
        TwinCatModalDialog dialog = new(
            IntPtr.Zero,
            "Activate Free Run",
            "Activate Free Run?",
            []);

        TwinCatModalDialogDecision decision = TwinCatModalDialogPolicy.Decide(
            dialog,
            TwinCatRuntimeSwitchDirection.ToRun);

        Assert.That(decision.ShouldConfirm, Is.False);
        Assert.That(decision.ShouldDecline, Is.True);
        Assert.That(decision.BlockReason, Does.Contain("Free Run"));
    }
}
