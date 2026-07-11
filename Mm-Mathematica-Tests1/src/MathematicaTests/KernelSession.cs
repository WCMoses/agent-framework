using Wolfram.NETLink;

namespace MathematicaTests;

/// <summary>
/// Owns a single link to a Mathematica kernel. The kernel is launched lazily on
/// the first evaluation and shut down when this object is disposed.
/// </summary>
public sealed class KernelSession : IDisposable
{
    /// <summary>
    /// Used only if the default launch (which locates the most recently
    /// installed Mathematica automatically) fails. Adjust if Mathematica
    /// is installed somewhere non-standard.
    /// </summary>
    public const string FallbackKernelPath =
        @"C:\Program Files\Wolfram Research\Mathematica\15.0\MathKernel.exe";

    private IKernelLink? _link;

    public bool IsStarted => _link is not null;

    /// <summary>
    /// Evaluates a Wolfram Language expression and returns the result in
    /// OutputForm, launching the kernel first if necessary.
    /// </summary>
    public string Evaluate(string expression)
    {
        IKernelLink link = _link ?? Start();

        string? result = link.EvaluateToOutputForm(expression, 0);
        if (result is null)
        {
            string error = link.LastError?.Message ?? link.ErrorMessage ?? "unknown error";
            link.ClearError();
            link.NewPacket();
            throw new InvalidOperationException($"Evaluation failed: {error}");
        }

        return result;
    }

    private IKernelLink Start()
    {
        IKernelLink link;
        try
        {
            // Finds and launches the most recently installed Mathematica.
            link = MathLinkFactory.CreateKernelLink();
        }
        catch (MathLinkException)
        {
            string[] args = ["-linkmode", "launch", "-linkname", FallbackKernelPath];
            link = MathLinkFactory.CreateKernelLink(args);
        }

        try
        {
            // Absorb the kernel's initial InputNamePacket.
            link.WaitAndDiscardAnswer();
        }
        catch
        {
            link.Close();
            throw;
        }

        _link = link;
        return link;
    }

    public void Dispose()
    {
        if (_link is not null)
        {
            _link.Close();
            _link = null;
        }
    }
}
