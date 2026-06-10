# Transforming Output

`ProcessRunOptions.TransformOutput` and `ProcessRunOptions.TransformError` are optional per-line
hooks applied to stdout and stderr respectively, *before* each line is logged and captured.

Each delegate has the shape `Func<string, string?>`:

- It receives the raw line (never `null` — null data events are filtered out beforehand).
- Return the same string to pass the line through unchanged.
- Return a different string to rewrite the line.
- Return `null` to **suppress** the line entirely — it is neither logged nor included in
  `ProcessRunResult.Output` / `ProcessRunResult.Error`.

## Redacting secrets

```csharp
var options = new ProcessRunOptions("deploy", "--token " + token)
{
    TransformOutput = line => line.Contains(token)
        ? line.Replace(token, "[redacted]")
        : line,
};
```

The redaction applies to both the live log stream *and* the captured `result.Output`, so the secret
never leaves the process boundary in either form.

## Filtering noise

Drop high-volume progress lines that would otherwise flood the logs and the captured output:

```csharp
var options = new ProcessRunOptions("docker", "build .")
{
    TransformError = line => line.StartsWith("#") || line.Contains("DONE")
        ? null   // suppressed: not logged, not captured
        : line,
};
```

## Normalizing output

Transforms can also reshape lines for downstream parsing:

```csharp
var options = new ProcessRunOptions("git", "status --porcelain")
{
    TransformOutput = line => line.TrimEnd(),
};
```

## Behavior notes

- Transforms run on the process's output-reader callbacks, once per line, as lines arrive.
  Keep them fast and side-effect-free.
- A suppressed line is excluded from failure promotion too — if the process fails, the re-logged
  "full output" is the *captured* (post-transform) output.
- Transforms are per-invocation. For a shared policy, build a helper that returns pre-configured
  `ProcessRunOptions` instances, or wrap the delegate in a reusable method.

