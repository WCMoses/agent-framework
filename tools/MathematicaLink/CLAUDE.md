# OpticaEmLink — project context

> Handoff note for any Claude Code session picking this up (especially a **local**
> Windows session — only local can actually run it; see "Where this must run").
> This file is the durable context: read it plus the two docs and you're caught up.

## What this is

Section 1 of a larger system: a reliable C# command channel to **Mathematica +
OpticaEM** (optics / holography / interferometry). Goal is "works most of the
time; occasional restart is fine." Effort goes into diagnosing **syntax and
functionality** problems, not chasing link flakiness.

The bigger project (later, separate): an AI loop that optimizes an optics RAG
system (models, params, agent/workflow structure, tool grants), scored partly by
a Mathematica oracle. NL→OpticaEM translation is Section 2, deferred until this
channel is solid.

## Where this must run

Only a machine with **Mathematica 15.0** installed can start the kernel, load
OpticaEM, and test anything. A cloud/web Claude session can write code but can
**never run it**. Do the actual bring-up in a local session at `C:\Dev\OpticaEmLink`.

## Architecture (files in this folder)

- `Results.cs` — the error model. Every call returns `MathResult` / `MathResult<T>`:
  `Status` + `Value` + a **list** of `MathematicaMessage` + `Raw`. This is the
  answer to "return a value or a sequence of errors": one evaluation can yield a
  value *and* a stream of messages, and separately the link can die.
- `IMathematicaLink.cs` — the contract. One interface so local / cloud /
  Wolfram-Cloud implementations are swappable (deployment target is still fluid).
- `NetLinkMathematicaLink.cs` — local persistent-kernel impl over Wolfram.NETLink.
- `MathematicaLocator.cs` — finds the install, puts native `ml64i4.dll` on PATH.
- `OpticaEmSession.cs` — thin OpticaEM layer; runs **author-supplied program files**
  (no OpticaEM syntax baked into C#).

## Key decisions (don't relitigate)

- **Result envelope, not bare strings** — see `Results.cs`.
- **One persistent kernel, calls serialized** — a kernel does one eval at a time;
  concurrent calls corrupt the link (`SemaphoreSlim` gate).
- **Launch strategy** (proven against Mathematica 15.0 in
  `WCMoses/Mathematica-Basic-NetLink-Connection`): `MathematicaLocator` →
  explicit `-linkmode launch -linkname "\"<kernel>\" -mathlink"` → fallback to
  default `CreateKernelLink()` → 60s worker-thread startup timeout that names the
  stalled stage. A handshake stall almost always = kernel blocked on a
  license/activation prompt (run `MathKernel.exe` by hand once).
- **`ml64i4.dll`** (native MathLink lib; the `Wolfram.NETLink` NuGet is
  managed-only) lives in the **project root** (`src/OpticaEmLink/ml64i4.dll`),
  git-ignored, copied to output on build so `dotnet clean` can't delete it and the
  locator's exe-dir check pins that copy. Source: `<install>\SystemFiles\Links\NETLink\`.
- **No OpticaEM syntax in C#** — the load call and the 5 ladder programs are files.
- **Message capture is the seam to validate first**: `NetLinkMathematicaLink`
  wraps each eval as `Module[{afRes=(expr)}, …$MessageList…]` with sentinels and
  parses the frame in `ParseFramed`. How OpticaEM actually reports errors is the
  top unknown — tune the wrapper/parser here as you learn, and log findings.

## Status

- **Startup half: done and proven.** Locator + launch + timeout + native-lib
  handling are in and match the working connect test.
- **OpticaEM half: pending inputs** (below).

## Next steps

1. Get the **OpticaEM load call** for the current version (waiting on a password /
   newer OpticaEM; the author may help directly).
2. Get **one validated program** (even just `01_element.m`) to seed the ladder —
   from the GUI export or the author, so we debug real syntax, not guesses.
3. Run the boot sequence in `docs/mathematica-startup-verify.md`; work the 5-step
   ladder; fill the `load` / `evaluate` entries in the runbook with real behavior.
4. Later: turn "rays are altered" into a numeric before/after check — that same
   channel becomes the Mathematica scoring oracle for the bigger project.

## Read next

- `docs/mathematica-startup-verify.md` — boot sequence + the 5-step ladder.
- `docs/mathematica-opticaem-runbook.md` — the idiosyncrasy log (name/brief/full/
  identify/fix), already seeded with the native-lib, license-prompt, and
  version-mismatch entries.

> These files currently live in the `agent-framework` fork, branch
> `claude/llm-optics-parameter-automation-ra2723`. Pull **both** `tools/MathematicaLink/`
> and `docs/mathematica-*.md` when moving to the standalone `OpticaEmLink` repo.
