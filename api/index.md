# API Reference

The reference pages in this section are generated from the XML documentation in the source code, so they always
match the shipped package; the table below is a map of where to start.

## Invex.Process

| Type | Kind | Description |
|------|------|-------------|
| [`IProcessRunner`](xref:Invex.Process.IProcessRunner) | Interface | Injectable service for executing external processes. Exposes synchronous `Run` and asynchronous `RunAsync`. Registered as a singleton. |
| [`ProcessRunOptions`](xref:Invex.Process.ProcessRunOptions) | Record | Immutable configuration for a single execution: executable name, arguments, working directory, environment variables, log levels, per-line transforms, and failure tolerance. |
| [`ProcessRunResult`](xref:Invex.Process.ProcessRunResult) | Record | The result of a completed execution: exit code, full captured stdout/stderr, and the originating options. |
| [`ProcessHostExtensions`](xref:Invex.Process.ProcessHostExtensions) | Static class | `IServiceCollection` extensions; call `AddProcessRunner()` once during host setup. |

## Typical flow

1. Register the runner with `AddProcessRunner()` from [`ProcessHostExtensions`](xref:Invex.Process.ProcessHostExtensions).
2. Inject [`IProcessRunner`](xref:Invex.Process.IProcessRunner) where you need to shell out.
3. Build a [`ProcessRunOptions`](xref:Invex.Process.ProcessRunOptions) describing the invocation.
4. Call `Run` or `RunAsync` and use the returned [`ProcessRunResult`](xref:Invex.Process.ProcessRunResult).

```csharp
var result = await processRunner.RunAsync(
    new ProcessRunOptions("git", "rev-parse HEAD")
    {
        WorkingDirectory = repoRoot,
    },
    cancellationToken);

var commitSha = result.Output.Trim();
```

For conceptual documentation, see the [Introduction](../docs/introduction.md); for what you can rely on between
releases, see [Versioning](../docs/versioning.md).
