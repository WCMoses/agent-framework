// Wolfram.NETLink lives in Wolfram.NETLink.dll, shipped INSIDE your Mathematica
// install (…/SystemFiles/Links/NETLink/Wolfram.NETLink.dll). It is not on NuGet.
// See MathematicaLink.csproj for how to reference it. This file will not compile
// until that reference resolves and a kernel is installed.
using Wolfram.NETLink;

namespace MathematicaLink;

/// <summary>
/// A local, persistent-kernel implementation of <see cref="IMathematicaLink"/>
/// over Wolfram.NETLink / WSTP. One process, one kernel, calls serialized.
///
/// This is the "local machine" implementation. Cloud / Wolfram-Cloud variants
/// implement the same interface so nothing above the link cares which is live.
/// </summary>
public sealed class NetLinkMathematicaLink : IMathematicaLink
{
    // Sentinels used to frame the {result, $MessageList} payload so we can split
    // it back apart in C# unambiguously. See EvaluateAsync. This wrapper is the
    // PRIMARY SEAM to validate against the real kernel (Phase 1, Goal 2) — if
    // message capture looks wrong, this string is what you tune.
    private const string ResultMarker = "<<<AF-RESULT>>>";
    private const string MsgMarker = "<<<AF-MESSAGES>>>";
    private const string EndMarker = "<<<AF-END>>>";

    private readonly string _kernelCommandLine;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IKernelLink? _link;

    /// <param name="kernelCommandLine">
    /// The MathLink launch string, e.g. on Windows:
    ///   "-linkmode launch -linkname \"C:\\Program Files\\Wolfram Research\\Mathematica\\14.0\\MathKernel.exe\""
    /// or, if the kernel is on PATH: "-linkmode launch -linkname math -mathlink".
    /// </param>
    public NetLinkMathematicaLink(string kernelCommandLine) =>
        _kernelCommandLine = kernelCommandLine;

    public bool IsRunning => _link is not null;

    public async Task<MathResult> StartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_link is not null) return MathResult.Ok();

            return await Task.Run(() =>
            {
                try
                {
                    var link = MathLinkFactory.CreateKernelLink(_kernelCommandLine);
                    // Discard the initial InputNamePacket the kernel emits on connect.
                    link.WaitAndDiscardAnswer();
                    _link = link;
                    return MathResult.Ok(raw: "kernel started");
                }
                catch (Exception ex)
                {
                    return MathResult.Link($"failed to start kernel: {ex.Message}");
                }
            }, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public Task<MathResult> LoadPackageAsync(string loadCommand, CancellationToken ct = default) =>
        // A package load is just an evaluation; surfaced separately so the runbook
        // can log load-specific quirks. Callers should inspect Messages for
        // "package not found" / context shadowing warnings.
        EvaluateAsync(loadCommand, ct).ContinueWith(t =>
        {
            var r = t.Result;
            return new MathResult { Status = r.Status, Messages = r.Messages, Raw = r.Raw };
        }, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    public async Task<MathResult<string>> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        if (_link is null) return MathResult<string>.Failure("kernel not started", MathStatus.NotStarted);

        // Wrap so the kernel returns BOTH the value (InputForm) and the message
        // list generated during evaluation, framed by sentinels. $MessageList is
        // reset per top-level evaluation, so it must be read in the same round trip.
        string wrapped =
            "Module[{afRes = (" + expression + ")}, " +
            "\"" + ResultMarker + "\" <> ToString[afRes, InputForm] <> " +
            "\"" + MsgMarker + "\" <> ToString[$MessageList, InputForm] <> " +
            "\"" + EndMarker + "\"]";

        return await RunGuarded(link =>
        {
            string raw = link.EvaluateToOutputForm(wrapped, 0);
            if (link.Error != 0)
            {
                string err = link.ErrorMessage;
                link.ClearError();
                return MathResult<string>.Link(err);
            }

            var (value, messages) = ParseFramed(raw);
            var status = messages.Any(m => m.Severity == MessageSeverity.Error)
                ? MathStatus.EvaluationError
                : messages.Count > 0 ? MathStatus.CompletedWithMessages : MathStatus.Ok;

            return new MathResult<string> { Status = status, Value = value, Messages = messages, Raw = raw };
        }, () => MathResult<string>.TimedOut(), ct).ConfigureAwait(false);
    }

    public async Task<MathResult<byte[]>> EvaluateToImageAsync(
        string expression, int width = 600, int height = 600, CancellationToken ct = default)
    {
        if (_link is null) return MathResult<byte[]>.Failure("kernel not started", MathStatus.NotStarted);

        return await RunGuarded(link =>
        {
            // NOTE: EvaluateToImage returns a System.Drawing.Image (needs
            // System.Drawing.Common; desktop only). It does not surface
            // $MessageList — if an image step needs its messages too, evaluate
            // the graphic with EvaluateAsync first, then export separately.
            using var img = link.EvaluateToImage(expression, width, height);
            if (link.Error != 0)
            {
                string err = link.ErrorMessage;
                link.ClearError();
                return MathResult<byte[]>.Link(err);
            }
            if (img is null)
                return MathResult<byte[]>.Failure("kernel returned no image (expression may not be a graphic)");

            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return MathResult<byte[]>.Ok(ms.ToArray(), raw: $"{width}x{height} png, {ms.Length} bytes");
        }, () => MathResult<byte[]>.TimedOut(), ct).ConfigureAwait(false);
    }

    public async Task<MathResult> HealthCheckAsync(CancellationToken ct = default)
    {
        var r = await EvaluateAsync("2+2", ct).ConfigureAwait(false);
        if (!r.IsSuccess) return r;
        return r.Value?.Trim() == "4"
            ? MathResult.Ok(raw: r.Raw)
            : MathResult.Failure($"health check expected 4, got '{r.Value}'", MathStatus.LinkError);
    }

    public async Task<MathResult> RestartAsync(CancellationToken ct = default)
    {
        await ShutdownAsync(ct).ConfigureAwait(false);
        return await StartAsync(ct).ConfigureAwait(false);
    }

    public async Task ShutdownAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _link?.Close();
            _link = null;
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await ShutdownAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    /// <summary>
    /// Serialize a blocking kernel call onto a worker thread, enforce the
    /// cancellation token as a hard timeout by aborting the in-flight evaluation,
    /// and translate transport exceptions into a LinkError result.
    /// </summary>
    private async Task<TResult> RunGuarded<TResult>(
        Func<IKernelLink, TResult> body, Func<TResult> onTimeout, CancellationToken ct)
        where TResult : MathResult
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var link = _link;
            if (link is null) return onTimeout(); // shut down between the guard and here

            using var reg = ct.Register(() =>
            {
                // Cooperative abort: tell the kernel to stop the current evaluation.
                try { link.AbortEvaluation(); } catch { /* link may already be dead */ }
            });

            return await Task.Run(() =>
            {
                try { return body(link); }
                catch (MathLinkException mlx)
                {
                    try { link.ClearError(); } catch { /* ignore */ }
                    return (TResult)(object)MathResult.Link($"MathLink error {mlx.ErrCode}: {mlx.Message}");
                }
                catch (Exception ex) when (ct.IsCancellationRequested)
                {
                    return onTimeout();
                }
                catch (Exception ex)
                {
                    return (TResult)(object)MathResult.Failure(ex.Message, MathStatus.LinkError);
                }
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { return onTimeout(); }
        finally { _gate.Release(); }
    }

    /// <summary>Split the sentinel-framed payload into a value and a message list.</summary>
    private static (string value, List<MathematicaMessage> messages) ParseFramed(string raw)
    {
        var messages = new List<MathematicaMessage>();

        int r = raw.IndexOf(ResultMarker, StringComparison.Ordinal);
        int m = raw.IndexOf(MsgMarker, StringComparison.Ordinal);
        int e = raw.IndexOf(EndMarker, StringComparison.Ordinal);
        if (r < 0 || m < 0 || e < 0 || !(r < m && m < e))
        {
            // Framing missing — the wrapper itself failed (syntax error before our
            // Module ran, aborted output, etc.). Return the raw text as the value
            // so the runbook has something to diagnose.
            return (raw.Trim(), messages);
        }

        string value = raw.Substring(r + ResultMarker.Length, m - (r + ResultMarker.Length)).Trim();
        string msgBlock = raw.Substring(m + MsgMarker.Length, e - (m + MsgMarker.Length)).Trim();

        // $MessageList comes back as InputForm: {HoldForm[Power::infy], HoldForm[...]}.
        // First-pass parse: strip the outer braces and split on top-level commas.
        // Refine here as we learn the real shapes (see runbook: "message parsing").
        if (msgBlock.Length > 2 && msgBlock.StartsWith("{") && msgBlock.EndsWith("}"))
        {
            string inner = msgBlock[1..^1].Trim();
            foreach (var tag in SplitTopLevel(inner))
            {
                string t = tag.Trim();
                if (t.Length == 0) continue;
                // t looks like "HoldForm[Power::infy]" — pull out the sym::tag.
                string clean = t.Replace("HoldForm[", "").Replace("]", "").Trim();
                var sev = LooksLikeError(clean) ? MessageSeverity.Error : MessageSeverity.Warning;
                messages.Add(new MathematicaMessage(clean, t, sev));
            }
        }

        return (value, messages);
    }

    // Split on commas that are not nested inside [] or {}.
    private static IEnumerable<string> SplitTopLevel(string s)
    {
        int depth = 0, start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is '[' or '{') depth++;
            else if (c is ']' or '}') depth--;
            else if (c == ',' && depth == 0) { yield return s[start..i]; start = i + 1; }
        }
        if (start < s.Length) yield return s[start..];
    }

    // Heuristic until we catalogue real tags in the runbook. OpticaEM/Mathematica
    // error tags are not reliably distinguishable from warnings by name alone;
    // treat a few well-known fatal-ish ones as errors and refine over time.
    private static bool LooksLikeError(string tag) =>
        tag.Contains("::nonopt") || tag.Contains("::argx") || tag.Contains("::argr") ||
        tag.Contains("::badarg") || tag.Contains("::syntax") || tag.Contains("Syntax::");
}
