# Cancellation

`RunAsync` accepts a `CancellationToken` that cancels the **wait** for the process to exit:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    var result = await processRunner.RunAsync(
        new ProcessRunOptions("dotnet", "test"),
        cts.Token);
}
catch (OperationCanceledException)
{
    // The wait was abandoned after 5 minutes.
}
```

## What cancellation does — and doesn't — do

> [!IMPORTANT]
> Cancelling the token aborts the *wait*, not the *process*. The underlying OS process keeps
> running after `OperationCanceledException` is thrown.

When the token fires:

- The returned task faults with `OperationCanceledException` (a `TaskCanceledException`).
- Output captured up to that point is discarded — you do not receive a partial
  `ProcessRunResult`.
- The child process continues running to completion (or until it is killed externally).

This design keeps the runner predictable: it never kills processes it didn't decide to start
killing, which matters for tools that hold locks or write files non-atomically.

## Killing the process yourself

If you need hard timeout semantics — "stop waiting *and* terminate the tool" — run the process
through a wrapper that owns process-tree termination, or kill by name/ID after cancellation:

```csharp
try
{
    await processRunner.RunAsync(options, cts.Token);
}
catch (OperationCanceledException)
{
    foreach (var p in System.Diagnostics.Process.GetProcessesByName("stuck-tool"))
        p.Kill(entireProcessTree: true);

    throw;
}
```

## Synchronous `Run`

The synchronous `Run` method has no cancellation support — it blocks until the process exits.
Prefer `RunAsync` whenever a timeout or cooperative cancellation may be needed.

