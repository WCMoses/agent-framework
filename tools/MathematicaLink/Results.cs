namespace MathematicaLink;

/// <summary>
/// How a kernel operation ended. Distinguishes transport/link failures (the
/// kernel is unreachable) from evaluation failures (the kernel computed
/// something but emitted error messages) — they need different recovery.
/// </summary>
public enum MathStatus
{
    /// <summary>The link has not been started yet, or has been shut down.</summary>
    NotStarted,

    /// <summary>Completed with no error-severity messages.</summary>
    Ok,

    /// <summary>Completed, but the kernel emitted one or more messages (see <see cref="MathResult.Messages"/>).</summary>
    CompletedWithMessages,

    /// <summary>The kernel produced a value we treat as failure (e.g. $Failed) or error-tagged messages.</summary>
    EvaluationError,

    /// <summary>Transport failure — the MathLink/WSTP connection errored or the kernel died.</summary>
    LinkError,

    /// <summary>The evaluation was aborted because it exceeded its time budget.</summary>
    Timeout,
}

public enum MessageSeverity
{
    Info,
    Warning,
    Error,

    /// <summary>A transport-level problem, not a Mathematica <c>Message[...]</c>.</summary>
    LinkError,
}

/// <summary>
/// One diagnostic emitted during an evaluation. <see cref="Tag"/> is the
/// Mathematica message name (e.g. <c>Power::infy</c>) when we can recover it;
/// <see cref="Text"/> is the formatted message text or link error string.
/// </summary>
public sealed record MathematicaMessage(string Tag, string Text, MessageSeverity Severity)
{
    public override string ToString() =>
        string.IsNullOrEmpty(Tag) ? $"[{Severity}] {Text}" : $"[{Severity}] {Tag}: {Text}";
}

/// <summary>Result of a kernel operation that returns no payload.</summary>
public class MathResult
{
    public MathStatus Status { get; init; }

    /// <summary>Every diagnostic emitted during the operation, in order. May be empty.</summary>
    public IReadOnlyList<MathematicaMessage> Messages { get; init; } = Array.Empty<MathematicaMessage>();

    /// <summary>Raw text captured from the kernel, kept verbatim for diagnosis.</summary>
    public string Raw { get; init; } = string.Empty;

    public bool IsSuccess => Status is MathStatus.Ok or MathStatus.CompletedWithMessages;

    public bool HasErrors =>
        Status is MathStatus.EvaluationError or MathStatus.LinkError or MathStatus.Timeout
        || Messages.Any(m => m.Severity is MessageSeverity.Error or MessageSeverity.LinkError);

    public static MathResult Ok(IReadOnlyList<MathematicaMessage>? messages = null, string raw = "") => new()
    {
        Status = (messages is { Count: > 0 }) ? MathStatus.CompletedWithMessages : MathStatus.Ok,
        Messages = messages ?? Array.Empty<MathematicaMessage>(),
        Raw = raw,
    };

    public static MathResult Link(string error) => new()
    {
        Status = MathStatus.LinkError,
        Messages = new[] { new MathematicaMessage("", error, MessageSeverity.LinkError) },
        Raw = error,
    };

    public static MathResult TimedOut(string raw = "") => new() { Status = MathStatus.Timeout, Raw = raw };

    public static MathResult Failure(string error, MathStatus status = MathStatus.EvaluationError) => new()
    {
        Status = status,
        Messages = new[] { new MathematicaMessage("", error, MessageSeverity.Error) },
        Raw = error,
    };
}

/// <summary>Result of a kernel operation that returns a payload of type <typeparamref name="T"/>.</summary>
public sealed class MathResult<T> : MathResult
{
    public T? Value { get; init; }

    public static MathResult<T> Ok(T value, IReadOnlyList<MathematicaMessage>? messages = null, string raw = "") => new()
    {
        Status = (messages is { Count: > 0 }) ? MathStatus.CompletedWithMessages : MathStatus.Ok,
        Value = value,
        Messages = messages ?? Array.Empty<MathematicaMessage>(),
        Raw = raw,
    };

    public static new MathResult<T> Link(string error) => new()
    {
        Status = MathStatus.LinkError,
        Messages = new[] { new MathematicaMessage("", error, MessageSeverity.LinkError) },
        Raw = error,
    };

    public static new MathResult<T> TimedOut(string raw = "") => new() { Status = MathStatus.Timeout, Raw = raw };

    public static new MathResult<T> Failure(string error, MathStatus status = MathStatus.EvaluationError) => new()
    {
        Status = status,
        Messages = new[] { new MathematicaMessage("", error, MessageSeverity.Error) },
        Raw = error,
    };
}
