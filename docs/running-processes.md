# Running Processes

Every invocation is described by a single immutable `ProcessRunOptions` record. This page covers
the core execution options; failure handling and logging have their own pages.

## Sync or async

`IProcessRunner` exposes two methods:

```csharp
// Blocks the calling thread until the process exits.
ProcessRunResult Run(ProcessRunOptions options);

// Awaits process exit; supports cancellation.
Task<ProcessRunResult> RunAsync(ProcessRunOptions options, CancellationToken cancellationToken = default);
```

Both behave identically apart from threading: same logging, same capture, same failure handling.
Prefer `RunAsync` in async code paths and anywhere you want cancellation support (see
[Cancellation](cancellation.md)).

## Passing arguments

Arguments can be supplied as a single pre-joined string or as an array of tokens:

```csharp
// As a single string
var options = new ProcessRunOptions("git", "status --short");

// As tokens
var options2 = new ProcessRunOptions("git", ["status", "--short"]);
```

When using the array form, empty and whitespace-only entries are removed before joining. This makes
conditional arguments clean — no spurious double spaces:

```csharp
var verbose = ShouldBeVerbose();

var options = new ProcessRunOptions("git",
[
    "status",
    "--short",
    verbose ? "--verbose" : "",   // dropped entirely when empty
]);
```

> [!IMPORTANT]
> Arguments are ultimately passed to the OS as a single string
> (`ProcessStartInfo.Arguments`). If an argument value can contain spaces or special characters
> (e.g. a user-supplied file path), quote it yourself: `$"\"{path}\""`.

## Working directory

By default the child process inherits the current process's working directory. Set
`WorkingDirectory` to run a tool from a specific location:

```csharp
var result = processRunner.Run(new ProcessRunOptions("npm", "ci")
{
    WorkingDirectory = "/repo/frontend",
});
```

## Environment variables

`EnvironmentVariables` lets you add, override, or remove variables for the child process. Variables
you don't list are inherited unchanged from the current process:

```csharp
var result = processRunner.Run(new ProcessRunOptions("npm", "ci")
{
    EnvironmentVariables =
    {
        ["NODE_ENV"] = "production",  // add or override
        ["CI"] = "true",
        ["NPM_TOKEN"] = null,         // a null value removes the inherited variable
    },
});
```

The invocation log line includes the variables you set, so be careful not to put secrets in values
you don't want logged (or lower `InvocationLogLevel` — see [Logging](logging.md)).

## Reusing and varying options

`ProcessRunOptions` is a record, so you can keep a baseline and vary it with `with`:

```csharp
var git = new ProcessRunOptions("git", "")
{
    WorkingDirectory = repoRoot,
    InvocationLogLevel = LogLevel.Debug,
};

var status = git with { Args = "status --short" };
var fetch  = git with { Args = "fetch --prune" };
```

## Concurrency

`IProcessRunner` is stateless and registered as a singleton — it is safe to run multiple processes
concurrently from multiple threads.

