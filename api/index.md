# API Reference

Welcome to the **Invex.Process** API reference. The reference pages in this section are generated
directly from the XML documentation in the source code, so they always match the shipped package.

## Namespace

Everything lives in a single namespace: [`Invex.Process`](Invex.Process.yml).

## Public surface at a glance

| Type | Kind | Description |
|------|------|-------------|
| [`IProcessRunner`](Invex.Process.IProcessRunner.yml) | interface | Injectable service for executing external processes. Exposes synchronous `Run` and asynchronous `RunAsync`. Registered as a singleton. |
| [`ProcessRunOptions`](Invex.Process.ProcessRunOptions.yml) | record | Immutable configuration for a single execution — executable name, arguments, working directory, environment variables, log levels, per-line transforms, and failure tolerance. |
| [`ProcessRunResult`](Invex.Process.ProcessRunResult.yml) | record | The result of a completed execution — exit code, full captured stdout/stderr, and the originating options. |
| [`ProcessHostExtensions`](Invex.Process.ProcessHostExtensions.yml) | static class | `IServiceCollection` extensions; call `AddProcessRunner()` once during host setup. |

## Typical flow

1. Register the runner with [`AddProcessRunner()`](Invex.Process.ProcessHostExtensions.yml).
2. Inject [`IProcessRunner`](Invex.Process.IProcessRunner.yml) where you need to shell out.
3. Build a [`ProcessRunOptions`](Invex.Process.ProcessRunOptions.yml) describing the invocation.
4. Call `Run` or `RunAsync` and use the returned
   [`ProcessRunResult`](Invex.Process.ProcessRunResult.yml).

```csharp
var result = await processRunner.RunAsync(
    new ProcessRunOptions("git", "rev-parse HEAD")
    {
        WorkingDirectory = repoRoot,
    },
    cancellationToken);

var commitSha = result.Output.Trim();
```

## Versioning

The public API surface follows Semantic Versioning — see [SemVer policy](SemVer.md) for what you
can rely on between releases.

