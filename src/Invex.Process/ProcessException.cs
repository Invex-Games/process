namespace Invex.Process;

/// <summary>
///     The exception thrown by <see cref="IProcessRunner" /> when a process exits with a non-zero
///     exit code and <see cref="ProcessRunOptions.AllowFailedResult" /> is <c>false</c>.
/// </summary>
/// <remarks>
///     <para>
///         The <see cref="Exception.Message" /> describes the failure (command line, exit code,
///         working directory when set, and any captured stderr), and <see cref="Result" /> exposes
///         the full result of the failed run — including the captured stdout and stderr and the
///         originating <see cref="ProcessRunOptions" /> — for programmatic inspection.
///     </para>
///     <para>
///         Derives directly from <see cref="Exception" />, so existing <c>catch (Exception)</c>
///         blocks continue to handle it.
///     </para>
/// </remarks>
/// <example>
///     Inspecting a failed run:
///     <code>
///     try
///     {
///         processRunner.Run(new ProcessRunOptions("git", "rev-parse HEAD"));
///     }
///     catch (ProcessException ex) when (ex.ExitCode == 128)
///     {
///         logger.LogWarning("Not a git repository: {Error}", ex.Result.Error);
///     }
///     </code>
/// </example>
[PublicAPI]
[SuppressMessage("Roslynator",
    "RCS1194:Implement exception constructors",
    Justification = "A ProcessException always describes a failed run, so every constructor requires its result.")]
public sealed class ProcessException : Exception
{
    /// <summary>
    ///     Initializes a new instance of <see cref="ProcessException" /> for a failed process run.
    /// </summary>
    /// <param name="message">The message that describes the failure.</param>
    /// <param name="result">The result of the failed process run.</param>
    public ProcessException(string message, ProcessRunResult result) : base(message)
    {
        Result = result;
    }

    /// <summary>
    ///     Gets the result of the failed process run, including the captured stdout and stderr and
    ///     the options the process was run with.
    /// </summary>
    public ProcessRunResult Result { get; }

    /// <summary>
    ///     Gets the non-zero exit code the process returned.
    /// </summary>
    /// <remarks>
    ///     Shorthand for <c>Result.ExitCode</c>.
    /// </remarks>
    public int ExitCode => Result.ExitCode;
}
