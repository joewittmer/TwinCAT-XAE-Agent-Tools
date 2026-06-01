using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinSafeProjectImportMode
{
    [Test]
    public void Test_Parse_WhenBlank_DefaultsToCopy()
    {
        TwinSafeProjectImportMode mode = TwinSafeProjectImportModeParser.Parse(null);

        Assert.That(mode, Is.EqualTo(TwinSafeProjectImportMode.CopyToSolutionDirectory));
    }

    [TestCase("0", 0)]
    [TestCase("Copy", 0)]
    [TestCase("CopyToSolutionDirectory", 0)]
    [TestCase("1", 1)]
    [TestCase("Move", 1)]
    [TestCase("MoveToSolutionDirectory", 1)]
    [TestCase("2", 2)]
    [TestCase("UseOriginal", 2)]
    [TestCase("UseOriginalLocation", 2)]
    public void Test_Parse_AcceptsAliases(string value, int expectedSubtype)
    {
        TwinSafeProjectImportMode mode = TwinSafeProjectImportModeParser.Parse(value);

        Assert.That(mode.Subtype, Is.EqualTo(expectedSubtype));
    }

    [Test]
    public void Test_Parse_RejectsUnknownMode()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            TwinSafeProjectImportModeParser.Parse("Clone"))!;

        Assert.That(exception.Message, Does.Contain("Import mode must be"));
    }
}
