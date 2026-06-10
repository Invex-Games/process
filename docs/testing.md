# Testing Your Code

Because your code depends on the `IProcessRunner` *interface*, unit tests never have to start real
processes. Fake the interface, return canned `ProcessRunResult` instances, and assert on the
options your code passed in.

The examples below use [FakeItEasy](https://fakeiteasy.github.io/), but any mocking library (or a
hand-rolled fake) works the same way.

## Returning a canned result

```csharp
[Test]
public async Task GetDotnetVersion_returns_trimmed_output()
{
    var processRunner = A.Fake<IProcessRunner>();

    A.CallTo(() => processRunner.RunAsync(A<ProcessRunOptions>._, A<CancellationToken>._))
        .ReturnsLazily((ProcessRunOptions options, CancellationToken _) =>
            new ProcessRunResult(options, 0, "10.0.100\n", ""));

    var reporter = new VersionReporter(processRunner);

    var version = await reporter.GetDotnetVersionAsync(CancellationToken.None);

    Assert.That(version, Is.EqualTo("10.0.100"));
}
```

## Asserting on the invocation

`ProcessRunOptions` is a record, so captured options are easy to inspect:

```csharp
[Test]
public async Task Build_runs_dotnet_in_the_project_directory()
{
    var processRunner = A.Fake<IProcessRunner>();
    ProcessRunOptions? captured = null;

    A.CallTo(() => processRunner.RunAsync(A<ProcessRunOptions>._, A<CancellationToken>._))
        .Invokes((ProcessRunOptions options, CancellationToken _) => captured = options)
        .ReturnsLazily((ProcessRunOptions options, CancellationToken _) =>
            new ProcessRunResult(options, 0, "", ""));

    await new Builder(processRunner).BuildAsync("/repo/src/App", CancellationToken.None);

    Assert.That(captured!.Name, Is.EqualTo("dotnet"));
    Assert.That(captured.Args, Does.Contain("build"));
    Assert.That(captured.WorkingDirectory, Is.EqualTo("/repo/src/App"));
}
```

## Simulating failure

To test failure-handling paths, either return a non-zero result (for code using
`AllowFailedResult`) or have the fake throw (mirroring the default fail-fast behavior):

```csharp
// Code under test sets AllowFailedResult and inspects ExitCode:
A.CallTo(() => processRunner.RunAsync(A<ProcessRunOptions>._, A<CancellationToken>._))
    .ReturnsLazily((ProcessRunOptions options, CancellationToken _) =>
        new ProcessRunResult(options, 1, "", "fatal: not a git repository"));

// Code under test relies on the default throwing behavior:
A.CallTo(() => processRunner.RunAsync(A<ProcessRunOptions>._, A<CancellationToken>._))
    .Throws(new Exception("Process 'git' failed with exit code 128"));
```

## Tips

- **Don't fake what you don't own in integration tests.** For end-to-end confidence, run a real,
  harmless executable (e.g. `dotnet --version`) against the real runner registered via
  `AddProcessRunner()`.
- **Keep options construction in testable seams.** If a class builds complex
  `ProcessRunOptions`, consider exposing that construction through an internal method you can
  assert on directly.
- **Remember the contract:** with default options, your code only ever sees results with
  `ExitCode == 0`; non-zero exits arrive as exceptions.

