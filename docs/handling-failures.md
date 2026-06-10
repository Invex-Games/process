# Handling Failures

## Fail-fast by default

When a process exits with a **non-zero exit code**, `Run` and `RunAsync` throw an exception rather
than returning a result. This means build steps and tools fail immediately without callers having
to remember to check exit codes:

```csharp
// Throws if the build fails.
processRunner.Run(new ProcessRunOptions("dotnet", "build --configuration Release"));
```

The exception message is designed to be actionable on its own:

```text
Process 'dotnet' failed with exit code 1
  Command: dotnet build --configuration Release
  Working Directory: /repo/src
  Error Output:
    MSBUILD : error MSB1009: Project file does not exist.
```

It includes:

- the executable name and exit code,
- the full command line,
- the working directory (when one was set),
- the captured stderr (when the process wrote any).

## Tolerating failures with `AllowFailedResult`

Sometimes a non-zero exit code is an *expected* outcome you want to inspect — linters, format
verifiers, `git diff --exit-code`, and similar. Set `AllowFailedResult` to receive the result
instead of an exception:

```csharp
var result = processRunner.Run(new ProcessRunOptions("dotnet", "format --verify-no-changes")
{
    AllowFailedResult = true,
});

if (result.ExitCode != 0)
{
    Console.WriteLine("Formatting issues found:");
    Console.WriteLine(result.Output);
}
```

> [!WARNING]
> With `AllowFailedResult = true` it becomes *your* responsibility to check
> `result.ExitCode`. Forgetting the check silently swallows failures.

## Failure visibility in logs

On any non-zero exit — whether or not `AllowFailedResult` is set — the runner makes sure the
failure is visible in the logs even if per-line output logging was quiet:

- If `OutputLogLevel` was below `Information`, the full captured stdout is re-logged at
  `Information`.
- If `ErrorLogLevel` was below `Information`, the full captured stderr is re-logged at `Warning`.

So a process that ran with `OutputLogLevel = LogLevel.Debug` (the default) still shows its complete
output in the logs when it fails, without you having to re-run at a higher verbosity.

## Startup failures

If the executable itself cannot be found or started (bad name, not on `PATH`, missing file), the
underlying `Process.Start` call throws `System.ComponentModel.Win32Exception`. This is distinct
from a process that starts and then exits non-zero.

```csharp
try
{
    processRunner.Run(new ProcessRunOptions("definitely-not-a-real-tool", ""));
}
catch (Win32Exception ex)
{
    // Tool not installed / not on PATH
}
```

