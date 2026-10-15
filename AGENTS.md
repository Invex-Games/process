# Agent Instructions

Guidance for AI agents working in **Invex.Process** — a small, focused C# library for running
external processes with first-class logging, real-time output streaming, full output capture, and
fail-fast error handling. Keep changes focused and defer to the linked docs for detail.

## What's in the repo

| Project | Role | Target frameworks |
|---------|------|-------------------|
| `Invex.Process` | The library: `IProcessRunner` / `ProcessRunner`, `ProcessRunOptions`, `ProcessRunResult`, `ProcessException`, `ProcessHostExtensions` | `net10.0` |
| `Invex.Process.Tests` | NUnit test suite, including a public API surface snapshot test | `net10.0` |
| `_atom` | Atom build definition (`IBuild.cs`) that generates the GitHub Actions workflows | `net10.0` |

Sources live under `src/`, tests under `tests/`, the Atom build definition under `_atom/`, and the
DocFX documentation site is configured by `docfx.json` with content in `docs/`, `api/`, `index.md`,
and `toc.yml`.

## Build & language specifics

- **.NET 11 SDK** (currently RC2) is required. Library and test projects target `net10.0` and `net11.0`;
  `ProcessRunner` uses the .NET 11 `Process.ReadAllLines` APIs under `#if NET11_0_OR_GREATER`.
- C# `LangVersion` 15, `ImplicitUsings` and `Nullable` enabled, `TreatWarningsAsErrors` on.
- Global usings live in each project's `_usings.cs` — add shared usings there, not per-file.
- `GenerateDocumentationFile` is on and `CS1591` is **enforced** in `src/` — every public type
  and member needs XML doc comments. (`_atom` and the test project suppress `CS1591` locally; do
  not re-add it to the repo-wide `NoWarn` in `Directory.Build.props`.)

Build and test the whole solution:

```shell
dotnet build Invex.Process.slnx
dotnet test Invex.Process.slnx
```

Build the docs site:

```shell
dotnet build Invex.Process.slnx --configuration Release
docfx docfx.json          # add --serve to preview locally
```

DocFX reads the compiled `bin/Release/net10.0` assembly and its XML documentation file, because
source extraction omits members declared in C# 14 extension blocks. Build in Release before
regenerating docs; `atom BuildDocs` does this for you.

## Architecture overview

The entire public surface is five types in the `Invex.Process` namespace:

- **`IProcessRunner`** — the injectable service: synchronous `Run(options)` and asynchronous
  `RunAsync(options, ct)`. Registered as a singleton; stateless and safe for concurrent use.
- **`ProcessHostExtensions`** — the `AddProcessRunner()` extension on `IServiceCollection`, which
  registers `IProcessRunner` as a singleton backed by the default implementation.
- **`ProcessRunOptions`** — immutable record describing one invocation: name, args, working
  directory, environment variables, per-stream log levels, per-line transforms
  (`TransformOutput`/`TransformError`), and `AllowFailedResult`.
- **`ProcessRunResult`** — immutable record with `ExitCode`, captured `Output`/`Error`, and the
  originating `RunOptions`.
- **`ProcessException`** — thrown on a non-zero exit (unless `AllowFailedResult`); derives from
  `Exception` and carries `ExitCode` and the full `Result`.

The default implementation, `ProcessRunner`, is `internal sealed` and not part of the public
surface. It wraps `System.Diagnostics.Process`; stdout/stderr are redirected and streamed
line-by-line to `ILogger` in real time via `BeginOutputReadLine`/`BeginErrorReadLine`, while also
being fully captured into the result.

### Behavioral contracts (do not break these)

- **Fail-fast by default**: a non-zero exit code throws a descriptive `ProcessException` (command
  line, exit code, working directory, stderr) unless `AllowFailedResult` is `true`.
- **Environment variable values are never logged** — the invocation line names the variables only.
- **Output is always fully captured** into the result regardless of log levels.
- **Failure promotion**: on any non-zero exit, stdout/stderr configured below `Information` are
  re-logged in full at `Information`/`Warning` respectively — regardless of `AllowFailedResult`.
- **Transforms returning `null` suppress the line** from both the log and the captured result.
- **Cancellation aborts the wait, not the process** — `RunAsync` never kills the child process.
- `Run` and `RunAsync` must remain behaviorally identical apart from threading/cancellation; if
  you change one, mirror the change in the other.

## Key design rules

- Consumers depend on `IProcessRunner`, never on the concrete `ProcessRunner` (which stays
  `internal`; tests reach it via `InternalsVisibleTo`).
- Keep the public surface minimal — this library does one thing. Push back on scope creep.
- New options belong on `ProcessRunOptions` as `init` properties with sensible defaults that
  preserve existing behavior.

## Atom workflows

The GitHub Actions workflow YAML under `.github/workflows/` (`Validate.yml`, `Build.yml`,
`Dependabot Enable auto-merge.yml`, `Cleanup Prereleases.yml`) is **generated** from the Atom
build definition in `_atom/IBuild.cs`.

Whenever you change anything that affects the workflows — targets, workflow definitions, triggers,
options, or params/secrets — regenerate the YAML:

```shell
atom gen
```

(equivalently `dotnet run --project _atom -- gen`). Commit the regenerated `.github/workflows/`
files alongside your `_atom/` changes; never hand-edit the generated YAML.

A drift between `_atom/IBuild.cs` and the committed YAML should be treated as a missing
`atom gen` run.

## Conventions

- Annotate every new public type with `[PublicAPI]` — the in-repo analyzer flags anything missing,
  and warnings are errors.
- Add XML doc comments to all public types and members. Match the existing `<summary>` /
  `<param>` / `<remarks>` / `<example>` style, and keep docs **accurate to the implementation**
  (e.g. exact log levels and promotion behavior).
- Use Conventional Commits — the prefix drives versioning:

  | Prefix | Version bump |
  |--------|--------------|
  | `breaking:` / `major:` | Major |
  | `feat:` / `feature:` / `minor:` | Minor |
  | `fix:` / `patch:` | Patch |
  | `semver-none` / `semver-skip` | No bump |

- When adding user-facing features, update the relevant `docs/` page and `README.md`. The README
  is packed into the NuGet package — keep links absolute (no repo-relative links like
  `LICENSE.txt`).

## Testing & the Verify workflow

- Tests use **NUnit** with **Shouldly**, **FakeItEasy**, and **Verify** (`Verify.NUnit`) for
  snapshot/approval testing.
- A snapshot test fails when its output differs from the committed `*.verified.txt`. On failure,
  Verify writes a `*.received.txt` next to it.
- If the diff is unintended, fix the code. If the change is valid (expected new output), accept
  it and re-run:
  1. Overwrite the `*.verified.txt` with the contents of the matching `*.received.txt`.
  2. Delete the `*.received.txt`.
  3. Re-run `dotnet test` to confirm the suite is green.
- `PublicApiTests.VerifyPublicApiSurface.verified.txt` tracks the **complete public API**. An
  unexpected diff there signals an unintentional API change — treat it as such and double-check
  before accepting. The Validate workflow's `CheckPrForBreakingChanges` target inspects changes
  to `tests/**/*.verified.txt` on PRs, so API-surface changes must be intentional and committed.

## Adding a new option to `ProcessRunOptions`

1. Add an `init` property with a default that preserves current behavior, plus full XML docs.
2. Honor it in **both** `ProcessRunner.Run` and `ProcessRunner.RunAsync`.
3. Add unit tests covering both code paths.
4. Update `PublicApiTests.VerifyPublicApiSurface.verified.txt` (see the Verify workflow above).
5. Document it in the relevant `docs/` page and, if user-facing, the README.

## Documentation

Docs follow the shared Invex documentation standard used by every Invex library:

- **README.md** is also the NuGet readme. Keep the fixed section order (title, badges, pitch, Features,
  Installation, Quick start, Usage, Documentation, Development, License) and use absolute links only
  (`https://github.com/Invex-Games/process/blob/main/...` or the docs site), so they work on nuget.org.
- **DocFX site** (`docfx.json`, `toc.yml`, `index.md`, `docs/`, `api/index.md`), published to
  https://invex-games.github.io/process/. `docs/introduction.md` and `docs/getting-started.md` come first; every page
  opens with one to three sentences on what it covers and ends with a "See also" or "Next steps" list. Link API
  types with `<xref:Namespace.Type>`, not file paths.
- **`api/index.md`** is the only hand-written file under `api/`; everything else there is generated. Put per-type
  detail in XML docs, not in hand-written API pages.
- **XML docs are enforced.** `CS1591` is not suppressed for shipped projects, so every public type and member needs
  `<summary>`, plus `<param>`, `<typeparam>`, `<returns>` and `<exception>` where they apply, `<remarks>` for
  non-obvious behaviour, and `<see cref>`/`<paramref>`/`<see langword>` for references.

## Defer to the docs

For anything beyond the above, prefer these over duplicating detail:

- `README.md` — package overview and quick start.
- `docs/introduction.md` — what the library is and why it exists.
- `docs/getting-started.md` — installation, DI registration, first run.
- `docs/running-processes.md` — arguments, working directory, environment variables, concurrency.
- `docs/handling-failures.md` — fail-fast behavior, `AllowFailedResult`, failure promotion.
- `docs/logging.md` — log streams, levels, and secrets guidance.
- `docs/transforming-output.md` — per-line rewriting and suppression.
- `docs/cancellation.md` — what cancellation does and doesn't do.
- `docs/testing.md` — faking `IProcessRunner` in consumer tests.
- `docs/versioning.md` — the versioning policy and what counts as the public API.

