# Getting Started

## Installation

```shell
dotnet add package Invex.Process
```

The package targets `net8.0`, `net9.0`, and `net10.0`.

## Register the service

Add the process runner to your dependency injection container once during host setup:

```csharp
using Invex.Process;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddProcessRunner();
```

`AddProcessRunner()` registers `IProcessRunner` as a **singleton**. The runner is stateless, so a
single instance can safely serve concurrent callers.

> [!NOTE]
> The runner logs through `ILogger<ProcessRunner>`, so make sure logging is configured on the host
> (it is by default with `Host.CreateApplicationBuilder`).

## Run your first process

Inject `IProcessRunner` and call `RunAsync` (or the synchronous `Run`):

```csharp
using Invex.Process;

public sealed class VersionReporter(IProcessRunner processRunner)
{
    public async Task<string> GetDotnetVersionAsync(CancellationToken cancellationToken)
    {
        var result = await processRunner.RunAsync(
            new ProcessRunOptions("dotnet", "--version"),
            cancellationToken);

        return result.Output.Trim();
    }
}
```

When this runs you'll see log entries like:

```text
info: Invex.Process.ProcessRunner[0]
      Run: dotnet --version
dbug: Invex.Process.ProcessRunner[0]
      10.0.100
```

## What you get back

Both `Run` and `RunAsync` return a `ProcessRunResult`:

| Property | Description |
|----------|-------------|
| `ExitCode` | The process exit code. Zero unless `AllowFailedResult` was set (a non-zero code otherwise throws). |
| `Output` | The complete captured stdout, one line per `Environment.NewLine`. |
| `Error` | The complete captured stderr. |
| `RunOptions` | The options the process was started with. |

The output is *always* fully captured — even if you turn logging down or off entirely.

## Next steps

- [Running Processes](running-processes.md) — arguments, working directories, environment variables.
- [Handling Failures](handling-failures.md) — what happens when the exit code isn't zero.
- [Logging](logging.md) — tune how invocations and output are logged.

