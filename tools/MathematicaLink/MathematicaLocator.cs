namespace MathematicaLink;

/// <summary>
/// Locates the local Mathematica installation so the native MathLink library
/// (<c>ml64i4.dll</c>) and the kernel executable resolve at runtime.
///
/// Ported from the WCMoses/Mathematica-Basic-NetLink-Connection connect test,
/// which proved this out against Mathematica 15.0. The point: the
/// <c>Wolfram.NETLink</c> NuGet package is managed-only; the native DLL it
/// P/Invokes ships inside the Mathematica install, so that directory must be on
/// the DLL search path before the first .NET/Link call — otherwise you get
/// "The type initializer for 'Wolfram.NETLink.Internal.NativeLink' threw an
/// exception". Calling <see cref="EnsureNativeLibraryOnPath"/> once at process
/// startup removes the need to manually copy ml64i4.dll into bin\Debug.
/// </summary>
public static class MathematicaLocator
{
    private const string NativeLibraryName = "ml64i4.dll";

    /// <summary>Directory containing ml64i4.dll after <see cref="EnsureNativeLibraryOnPath"/>, or null.</summary>
    public static string? NativeLibraryDirectory { get; private set; }

    public static string? InstallRoot { get; private set; }

    /// <summary>
    /// Make ml64i4.dll resolvable by P/Invoke: prefer a copy next to the exe
    /// (i.e. someone copied it into bin\Debug), otherwise prepend the install's
    /// native-library directory to the process PATH. Must run before any
    /// Wolfram.NETLink type is used. Returns the directory used, or null if the
    /// install / native library could not be found.
    /// </summary>
    public static string? EnsureNativeLibraryOnPath()
    {
        string exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, NativeLibraryName)))
            return NativeLibraryDirectory = exeDir;

        string? root = FindInstallRoot();
        if (root is null) return null;

        string? dir = FindNativeLibraryDirectory(root);
        if (dir is null) return null;

        string path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        Environment.SetEnvironmentVariable("PATH", dir + Path.PathSeparator + path);
        return NativeLibraryDirectory = dir;
    }

    /// <summary>
    /// Find the install root: MATHEMATICA_HOME if set, else the newest version
    /// under the standard Program Files locations.
    /// </summary>
    public static string? FindInstallRoot()
    {
        if (InstallRoot is not null) return InstallRoot;

        string? home = Environment.GetEnvironmentVariable("MATHEMATICA_HOME");
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
            return InstallRoot = home;

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string[] families =
        [
            Path.Combine(programFiles, "Wolfram Research", "Mathematica"),
            Path.Combine(programFiles, "Wolfram Research", "Wolfram"),
        ];

        foreach (string family in families)
        {
            if (!Directory.Exists(family)) continue;

            string? newest = Directory.GetDirectories(family)
                .OrderByDescending(d => Version.TryParse(Path.GetFileName(d), out Version? v) ? v : new Version(0, 0))
                .FirstOrDefault();
            if (newest is not null) return InstallRoot = newest;
        }

        return null;
    }

    /// <summary>Find the kernel executable for an explicit -linkmode launch.</summary>
    public static string? FindKernelExecutable()
    {
        string? root = FindInstallRoot();
        if (root is null) return null;

        string[] candidates =
        [
            Path.Combine(root, "MathKernel.exe"),
            Path.Combine(root, "WolframKernel.exe"),
            Path.Combine(root, "math.exe"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindNativeLibraryDirectory(string installRoot)
    {
        string[] knownDirs =
        [
            Path.Combine(installRoot, "SystemFiles", "Links", "NETLink"),
            Path.Combine(installRoot, "SystemFiles", "Links", "MathLink",
                "DeveloperKit", "Windows-x86-64", "SystemAdditions"),
        ];

        foreach (string dir in knownDirs)
            if (File.Exists(Path.Combine(dir, NativeLibraryName)))
                return dir;

        // Layouts move between versions; sweep SystemFiles\Links as a last resort.
        string linksRoot = Path.Combine(installRoot, "SystemFiles", "Links");
        if (Directory.Exists(linksRoot))
        {
            try
            {
                string? hit = Directory
                    .EnumerateFiles(linksRoot, NativeLibraryName, SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (hit is not null) return Path.GetDirectoryName(hit);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Fall through; the caller reports an actionable error.
            }
        }

        return null;
    }
}
