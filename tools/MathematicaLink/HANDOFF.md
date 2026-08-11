# Full Handoff — Optics LLM project & the Mathematica/OpticaEM link

A complete summary of the cloud session that produced this code, written for a
**local Claude Code session** on the Windows machine. Read this end-to-end; it
carries the full context (the chat transcript itself does not transfer between
sessions). Companion files: `CLAUDE.md` (tight project context, auto-loaded),
`../../docs/mathematica-startup-verify.md`, `../../docs/mathematica-opticaem-runbook.md`.

---

## 0. Start here (what to do first)

1. You are picking up **Section 1**: a reliable C# → Mathematica + OpticaEM command
   channel. The scaffolding is built and committed; it needs to be *run against a
   real kernel*, which is why the work moved to your machine.
2. Read `CLAUDE.md` (next to this file), then `docs/mathematica-startup-verify.md`.
3. Two inputs are still pending from the user before the OpticaEM half can run:
   the **OpticaEM load call** for the current version, and **one validated OpticaEM
   program**. Ask for them if not yet provided.
4. Environment must have **Mathematica 15.0** installed and licensed. Only a local
   session can build/run this — a cloud session cannot reach the kernel.

---

## 1. The big picture (the whole project)

The user is building an **LLM system about optics** (domain: optics, holography,
interferometry). Stack: **C# / .NET, Microsoft Agent Framework,
Microsoft.Extensions.AI**, with a **RAG store in SurrealDB**. The data is already
ingested into SurrealDB.

The long-term goal is an **AI-driven optimization loop**: Claude explores the
system's design space — picks LLM models, tunes their parameters, restructures the
agents and the workflow, grants/removes tools — runs an eval, reads the results,
forms a new hypothesis, applies it, and repeats. Agents and workflows are composed
as **text files** via the Agent Framework's declarative support
(`Microsoft.Agents.AI.Workflows.Declarative` — confirmed present in the framework).

This link project (Section 1) is a **prerequisite substrate** for that: the
optimizer's scoring depends on Mathematica, and the product's agents can call
OpticaEM. Get the channel solid first.

### Two distinct "Claudes" (important mental model)
- **Optimizer / Driver** — the Claude that runs the exploration loop (reads results,
  plans, edits config, runs tests). Not part of the product.
- **System-Under-Test (SUT) models** — the models used *inside* the optics agents,
  swappable via `IChatClient`. "Try different models" is a variable the Driver
  optimizes, not a fixed choice.

### Model recommendation for the Driver (when the optimizer is built)
Cost matters because the loop runs many iterations. Recommended tiering:
- **Sonnet 5** — default for each iteration (plan, edit config, run, analyze). Near-Opus
  on coding/agentic at ~40% of Opus cost.
- **Opus 5** — reserved for periodic "deep replan" steps (every ~8 iters or on
  plateau) and for authoring new agent/workflow structures from scratch.
- **Haiku 4.5** — mechanical glue (parsing, formatting) if trimming cost.
(Pricing at time of writing: Opus 5 / 4.8 $5/$25 per Mtok; Sonnet 5 $3/$15, intro
$2/$10; Haiku 4.5 $1/$5.)

### The optimizer loop (future work, Phases 1–3 below)
`LOAD → PLAN (hypothesis) → APPLY (write candidate config) → RUN eval → SCORE →
ANALYZE vs hypothesis → RECORD to ledger → DECIDE (keep/revert; escalate to Opus on
plateau) → repeat.` Two rules: everything the Driver changes is a **config/text
artifact** (reproducible, diffable, revertible — it does not free-edit product C#
each tick); the **results ledger** (append-only JSONL, mirrored to SurrealDB) is
its memory.

### Search space the Driver explores
model · model params (temp/top_p/max_tokens) · RAG knobs (top_k, rerank,
chunk strategy) · **agent structure** (declarative yaml) · **workflow structure**
(declarative yaml) · **tool grants per agent** (optics math API on/off,
Mathematica on/off) — tool grants are often the biggest lever.

### Scoring & objective (decided with the user)
- Test set: a **CSV/JSON file** with per-question tags. Columns: `q`, `gold_answer`,
  `type` (`computational` | `conceptual`), `in_domain` (bool), `tolerance`.
- **Tagged-hybrid scorer**: `computational` → **Mathematica oracle** (compute/verify
  the expected result, compare within tolerance — the strongest signal, and the
  reason the link matters here); `conceptual` → **LLM-as-judge** (Sonnet 5 + rubric);
  `in_domain=false` → **refusal check** (system must decline out-of-domain).
- **Objective (agreed default):** maximize `in_domain_accuracy` subject to
  `ood_refusal_rate ≥ 0.95` (hard floor), `cost_usd_per_q ≤ 0.02`,
  `latency_p50 ≤ 3000ms`. A constraint violation *rejects* the candidate. Promotion
  requires improvement on a held-out split (≈70/30), with auto-revert and hard
  budget caps for safe **overnight autonomy** on the **local machine**.

### Deployment note
Mathematica may run on the user's machine, in cloud containers, or on Wolfram
Cloud. That's why the link is behind one interface — local now, other backends
later, without changing callers.

### Phases (future)
- **Phase 0** — build the eval harness (CSV loader, scorer router incl. Mathematica
  oracle, ledger writer). Foundation for everything.
- **Phase 1** — param/model/tool sweep loop (no structural code changes).
- **Phase 2** — Driver authors declarative agent/workflow `.yaml` (restructure the
  system). Where Opus-tier replanning earns its cost.
- **Phase 3** — overnight autonomy: budget caps, holdout-gated promotion,
  auto-revert, morning report.

---

## 2. Section 1 — the Mathematica/OpticaEM link (current focus)

### Scope & philosophy
Semi-reliably pass **premade** Mathematica/OpticaEM commands and get results — or a
**sequence of errors** — back. "Works most of the time" is the bar; occasional
**restart of either is acceptable**. Spend effort on **syntax and functionality**
problems, not deployment-environment flakiness we may never hit.

Section 2 (AI translating a user's natural-language description into OpticaEM) is
**deferred** until this channel is solid.

### The lifecycle to support
Startup (cold-start Mathematica → load OpticaEM → establish link → prep) · verify
minimal link (`2+2`) · send a known-good full OpticaEM layout · verify results ·
identify & diagnose link problems (build a reusable technique library) · fix them ·
close down (shutdown kernel; unload OpticaEM if needed).

### Goals (in order)
1. Start & connect to Mathematica from fully unloaded.
2. Verify comms (`2+2` → `4`).
3. Load OpticaEM after the kernel starts.
4. Verify OpticaEM by sending a known-good program that draws a simple optical
   element and returns a picture.
5. Send progressively more complex known-good programs.

### Known-good program ladder (user authors these as files)
1. A simple optical element → 3D image.
2. A cone of rays → picture.
3. (1)+(2): ray through the element → picture; **verify the rays are altered**.
4. As (3) plus a **second** element → picture; verify rays altered by both.
5. As (2)–(4) but a **Gaussian beam** instead of a ray cone.
Put these in a `programs/` folder; `OpticaEmSession` runs them. **No OpticaEM
syntax is baked into the C#** — the programs are the source of truth (validated
from the GUI export / OpticaEM docs / the author).

### Diagnostics runbook format
Every idiosyncrasy logged as: **name / brief / full description / severity / where /
message tag(s) / how to identify / how to fix / status**. See the runbook doc.

---

## 3. What was built (this code)

All in the `agent-framework` fork, branch
`claude/llm-optics-parameter-automation-ra2723`:

| File | Role |
|---|---|
| `tools/MathematicaLink/Results.cs` | `MathResult` / `MathResult<T>` error envelope, `MathematicaMessage`, status/severity enums |
| `tools/MathematicaLink/IMathematicaLink.cs` | link contract (local/cloud/Wolfram-Cloud all implement it) |
| `tools/MathematicaLink/NetLinkMathematicaLink.cs` | local persistent-kernel impl over Wolfram.NETLink |
| `tools/MathematicaLink/MathematicaLocator.cs` | finds the install; puts native `ml64i4.dll` on PATH |
| `tools/MathematicaLink/OpticaEmSession.cs` | thin OpticaEM layer; runs author-supplied program files |
| `tools/MathematicaLink/MathematicaLink.csproj` | net8.0-windows; Wolfram.NETLink 1.7.1 NuGet; copies `ml64i4.dll` from project root to output |
| `tools/MathematicaLink/.gitignore` | ignores `ml64i4.dll`, bin/obj |
| `tools/MathematicaLink/README.md` | error-model + wiring writeup |
| `tools/MathematicaLink/CLAUDE.md` | auto-loaded project context |
| `docs/mathematica-opticaem-runbook.md` | idiosyncrasy log (seeded with real entries) |
| `docs/mathematica-startup-verify.md` | boot sequence + ladder |

Commits: `4431da9` scaffold · `da74908` adopt connect-test launch strategy ·
`0cde6e5` pin `ml64i4.dll` (survives clean) · `bc1c039` add CLAUDE.md.

### The error model (the design answer to "value or a sequence of errors")
A bare string loses too much: one Mathematica evaluation can return a good value
**and** a stream of messages (`Power::infy`, `OpticaEM::badarg`, …), while separately
the link can die. So every call returns `MathResult` / `MathResult<T>` =
`Status` + `Value` + **`Messages` list** + `Raw`. Three failure classes handled
differently: `LinkError`/`Timeout` → **restart** (accepted recovery);
`EvaluationError`/`CompletedWithMessages` → the syntax/functionality bucket to
diagnose & log; `NotStarted` → lifecycle bug.

### The startup solution (proven, ported from the connect test)
Source: **`WCMoses/Mathematica-Basic-NetLink-Connection`** (public), verified against
**Mathematica 15.0**. Key facts learned there and folded in:
- The `Wolfram.NETLink` NuGet package (**v1.7.1**) is **managed-only**. It P/Invokes
  the native **`ml64i4.dll`**, which ships inside the Mathematica install
  (`<install>\SystemFiles\Links\NETLink\`). Missing it → "type initializer for
  'Wolfram.NETLink.Internal.NativeLink' threw an exception". **This is the "file to
  copy into bin/Debug" the user remembered.**
- `MathematicaLocator.EnsureNativeLibraryOnPath()` finds the install (newest under
  Program Files, or `MATHEMATICA_HOME`) and prepends its native-lib dir to PATH — so
  no manual copy is strictly required.
- Launch strategy: explicit `-linkmode launch -linkname "\"<MathKernel.exe>\" -mathlink"`,
  fallback to default `MathLinkFactory.CreateKernelLink()`, wrapped in a **60s
  worker-thread startup timeout** that reports whether it stalled *creating the link*
  vs *waiting for the handshake*.
- A handshake stall almost always = kernel **blocked on a license/activation prompt**.
  Fix: run `MathKernel.exe`/`WolframKernel.exe` by hand once to clear it.

### The `ml64i4.dll` "survive clean" pattern
Keep `ml64i4.dll` in the **project root** (next to the `.csproj`), git-ignored. The
csproj copies it to the build output (`<None ... CopyToOutputDirectory=PreserveNewest>`).
`dotnet clean` wipes `bin/` but not the root copy, and the next build re-copies it;
the locator's exe-dir check then **pins** that copy over PATH discovery.
- Copy from: `C:\Program Files\Wolfram Research\Mathematica\15.0\SystemFiles\Links\NETLink\ml64i4.dll`
- Drop at (current layout): `<repo>\tools\MathematicaLink\ml64i4.dll`
- Drop at (after moving to OpticaEmLink): `C:\Dev\OpticaEmLink\src\OpticaEmLink\ml64i4.dll`

### The seam to validate first
`NetLinkMathematicaLink` captures messages by wrapping each eval as
`Module[{afRes=(expr)}, "…"<>ToString[afRes,InputForm]<>"…"<>ToString[$MessageList,InputForm]<>"…"]`
with sentinel markers, parsed by `ParseFramed`. **How OpticaEM actually surfaces
errors is the top unknown** — validate/tune this wrapper + parser (`ParseFramed`,
`LooksLikeError`) against the real kernel and record findings in the runbook.

---

## 4. Key decisions (don't relitigate)
- Result envelope, not bare strings.
- One persistent kernel; **serialize all calls** (a kernel does one eval at a time —
  concurrent calls corrupt the link; enforced with a `SemaphoreSlim`).
- Locator + explicit-`-mathlink`-then-default launch + 60s startup timeout.
- `ml64i4.dll` in project root, copied to output, git-ignored.
- No OpticaEM syntax in C#; programs are author-supplied files.
- One interface for all backends (local now; cloud / Wolfram Cloud later).
- The image path (`EvaluateToImage`) returns a `System.Drawing.Image` (desktop only)
  and does **not** capture `$MessageList`; if an image step needs its messages,
  evaluate once with `EvaluateAsync` first.

---

## 5. Status & next steps
- **Startup half: built and proven-equivalent to the working connect test.**
- **OpticaEM half: pending two inputs from the user:**
  1. The **OpticaEM load call** for the current version (user waiting on a password /
     newer OpticaEM; the author may help directly).
  2. **One validated program** (even just `01_element.m`) to seed the ladder.
- Then: build; run the boot sequence in `docs/mathematica-startup-verify.md`; work
  the 5-step ladder; fill the runbook's `load` / `evaluate` entries with real
  OpticaEM behavior.
- Later: turn "rays are altered" into a **numeric** before/after check — that same
  channel becomes the Mathematica scoring oracle for the bigger project.

---

## 6. Logistics & loose ends
- **New repo requested:** `OpticaEmLink` (private, **link layer only**), local dir
  `C:\Dev\OpticaEmLink`. The cloud session **could not create it** (GitHub returned
  403 "Resource not accessible by integration"). The user needs to create the empty
  repo. Planned standalone layout: `src/OpticaEmLink/` (the 5 `.cs` + `.csproj` +
  git-ignored `ml64i4.dll`), `docs/`, root `README.md`, `.gitignore`, `programs/`.
  Align the C# namespace from `MathematicaLink` to `OpticaEmLink` when moving.
- **Remove from the fork:** after the files are safely in `OpticaEmLink`, delete
  `tools/MathematicaLink/` and `docs/mathematica-*.md` from the agent-framework
  branch (the user asked for this cleanup).
- **Unrelated misdelivery:** an earlier request to process `3DModels.xlsx` + `.obj`
  files was meant for a different container (**TestIngest**), not this project. Those
  files never arrived here; nothing was done with them. Ignore unless the user
  re-raises it (in the right place).

---

## 7. How to run locally (suggested first moves)
```powershell
# get the code (whole fork, or just the two paths below)
cd C:\Dev\OpticaEmLink
git clone https://github.com/WCMoses/agent-framework.git .
git checkout claude/llm-optics-parameter-automation-ra2723
# (or pull only tools/MathematicaLink/ and docs/mathematica-*.md)

# put the native lib where the build will pin it
copy "C:\Program Files\Wolfram Research\Mathematica\15.0\SystemFiles\Links\NETLink\ml64i4.dll" ^
     tools\MathematicaLink\ml64i4.dll

# then, in a local `claude` session:
#  - read tools/MathematicaLink/CLAUDE.md and this HANDOFF.md
#  - build tools/MathematicaLink, wire a tiny console entrypoint
#  - run the boot sequence: StartAsync -> HealthCheckAsync (2+2) -> LoadPackageAsync(<OpticaEM load call>)
#  - once a program file exists, RunProgramFileToPngAsync to render the ladder
#  - log every idiosyncrasy in docs/mathematica-opticaem-runbook.md
```

Note: `MathematicaLink` has no `Program.cs`/entrypoint yet — it's a library. Add a
small console or test harness locally to drive it.
