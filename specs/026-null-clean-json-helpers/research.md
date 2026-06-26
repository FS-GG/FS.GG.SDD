# Research: Null-Clean JSON Access + Warnings-as-Errors Gate

**Feature**: 026-null-clean-json-helpers | **Date**: 2026-06-26 | **Phase**: 0

All Technical Context unknowns are resolved below. Each decision is grounded in a
measured build of the merge base (Release, `--no-incremental`, .NET SDK 10.0.x,
build `0.2.0`).

## Baseline measurement (ground truth)

| Metric | Value | How measured |
|---|---|---|
| Raw FS3261 emissions | 952 | `dotnet build -c Release --no-incremental \| grep -c "warning FS3261"` |
| Unique FS3261 sites | 283 (275 src + 8 test) | dedup of `file(line,col)` across projects |
| FS0025 sites | 0 | no `warning FS0025` in output (cleared by R4) |
| Any other warning category | **0** | the only category emitted is FS3261 |
| Dominant message shapes | "non-nullable 'string' expected but expression is nullable" (~225), "type 'string' does not support 'null'" (~44), "'string'/'string \| null' not compatible" (~15), a few `Process`/`DirectoryInfo` (mostly tests) | grouped by message tail |

The 952-vs-283 gap is the known per-referencing-project re-emission: an Artifacts
warning re-counts in every project that references Artifacts. Fixing the 283
unique sites clears all 952 emissions.

## D1 — Gate form: scoped `WarningsAsErrors`, not global `TreatWarningsAsErrors`

**Decision**: Add `<WarningsAsErrors>FS3261;FS0025</WarningsAsErrors>` to
`Directory.Build.props`; leave `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`
as-is.

**Rationale**: The build emits **only** FS3261 today, so scoped and global are
behaviorally identical right now — but scoped is strictly safer going forward: a
future compiler/SDK upgrade or new dependency that introduces an unrelated warning
category (e.g. obsolete-API, unused-binding) would silently start failing every
build under a global flag, ballooning unrelated work onto whoever next touches the
repo. Scoping promotes exactly the two categories this feature owns and cleans,
satisfying FR-006/SC-006 by construction. The refactor report explicitly offers
this form ("or `WarningsAsErrors=FS3261;FS0025`").

**Alternatives considered**:
- *Global `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`*: rejected — couples
  this feature to every future warning category; higher latent breakage; no benefit
  today since no other category is emitted.
- *Per-project `<WarningsAsErrors>` in each `.fsproj`*: rejected — duplicative and
  drift-prone; `Directory.Build.props` already single-sources shared MSBuild
  properties (`Nullable`, `Deterministic`, version).

## D2 — Gate scope across projects: include tests via `Directory.Build.props`

**Decision**: Place the property in `Directory.Build.props` so it is inherited by
all `src` **and** test projects; clean the 8 test sites as part of Story 1.

**Rationale**: Uniform discipline — a nullness defect in a test is as misleading as
one in `src`. The test sites are few (8) and shape-identical to `src` sites, so the
marginal cost is small (FR-007 "test projects also brought to 0"). Keeping a single
inherited property avoids a split policy where `src` is gated but tests are not.

**Alternatives considered**:
- *Exclude tests* (gate only `src`, e.g. via a `Directory.Build.props` under `src/`):
  rejected — leaves a blind spot and a second policy surface; the assumption in the
  spec already chose inclusion.

## D3 — Null-handling strategy: idiomatic F# nullness at the lowest shared boundary

**Decision**: Resolve every nullable with a built-in F# nullness idiom, applied at
the lowest shared boundary available in each assembly:
- `JsonElement.GetString()` (the dominant source) → `Option.ofObj (e.GetString())`
  yielding a clean `string option`, centralized in `LifecycleArtifacts/Internal.fs`
  (`jsonString`, `jsonStringList`, `parseJsonDigest`). This one cluster of edits
  clears the 8 `Internal` sites **and** the downstream "compatible nullability"
  propagation in the `Analysis`/`Verify`/`Ship`/`Guidance` `build` callbacks.
- Parameters that legitimately receive null (e.g. `normalizePath (path: string)` whose
  body already does `if isNull path`) → annotate the parameter `string | null` so the
  `isNull` test is well-typed, or replace `isNull x` with `String.IsNullOrEmpty x`
  (whose BCL signature accepts `string | null`).
- Remaining one-offs → explicit `match x with | null -> … | s -> …` pattern-match.

**Rationale**: These are the standard F# 9/10 nullness remedies and match the
constitution's "idiomatic simplicity" principle. Centralizing at the JSON helper
boundary is the highest-leverage move (the report's estimate): a few dozen helper
edits clear the ~144 parser-family sites and ~53 `WorkModel` sites that cluster at
`GetString()`. Crucially, `Option.ofObj x |> Option.defaultValue ""` is
**behaviorally identical** to the existing `if isNull x then "" else x`, so output
stays byte-identical (FR-003).

**Alternatives considered**:
- *A new shared public `NullSafe` helper module*: rejected — adds public surface and
  changes baselines, breaking Tier 2; and `SchemaVersion.fs`/`GenerationManifest.fs`
  compile *before* the natural helper location anyway.
- *`InternalsVisibleTo` to share Artifacts internals with Commands/Validation*:
  rejected — extra machinery for a handful of cross-assembly sites; inline idioms are
  simpler and local.
- *Blanket `#nowarn "3261"` or per-site `[<SuppressMessage>]`*: rejected as the
  primary strategy — it hides the defect rather than fixing it and would defeat the
  gate's purpose. Reserved only for the genuinely-intractable residue (D6).

## D4 — Cross-assembly / pre-`Internal` sites: fix in place

**Decision**: `SchemaVersion.fs` and `GenerationManifest.fs` (compile *before*
`Internal.fs`) and all `Commands`/`Validation` sites are fixed in place with the D3
idioms. Where a single assembly has a dense cluster (`ValidationContracts.fs`, 14),
an `[<AutoOpen>] module internal` null helper *local to that assembly* is permitted
but optional.

**Rationale**: Respects the existing one-way layering and compile order with zero
structural change; no file moves, no new dependency edges. Each assembly owns its
own (internal) null hygiene.

## D5 — Non-`string` nullness (`Process`, `DirectoryInfo`)

**Decision**: Handle the few non-string nullable sites (`Process | null` in Cli
tests, `DirectoryInfo | null` in Artifacts tests, a couple in `ReleaseContract.fs`)
with the same pattern-match / `Option.ofObj` idiom locally.

**Rationale**: Same remedy family; low count (≈8, mostly tests). No special-casing
needed.

## D6 — Intractable residue: enumerate, then suppress explicitly

**Decision**: If any site cannot be made null-clean by D3–D5 (none expected after
inspection), handle it with an explicit, commented per-site suppression and record
it in `data-model.md`/the PR so the gate still fails on *new* sites (FR-009).

**Rationale**: The gate's value is catching regressions; a known, justified, single
suppression preserves that while acknowledging a real BCL nullability gap. The
expectation from the message-shape analysis is **zero** residue — every shape maps
to a D3 idiom.

## D7 — Sequencing: clean to zero, then flip the gate

**Decision**: Land all null-cleanup edits (Story 1) and confirm a 0-count Release
build **before** adding the `WarningsAsErrors` property (Story 2). Within Story 1,
order by leverage: JSON-boundary helpers first (largest blast radius), then the
remaining clusters, then the long tail and tests.

**Rationale**: Adding the gate while any FS3261 site remains turns the warning into a
hard build break mid-refactor (FR-004 depends on FR-002 being complete). The two
stories are independently shippable in this order: Story 1 alone delivers a clean
signal; Story 2 adds the ratchet.

## Resolved unknowns

No `NEEDS CLARIFICATION` markers remain. The two spec-level scope decisions (gate
form D1, test inclusion D2) were carried as Assumptions in the spec and are now
confirmed by the measurement (only FS3261 emitted; 8 test sites are trivially
cleanable).
