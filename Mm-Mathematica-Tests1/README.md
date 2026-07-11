# Mm Mathematica Tests

A WinForms (.NET 10) sandbox for exploring how to call **Mathematica 15.0** from C#
using Wolfram's **.NET/Link** library (the high-level API — no raw WSTP calls).

The window has a multiline output box on the left and a tab group on the right.
Each tab is meant to hold increasingly complex interactions with the kernel;
the first tab ("Basics") has a single button that evaluates `2+2`.

## Prerequisites

- Windows
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (or Visual Studio 2026 with the .NET desktop workload)
- Mathematica 15.0 installed and licensed on the same machine

## Build and run

```
git clone <this repo>
cd Mm-Mathematica-Tests1
dotnet run --project src/MathematicaTests
```

or open `MathematicaTests.sln` in Visual Studio and press F5.

Click **Evaluate 2+2** on the *Basics* tab. The first click launches the
Mathematica kernel (a few seconds); the output box should then show:

```
In:  2+2
Out: 4
```

## How the Mathematica connection works

- The project references the official [`Wolfram.NETLink`](https://www.nuget.org/packages/Wolfram.NETLink)
  NuGet package, so no assemblies need to be copied from the Mathematica
  installation.
- `KernelSession.cs` owns the kernel link:
  - `MathLinkFactory.CreateKernelLink()` launches the most recently installed
    Mathematica automatically.
  - If that fails, it retries with the explicit path in
    `KernelSession.FallbackKernelPath`
    (`C:\Program Files\Wolfram Research\Mathematica\15.0\MathKernel.exe`) —
    edit that constant if Mathematica lives somewhere else.
  - Evaluations go through `IKernelLink.EvaluateToOutputForm(expr, 0)`, which
    returns the result formatted as text.
- The kernel is launched lazily on the first evaluation and closed when the
  window closes.

## Adding more interactions

Add a new `TabPage` to `tabControlInteractions` in the Visual Studio designer,
drop in buttons/inputs, and call `EvaluateAndShow("<expr>")` (or use
`KernelSession` directly for typed results via `EvaluateToInputForm`,
`link.GetInteger()`, etc.).

## Troubleshooting

- **Kernel fails to launch / license errors** — run Mathematica itself once to
  confirm the license is active, and check `KernelSession.FallbackKernelPath`.
- **Assembly or version problems with the NuGet package** — you can instead
  reference the copy shipped with Mathematica: remove the `PackageReference`
  in `MathematicaTests.csproj` and add
  `C:\Program Files\Wolfram Research\Mathematica\15.0\SystemFiles\Links\NETLink\Wolfram.NETLink.dll`
  as a direct reference (the commented block in the `.csproj` shows how).
- **UI freezes during long evaluations** — expected for now: evaluations run on
  the UI thread to keep this sandbox simple. Moving them to a background task
  is a natural next step once interactions get heavier.
