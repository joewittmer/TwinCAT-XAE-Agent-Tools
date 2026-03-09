using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TwincatMcpServer;

class Program
{
    static async Task Main(string[] args)
    {
        // Setup Configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var port = config["McpConfig:Port"];
        var twinCatPath = config["McpConfig:TwinCatProjectPath"];

        // Log configuration to Stderr so it doesn't interfere with Stdout JSON-RPC
        Console.Error.WriteLine($"[Config] Port: {port}");
        Console.Error.WriteLine($"[Config] TwinCAT Path: {twinCatPath}");

        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) break;

            try 
            {
                var request = JsonNode.Parse(line);
                if (request == null) continue;

                string method = request["method"]?.ToString() ?? "";
                var id = request["id"];

                if (method == "initialize")
                {
                    SendResponse(id, new
                    {
                        protocolVersion = "2024-11-05",
                        capabilities = new { tools = new { } },
                        serverInfo = new { name = "TwincatMcpServer", version = "1.2.0" }
                    });
                }
                else if (method == "tools/list")
                {
                    SendResponse(id, new {
                        tools = new[] {
                            new { name = "read_file", description = "Read content from a file", inputSchema = new { type = "object", properties = new { path = new { type = "string" } }, required = new[] { "path" } } },
                            new { name = "write_file", description = "Write content to a file", inputSchema = new { type = "object", properties = new { path = new { type = "string" }, content = new { type = "string" } }, required = new[] { "path", "content" } } },
                            new { name = "get_config", description = "Get current server configuration", inputSchema = new { type = "object", properties = new { } } }
                        }
                    });
                }
                else if (method == "tools/call")
                {
                    string toolName = request["params"]?["name"]?.ToString() ?? "";
                    var toolArgs = request["params"]?["arguments"];

                    if (toolName == "read_file")
                    {
                        string path = toolArgs?["path"]?.ToString() ?? "";
                        if (File.Exists(path)) SendToolResult(id, await File.ReadAllTextAsync(path));
                        else SendError(id, "File not found");
                    }
                    else if (toolName == "write_file")
                    {
                        string path = toolArgs?["path"]?.ToString() ?? "";
                        string content = toolArgs?["content"]?.ToString() ?? "";
                        await File.WriteAllTextAsync(path, content);
                        SendToolResult(id, $"File written to {path}");
                    }
                    else if (toolName == "get_config")
                    {
                        SendToolResult(id, $"Port: {port}, TwinCAT Project: {twinCatPath}");
                    }
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"Error: {ex.Message}"); }
        }
    }

    static void SendResponse(JsonNode? id, object result) => 
        Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id, result = result }));

    static void SendToolResult(JsonNode? id, string text) => 
        SendResponse(id, new { content = new[] { new { type = "text", text = text } } });

    static void SendError(JsonNode? id, string message) => 
        Console.WriteLine(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = id, error = new { code = -32000, message = message } }));
}
