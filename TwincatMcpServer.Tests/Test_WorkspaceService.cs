using Microsoft.Extensions.Options;
using System.Text.Json;
using TwincatMcpServer.TwinCat;
using TwincatMcpServer.Workspace;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_WorkspaceService
{
    [Test]
    public async Task Test_ListReadAndSearch_StayInsideWorkspace()
    {
        string root = CreateTempWorkspace();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            await File.WriteAllTextAsync(Path.Combine(root, "src", "Main.cs"), "class Main\n{\n    string name = \"TwinCAT\";\n}\n");
            await File.WriteAllTextAsync(Path.Combine(root, "README.md"), "hello\n");

            WorkspaceService service = CreateService(root);

            object listed = await service.ListFilesAsync(null, "**/*.cs", recursive: true, includeDirectories: false, maxResults: 10);
            object read = await service.ReadFileAsync("src/Main.cs", startLine: 2, lineCount: 2, maxBytes: 1000);
            object searched = await service.SearchTextAsync("TwinCAT", null, "*.cs", regex: false, caseSensitive: true, maxResults: 10);

            string listedText = JsonSerializer.Serialize(listed);
            string readText = JsonSerializer.Serialize(read);
            string searchedText = JsonSerializer.Serialize(searched);

            Assert.That(listedText, Does.Contain("src/Main.cs"));
            Assert.That(readText, Does.Contain("string name"));
            Assert.That(searchedText, Does.Contain("TwinCAT"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Test_ReadFile_RejectsPathOutsideWorkspace()
    {
        string root = CreateTempWorkspace();
        try
        {
            WorkspaceService service = CreateService(root);

            Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.ReadFileAsync("..\\outside.txt", startLine: 1, lineCount: null, maxBytes: 1000));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Test_WriteFile_DoesNotOverwriteUnlessAllowed()
    {
        string root = CreateTempWorkspace();
        try
        {
            string path = Path.Combine(root, "notes.txt");
            await File.WriteAllTextAsync(path, "first");
            WorkspaceService service = CreateService(root);

            Assert.ThrowsAsync<IOException>(() =>
                service.WriteFileAsync("notes.txt", "second", overwrite: false, expectedSha256: null, createDirectories: true));

            object result = await service.WriteFileAsync("notes.txt", "second", overwrite: true, expectedSha256: null, createDirectories: true);

            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("second"));
            Assert.That(JsonSerializer.Serialize(result), Does.Contain("\"overwritten\":true"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task Test_ReplaceText_UsesExpectedReplacementGuard()
    {
        string root = CreateTempWorkspace();
        try
        {
            string path = Path.Combine(root, "plc.txt");
            await File.WriteAllTextAsync(path, "foo\nfoo\n");
            WorkspaceService service = CreateService(root);

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ReplaceTextAsync("plc.txt", "foo", "bar", replaceAll: true, expectedReplacements: 1));

            await service.ReplaceTextAsync("plc.txt", "foo", "bar", replaceAll: true, expectedReplacements: 2);

            Assert.That(await File.ReadAllTextAsync(path), Is.EqualTo("bar\nbar\n"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static WorkspaceService CreateService(string root)
    {
        return new WorkspaceService(Options.Create(new TwinCatAutomationOptions
        {
            WorkspaceRoot = root
        }));
    }

    private static string CreateTempWorkspace()
    {
        string root = Path.Combine(Path.GetTempPath(), "twincat-mcp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
