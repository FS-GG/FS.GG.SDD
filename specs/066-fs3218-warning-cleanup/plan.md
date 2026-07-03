# Implementation Plan: Clear the FS3218 / FS3262 warning debt and drop the ratchet exemption

**Branch**: `066-fs3218-warning-cleanup` | **Spec**: `specs/066-fs3218-warning-cleanup/spec.md`

**Input**: Issue #85 §2. Design context already settled in `specs/064-build-ci-hygiene/` (the ratchet + exemption) and this repo's `Directory.Build.local.props`.

## Summary

Pay down the two warning classes feature 064 exempted, then delete the exemption.
The work is concentrated and mechanical: 226 canonical FS3218 warnings all in one
re-export facade, and 2 canonical FS3262 warnings in one file. No public surface,
no schema, no golden output moves. The load-bearing risk is behaviour-preservation,
verified by a green suite plus zero `.fsi`/golden drift under the now-stricter ratchet.

## Technical Context

- **Language/Runtime**: F# on `net10.0`, `Nullable enable`, `TreatWarningsAsErrors=true`.
- **Warning ratchet**: `Directory.Build.local.props` promotes every compile-phase F#
  warning to an error, with `FS3218;FS3262` currently carved out of the promotion.
  The restore-phase (`NU16xx`), API-gate (`RS####`), and NuGet-audit (`NU190x`)
  exemptions on adjacent lines are unrelated and MUST be preserved.
- **Measured scale (feature start)**: `dotnet build -t:Rebuild` emits 452 FS3218 (226
  unique source lines, all in `CommandReports.fs`) and 4 FS3262 (2 unique source
  lines, `HandlersScaffold.fs:42` and `:76`).

## Decisions

- **D1 — Fix FS3218 by moving the implementation to the signature.** The committed
  `CommandReports.fsi` is the public contract and names every argument. The facade
  `CommandReports.fs` forwards with positional `a0 a1 …`. Rename each forwarding
  function's parameters to the exact names in the `.fsi`, keeping order and arity.
  Rejected: editing the `.fsi` to say `a0` — that would move the public surface and
  is a worse contract. Rejected: `#nowarn "3218"` in the file — it re-hides the class
  we are trying to make the ratchet cover.

- **D2 — Fix FS3262 by using the known-non-null value directly.** At both sites the
  parameter is typed `string` (non-null under `Nullable enable`), so
  `Option.ofObj v |> Option.defaultValue ""` is exactly `v`. Replace the guard with
  `v`. Observationally identical; removes dead code the compiler already proved
  unreachable-for-null.

- **D3 — Drop the exemption and update the comment.** Remove `FS3218;FS3262` from the
  first `WarningsNotAsErrors` append in `Directory.Build.local.props`, and rewrite the
  preceding comment to record that the classes were cleared in feature 066 and are now
  covered by the ratchet (rather than describing them as deferred debt).

- **D4 — Verify under the stricter ratchet, not the old one.** The exemption drop must
  precede the final verifying build so both classes are promoted to errors — a green
  clean rebuild is then proof there are zero residual occurrences (SC-001 + SC-004).

## Files Touched

- `src/FS.GG.SDD.Commands/CommandReports.fs` — rename forwarding params to match `.fsi` (FR-001).
- `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` — replace two `ofObj` guards (FR-003).
- `Directory.Build.local.props` — drop `FS3218;FS3262` from `WarningsNotAsErrors`; update comment (FR-004).
- `specs/066-fs3218-warning-cleanup/` — this feature's authored artifacts.

Explicitly NOT touched: `CommandReports.fsi` (FR-002), any managed org build file (FR-006),
any golden/deterministic baseline, any CLI/schema surface.

## Verification Plan

1. `dotnet build -t:Rebuild` → 0 warnings, 0 errors (both classes now promoted). (SC-001, FR-005)
2. `git diff --stat` shows `CommandReports.fsi` unchanged and no golden `.json` baseline changed. (FR-002, SC-003)
3. Full test suite green: `dotnet test`. (FR-005, US2 §3)
4. `fsgg-sdd validate` stays `overallPassed`. (FR-005)
5. `WarningsNotAsErrors` no longer lists FS3218/FS3262; the NU16xx/RS/NU190x exemptions remain. (SC-002)
6. Managed org files byte-identical to `FS-GG/.github` (the `build-config-drift` gate stays green). (SC-005, FR-006)
7. Spot-check the ratchet: temporarily reintroduce one FS3218 and confirm the build fails. (SC-004)

## Risks & Mitigations

- **Hidden shadowing regression** (e.g. a body that uses `id`/`path` as the operator):
  each facade body only forwards its own arguments, so a param named `id` shadows nothing
  it uses. Mitigation: the green suite + zero golden drift is the proof.
- **An FS3218 site outside `CommandReports.fs`**: the measurement says there is none, but
  the verifying clean rebuild under the promoted ratchet fails closed if one exists.
