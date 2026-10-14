# Logging

The runner logs through the standard `Microsoft.Extensions.Logging` abstractions
(`ILogger<ProcessRunner>`), so output flows to whatever providers your host configures — console,
Application Insights, Serilog, etc.

## What gets logged

Each invocation produces three kinds of log entries:

1. **The invocation line** — logged once when the process starts:

   ```text
   Run: dotnet build --configuration Release in /repo/src with env CI, NUGET_API_KEY
   ```

   The working directory and environment variables are included only when set. Only the
   environment variable *names* are logged, never their values.

2. **Output lines** — each stdout line, streamed in real time as the process runs.

3. **Error lines** — each stderr line, streamed in real time.

## Tuning log levels

All three are configurable per invocation via `ProcessRunOptions`:

| Property | Default | Controls |
|----------|---------|----------|
| `InvocationLogLevel` | `Information` | The `Run: …` line. |
| `OutputLogLevel` | `Debug` | Each stdout line. |
| `ErrorLogLevel` | `Warning` | Each stderr line. |

```csharp
var options = new ProcessRunOptions("dotnet", "test")
{
    InvocationLogLevel = LogLevel.Debug,   // quieter invocation line
    OutputLogLevel = LogLevel.Information, // show test output at info
    ErrorLogLevel = LogLevel.Error,        // treat stderr lines as errors
};
```

Use `LogLevel.None` to suppress a stream from the log entirely.

> [!NOTE]
> Log levels only affect *logging*. The full stdout and stderr are always captured into
> `ProcessRunResult.Output` / `ProcessRunResult.Error` regardless of these settings.

## Failure promotion

When a process exits non-zero, quiet output is automatically promoted so the failure is
diagnosable from the logs:

- stdout configured below `Information` → re-logged in full at `Information`;
- stderr configured below `Information` → re-logged in full at `Warning`.

This happens before the exception is thrown (and also when `AllowFailedResult` tolerates the
failure). The common pattern is therefore:

- Leave `OutputLogLevel` at `Debug` so successful runs are quiet.
- Rely on promotion to surface the full output automatically when something fails.

## Secrets in logs

Two things to watch:

- The **invocation line** includes the full command arguments and the *names* (not values) of any
  environment variables you set. Prefer passing secrets through `EnvironmentVariables`; if a
  secret must go on the command line, lower `InvocationLogLevel` (or set it to `LogLevel.None`).
  The exception thrown on a non-zero exit also includes the full command line.
- **Output lines** may echo secrets printed by the tool. Use `TransformOutput` /
  `TransformError` to redact or suppress them — see
  [Transforming Output](transforming-output.md).

## See also

- [Transforming Output](transforming-output.md): redacting and filtering lines before they are logged.
- [Handling Failures](handling-failures.md): what happens after a non-zero exit is logged.
- <xref:Invex.Process.ProcessRunOptions>
