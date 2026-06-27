# Phase 0 Research: SDD skeleton emits `.fsgg/constitution.md`

All decisions are grounded in the current tree (verified 2026-06-27 @ `50b086e`). The feature
is unusually small in production surface; the research below is mostly *disproving* the need for
edits the spec might appear to require, and *resolving the one genuine design choice* (the write
kind).

## D1 — Write kind: `AgentGuidanceTarget`, not `AuthoredSource`

**Decision**: Emit the constitution with `ArtifactWriteKind.AgentGuidanceTarget`.

**Rationale**: FR-008 requires the constitution to be **no-clobber** — an author edit must
survive a re-`init`. The overwrite policy is decided by `canOverwrite`
(`CommandEffects.fs:42-48`):

```fsharp
let canOverwrite (kind: ArtifactWriteKind) (existing: FileSnapshot option) (text: string) =
    match existing, kind with
    | None, _ -> true                              // create when absent
    | Some snapshot, _ when snapshot.Text = text -> true  // identical → NoChange
    | Some _, AuthoredSource -> true               // ← overwrites differing content!
    | Some _, GeneratedView -> true                // refreshable
    | Some _, _ -> false                           // StructuredSource / AgentGuidanceTarget → REFUSE
```

The spec's Key-Entity hint listed "`AuthoredSource` / `AgentGuidanceTarget`", but
`AuthoredSource` returns `true` for a *differing* existing file — it would **silently overwrite**
an author-ratified constitution and **violate FR-008**. (`AuthoredSource` is correct for
lifecycle work files like `spec.md`/`plan.md`, which the owning command re-authors from user
input — `HandlersEarly.fs:56,93,132,177`.) The two no-clobber kinds are `StructuredSource` and
`AgentGuidanceTarget`. Both also report `Ownership = "authored"` (`CommandReports.fs:934`,
`if kind = GeneratedView then "generated" else "authored"`), satisfying FR-010.

Between them, `AgentGuidanceTarget` is the kind already used for **root authored markdown**
skeleton files — `CLAUDE.md` and `AGENTS.md` (`Foundation.fs:88-89`) — which User Story 3
explicitly names as the behavioral analog ("behaves like the other authored skeleton files
(CLAUDE.md, AGENTS.md)"). The constitution is prose markdown, like those, not structured YAML.

**Alternatives considered**:
- *`AuthoredSource`* — **rejected**: overwrite-allowed on differing content (`canOverwrite` arm
  3), directly violating FR-008. This is the trap the spec's hint half-suggested; the code
  disproves it.
- *`StructuredSource`* — viable (no-clobber, authored ownership) and arguably consistent with
  the file's `.fsgg/` location next to `project.yml`/`sdd.yml`. **Rejected** as second choice
  only because the constitution is prose markdown, not structured config, and US3 anchors the
  behavioral analog on the markdown files; the report `Kind="structuredSource"` would mislabel
  prose.
- *A new `ArtifactWriteKind` case (e.g. `AuthoredGovernance`)* — **rejected**: a Tier-1 public
  surface addition (`.fsi` case, `PublicSurface.baseline` regen, new `writeKindValue`/
  `canOverwrite`/ownership arms, broader test surface) for a label nicety. The spec scopes the
  feature to reusing an existing kind ("the plan selects the precise `ArtifactWriteKind` — e.g.
  `AuthoredSource` / `AgentGuidanceTarget`"). Reuse wins.

**Residual note**: the report `Kind` will read `"agentGuidance"` for the constitution. This is a
known, accepted imprecision (it is governance, not agent guidance); the *behavior* (no-clobber,
authored ownership, SDD-skeleton attribution) is exactly right, which is what FR-008/FR-010
require. If a future feature wants a precise label it can add a dedicated kind without changing
this feature's behavior.

## D2 — One `initEffects` line is sufficient; everything else is derived

**Decision**: The only production change is adding one `WriteFile` to `initEffects`
(`Foundation.fs:81-91`) plus its content constant. No edit to scaffold, refresh, provenance,
serialization, or the report.

**Rationale** — each spec obligation traced to existing derived behavior:

- **FR-001 (init emits it)**: `initEffects` is the skeleton planner; adding the `WriteFile`
  emits the file on every `init`.
- **FR-004 (scaffold delivers it, reused/unchanged effects)**: `scaffold` lays the skeleton by
  replaying `initEffects` (the same list), so the file is written on the scaffold path with no
  scaffold-specific code.
- **FR-005 (absent from `generatedProduct`)**: scaffold computes
  `produced = after − before − skeletonFiles − provenance` (`HandlersScaffold.fs:308-310`), and
  `skeletonFiles` is **derived from `initEffects`** by collecting its `WriteFile` paths
  (`HandlersScaffold.fs:77-82`). Adding the constitution to `initEffects` adds it to the
  subtracted `skeletonFiles` set in the same change — it is structurally impossible for it to
  appear in `generatedProduct`. No provenance schema or writer change.
- **FR-009 (refresh leaves it untouched)**: `refresh` regenerates only a fixed set of
  `readiness/<id>/*` views and configured agent-guidance targets (`HandlersRefresh.fs:178-295`);
  it has **no generator** targeting any `.fsgg/` root file, so the constitution is never
  regenerated, diffed, or flagged stale/generated/external. The `authoredPreserved` list
  (`HandlersRefresh.fs:113-123`) is informational only.
- **FR-010 (report attribution)**: the existing `WriteFile` arm of `changeFromEffectResult`
  (`CommandReports.fs:914-944`) emits a changed-artifact entry with `Kind`/`Ownership`/operation
  for *every* `WriteFile`, including the new one — no report change needed.

**Alternatives considered**: extending a hardcoded skeleton list, a refresh allow-list, or a
provenance exclusion list — **all rejected** because no such hardcoded list exists; the relevant
sets are derived from `initEffects`. Adding code would be redundant and a second source of truth
(constitution VII).

## D3 — Optional: register `.fsgg/constitution.md` in `authoredPreserved`

**Decision**: Optionally add `".fsgg/constitution.md"` to the informational `authoredPreserved`
list (`HandlersRefresh.fs:113-123`), alongside the existing `.fsgg/project.yml`/`sdd.yml`/
`agents.yml` entries.

**Rationale**: This list does not *protect* the file (refresh already never touches it); it only
makes the refresh summary report which authored files it preserved. Adding the constitution keeps
the summary symmetric with the other `.fsgg/*` authored files and strengthens the FR-009
observability story ("not reported as a generated/stale/external path" → it is reported, if at
all, as *preserved authored*). Low-risk, additive, no behavior change. Marked optional because
FR-009 is satisfied without it.

## D4 — Generic, deterministic, placeholder-free body

**Decision**: The body is a constant `string` literal `constitutionText` beside the other
skeleton strings in `Foundation.fs`, fixed verbatim in
[contracts/constitution-content.md](./contracts/constitution-content.md).

**Rationale**:
- **Determinism (FR-007/SC-003)**: a constant literal with **no** date, timestamp, randomness,
  machine name, or environment-derived value is byte-identical across runs and machines — the
  same mechanism that already makes `sddConfigText`/`agentGuidance` deterministic. Critically,
  the seed contains **no ratification date** (unlike this repo's own
  `.specify/memory/constitution.md`, which carries `Ratified: 2026-06-19`); the author adds a
  date when they ratify.
- **Populated, not placeholder (FR-002/SC-001)**: a complete, opinionated F#-SDD-product
  constitution with a real title and real principles, no `[BRACKET]` tokens. Consistent with the
  skeleton convention (populated `project.yml`/`sdd.yml`/`agents.yml`, never blank stubs).
- **Generic (FR-003/SC-006)**: contains **no** `FS.GG.SDD`, `FS.GG.Rendering`, `FS.GG.Governance`,
  provider package/template id, path, or docs URL. It speaks of "this product", "optional
  external governance tooling", and generic F#/.NET/MVU/Spec-Kit practice — the F#-SDD baseline
  per the Engineering Constraints, which every SDD-managed product shares. (The product CLI name
  `fsgg-sdd` is generic product identity, not a repo-specific string, and is avoided in the body
  anyway to keep it tool-agnostic.) Distinct from this repo's own constitution, which is
  deliberately repo-specific and must **not** be copied.

**Verification**: SC-006/FR-003 are enforced two ways — the repo-wide **C1** leak scan in
`ScaffoldGuardTests.fs` (over `src/**/*.fs(i)`, which now includes the `Foundation.fs` literal)
catches provider/rendering identifiers, and a dedicated US1 generic-content assertion scans the
emitted file for the broader repo-/template-specific token set.

## D5 — Re-baseline is automatic; release catalog is untouched

**Decision**: Add positive US1/US2/US3 tests; force **no** golden regeneration.

**Rationale**:
- The init/scaffold skeleton-set tests enumerate the skeleton **dynamically**
  (`ScaffoldCommandTests.fs:442-469` "provenance records exactly the app-only tree",
  `:474-492` "skeleton byte-identical to init", `:498-509` determinism) via
  `relativeFiles initRoot` / `skeletonFiles`. They self-adjust to the new file and keep passing;
  the **app-only** produced set is hardcoded (`["App.fsproj"; "Program.fs";
  "scaffold-manifest.txt"]`) and is unaffected, which is itself the FR-005 proof.
- **`release-readiness.json`** (`tests/FS.GG.SDD.Artifacts.Tests/baselines/`, asserted by
  `ReleaseContractTests.fs:112-119`) catalogs **lifecycle artifacts and commands**, not skeleton
  files. A grep of `docs/release/` for `constitution`/`skeleton`/`project.yml`/`CLAUDE.md`
  returns nothing, confirming the skeleton is not enumerated there. The release contract and its
  golden baseline are therefore **unchanged** — the constitution is authored skeleton content,
  not a produced lifecycle artifact (consistent with the spec's framing and CLAUDE.md's note
  that not every emitted file is a catalogued artifact).
- **`PublicSurface.baseline` (×4)** snapshots public type/member surfaces; this feature adds no
  type or signature, so all four are unchanged.

**Alternatives considered**: pre-emptively regenerating goldens — **rejected**: nothing in the
golden set enumerates the skeleton; regenerating would be a no-op churn that obscures the real
(zero) contract delta. If any dynamic test surprises us at implementation time, that is a real
finding to investigate, not a baseline to rubber-stamp.

## Open questions

None. The one genuine design choice (D1, write kind) is resolved against `canOverwrite`; all
other obligations are discharged by derived behavior (D2) and the content contract (D4).
