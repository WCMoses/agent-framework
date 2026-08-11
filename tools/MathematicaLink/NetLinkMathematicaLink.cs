// Wolfram.NETLink comes from the Wolfram.NETLink NuGet package (managed-only).
// At runtime it P/Invokes the native ml64i4.dll from the Mathematica install —
// MathematicaLocator.EnsureNativeLibraryOnPath() makes that resolvable. This file
// will not compile/run in a container without a Mathematica install present.
using Wolfram.NETLink;

namespace MathematicaLink;

/// <summary>
/// Local, persistent-kernel implementation of <see cref="IMathematicaLink"/> over
/// Wolfram.NETLink / WSTP. One process, one kernel, calls serialized.
///
/// The startup strategy (native-lib-on-PATH, explicit "-mathlink" launch with a
/// default-discovery fallback, and a worker-thread startup timeout that reports
/// which stage stalled) is ported from the proven WCMoses connect test against
/// Mathematica 15.0.
/// </summary>
public sealed class NetLinkMathematicaLink : IMathematicaLink
{
    // Sentinels framing the {result, $MessageList} payload so EvaluateAsync can
    // split it back apart. This wrapper + ParseFramed is the PRIMARY SEAM to
    // validate against the real kernel — see the runbook "evaluate" entry.
    private const string ResultMarker = "<<<AF-RESULT>>>";
    private const string MsgMarker = "<<<AF-MESSAGES>>>";
    private const string EndMarker = "<<<AF-END>>>";

    // The MathLink launch protocol has no timeout of its own; without this a bad
    // launch (e.g. kernel stuck on a license prompt) hangs forever.
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    private readonly Action<string>? _log;
    private readonly string? _kernelPathOverride;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IKernelLink? _link;

    /// <param name="log">Optional startup/diagnostic log sink.</param>
    /// <param name="kernelPathOverride">
    /// Explicit kernel executable path. Leave null to let <see cref="MathematicaLocator"/>
    /// find the newest install (honoring MATHEMATICA_HOME).
    /// </param>
    public NetLinkMathematicaLink(Action<string>? log = null, string? kernelPathOverride = null)
    {
        _log = log;
        _kernelPathOverride = kernelPathOverride;
    }

    public bool IsRunning => _link is not null;

    public async Task<MathResult> StartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_link is not null) return MathResult.Ok();

            return await Task.Run(() =>
            {
                // Must run before any Wolfram.NETLink type is touched.
                if (MathematicaLocator.EnsureNativeLibraryOnPath() is null)
                {
                    string probed = MathematicaLocator.FindInstallRoot()
                        ?? "no Wolfram Research installation found under Program Files";
                    return MathResult.Link(
                        "Could not locate the native MathLink library (ml64i4.dll). Searched: " + probed +
                        ". Set MATHEMATICA_HOME to the install folder (e.g. " +
                        @"C:\Program Files\Wolfram Research\Mathematica\15.0) or copy ml64i4.dll into bin\Debug.");
                }

                Log("Native MathLink dir: " + MathematicaLocator.NativeLibraryDirectory);
                Log("Install root:        " + (MathematicaLocator.InstallRoot ?? "(not located)"));

                string? kernel = _kernelPathOverride ?? MathematicaLocator.FindKernelExecutable();
                try
                {
                    if (kernel is not null)
                    {
                        try
                        {
                            // Quoted path + -mathlink inside the linkname, per the launch protocol.
                            string[] args = ["-linkmode", "launch", "-linkname", $"\"{kernel}\" -mathlink"];
                            _link = CreateAndConnect(args, "explicit launch of " + kernel);
                            return MathResult.Ok(raw: "kernel started (explicit)");
                        }
                        catch (Exception ex)
                        {
                            Log("Explicit launch failed: " + ex.Message + "; falling back to default discovery.");
                        }
                    }

                    _link = CreateAndConnect([], "default CreateKernelLink()");
                    return MathResult.Ok(raw: "kernel started (default discovery)");
                }
                catch (Exception ex)
                {
                    return MathResult.Link($"failed to start kernel: {ex.Message}");
                }
            }, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    /// <summary>
    /// Create the link and complete the kernel handshake on a worker thread,
    /// failing loudly if it exceeds <see cref="StartupTimeout"/>. Ported from the
    /// connect test — a stalled handshake almost always means a license/activation
    /// prompt (run MathKernel.exe by hand once to clear it).
    /// </summary>
    private IKernelLink CreateAndConnect(string[] args, string description)
    {
        Log("Launching kernel: " + description);

        IKernelLink? pending = null;
        IKernelLink? connected = null;
        Exception? failure = null;
        using var done = new ManualResetEventSlim(false);

        var worker = new Thread(() =>
        {
            try
            {
                IKernelLink l = args.Length == 0
                    ? MathLinkFactory.CreateKernelLink()
                    : MathLinkFactory.CreateKernelLink(args);
                pending = l;
                Log("Link created; waiting for kernel handshake (first packet)...");
                l.WaitAndDiscardAnswer();
                connected = l;
            }
            catch (Exception ex) { failure = ex; }
            finally { done.Set(); }
        })
        { IsBackground = true, Name = "KernelStartup" };
        worker.Start();

        if (!done.Wait(StartupTimeout))
        {
            try { pending?.Close(); } catch { }
            string stage = pending is null ? "creating the link" : "waiting for the kernel handshake";
            throw new TimeoutException(
                $"Kernel startup timed out after {StartupTimeout.TotalSeconds:0}s while {stage} ({description}). " +
                "Most common cause: the kernel is stuck on a license/activation prompt — run MathKernel.exe " +
                "by hand once; it should show an In[1]:= prompt. See the runbook.");
        }

        if (failure is not null)
            throw new InvalidOperationException($"Kernel launch failed ({description}): {failure.Message}", failure);

        Log("Kernel handshake complete.");
        return connected!;
    }

    public Task<MathResult> LoadPackageAsync(string loadCommand, CancellationToken ct = default) =>
        EvaluateAsync(loadCommand, ct).ContinueWith(t =>
        {
            var r = t.Result;
            return new MathResult { Status = r.Status, Messages = r.Messages, Raw = r.Raw };
        }, ct, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

    public async Task<MathResult<string>> EvaluateAsync(string expression, CancellationToken ct = default)
    {
        if (_link is null) return MathResult<string>.Failure("kernel not started", MathStatus.NotStarted);

        // Wrap so the kernel returns BOTH the value (InputForm) and the messages
        // generated during evaluation, framed by sentinels. $MessageList resets per
        // top-level evaluation, so it must be read in the same round trip.
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
                link.NewPacket();
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
            // EvaluateToImage returns a System.Drawing.Image (desktop only) and does
            // NOT surface $MessageList — if an image step needs its messages, run it
            // through EvaluateAsync first, then export.
            using var img = link.EvaluateToImage(expression, width, height);
            if (link.Error != 0)
            {
                string err = link.ErrorMessage;
                link.ClearError();
                link.NewPacket();
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
            if (link is null) return onTimeout();

            using var reg = ct.Register(() =>
            {
                try { link.AbortEvaluation(); } catch { /* link may already be dead */ }
            });

            return await Task.Run(() =>
            {
                try { return body(link); }
                catch (MathLinkException mlx)
                {
                    try { link.ClearError(); } catch { }
                    return (TResult)(object)MathResult.Link($"MathLink error {mlx.ErrCode}: {mlx.Message}");
                }
                catch (Exception) when (ct.IsCancellationRequested) { return onTimeout(); }
                catch (Exception ex) { return (TResult)(object)MathResult.Failure(ex.Message, MathStatus.LinkError); }
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
            return (raw.Trim(), messages); // framing missing — wrapper itself failed

        string value = raw.Substring(r + ResultMarker.Length, m - (r + ResultMarker.Length)).Trim();
        string msgBlock = raw.Substring(m + MsgMarker.Length, e - (m + MsgMarker.Length)).Trim();

        if (msgBlock.Length > 2 && msgBlock.StartsWith("{") && msgBlock.EndsWith("}"))
        {
            string inner = msgBlock[1..^1].Trim();
            foreach (var tag in SplitTopLevel(inner))
            {
                string t = tag.Trim();
                if (t.Length == 0) continue;
                string clean = t.Replace("HoldForm[", "").Replace("]", "").Trim();
                var sev = LooksLikeError(clean) ? MessageSeverity.Error : MessageSeverity.Warning;
                messages.Add(new MathematicaMessage(clean, t, sev));
            }
        }

        return (value, messages);
    }

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

    private static bool LooksLikeError(string tag) =>
        tag.Contains("::nonopt") || tag.Contains("::argx") || tag.Contains("::argr") ||
        tag.Contains("::badarg") || tag.Contains("::syntax") || tag.Contains("Syntax::");

    private void Log(string line) => _log?.Invoke(line);
}
