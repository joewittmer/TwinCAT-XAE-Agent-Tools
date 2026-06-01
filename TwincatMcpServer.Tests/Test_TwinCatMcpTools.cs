using ModelContextProtocol.Server;
using System.Reflection;
using TwincatMcpServer.Mcp;

namespace TwincatMcpServer.Tests;

[Category("Unit")]
public sealed class Test_TwinCatMcpTools
{
    private static readonly string[] ExpectedToolNames =
    [
        "twincat_config",
        "xae_status",
        "xae_attach_or_launch",
        "xae_open_solution",
        "xae_set_active_project",
        "xae_save_solution",
        "xae_close_solution",
        "xae_quit",
        "xae_build_solution",
        "twincat_lookup_tree_item",
        "twincat_produce_xml",
        "twincat_consume_xml",
        "twincat_create_child",
        "twincat_get_target_net_id",
        "twincat_set_target_net_id",
        "twincat_activate_configuration",
        "twincat_restart_runtime",
        "twincat_set_runtime_state",
        "twincat_get_last_error_messages",
        "workspace_info",
        "workspace_list_files",
        "workspace_get_file_info",
        "workspace_read_file",
        "workspace_search_text",
        "workspace_write_file",
        "workspace_replace_text"
    ];

    [Test]
    public void Test_Tools_ExposeExpectedNames()
    {
        string[] actualNames = GetToolAttributes()
            .Select(attribute => attribute.Name)
            .Order(StringComparer.Ordinal)
            .ToArray()!;

        Assert.That(actualNames, Is.EqualTo(ExpectedToolNames.Order(StringComparer.Ordinal)));
    }

    [Test]
    public void Test_Tools_HaveUniqueNames()
    {
        string?[] names = GetToolAttributes()
            .Select(attribute => attribute.Name)
            .ToArray();

        Assert.That(names, Is.Unique);
    }

    [Test]
    public void Test_DangerousTools_AreMarkedDestructive()
    {
        Dictionary<string, McpServerToolAttribute> attributes = GetToolAttributes()
            .ToDictionary(attribute => attribute.Name!, StringComparer.Ordinal);

        Assert.That(attributes["twincat_consume_xml"].Destructive, Is.True);
        Assert.That(attributes["twincat_activate_configuration"].Destructive, Is.True);
        Assert.That(attributes["twincat_restart_runtime"].Destructive, Is.True);
        Assert.That(attributes["twincat_set_runtime_state"].Destructive, Is.True);
        Assert.That(attributes["workspace_write_file"].Destructive, Is.True);
        Assert.That(attributes["workspace_replace_text"].Destructive, Is.True);
    }

    [Test]
    public void Test_ReadOnlyTools_AreMarkedReadOnly()
    {
        Dictionary<string, McpServerToolAttribute> attributes = GetToolAttributes()
            .ToDictionary(attribute => attribute.Name!, StringComparer.Ordinal);

        Assert.That(attributes["twincat_config"].ReadOnly, Is.True);
        Assert.That(attributes["xae_status"].ReadOnly, Is.True);
        Assert.That(attributes["twincat_lookup_tree_item"].ReadOnly, Is.True);
        Assert.That(attributes["twincat_produce_xml"].ReadOnly, Is.True);
        Assert.That(attributes["twincat_get_target_net_id"].ReadOnly, Is.True);
        Assert.That(attributes["twincat_get_last_error_messages"].ReadOnly, Is.True);
        Assert.That(attributes["workspace_info"].ReadOnly, Is.True);
        Assert.That(attributes["workspace_list_files"].ReadOnly, Is.True);
        Assert.That(attributes["workspace_get_file_info"].ReadOnly, Is.True);
        Assert.That(attributes["workspace_read_file"].ReadOnly, Is.True);
        Assert.That(attributes["workspace_search_text"].ReadOnly, Is.True);
    }

    [Test]
    public void Test_FormatTools_AreNotExposedUntilStructuredTextFormatterExists()
    {
        string?[] names = GetToolAttributes()
            .Select(attribute => attribute.Name)
            .ToArray();

        Assert.That(names, Does.Not.Contain("workspace_format_dotnet"));
    }

    private static IEnumerable<McpServerToolAttribute> GetToolAttributes()
    {
        return new[] { typeof(TwinCatMcpTools), typeof(WorkspaceMcpTools) }
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .OfType<McpServerToolAttribute>();
    }
}
