# Mathematica + OpticaEM — Startup & Verification Sequence

The ordered path from "nothing running" to "OpticaEM drawing pictures", and the
5-step known-good ladder. Each step maps to a `MathResult` you can assert on.

## Boot sequence

| # | Action | Call | Pass condition |
|---|--------|------|----------------|
| 1 | Cold-start kernel | `StartAsync()` | `Status = Ok` |
| 2 | Verify the link | `HealthCheckAsync()` (sends `2+2`) | `Value == "4"` |
| 3 | Load OpticaEM | `LoadPackageAsync("<load call>")` | `IsSuccess`, no error-severity messages |
| 4 | Verify OpticaEM is live | `EvaluateAsync("<a trivial OpticaEM call>")` | returns a sane value, no `::` errors |
| 5 | Run the ladder | see below | each returns a PNG; rays altered as expected |

Steps 1–2 are generic Mathematica. Steps 3–4 are the first place OpticaEM-specific
idiosyncrasies show up — log anything odd in the runbook.

## Known-good program ladder (you author these files)

Drop each program in `tools/MathematicaLink/programs/` and run it through
`OpticaEmSession.RunProgramFileToPngAsync`. The C# never contains OpticaEM syntax —
these files are the source of truth, generated from the GUI / OpticaEM docs / the
author, then validated.

| # | Program file | Produces | What to verify |
|---|--------------|----------|----------------|
| 1 | `01_element.m` | 3D image of a single optical element | element renders |
| 2 | `02_ray_cone.m` | picture of a cone of rays | cone renders |
| 3 | `03_cone_through_element.m` | rays from (2) through element from (1) | **rays are altered** by the element |
| 4 | `04_two_elements.m` | as (3) plus a second element | rays altered by **both** elements |
| 5 | `05_gaussian_beam.m` | (2)–(4) with a Gaussian beam instead of a ray cone | beam propagates / focuses as expected |

Each step's PNG lands next to the program (or in `out/`) for eyeball verification.
"Rays are altered" is a human check at first; once we know the OpticaEM output
shape we can add a numeric assertion (ray directions before/after) — that same
numeric channel becomes the Mathematica scoring oracle for the larger project.

## What we still need to fill in

1. The **kernel command line** for the machine (MathKernel path).
2. The **OpticaEM load call** for the current version (waiting on the password /
   new version).
3. **One validated program** (even just `01_element.m`) to seed the ladder — from
   the GUI export or the author, so we debug real syntax, not guesses.
