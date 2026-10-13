# Invex.Process

[![Build](https://github.com/Invex-Games/process/actions/workflows/Build.yml/badge.svg)](https://github.com/Invex-Games/process/actions/workflows/Build.yml)
[![NuGet](https://img.shields.io/nuget/v/Invex.Process.svg)](https://www.nuget.org/packages/Invex.Process)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Invex-Games/process/blob/main/LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-512BD4.svg)](https://dotnet.microsoft.com/)

A small, focused .NET library for running external processes with first-class logging, real-time output streaming,
full output capture, and fail-fast error handling. It wraps `System.Diagnostics.Process` behind a clean, injectable
`IProcessRunner` abstraction so your build targets, tools, and services can shell out to executables (`dotnet`, `git`,
`npm`, …) without re-implementing redirection, buffering, and exit-code handling every time.

## Features

- **Sync and async APIs.** `Run` and `RunAsync`, the latter with `CancellationToken` support.
- **Real-time streaming.** Stdout and stderr are read asynchronously and logged line-by-line as the process runs,
  instead of being buffered until exit.
- **Full output capture.** Complete stdout/stderr are always captured into the result, regardless of log level, so you
  can inspect them even when logging is suppressed.
- **Fail-fast by default.** A non-zero exit code throws a descriptive exception (including command, exit code, working
  directory, and stderr), or you opt in to inspect failures yourself.
- **Configurable log levels.** Control how the invocation, stdout, and stderr are logged.
- **Per-line transforms.** Rewrite or suppress individual output/error lines, for example to redact secrets or filter
  noisy progress output.
- **Working directory and environment variables.** Run from any directory and inject or remove environment variables
  per invocation.
- **DI-ready.** Register once with `AddProcessRunner()` and inject `IProcessRunner` anywhere.

## Installation

```shell
dotnet add package Invex.Process
```

The package targets `net8.0`, `net9.0`, and `net10.0`. It depends on
`Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Hosting.Abstractions`, and logs
through `ILogger`, so configure logging on your host (it is by default with `Host.CreateApplicationBuilder`).

## Quick start

Register the process runner with your dependency injection container:

```csharp
using Invex.Process;
using Microsoft.Extensions.DependencyInjection;

services.AddProcessRunner();
```

Then inject `IProcessRunner` and run a process:

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

## Usage

### Configure the invocation

Arguments can be a single pre-joined string or an array of tokens; in the array form, empty and whitespace-only entries
are removed before joining, which makes optional arguments easy. Set a working directory and add, override or remove
environment variables per call:

```csharp
var verbose = true;
var status = new ProcessRunOptions("git", ["status", "--short", verbose ? "--verbose" : ""]);

var result = processRunner.Run(new ProcessRunOptions("npm", "ci")
{
    WorkingDirectory = "/repo/frontend",
    EnvironmentVariables =
    {
        ["NODE_ENV"] = "production",
        ["NPM_TOKEN"] = null, // a null value removes the inherited variable
    },
});
```

See [Running Processes](https://github.com/Invex-Games/process/blob/main/docs/running-processes.md).

### Handle failures

By default a non-zero exit code throws with a message that includes the command, exit code, working directory and
stderr. Set `AllowFailedResult` to inspect the result yourself instead:

```csharp
var result = processRunner.Run(new ProcessRunOptions("dotnet", "format --verify-no-changes")
{
    AllowFailedResult = true,
});

if (result.ExitCode != 0)
{
    // Decide what to do with the failure...
}
```

See [Handling Failures](https://github.com/Invex-Games/process/blob/main/docs/handling-failures.md).

### Control logging

Each invocation can tune how it is logged. The full output is still captured in the result regardless of these
settings:

```csharp
var options = new ProcessRunOptions("dotnet", "test")
{
    InvocationLogLevel = LogLevel.Debug,   // the "Run: dotnet test" line
    OutputLogLevel = LogLevel.Information, // each stdout line
    ErrorLogLevel = LogLevel.Error,        // each stderr line
};
```

> [!NOTE]
> On a non-zero exit, stdout/stderr configured below `Information` are re-logged in full at `Information`/`Warning`
> respectively, so the failure is visible in the logs even when per-line logging was quiet.

See [Logging](https://github.com/Invex-Games/process/blob/main/docs/logging.md).

### Transform or filter output

`TransformOutput` and `TransformError` are applied to each line before it is logged and captured. Returning `null`
suppresses the line entirely:

```csharp
var options = new ProcessRunOptions("docker", "build .")
{
    // Drop noisy progress lines completely
    TransformError = line => line.StartsWith("progress:")
        ? null
        : line,
};
```

See [Transforming Output](https://github.com/Invex-Games/process/blob/main/docs/transforming-output.md).

### Cancel a long-running wait

`RunAsync` accepts a `CancellationToken` that cancels the wait for the process to exit and throws
`OperationCanceledException`.

> [!IMPORTANT]
> Cancelling the token aborts the wait but does **not** kill the underlying OS process.

See [Cancellation](https://github.com/Invex-Games/process/blob/main/docs/cancellation.md).

## Documentation

Full documentation: [https://invex-games.github.io/process/](https://invex-games.github.io/process/)

| Guide                                                                                             | Contents                                                                 |
|---------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------|
| [Introduction](https://github.com/Invex-Games/process/blob/main/docs/introduction.md)             | The problem it solves, when to use it, and design principles.            |
| [Getting Started](https://github.com/Invex-Games/process/blob/main/docs/getting-started.md)       | Installation, registration, and your first process.                      |
| [Running Processes](https://github.com/Invex-Games/process/blob/main/docs/running-processes.md)   | Arguments, working directories, environment variables, concurrency.      |
| [Handling Failures](https://github.com/Invex-Games/process/blob/main/docs/handling-failures.md)   | Fail-fast exceptions, `AllowFailedResult`, and startup failures.         |
| [Logging](https://github.com/Invex-Games/process/blob/main/docs/logging.md)                       | What is logged, log levels, failure promotion, and secrets.              |
| [Transforming Output](https://github.com/Invex-Games/process/blob/main/docs/transforming-output.md) | Redacting, filtering and normalizing output lines.                     |
| [Cancellation](https://github.com/Invex-Games/process/blob/main/docs/cancellation.md)             | What cancelling `RunAsync` does and doesn't do.                          |
| [Testing Your Code](https://github.com/Invex-Games/process/blob/main/docs/testing.md)             | Faking `IProcessRunner` in unit tests.                                   |
| [Versioning](https://github.com/Invex-Games/process/blob/main/docs/versioning.md)                 | The Semantic Versioning policy and what counts as the public API.        |
| [API Reference](https://github.com/Invex-Games/process/blob/main/api/index.md)                    | Every public type, with links into the generated reference.              |

## Development

```shell
dotnet build Invex.Process.slnx
dotnet test Invex.Process.slnx
```

The documentation site is built with [DocFX](https://dotnet.github.io/docfx/) through the
[Atom](https://github.com/Invex-Games/atom) build in `_atom/`:

```shell
atom BuildDocs   # generate the site
atom ServeDocs   # build and serve it locally
```

Without the `atom` tool installed, run the same targets with `dotnet run --project _atom -- BuildDocs`.

## License

Licensed under the [MIT License](https://github.com/Invex-Games/process/blob/main/LICENSE.txt).
