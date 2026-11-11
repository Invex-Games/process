namespace Invex.Process.Tests;

/// <summary>
///     Integration tests for <see cref="ProcessRunner" /> that spawn real OS processes.
///     These tests use <c>dotnet</c> (always available in a .NET test environment) and
///     OS-native shell commands for cross-platform coverage.
/// </summary>
[TestFixture]
internal sealed class ProcessRunnerTests
{
    // Cross-platform command helpers
    private static ProcessRunOptions DotnetVersionOptions() =>
        new("dotnet", "--version");

    private static ProcessRunOptions FailOptions() =>
        OperatingSystem.IsWindows()
            ? new ProcessRunOptions("cmd", "/c exit 1")
            {
                AllowFailedResult = true,
            }
            : new("sh", "-c \"exit 1\"")
            {
                AllowFailedResult = true,
            };

    private static ProcessRunOptions EchoStderrOptions(string text) =>
        OperatingSystem.IsWindows()
            ? new ProcessRunOptions("cmd", $"/c echo {text} 1>&2")
            {
                AllowFailedResult = true,
            }
            : new("sh", $"-c \"echo {text} >&2\"")
            {
                AllowFailedResult = true,
            };

    private static ProcessRunOptions EchoEnvVarOptions(string varName) =>
        OperatingSystem.IsWindows()
            ? new ProcessRunOptions("cmd", $"/c echo %{varName}%")
            : new("sh", $"-c \"echo ${varName}\"");

    private static ProcessRunOptions PrintCwdOptions() =>
        OperatingSystem.IsWindows()
            ? new ProcessRunOptions("cmd", "/c cd")
            : new("pwd", string.Empty);

    // Writes to both streams, ending with an stdout line that has no trailing newline.
    private static ProcessRunOptions MixedStreamsOptions() =>
        new("sh", "-c \"echo out1; echo err1 >&2; echo out2; echo drop-err >&2; printf out3\"")
        {
            TransformOutput = line => line == "out2"
                ? null
                : $"[O]{line}",
            TransformError = line => line.StartsWith("drop", StringComparison.Ordinal)
                ? null
                : $"[E]{line}",
            OutputLogLevel = LogLevel.Information,
            ErrorLogLevel = LogLevel.Warning,
        };

    private static void ShouldHaveMixedStreamsResult(ProcessRunResult result, RecordingLogger logger)
    {
        result.Output.ShouldBe($"[O]out1{Environment.NewLine}[O]out3{Environment.NewLine}");
        result.Error.ShouldBe($"[E]err1{Environment.NewLine}");

        var entries = logger.Entries;
        entries.Where(e => e.Level == LogLevel.Information && e.Message.StartsWith("[O]", StringComparison.Ordinal))
            .Select(e => e.Message)
            .ShouldBe(["[O]out1", "[O]out3"], ignoreOrder: true);
        entries.Where(e => e.Level == LogLevel.Warning && e.Message.StartsWith("[E]", StringComparison.Ordinal))
            .Select(e => e.Message)
            .ShouldBe(["[E]err1"]);
        // The invocation log line names the command, so skip it when checking that suppressed lines never reach the log.
        entries.Where(e => !e.Message.Contains("echo", StringComparison.Ordinal))
            .ShouldNotContain(e => e.Message.Contains("out2") || e.Message.Contains("drop-err"));
    }

    private static ProcessRunOptions LongRunningOptions() =>
        // ping sends one packet per second and works reliably with redirected stdout;
        // unlike `timeout`, it does not exit early when used in a non-interactive context.
        OperatingSystem.IsWindows()
            ? new ProcessRunOptions("ping", "127.0.0.1 -n 60")
            {
                AllowFailedResult = true,
            }
            : new("ping", "-c 60 127.0.0.1")
            {
                AllowFailedResult = true,
            };

    private static ProcessRunner CreateRunner() =>
        new(A.Fake<ILogger<ProcessRunner>>());

    [Test]
    public void Run_WithSuccessfulCommand_ReturnsExitCodeZero()
    {
        // Arrange
        var runner = CreateRunner();

        // Act
        var result = runner.Run(DotnetVersionOptions());

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Test]
    public void Run_WithSuccessfulCommand_CapturesStdout()
    {
        // Arrange
        var runner = CreateRunner();

        // Act
        var result = runner.Run(DotnetVersionOptions());

        // Assert
        result
            .Output
            .Trim()
            .ShouldNotBeEmpty();
    }

    [Test]
    public void Run_WithSuccessfulCommand_StoresRunOptions()
    {
        // Arrange
        var runner = CreateRunner();
        var options = DotnetVersionOptions();

        // Act
        var result = runner.Run(options);

        // Assert
        result.RunOptions.ShouldBeSameAs(options);
    }

    [Test]
    public void Run_WhenExitCodeIsNonZero_AndAllowFailedResultIsFalse_Throws()
    {
        // Arrange
        var runner = CreateRunner();

        var options = FailOptions() with
        {
            AllowFailedResult = false,
        };

        // Act / Assert
        Should.Throw<Exception>(() => runner.Run(options));
    }

    [Test]
    public void Run_WhenExitCodeIsNonZero_AndAllowFailedResultIsTrue_ReturnsResult()
    {
        // Arrange
        var runner = CreateRunner();
        var options = FailOptions();

        // Act
        var result = runner.Run(options);

        // Assert
        result.ExitCode.ShouldNotBe(0);
    }

    [Test]
    public void Run_WhenExceptionThrown_ErrorMessageContainsCommandName()
    {
        // Arrange
        var runner = CreateRunner();

        var options = FailOptions() with
        {
            AllowFailedResult = false,
        };

        // Act / Assert
        Should
            .Throw<Exception>(() => runner.Run(options))
            .Message
            .ShouldContain(options.Name);
    }

    [Test]
    public void Run_WithWorkingDirectory_ProcessRunsInThatDirectory()
    {
        // Arrange
        var runner = CreateRunner();

        var tempDir = Path
            .GetTempPath()
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var options = PrintCwdOptions() with
        {
            WorkingDirectory = tempDir,
        };

        // Act
        var result = runner.Run(options);

        // Assert
        // The output contains the working directory path (normalise separators for comparison)
        result
            .Output
            .Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar)
            .ShouldContain(tempDir
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar));
    }

    [Test]
    public void Run_WithEnvironmentVariable_ProcessReceivesIt()
    {
        // Arrange
        var runner = CreateRunner();
        const string key = "ATOM_TEST_VAR_RUNNER";
        const string value = "hello-from-atom";

        var options = EchoEnvVarOptions(key) with
        {
            EnvironmentVariables = new()
            {
                [key] = value,
            },
        };

        // Act
        var result = runner.Run(options);

        // Assert
        result
            .Output
            .Trim()
            .ShouldContain(value);
    }

    [Test]
    public void Run_WithEnvironmentVariable_LogsNameButNotValue()
    {
        // Arrange
        var logger = new RecordingLogger();
        var runner = new ProcessRunner(logger);
        const string key = "ATOM_TEST_SECRET_RUNNER";
        const string value = "super-secret-value";

        var options = EchoEnvVarOptions(key) with
        {
            EnvironmentVariables = new()
            {
                [key] = value,
            },
            TransformOutput = _ => null,
        };

        // Act
        runner.Run(options);

        // Assert
        logger
            .Messages
            .Where(m => m.StartsWith("Run: ", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ShouldContain(key);
        logger.Messages.ShouldAllBe(m => !m.Contains(value));
    }

    [Test]
    public void Run_WhenExitCodeIsNonZero_ThrowsProcessExceptionWithResult()
    {
        // Arrange
        var runner = CreateRunner();

        var options = FailOptions() with
        {
            AllowFailedResult = false,
        };

        // Act
        var exception = Should.Throw<ProcessException>(() => runner.Run(options));

        // Assert
        exception.ExitCode.ShouldBe(1);
        exception.Result.ExitCode.ShouldBe(1);
        exception.Result.RunOptions.ShouldBeSameAs(options);
        exception.Message.ShouldContain("exit code 1");
    }

    [Test]
    public void Run_WithTransformOutput_TransformIsAppliedToEachLine()
    {
        // Arrange
        var runner = CreateRunner();

        var options = DotnetVersionOptions() with
        {
            TransformOutput = line => $"[PREFIXED] {line}",
        };

        // Act
        var result = runner.Run(options);

        // Assert
        result.Output.ShouldContain("[PREFIXED]");
    }

    [Test]
    public void Run_WithTransformOutputReturningNull_LineIsOmittedFromOutput()
    {
        // Arrange
        var runner = CreateRunner();

        var options = DotnetVersionOptions() with
        {
            TransformOutput = _ => null,
        };

        // Act
        var result = runner.Run(options);

        // Assert
        result.Output.ShouldBeEmpty();
    }

    [Test]
    public void Run_WithTransformError_TransformIsAppliedToErrorLines()
    {
        // Arrange
        var runner = CreateRunner();
        const string marker = "ERRORMARKER";

        var options = EchoStderrOptions(marker) with
        {
            TransformError = line => $"[ERR] {line}",
        };

        // Act
        var result = runner.Run(options);

        // Assert
        result.Error.ShouldContain("[ERR]");
    }

    [Test]
    public void Run_WithTransformErrorReturningNull_LineIsOmittedFromError()
    {
        // Arrange
        var runner = CreateRunner();
        const string marker = "ERRORMARKER";

        var options = EchoStderrOptions(marker) with
        {
            TransformError = _ => null,
        };

        // Act
        var result = runner.Run(options);

        // Assert
        result.Error.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_WithSuccessfulCommand_ReturnsExitCodeZero()
    {
        // Arrange
        var runner = CreateRunner();

        // Act
        var result = await runner.RunAsync(DotnetVersionOptions());

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Test]
    public async Task RunAsync_WithSuccessfulCommand_CapturesStdout()
    {
        // Arrange
        var runner = CreateRunner();

        // Act
        var result = await runner.RunAsync(DotnetVersionOptions());

        // Assert
        result
            .Output
            .Trim()
            .ShouldNotBeEmpty();
    }

    [Test]
    public async Task RunAsync_WhenExitCodeIsNonZero_AndAllowFailedResultIsFalse_Throws()
    {
        // Arrange
        var runner = CreateRunner();

        var options = FailOptions() with
        {
            AllowFailedResult = false,
        };

        // Act / Assert
        await Should.ThrowAsync<Exception>(() => runner.RunAsync(options));
    }

    [Test]
    public async Task RunAsync_WhenExitCodeIsNonZero_AndAllowFailedResultIsTrue_ReturnsResult()
    {
        // Arrange
        var runner = CreateRunner();

        // Act
        var result = await runner.RunAsync(FailOptions());

        // Assert
        result.ExitCode.ShouldNotBe(0);
    }

    [Test]
    public async Task RunAsync_WithEnvironmentVariable_ProcessReceivesIt()
    {
        // Arrange
        var runner = CreateRunner();
        const string key = "ATOM_TEST_VAR_RUNNER_ASYNC";
        const string value = "async-hello-from-atom";

        var options = EchoEnvVarOptions(key) with
        {
            EnvironmentVariables = new()
            {
                [key] = value,
            },
        };

        // Act
        var result = await runner.RunAsync(options);

        // Assert
        result
            .Output
            .Trim()
            .ShouldContain(value);
    }

    [Test]
    public async Task RunAsync_WithEnvironmentVariable_LogsNameButNotValue()
    {
        // Arrange
        var logger = new RecordingLogger();
        var runner = new ProcessRunner(logger);
        const string key = "ATOM_TEST_SECRET_RUNNER_ASYNC";
        const string value = "async-super-secret-value";

        var options = EchoEnvVarOptions(key) with
        {
            EnvironmentVariables = new()
            {
                [key] = value,
            },
            TransformOutput = _ => null,
        };

        // Act
        await runner.RunAsync(options);

        // Assert
        logger
            .Messages
            .Where(m => m.StartsWith("Run: ", StringComparison.Ordinal))
            .ShouldHaveSingleItem()
            .ShouldContain(key);
        logger.Messages.ShouldAllBe(m => !m.Contains(value));
    }

    [Test]
    public async Task RunAsync_WhenExitCodeIsNonZero_ThrowsProcessExceptionWithResult()
    {
        // Arrange
        var runner = CreateRunner();

        var options = FailOptions() with
        {
            AllowFailedResult = false,
        };

        // Act
        var exception = await Should.ThrowAsync<ProcessException>(() => runner.RunAsync(options));

        // Assert
        exception.ExitCode.ShouldBe(1);
        exception.Result.ExitCode.ShouldBe(1);
        exception.Result.RunOptions.ShouldBeSameAs(options);
        exception.Message.ShouldContain("exit code 1");
    }

    [Test]
    public async Task RunAsync_WithTransformOutput_TransformIsAppliedToEachLine()
    {
        // Arrange
        var runner = CreateRunner();

        var options = DotnetVersionOptions() with
        {
            TransformOutput = line => $"[ASYNC-PREFIXED] {line}",
        };

        // Act
        var result = await runner.RunAsync(options);

        // Assert
        result.Output.ShouldContain("[ASYNC-PREFIXED]");
    }

    [Test]
    public async Task RunAsync_WithTransformOutputReturningNull_LineIsOmittedFromOutput()
    {
        // Arrange
        var runner = CreateRunner();

        var options = DotnetVersionOptions() with
        {
            TransformOutput = _ => null,
        };

        // Act
        var result = await runner.RunAsync(options);

        // Assert
        result.Output.ShouldBeEmpty();
    }

    [Test]
    public async Task RunAsync_WhenCancellationTokenIsCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var runner = CreateRunner();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act / Assert
        // The token is pre-cancelled; WaitForExitAsync will throw immediately.
        // dotnet is used as the process so even if it starts it exits quickly.
        await Should.ThrowAsync<OperationCanceledException>(() => runner.RunAsync(LongRunningOptions(), cts.Token));
    }

    [Test]
    public async Task RunAsync_WhenCancelledMidRun_ThrowsOperationCanceledException()
    {
        // Arrange
        var runner = CreateRunner();

        using var cts = new CancellationTokenSource();

        // Cancel after a short delay to let the process start
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        // Act / Assert
        await Should.ThrowAsync<OperationCanceledException>(() => runner.RunAsync(LongRunningOptions(), cts.Token));
    }

    [Test]
    public void Run_WithMixedStreams_CapturesAndLogsEachStreamSeparately()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Uses sh to write to both streams.");

        // Arrange
        var logger = new RecordingLogger();
        var runner = new ProcessRunner(logger);

        // Act
        var result = runner.Run(MixedStreamsOptions());

        // Assert
        ShouldHaveMixedStreamsResult(result, logger);
    }

    [Test]
    public async Task RunAsync_WithMixedStreams_CapturesAndLogsEachStreamSeparately()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Uses sh to write to both streams.");

        // Arrange
        var logger = new RecordingLogger();
        var runner = new ProcessRunner(logger);

        // Act
        var result = await runner.RunAsync(MixedStreamsOptions());

        // Assert
        ShouldHaveMixedStreamsResult(result, logger);
    }

#if NET11_0_OR_GREATER
    private static ProcessRunOptions KilledBySigtermOptions() =>
        new("sh", "-c \"kill -TERM $$\"");

    [Test]
    public void Run_WhenProcessIsKilledBySignal_ErrorMessageNamesTheSignal()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("POSIX signals are not available on Windows.");

        // Arrange
        var runner = CreateRunner();

        // Act
        var exception = Should.Throw<ProcessException>(() => runner.Run(KilledBySigtermOptions()));

        // Assert
        exception.Message.ShouldContain("terminated by signal SIGTERM");
        exception.Result.ExitCode.ShouldNotBe(0);
    }

    [Test]
    public async Task RunAsync_WhenProcessIsKilledBySignal_ErrorMessageNamesTheSignal()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("POSIX signals are not available on Windows.");

        // Arrange
        var runner = CreateRunner();

        // Act
        var exception = await Should.ThrowAsync<ProcessException>(() => runner.RunAsync(KilledBySigtermOptions()));

        // Assert
        exception.Message.ShouldContain("terminated by signal SIGTERM");
        exception.Result.ExitCode.ShouldNotBe(0);
    }

#endif
    /// <summary>
    ///     A minimal <see cref="ILogger{TCategoryName}" /> that records every formatted message.
    /// </summary>
    private sealed class RecordingLogger : ILogger<ProcessRunner>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public IReadOnlyList<string> Messages
        {
            get
            {
                lock (_entries)
                    return [.. _entries.Select(e => e.Message)];
            }
        }

        public IReadOnlyList<(LogLevel Level, string Message)> Entries
        {
            get
            {
                lock (_entries)
                    return [.. _entries];
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull =>
            null;

        public bool IsEnabled(LogLevel logLevel) =>
            true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (_entries)
                _entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
