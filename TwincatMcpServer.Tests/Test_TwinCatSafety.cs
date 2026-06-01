using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinCatSafety
{
    [Test]
    public void Test_RequireConfirmation_WhenConfirmed_DoesNotThrow()
    {
        Assert.DoesNotThrow(() =>
            TwinCatSafety.RequireConfirmation(confirm: true, "activate the TwinCAT configuration"));
    }

    [Test]
    public void Test_RequireConfirmation_WhenNotConfirmed_ThrowsHelpfulMessage()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            TwinCatSafety.RequireConfirmation(confirm: false, "restart TwinCAT"))!;

        Assert.That(exception.Message, Is.EqualTo("Set confirm=true to restart TwinCAT."));
    }
}
