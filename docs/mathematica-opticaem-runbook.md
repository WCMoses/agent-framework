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

### startup — cold-start the kernel
- **Brief:** _tbd_
- **How to identify:** `StartAsync` returns `Status = LinkError`.
- **How to fix:** _tbd — verify kernel command line / MathKernel path._
- **Status:** open

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
