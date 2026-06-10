# Invex.Process

A small, focused .NET library for running external processes with first-class logging, real-time
output streaming, full output capture, and fail-fast error handling.

It wraps `System.Diagnostics.Process` behind a clean, injectable `IProcessRunner` abstraction so your
build targets, tools, and services can shell out to executables (`dotnet`, `git`, `npm`, …) without
re-implementing redirection, buffering, and exit-code handling every time.

## Features

### Sync and async APIs

`Run` and `RunAsync` with `CancellationToken` support.

### Real-time streaming

Stdout and stderr are read asynchronously and logged line-by-line as the
process runs, instead of being buffered until exit.

### Full output capture

Complete stdout/stderr are always captured into the result, regardless of
log level, so you can inspect them even when logging is suppressed.

### Fail-fast by default

A non-zero exit code throws a descriptive exception (including command,
exit code, working directory, and stderr), or opt in to inspect failures yourself.

### Configurable log levels

Control how the invocation, stdout, and stderr are logged.

### Per-line transforms

Rewrite or suppress individual output/error lines (e.g. to redact secrets
or filter noisy progress output).

### Working directory & environment variables

Run from any directory and inject or remove
environment variables per invocation.

### DI-ready

Register once with `AddProcessRunner()` and inject `IProcessRunner` anywhere.

## Installation

```shell
dotnet add package Invex.Process
```

Targets `net8.0`, `net9.0`, and `net10.0`.

## Getting started

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

### Passing arguments

Arguments can be supplied as a single pre-joined string or as an array of tokens. When using an array,
empty and whitespace-only entries are removed before joining, which makes optional arguments easy:

```csharp
// As a single string
var options = new ProcessRunOptions("git", "status --short");

// As tokens — empty entries are stripped automatically
var verbose = true;
var options2 = new ProcessRunOptions("git", ["status", "--short", verbose ? "--verbose" : ""]);
```

### Handling failures

By default, a non-zero exit code throws with a detailed message:

```csharp
// Throws if the build fails — the exception includes the command, exit code, and stderr.
processRunner.Run(new ProcessRunOptions("dotnet", "build --configuration Release"));
```

Set `AllowFailedResult` to inspect the result yourself instead of throwing:

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

### Working directory and environment variables

```csharp
var result = processRunner.Run(new ProcessRunOptions("npm", "ci")
{
    WorkingDirectory = "/repo/frontend",
    EnvironmentVariables =
    {
        ["NODE_ENV"] = "production",
        ["CI"] = "true",
        ["NPM_TOKEN"] = null, // a null value removes the inherited variable
    },
});
```

### Controlling logging

Each invocation can tune how it is logged. The full output is still captured in the result regardless
of these settings:

```csharp
var options = new ProcessRunOptions("dotnet", "test")
{
    InvocationLogLevel = LogLevel.Debug,   // the "Run: dotnet test" line
    OutputLogLevel = LogLevel.Information, // each stdout line
    ErrorLogLevel = LogLevel.Error,        // each stderr line
};
```

> On a non-zero exit, stdout/stderr configured below `Information` are re-logged in full at
> `Information`/`Warning` respectively, so the failure is visible in the logs even when per-line
> logging was quiet.

### Transforming or filtering output

`TransformOutput` and `TransformError` are applied to each line before it is logged and captured.
Returning `null` suppresses the line entirely:

```csharp
var options = new ProcessRunOptions("deploy", "--token secret")
{
    // Redact secrets from logs and captured output
    TransformOutput = line => line.Contains("token")
        ? "[redacted]"
        : line,

    // Drop noisy progress lines completely
    TransformError = line => line.StartsWith("progress:")
        ? null
        : line,
};
```

## API overview

| Type                 | Description                                                                                                                          |
|----------------------|--------------------------------------------------------------------------------------------------------------------------------------|
| `IProcessRunner`     | Injectable service with `Run` and `RunAsync` methods. Registered as a singleton.                                                     |
| `ProcessRunOptions`  | Immutable record configuring a single execution (name, args, working directory, env vars, log levels, transforms, failure handling). |
| `ProcessRunResult`   | Result record exposing `ExitCode`, captured `Output`, captured `Error`, and the originating `RunOptions`.                            |
| `AddProcessRunner()` | `IServiceCollection` extension that registers `IProcessRunner`.                                                                      |

## Cancellation

`RunAsync` accepts a `CancellationToken` that cancels the wait for the process to exit and throws
`OperationCanceledException`. Note that cancelling the token aborts the wait but does **not** kill the
underlying OS process.

## License

Distributed under the terms described in
[LICENSE.txt](https://github.com/Invex-Games/process/blob/main/LICENSE.txt).
