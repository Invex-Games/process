# Introduction

**Invex.Process** is a small, focused .NET library for running external processes with
first-class logging, real-time output streaming, full output capture, and fail-fast error
handling.

## The problem

Shelling out to external tools (`dotnet`, `git`, `npm`, `docker`, …) from .NET code with the raw
`System.Diagnostics.Process` API requires a surprising amount of boilerplate to get right:

- Redirecting stdout and stderr and wiring up asynchronous read loops so long-running processes
  don't deadlock on full pipe buffers.
- Accumulating output for later inspection while *also* surfacing it live in logs.
- Checking exit codes and producing useful error messages when something fails.
- Plumbing in working directories and environment variables.
- Doing all of the above consistently across every call site.

Most codebases end up with a half-dozen subtly different copies of this logic.

## The solution

Invex.Process wraps all of this behind a single injectable abstraction:

```csharp
var result = await processRunner.RunAsync(
    new ProcessRunOptions("git", "rev-parse HEAD"),
    cancellationToken);

var commitSha = result.Output.Trim();
```

One line of registration (`services.AddProcessRunner()`), one options record describing *what* to
run, one result record telling you *what happened* — and consistent logging and error handling
everywhere.

## Design principles

- **Fail fast by default.** A non-zero exit code throws a descriptive exception that includes the
  command line, exit code, working directory, and captured stderr. Opt out per-invocation with
  `AllowFailedResult` when you want to handle failures yourself.
- **Never lose output.** Full stdout and stderr are always captured into the result, regardless of
  how (or whether) they were logged.
- **Stream, don't buffer.** Output is logged line-by-line in real time, so long-running processes
  show progress as they go.
- **Stay testable.** Your code depends on the `IProcessRunner` interface, which is trivial to fake
  in unit tests — no real processes needed.

## Library shape

The entire public surface is four types in one namespace (`Invex.Process`):

| Type | Role |
|------|------|
| `IProcessRunner` | The service you inject: `Run(options)` / `RunAsync(options, ct)`. |
| `ProcessRunOptions` | Immutable record describing one invocation. |
| `ProcessRunResult` | Immutable record describing the outcome. |
| `ProcessHostExtensions` | `AddProcessRunner()` DI registration. |

Continue with [Getting Started](getting-started.md).

