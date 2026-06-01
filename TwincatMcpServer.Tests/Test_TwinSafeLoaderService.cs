using Microsoft.Extensions.Options;
using TwincatMcpServer.TwinCat;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinSafeLoaderService
{
    [Test]
    public void Test_NormalizeProjectCrc_AcceptsHexWithOrWithoutPrefix()
    {
        Assert.That(TwinSafeLoaderService.NormalizeProjectCrc("0x2D63"), Is.EqualTo("0x2d63"));
        Assert.That(TwinSafeLoaderService.NormalizeProjectCrc("2D63"), Is.EqualTo("0x2d63"));
    }

    [Test]
    public void Test_NormalizeProjectCrc_RejectsNonHexValue()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            TwinSafeLoaderService.NormalizeProjectCrc("not-a-crc"))!;

        Assert.That(exception.Message, Does.Contain("Project CRC must be hexadecimal"));
    }

    [Test]
    public void Test_RedactArguments_HidesPasswordValue()
    {
        string[] arguments =
        [
            "--gw",
            "192.168.67.254",
            "--user",
            "Administrator",
            "--pass",
            "TwinSAFE",
            "--slave",
            "1004"
        ];

        IReadOnlyList<string> redacted = TwinSafeLoaderService.RedactArguments(arguments);

        Assert.That(redacted, Is.EqualTo(new[]
        {
            "--gw",
            "192.168.67.254",
            "--user",
            "Administrator",
            "--pass",
            "<redacted>",
            "--slave",
            "1004"
        }));
    }

    [Test]
    public void Test_ParseList_ReturnsLogicDevices()
    {
        const string deviceList = """
            Upload: TwinSAFE Logic Devices
            EtherCAT address;FSoE address;type;project crc;name;serial number
            1004;1;EL6910;0x2d63;Safety PLC;1106135
            """;

        IReadOnlyList<TwinSafeLogicDevice> devices = TwinSafeLogicDevice.ParseList(deviceList);

        Assert.That(devices, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(devices[0].EtherCatAddress, Is.EqualTo("1004"));
            Assert.That(devices[0].FsoeAddress, Is.EqualTo("1"));
            Assert.That(devices[0].Type, Is.EqualTo("EL6910"));
            Assert.That(devices[0].ProjectCrc, Is.EqualTo("0x2d63"));
            Assert.That(devices[0].Name, Is.EqualTo("Safety PLC"));
            Assert.That(devices[0].SerialNumber, Is.EqualTo("1106135"));
        });
    }

    [Test]
    public void Test_LoadProject_WhenNotConfirmed_ThrowsBeforeFileOrProcessAccess()
    {
        TwinSafeLoaderService service = new(Options.Create(new TwinCatAutomationOptions()));

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.LoadProjectAsync(
                "192.168.67.254",
                ams: null,
                localAms: null,
                "Administrator",
                "TwinSAFE",
                "1004",
                "C:\\missing\\safety.bin",
                loaderPath: null,
                timeoutMilliseconds: 10000,
                processTimeoutMilliseconds: 120000,
                confirm: false))!;

        Assert.That(exception.Message, Is.EqualTo("Set confirm=true to load a TwinSAFE safety project onto a logic component."));
    }

    [Test]
    public void Test_ActivateProject_WhenNotConfirmed_ThrowsBeforeFileOrProcessAccess()
    {
        TwinSafeLoaderService service = new(Options.Create(new TwinCatAutomationOptions()));

        InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.ActivateProjectAsync(
                "192.168.67.254",
                ams: null,
                localAms: null,
                "Administrator",
                "TwinSAFE",
                "1004",
                "C:\\missing\\safety.bin",
                "0x2d63",
                loaderPath: null,
                timeoutMilliseconds: 10000,
                processTimeoutMilliseconds: 120000,
                confirm: false))!;

        Assert.That(exception.Message, Is.EqualTo("Set confirm=true to activate a TwinSAFE safety project on a logic component."));
    }
}
