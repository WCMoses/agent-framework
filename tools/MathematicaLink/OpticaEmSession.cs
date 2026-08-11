namespace MathematicaLink;

/// <summary>
/// Thin OpticaEM layer over an <see cref="IMathematicaLink"/>. It owns no
/// OpticaEM syntax — the load command and every "known-good program" are
/// supplied as text (usually read from files you author). This keeps
/// idiosyncratic OpticaEM syntax out of the C# and in files you control, so a
/// bad program can never be blamed on the harness.
/// </summary>
public sealed class OpticaEmSession
{
    private readonly IMathematicaLink _link;
    private readonly string _loadCommand;

    /// <param name="loadCommand">
    /// The exact command that loads OpticaEM into a fresh kernel for YOUR version,
    /// e.g. <c>Needs["OpticaEM`"]</c> or <c>Get["/path/to/OpticaEM.m"]</c>.
    /// Left as a parameter because it changes between OpticaEM versions.
    /// </param>
    public OpticaEmSession(IMathematicaLink link, string loadCommand)
    {
        _link = link;
        _loadCommand = loadCommand;
    }

    /// <summary>Cold-start the kernel, verify 2+2, then load OpticaEM.</summary>
    public async Task<MathResult> StartAndLoadAsync(CancellationToken ct = default)
    {
        var start = await _link.StartAsync(ct).ConfigureAwait(false);
        if (!start.IsSuccess) return start;

        var health = await _link.HealthCheckAsync(ct).ConfigureAwait(false);
        if (!health.IsSuccess) return health;

        return await _link.LoadPackageAsync(_loadCommand, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Run a known-good OpticaEM program that produces a graphic, returning the
    /// picture as PNG bytes. Ladder steps 1–5 are just files passed here.
    /// </summary>
    public Task<MathResult<byte[]>> RunProgramToImageAsync(
        string program, int width = 700, int height = 700, CancellationToken ct = default) =>
        _link.EvaluateToImageAsync(program, width, height, ct);

    /// <summary>Run a program whose result is a value/expression rather than a picture.</summary>
    public Task<MathResult<string>> RunProgramAsync(string program, CancellationToken ct = default) =>
        _link.EvaluateAsync(program, ct);

    /// <summary>Read a program from a file and render it to a PNG on disk.</summary>
    public async Task<MathResult> RunProgramFileToPngAsync(
        string programPath, string outputPngPath, CancellationToken ct = default)
    {
        string program = await File.ReadAllTextAsync(programPath, ct).ConfigureAwait(false);
        var result = await RunProgramToImageAsync(program, ct: ct).ConfigureAwait(false);
        if (result.IsSuccess && result.Value is { Length: > 0 })
            await File.WriteAllBytesAsync(outputPngPath, result.Value, ct).ConfigureAwait(false);
        return result;
    }
}
