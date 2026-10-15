namespace Invex.Process;

/// <summary>
///     Defines a service for executing external processes with comprehensive logging, output
///     capture, and error handling.
/// </summary>
/// <remarks>
///     <para>
///         Both stdout and stderr are captured in real time via async read loops so that long-running
///         processes stream their output to the logger rather than buffering it until exit.
///     </para>
///     <para>
///         This interface is registered as a singleton by <see cref="ProcessHostExtensions" />.
///         Inject it into build targets and helpers instead of using
///         <see cref="System.Diagnostics.Process" /> directly, to benefit from consistent logging
///         and uniform error handling across all process invocations.
///     </para>
/// </remarks>
/// <example>
///     Running a tool and inspecting its captured output:
///     <code>
///     var result = await processRunner.RunAsync(
///         new ProcessRunOptions("git", "rev-parse HEAD")
///         {
///             WorkingDirectory = repoRoot,
///         },
///         cancellationToken);
///
///     var commitSha = result.Output.Trim();
///     </code>
/// </example>
[PublicAPI]
public interface IProcessRunner
{
    /// <summary>
    ///     Executes an external process synchronously, blocking until it exits, and returns its
    ///     result.
    /// </summary>
    /// <param name="options">The configuration for the process execution.</param>
    /// <returns>
    ///     A <see cref="ProcessRunResult" /> containing the exit code and captured output.
    ///     Only returned when the exit code is zero, or when
    ///     <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>true</c>.
    /// </returns>
    /// <exception cref="ProcessException">
    ///     Thrown when the process exits with a non-zero code and
    ///     <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>false</c>.  The message
    ///     includes the command line, exit code, working directory (when set), and any captured
    ///     stderr; <see cref="ProcessException.Result" /> carries the full captured result.
    /// </exception>
    /// <exception cref="System.ComponentModel.Win32Exception">
    ///     Thrown when the executable named by <see cref="ProcessRunOptions.Name" /> cannot be
    ///     found or started.
    /// </exception>
    ProcessRunResult Run(ProcessRunOptions options);

    /// <summary>
    ///     Executes an external process asynchronously and returns its result when it exits.
    /// </summary>
    /// <param name="options">The configuration for the process execution.</param>
    /// <param name="cancellationToken">
    ///     A token that cancels the wait for the process to exit.  Note: cancelling the token
    ///     aborts the wait but does <em>not</em> kill the underlying OS process, which continues
    ///     to run; any output captured so far is discarded.
    /// </param>
    /// <returns>
    ///     A task that resolves to a <see cref="ProcessRunResult" /> when the process exits.
    ///     Only completes successfully when the exit code is zero, or when
    ///     <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>true</c>.
    /// </returns>
    /// <exception cref="ProcessException">
    ///     Thrown (on the returned task) when the process exits with a non-zero code and
    ///     <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>false</c>.  The message
    ///     includes the command line, exit code, working directory (when set), and any captured
    ///     stderr; <see cref="ProcessException.Result" /> carries the full captured result.
    /// </exception>
    /// <exception cref="System.ComponentModel.Win32Exception">
    ///     Thrown when the executable named by <see cref="ProcessRunOptions.Name" /> cannot be
    ///     found or started.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    ///     Thrown (on the returned task) when <paramref name="cancellationToken" /> is cancelled
    ///     before the process exits.
    /// </exception>
    Task<ProcessRunResult> RunAsync(ProcessRunOptions options, CancellationToken cancellationToken = default);
}

/// <summary>
///     Default implementation of <see cref="IProcessRunner" /> that wraps
///     <see cref="System.Diagnostics.Process" />.
/// </summary>
/// <remarks>
///     <para>
///         stdout and stderr are both redirected and streamed to the logger in real time rather than
///         buffered. On .NET 11 and later they are read as one ordered stream of lines with
///         <c>Process.ReadAllLines</c> / <c>Process.ReadAllLinesAsync</c>; on .NET 10 they are read via
///         <see cref="System.Diagnostics.Process.BeginOutputReadLine" /> /
///         <see cref="System.Diagnostics.Process.BeginErrorReadLine" />.
///     </para>
///     <para>
///         Each received line is passed through <see cref="ProcessRunOptions.TransformOutput" /> or
///         <see cref="ProcessRunOptions.TransformError" /> when set: a <c>null</c> return from the
///         transform suppresses the line from both the log and the captured result.
///     </para>
///     <para>
///         On a non-zero exit code, the complete captured stdout and stderr are re-logged in full
///         — stdout at <see cref="LogLevel.Information" /> and stderr at
///         <see cref="LogLevel.Warning" /> — when their configured per-line levels
///         (<see cref="ProcessRunOptions.OutputLogLevel" /> / <see cref="ProcessRunOptions.ErrorLogLevel" />)
///         are below <see cref="LogLevel.Information" />.  This happens regardless of
///         <see cref="ProcessRunOptions.AllowFailedResult" />, ensuring failure diagnostics are
///         visible even when per-line output logging was suppressed.  A descriptive
///         <see cref="ProcessException" /> is then thrown unless <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>true</c>.
///     </para>
/// </remarks>
/// <param name="logger">The logger for capturing process execution diagnostics.</param>
[PublicAPI]
internal sealed class ProcessRunner(ILogger<ProcessRunner> logger) : IProcessRunner
{
    /// <inheritdoc />
    public ProcessRunResult Run(ProcessRunOptions options)
    {
        switch (options)
        {
            case { WorkingDirectory.Length: > 0, EnvironmentVariables.Count: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} in {WorkingDirectory} with env {EnvironmentVariables}",
                    options.Name,
                    options.Args,
                    options.WorkingDirectory,
                    FormatEnvironmentVariableNames(options));

                break;
            case { WorkingDirectory.Length: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} in {WorkingDirectory}",
                    options.Name,
                    options.Args,
                    options.WorkingDirectory);

                break;
            case { EnvironmentVariables.Count: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} with env {EnvironmentVariables}",
                    options.Name,
                    options.Args,
                    FormatEnvironmentVariableNames(options));

                break;
            default:
                logger.Log(options.InvocationLogLevel, "Run: {Name} {Args}", options.Name, options.Args);

                break;
        }

        using var process = new System.Diagnostics.Process();

        process.StartInfo = new()
        {
            FileName = options.Name,
            Arguments = options.Args,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var environmentVariable in options.EnvironmentVariables)
            process.StartInfo.Environment[environmentVariable.Key] = environmentVariable.Value;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

#if NET11_0_OR_GREATER
        process.Start();

        // stdout and stderr arrive as one stream of lines, in the order the process wrote them.
        foreach (var line in process.ReadAllLines())
            OnLine(line.Content, line.StandardError);

        var exitStatus = process.WaitForExitStatus();
        var exitCode = exitStatus.ExitCode;
        var signal = exitStatus.Signal;
#else
        process.OutputDataReceived += OnProcessOnOutputDataReceived;
        process.ErrorDataReceived += OnProcessOnErrorDataReceived;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
        }
        finally
        {
            process.OutputDataReceived -= OnProcessOnOutputDataReceived;
            process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
        }

        var exitCode = process.ExitCode;
        PosixSignal? signal = null;
#endif

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        var result = new ProcessRunResult(options, exitCode, output, error);

        if (result.ExitCode is 0)
            return result;

        if (options.OutputLogLevel < LogLevel.Information)
            logger.LogInformation("{Output}", output);

        if (options.ErrorLogLevel < LogLevel.Information)
            logger.LogWarning("{Error}", error);

        if (options.AllowFailedResult)
        {
            logger.Log(options.InvocationLogLevel, "Process finished with exit code {ExitCode}", result.ExitCode);

            return result;
        }

        throw new ProcessException(BuildProcessErrorMessage(options, result.ExitCode, signal, error), result);

        void OnLine(string? data, bool isError)
        {
            if (data is null)
                return;

            var transform = isError
                ? options.TransformError
                : options.TransformOutput;

            var text = transform is not null
                ? transform(data)
                : data;

            if (text is null)
                return;

            if (isError)
            {
                errorBuilder.AppendLine(text);
                logger.Log(options.ErrorLogLevel, "{Error}", text);
            }
            else
            {
                outputBuilder.AppendLine(text);
                logger.Log(options.OutputLogLevel, "{Output}", text);
            }
        }

#if !NET11_0_OR_GREATER
        void OnProcessOnErrorDataReceived(object _, DataReceivedEventArgs e) =>
            OnLine(e.Data, true);

        void OnProcessOnOutputDataReceived(object _, DataReceivedEventArgs e) =>
            OnLine(e.Data, false);
#endif
    }

    /// <inheritdoc />
    public async Task<ProcessRunResult> RunAsync(
        ProcessRunOptions options,
        CancellationToken cancellationToken = default)
    {
        switch (options)
        {
            case { WorkingDirectory.Length: > 0, EnvironmentVariables.Count: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} in {WorkingDirectory} with env {EnvironmentVariables}",
                    options.Name,
                    options.Args,
                    options.WorkingDirectory,
                    FormatEnvironmentVariableNames(options));

                break;
            case { WorkingDirectory.Length: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} in {WorkingDirectory}",
                    options.Name,
                    options.Args,
                    options.WorkingDirectory);

                break;
            case { EnvironmentVariables.Count: > 0 }:
                logger.Log(options.InvocationLogLevel,
                    "Run: {Name} {Args} with env {EnvironmentVariables}",
                    options.Name,
                    options.Args,
                    FormatEnvironmentVariableNames(options));

                break;
            default:
                logger.Log(options.InvocationLogLevel, "Run: {Name} {Args}", options.Name, options.Args);

                break;
        }

        using var process = new System.Diagnostics.Process();

        process.StartInfo = new()
        {
            FileName = options.Name,
            Arguments = options.Args,
            WorkingDirectory = options.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var environmentVariable in options.EnvironmentVariables)
            process.StartInfo.Environment[environmentVariable.Key] = environmentVariable.Value;

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

#if NET11_0_OR_GREATER
        process.Start();

        // stdout and stderr arrive as one stream of lines, in the order the process wrote them.
        await foreach (var line in process.ReadAllLinesAsync(cancellationToken))
            OnLine(line.Content, line.StandardError);

        var exitStatus = await process.WaitForExitStatusAsync(cancellationToken);
        var exitCode = exitStatus.ExitCode;
        var signal = exitStatus.Signal;
#else
        process.OutputDataReceived += OnProcessOnOutputDataReceived;
        process.ErrorDataReceived += OnProcessOnErrorDataReceived;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);
        }
        finally
        {
            process.OutputDataReceived -= OnProcessOnOutputDataReceived;
            process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
        }

        var exitCode = process.ExitCode;
        PosixSignal? signal = null;
#endif

        var output = outputBuilder.ToString();
        var error = errorBuilder.ToString();

        var result = new ProcessRunResult(options, exitCode, output, error);

        if (result.ExitCode is 0)
            return result;

        if (options.OutputLogLevel < LogLevel.Information)
            logger.LogInformation("{Output}", output);

        if (options.ErrorLogLevel < LogLevel.Information)
            logger.LogWarning("{Error}", error);

        if (options.AllowFailedResult)
        {
            logger.Log(options.InvocationLogLevel, "Process finished with exit code {ExitCode}", result.ExitCode);

            return result;
        }

        throw new ProcessException(BuildProcessErrorMessage(options, result.ExitCode, signal, error), result);

        void OnLine(string? data, bool isError)
        {
            if (data is null)
                return;

            var transform = isError
                ? options.TransformError
                : options.TransformOutput;

            var text = transform is not null
                ? transform(data)
                : data;

            if (text is null)
                return;

            if (isError)
            {
                errorBuilder.AppendLine(text);
                logger.Log(options.ErrorLogLevel, "{Error}", text);
            }
            else
            {
                outputBuilder.AppendLine(text);
                logger.Log(options.OutputLogLevel, "{Output}", text);
            }
        }

#if !NET11_0_OR_GREATER
        void OnProcessOnErrorDataReceived(object _, DataReceivedEventArgs e) =>
            OnLine(e.Data, true);

        void OnProcessOnOutputDataReceived(object _, DataReceivedEventArgs e) =>
            OnLine(e.Data, false);
#endif
    }

    /// <summary>
    ///     Formats the names of the configured environment variables for the invocation log line.
    /// </summary>
    /// <remarks>
    ///     Only the variable names are logged, never their values, because environment variables
    ///     are a common way to pass secrets (tokens, passwords) to child processes.
    /// </remarks>
    /// <param name="options">The options whose environment variables are formatted.</param>
    /// <returns>A comma-separated list of the variable names.</returns>
    private static string FormatEnvironmentVariableNames(ProcessRunOptions options) =>
        string.Join(", ", options.EnvironmentVariables.Keys);

    private static string BuildProcessErrorMessage(
        ProcessRunOptions options,
        int exitCode,
        PosixSignal? signal,
        string error)
    {
        var errorMessage = new StringBuilder();

        errorMessage.AppendLine(signal is { } terminatingSignal
            ? $"Process '{options.Name}' was terminated by signal {terminatingSignal} (exit code {exitCode})"
            : $"Process '{options.Name}' failed with exit code {exitCode}");
        errorMessage.AppendLine($"  Command: {options.Name} {options.Args}");

        if (options.WorkingDirectory is { Length: > 0 })
            errorMessage.AppendLine($"  Working Directory: {options.WorkingDirectory}");

        if (error is { Length: > 0 })
        {
            errorMessage.AppendLine("  Error Output:");
            errorMessage.AppendLine($"    {error}");
        }

        return errorMessage
            .ToString()
            .TrimEnd();
    }
}
