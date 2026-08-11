# Mathematica + OpticaEM — Idiosyncrasy Runbook

The living log of every quirk we hit talking to Mathematica and OpticaEM. One
entry per issue, in the format below. Goal: a reusable library of "how to
identify / how to fix" so the same problem never costs time twice.

Scope reminder (Section 1): we care about **syntax and functionality** problems.
Flaky-link problems we tolerate with a restart — log them briefly, don't chase
them.

## Entry template

Copy this block for each new issue.

```
### <short-name>
- **Brief:** one line.
- **Severity:** blocker | warning | cosmetic | link-flake
- **Where:** which lifecycle step (startup / load / evaluate / image / shutdown).
- **Full description:** what happens, with the exact expression/output.
- **Message tag(s):** e.g. Power::infy, OpticaEM::badarg (or "none").
- **How to identify:** the signature to match on (MathResult.Status, a tag, raw text).
- **How to fix:** the resolution, or "workaround: restart", or "open — needs author".
- **Status:** open | fixed | wontfix
```

## Lifecycle steps to watch (pre-seeded — fill as we learn)

### startup — native MathLink library (ml64i4.dll) not found
- **Brief:** the `Wolfram.NETLink` NuGet package is managed-only; it P/Invokes the
  native `ml64i4.dll`, which ships inside the Mathematica install, not the package.
- **Severity:** blocker
- **Where:** startup (first touch of any Wolfram.NETLink type).
- **Full description:** without the native DLL on the search path you get
  *"The type initializer for 'Wolfram.NETLink.Internal.NativeLink' threw an
  exception"*. This is the "file or two I had to copy into bin\Debug" — that file
  is `ml64i4.dll`, from `<install>\SystemFiles\Links\NETLink\`.
- **How to identify:** `StartAsync` returns `Status = LinkError` with the
  ml64i4.dll message; or the NativeLink type-initializer exception.
- **How to fix:** `MathematicaLocator.EnsureNativeLibraryOnPath()` (called at the
  top of `StartAsync`) finds the install and prepends its native-lib dir to PATH —
  no manual copy needed. If the install is non-standard, set `MATHEMATICA_HOME`.
  Manual/pinned fallback: keep `ml64i4.dll` in the **project root** (not bin\) so
  `dotnet clean` can't delete it; the csproj copies it to the output dir on build
  (`<None Include="ml64i4.dll"><CopyToOutputDirectory>PreserveNewest</...>`), and
  the locator's exe-directory check then pins that copy over PATH discovery. The
  DLL is git-ignored (licensed, machine/version-specific).
- **Gotcha:** copying it only into bin\Debug is fragile — `dotnet clean` wipes bin\
  and the file is gone. Root + copy-to-output is the durable form.
- **Status:** fixed (via MathematicaLocator; pinned-copy path documented)

### startup — kernel launches but never answers (handshake stall)
- **Brief:** kernel process starts but blocks, so the MathLink handshake never
  completes; the 60s startup timeout fires.
- **Severity:** blocker
- **Where:** startup (`CreateAndConnect`, "waiting for the kernel handshake").
- **Full description:** almost always the kernel is stuck on a license/activation
  prompt. The MathLink launch protocol has no timeout of its own, which is why the
  link uses a worker-thread + `StartupTimeout` and reports which stage stalled.
- **How to identify:** `StartAsync` returns `Status = LinkError` with a
  `TimeoutException` message naming "waiting for the kernel handshake".
- **How to fix:** run `MathKernel.exe` (or `WolframKernel.exe`) from the install
  by hand once — it should show an `In[1]:=` prompt; if it shows an activation
  dialog, activate it, then retry.
- **Status:** fixed (diagnosis captured; resolution is one-time activation)

### startup — NuGet managed/native version mismatch
- **Brief:** the NuGet `Wolfram.NETLink` (managed) and the install's `ml64i4.dll`
  (native) are a mismatched pair.
- **Severity:** warning
- **Where:** startup.
- **How to identify:** explicit launch and default discovery both fail/timeout
  despite a valid install and license.
- **How to fix:** switch from the `PackageReference` to a direct `Reference` on the
  `Wolfram.NETLink.dll` inside your install (matched pair) — the commented block in
  `MathematicaLink.csproj` shows how.
- **Status:** open (workaround documented)

### load — load OpticaEM into a fresh kernel
- **Brief:** _tbd — the exact load call differs by OpticaEM version._
- **How to identify:** `LoadPackageAsync` returns messages (context shadowing,
  `Needs::nocont`, "package not found").
- **How to fix:** _tbd — confirm the load command with the new version / author._
- **Status:** open

### evaluate — message capture wrapper
- **Brief:** verify the `Module[{afRes=…}, …$MessageList…]` sentinel wrapper
  actually captures OpticaEM errors, and that `ParseFramed` reads them.
- **How to identify:** framing markers missing in `MathResult.Raw`, or `Messages`
  empty when the kernel clearly complained.
- **How to fix:** _tbd — tune the wrapper / `ParseFramed` / `LooksLikeError` once
  real OpticaEM error shapes are known._
- **Status:** open

### image — EvaluateToImage returns a graphic
- **Brief:** OpticaEM draw call must return a `Graphics`/`Graphics3D`; image path
  does not capture `$MessageList`.
- **How to identify:** `EvaluateToImageAsync` returns "kernel returned no image".
- **How to fix:** _tbd — wrap the draw call so it yields a graphic; if messages
  matter, evaluate once with `EvaluateAsync` first._
- **Status:** open

### timeout / abort — long evaluation
- **Brief:** _tbd_
- **How to identify:** `Status = Timeout`.
- **How to fix:** raise the per-call timeout, or restart.
- **Status:** open

### shutdown
- **Brief:** _tbd_
- **Status:** open
