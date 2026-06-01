# TwinCAT XAE Agent Tools Guidelines

## Development Principles
- Write simple, readable C# that is easy to debug.
- Prefer clear control flow over clever LINQ, fluent chains, or dense expressions.
- Keep classes focused on one responsibility, but do not create interfaces or abstractions unless they are needed for testing, COM isolation, dependency injection, or a real protocol boundary.
- Use SOLID principles as guardrails, not as a reason to split small code into unnecessary layers.
- Prefer the official MCP C# SDK and TwinCAT Automation Interface behavior over hand-rolled protocol or COM abstractions when practical.
- Keep dangerous TwinCAT operations explicit. Activating configuration, restarting runtime, and importing XML must require clear confirmation.
- Expose MCP tools only when they fit the TwinCAT development workflow and do not duplicate existing tools without a clear reason. Document intentional overlap in the README.
- Do not add formatters, renamers, or language-smart editing tools unless they are appropriate for the target language. `dotnet format` is not a Structured Text formatter; TwinCAT ST formatting or rename support needs parser-backed, TwinCAT-aware tooling.
- Avoid unrelated refactors while implementing a requested change.

## C# Style
- Use 4 spaces, no tabs, final newline, and trimmed trailing whitespace.
- Keep `using` directives outside namespaces, with `System` first and no separated groups.
- Use `PascalCase` for types, methods, and properties.
- Use `IPascalCase` for interfaces.
- Use `_camelCase` for private fields.
- Use `camelCase` for locals and parameters.
- Prefer `var` when the type is obvious.
- Prefer guard clauses and `ArgumentNullException.ThrowIfNull()` for validation.
- Use `async Task` with `CancellationToken` for asynchronous work.
- Avoid `async void` except for UI events.
- Compare strings with `StringComparison.Ordinal` or `StringComparison.OrdinalIgnoreCase`.
- Add comments only to explain why non-obvious code exists.

## Project Shape
- Keep the HTTP MCP server focused on MCP transport, tool registration, configuration, and hosting.
- Keep TwinCAT XAE COM automation in Windows-only code.
- Keep COM calls on a dedicated STA thread.
- Keep COM retry/message-filter handling close to the COM dispatcher.
- Prefer small concrete classes until tests or protocol boundaries require an interface.
- Do not preserve legacy SSE endpoints unless a supported client requires them.

## Testing
- Add unit tests for pure behavior: configuration binding, tool argument validation, safety confirmation checks, and MCP tool registration.
- Do not unit test Beckhoff COM behavior with mocks unless it catches a real branching rule.
- Treat real XAE/TwinCAT automation as manual or integration testing that requires a Windows machine with TwinCAT XAE installed.
- Keep tests small and named for the behavior they document.

## Build And Verify
- Build with `dotnet build --configuration Release`.
- Run tests with `dotnet test --configuration Release` when tests exist.
- For MCP transport work, verify Codex can list and call tools through the Streamable HTTP endpoint.
- For TwinCAT automation work, verify read-only tools before write/activation tools.

## Git
- Keep commit messages concise.
- Use lowercase commit messages.

## Documentation
- Keep README examples aligned with the implemented endpoint.
- Document Codex first.
- Include `config.toml` setup as the Codex app-friendly path and CLI setup as the quickest path.
- Link to Beckhoff Automation Interface references for COM, DTE, `ITcSysManager`, `ITcSmTreeItem`, XML import/export, and XAE version ProgIDs.
