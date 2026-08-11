# MathematicaLink

A small, deployment-portable channel for driving Mathematica + OpticaEM from C#.
This is **Section 1 / Phase 1**: get commands in and results (or a sequence of
errors) out, reliably enough that occasional restarts are acceptable.

> Won't build in a container without Mathematica. `Wolfram.NETLink.dll` ships
> inside your Mathematica install, not NuGet — see `MathematicaLink.csproj`.

## The error model (why not a bare string)

Mathematica doesn't fail the way a normal function does. A single evaluation can
produce a perfectly good value **and** a stream of messages (`Power::infy`,
`OpticaEM::badarg`, …). Separately, the *link itself* can die (kernel crash,
timeout). One return type has to carry all three cases, so every call returns a
`MathResult` / `MathResult<T>`:

| Field | Meaning |
|---|---|
| `Status` | `Ok`, `CompletedWithMessages`, `EvaluationError`, `LinkError`, `Timeout`, `NotStarted` |
| `Value` (generic) | the payload — result text, or PNG bytes |
| `Messages` | the **sequence** of `MathematicaMessage(Tag, Text, Severity)` — this is your "maybe a sequence of errors" |
| `Raw` | verbatim kernel text, kept for diagnosis |

Three failure classes you handle differently:

- **`LinkError` / `Timeout`** → transport is broken. Don't retry in place; call
  `RestartAsync()`. (Your "restart now and then is fine" stance is the design.)
- **`EvaluationError` / `CompletedWithMessages`** → kernel is alive; the *program*
  emitted messages. This is the syntax/functionality bucket you want to diagnose
  and log in the runbook.
- **`NotStarted`** → programmer error / lifecycle bug.

## Wiring it up

```csharp
// No kernel path needed — MathematicaLocator finds the newest install
// (honors MATHEMATICA_HOME) and puts ml64i4.dll on PATH. Pass a log sink to
// see startup steps; pass kernelPathOverride only for a non-standard install.
await using var link = new NetLinkMathematicaLink(log: Console.WriteLine);

var opticaem = new OpticaEmSession(link, loadCommand: "Needs[\"OpticaEM`\"]"); // <- your version's real load call

var boot = await opticaem.StartAndLoadAsync();        // start -> 2+2 -> load OpticaEM
if (!boot.IsSuccess) { /* inspect boot.Messages, RestartAsync, log to runbook */ }

// Ladder step 1: a program file you author, rendered to a PNG.
var r = await opticaem.RunProgramFileToPngAsync("programs/01_element.m", "out/01.png");
foreach (var m in r.Messages) Console.WriteLine(m);    // catalogue idiosyncrasies here
```

## The seam to validate first

`NetLinkMathematicaLink` captures messages by wrapping each evaluation as
`Module[{afRes = (expr)}, ...$MessageList...]` with sentinel markers, then parsing
the frame in `ParseFramed`. That wrapper + parser is the **primary thing to
verify against the real kernel** — how OpticaEM actually surfaces errors is one of
the unknowns Phase 1 exists to pin down. When you find the real shapes, tune
`ParseFramed` / `LooksLikeError` and record what you learned in the runbook.

## Files

| File | Role |
|---|---|
| `Results.cs` | `MathResult`, `MathResult<T>`, `MathematicaMessage`, status/severity enums |
| `IMathematicaLink.cs` | the link contract (local, cloud, Wolfram-cloud all implement it) |
| `NetLinkMathematicaLink.cs` | local persistent-kernel implementation over Wolfram.NETLink |
| `MathematicaLocator.cs` | finds the install, puts `ml64i4.dll` on PATH (ported from the connect test) |
| `OpticaEmSession.cs` | thin OpticaEM layer; runs program *files* you supply |
| `../../docs/mathematica-opticaem-runbook.md` | the idiosyncrasy log (name/brief/full/identify/fix) |
| `../../docs/mathematica-startup-verify.md` | the boot + 5-step ladder sequence |
