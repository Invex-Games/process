# Logging

The runner logs through the standard `Microsoft.Extensions.Logging` abstractions
(`ILogger<ProcessRunner>`), so output flows to whatever providers your host configures — console,
Application Insights, Serilog, etc.

## What gets logged

Each invocation produces three kinds of log entries:

1. **The invocation line** — logged once when the process starts:

   ```text
   Run: dotnet build --configuration Release in /repo/src with env CI=true
   ```

   The working directory and environment variables are included only when set.

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

- The **invocation line** includes the command arguments and any environment variables you set.
  Avoid passing secrets as plain arguments/variables, or lower `InvocationLogLevel`.
- **Output lines** may echo secrets printed by the tool. Use `TransformOutput` /
  `TransformError` to redact or suppress them — see
  [Transforming Output](transforming-output.md).

