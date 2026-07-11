# Implementation Plan: Cited Evidence Artifacts Must Exist

**Spec**: `specs/099-evidence-artifact-existence/spec.md`

**Tracks**: FS.GG.SDD#349 (child of `.github` epic #266, instance (j))

**Tier**: **Tier 1** — new diagnostic id (`evidence.artifactNotFound`), new blocking behaviour in two
command contracts (`evidence`, `verify`), additive public functions in `FS.GG.SDD.Artifacts`.

## Summary

Make the evidence gate compare against the filesystem instead of against a string. One pure rule in
`Artifacts`, one second-wave read at the MVU edge, one diagnostic, one mirror in `verify`. Ship needs
no change.

## Technical Context

- F# / net10.0, Elmish-MVU command workflow, pure `update` + edge interpreter (`CommandEffects.interpret`).
- The existence fact must reach the pure core as *data*, not as a `File.Exists` call inside a handler
  (Constitution V; Engineering Constraints).

## Constitution Check

| Principle | How this feature satisfies it |
|---|---|
| I. Spec → `.fsi` → tests → impl | This spec; then `Evidence.fsi`; then the failure-leg tests; then the bodies. |
| II. Structured artifacts are the contract | No schema change. `evidence.yml` already carries `artifacts[]` / `sourceRefs[].path`; this feature only *believes* them. |
| III. Visibility lives in `.fsi` | Two additive functions exported from `Evidence.fsi`; `PublicSurface.baseline` regenerated. |
| IV. Idiomatic simplicity | No new effect case, no new vocabulary, no new schema field. One predicate, one path-collector. |
| V. MVU is the boundary for I/O | Existence is sensed by a `ReadFile` effect interpreted at the edge; the pure core reads the interpreted log. **No `System.IO` in `Artifacts` or in any handler.** |
| VI. Test evidence is mandatory | Failure leg asserted by diagnostic id for *both* buckets (FR-009); the two committed tests that cite a phantom `.png` are made honest. |
| VIII. Observability / safe failure | "missing artifacts" is named *verbatim* in Principle VIII as something that MUST produce an actionable diagnostic. Today it produces none. This feature is a constitution repair, not an enhancement. |

## Design Detail

### Why `ReadFile` and not a new `FileExists` effect

The interpreter already yields the existence fact for free (`CommandEffects.fs:342-350`):

```fsharp
| ReadFile path ->
    match snapshotIfExists projectRoot path with
    | Some snapshot -> success effect (Some snapshot)
    | None          -> success effect None      // the file is not there
```

So `hasInterpreted (readEffectKey p) model && (snapshot p model).IsNone` **is** "probed and absent",
and it already flows into the pure `update`. Three reasons to reuse it rather than add a case:

1. `CommandEffect` is public (`CommandTypes.fsi`). Adding a union case is a **breaking change** for
   F# consumers' exhaustive matches, and `API compatibility gate (breaking-change → SemVer major)` is
   a required check. A blocking-diagnostic bugfix should not force a major bump.
2. It keeps the change out of `CommandTypes.fs/.fsi`, `LifecycleSensing.fs`, and `Foundation.fs` —
   files another in-flight item (#352) is editing. Disjoint touch-sets, parallel landing.
3. `sensedPaths` (`LifecycleSensing.fs:74-82`) folds **only `EnumerateDirectory`** snapshots, so extra
   `ReadFile`s do not pollute lifecycle sensing or the report's sensed metadata.

**The cost, stated plainly**: `snapshotIfExists` calls `File.ReadAllText`, so probing a cited `.png`
reads its bytes into memory as text. For screenshots and logs this is noise. It would stop being
acceptable if workspaces start citing large binaries (video capture, trace dumps), at which point the
right move is a `FileExists of path: string` case — recorded in the spec's **Deferred** section, with
the exhaustive-match sites already enumerated there. This is a deliberate trade, not an oversight.

### The second-wave read

The cited paths are *data* — they are not known until `evidence.yml` has been read. The codebase
already has exactly this pattern: `duplicateCandidateReadEffects` (`Foundation.fs:910-924`) derives a
second wave of `ReadFile` effects from what the first wave read, and `CommandWorkflow.fs:78-93` only
computes the stage plan once that wave is empty.

So: `HandlersEvidence.citedArtifactReadEffects workId model` parses the already-read `evidence.yml`,
collects every cited path from *satisfying* declarations, and returns a `ReadFile` for each not already
planned. It is appended at the existing second-wave site in `CommandWorkflow.fs`, gated to the
`Evidence` / `Verify` / `Ship` commands. `appendNewEffects` dedups by `effectKey`, so the loop
converges (one extra wave, then empty).

The helper lives in `HandlersEvidence.fs`, **not** `Foundation.fs`, to stay off #352's touch-set.

### One rule, three callers (FR-007)

Following the `passesWithoutRenderedArtifact` precedent (`Evidence.fs:167-177`), the rule is written
once in `Artifacts` and consumed by the evidence gate, the `ED-` cascade, and the `TD-` mirror:

```fsharp
val citedArtifactPaths: declaration: EvidenceDeclaration -> string list
val missingCitedArtifacts: exists: (string -> bool) -> declaration: EvidenceDeclaration -> string list
```

`citedArtifactPaths` = `artifacts[]` ∪ `sourceRefs[].path` (never `uri`), blanks dropped, deduped,
sorted. `missingCitedArtifacts` returns `[]` for any non-satisfying declaration (FR-006), so the
`pass ∧ ¬synthetic` gate lives inside the rule and cannot drift between the three call sites.

`exists` is injected as a plain predicate — `Artifacts` never touches `System.IO` (SC-005).

### Ship needs no change

`HandlersShip.shipVerificationPrerequisite` (`:265-276`) already fails on any `blocking` finding in
`verify.json`. Turning the disposition `invalid` is sufficient (FR-005).

## Verification Plan

| Claim | How it is verified |
|---|---|
| FR-001 / SC-001 | `evidence` over a workspace citing a missing path emits `evidence.artifactNotFound` and writes nothing. |
| FR-002 | Two failure-leg tests: one for the `artifacts:` bucket, one for `sourceRefs[].path`. A `uri`-only declaration is asserted *not* to block. |
| FR-004 | `verify` test: `TD-` state `invalid`, severity `blocking`, readiness `needsVerificationCorrection`. |
| FR-006 | A `deferred` declaration citing an absent path is asserted green. |
| FR-008 / SC-002 | The example corpus is repaired; a census test asserts every cited path in `docs/examples/lifecycle-artifacts/` exists. |
| SC-003 | `EvidenceCommandTests.fs:1184` / `:1206` write the `.png` they cite. |
| SC-005 | `grep` for `System.IO` in `Artifacts` / handlers stays empty (already true; a reviewer check, not a test). |

Full offline suite (`dotnet test FS.GG.SDD.sln -c Debug`) must stay green: 1,629 passing at baseline
`d21774d`.

## Migration

None. No schema version moves; `evidence.yml` is unchanged on disk. The behaviour change is that
declarations which were *already lying* now block — which is the point. A workspace whose evidence
cites a phantom path will go red on its next `evidence`/`verify` run and must either produce the
artifact or stop claiming it.

## Complexity Tracking

No constitutional deviation. No new effect case, no new schema field, no new result vocabulary.
