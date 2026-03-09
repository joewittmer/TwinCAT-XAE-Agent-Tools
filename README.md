# Twincat MCP Server (.NET 10)

This is a cross-platform MCP server built with .NET.

## Setup on Windows
1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Clone/Copy these files to your Windows machine.
3. Open terminal in the project folder.
4. Run: `dotnet run` or `dotnet build`.

## Developing via Agent-Zero
Agent-Zero is connected to this project directory. You can ask Agent-Zero to:
- Add new MCP tools (e.g., TwinCAT PLC connectivity).
- Modify server logic.
- Compile and check for errors.

## Configuration in Agent-Zero
To use this server as a tool in Agent-Zero (local or remote), add it to your `mcp_servers.json` or equivalent configuration:

\`\`\`json
{
  "mcpServers": {
    "twincat": {
      "command": "dotnet",
      "args": ["run", "--project", "C:/path/to/TwincatMcpServer/TwincatMcpServer.csproj"],
      "env": {}
    }
  }
}
\`\`\`
