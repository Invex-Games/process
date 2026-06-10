# Semantic Versioning Policy

**Invex.Process** follows [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).
Version numbers take the form `MAJOR.MINOR.PATCH` and are derived automatically from the Git
history via GitVersion.

## What the numbers mean

| Component | Bumped when | Examples |
|-----------|-------------|----------|
| **MAJOR** | A breaking change is made to the public API. | Removing or renaming a public type or member, changing a method signature, changing a default value in a behavior-breaking way, dropping a target framework. |
| **MINOR** | New functionality is added in a backward-compatible way. | New optional properties on `ProcessRunOptions`, new overloads, new extension methods, adding a target framework. |
| **PATCH** | A backward-compatible bug fix or internal improvement is made. | Fixing incorrect output capture, logging corrections, documentation, performance improvements. |

## What counts as the public API

The public API is everything reachable from a consuming assembly:

- Public types: `IProcessRunner`, `ProcessRunOptions`, `ProcessRunResult`, `ProcessHostExtensions`.
- Their public members, signatures, and parameter names (named arguments are part of the contract).
- Documented behavioral contracts — for example, *"a non-zero exit code throws unless
  `AllowFailedResult` is `true`"* and *"stdout/stderr are always fully captured into the result"*.
- Supported target frameworks.

The public API surface is tracked and enforced at build time with public API analyzers, so
unintended breaking changes fail the build before they ship.

## What is *not* covered

- `internal` types (e.g. the `ProcessRunner` implementation class) — depend on `IProcessRunner`,
  not on the concrete type.
- Exact log message templates and wording — these may be refined in MINOR/PATCH releases.
- The exact text of exception messages.

## Pre-release versions

Pre-release packages (e.g. `2.1.0-beta.3`) may include API changes between pre-release builds
without a MAJOR bump. They are intended for early feedback, not production use.

