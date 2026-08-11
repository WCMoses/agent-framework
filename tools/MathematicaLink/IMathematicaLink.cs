namespace MathematicaLink;

/// <summary>
/// A single, long-lived connection to one Mathematica kernel.
///
/// Contract notes:
///  * The kernel processes one evaluation at a time. Implementations MUST
///    serialize calls — concurrent evaluations corrupt the link. Callers can
///    treat every method as safe to await from multiple tasks; the
///    implementation queues them.
///  * Every method returns a <see cref="MathResult"/> rather than throwing for
///    expected failures (kernel emitted messages, evaluation failed, timed
///    out). Only programmer errors (null args, disposed object) throw.
///  * "Restart is fine" is a first-class recovery path — see
///    <see cref="RestartAsync"/>. Callers that get a <see cref="MathStatus.LinkError"/>
///    or <see cref="MathStatus.Timeout"/> should restart rather than retry in place.
/// </summary>
public interface IMathematicaLink : IAsyncDisposable
{
    /// <summary>True once <see cref="StartAsync"/> has connected a live kernel.</summary>
    bool IsRunning { get; }

    /// <summary>Cold-start a kernel and prepare the link for evaluation.</summary>
    Task<MathResult> StartAsync(CancellationToken ct = default);

    /// <summary>
    /// Load a package into the running kernel (e.g. the OpticaEM load command).
    /// This is just an evaluation, but modelled separately so the runbook can
    /// track package-load idiosyncrasies distinctly from ordinary evals.
    /// </summary>
    Task<MathResult> LoadPackageAsync(string loadCommand, CancellationToken ct = default);

    /// <summary>
    /// Evaluate an expression and return its InputForm as text, plus any
    /// messages the kernel emitted while producing it.
    /// </summary>
    Task<MathResult<string>> EvaluateAsync(string expression, CancellationToken ct = default);

    /// <summary>
    /// Evaluate an expression that produces a graphic and return it as PNG bytes.
    /// Use for the OpticaEM ladder steps that "return a picture".
    /// </summary>
    Task<MathResult<byte[]>> EvaluateToImageAsync(
        string expression, int width = 600, int height = 600, CancellationToken ct = default);

    /// <summary>Minimal liveness probe — sends <c>2+2</c> and checks for <c>4</c>.</summary>
    Task<MathResult> HealthCheckAsync(CancellationToken ct = default);

    /// <summary>Shut the kernel down and cold-start a fresh one. The accepted recovery path.</summary>
    Task<MathResult> RestartAsync(CancellationToken ct = default);

    /// <summary>Shut the kernel down cleanly.</summary>
    Task ShutdownAsync(CancellationToken ct = default);
}
