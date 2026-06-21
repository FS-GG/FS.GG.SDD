# Phase 0 Research: Release and Distribution Readiness

All Technical Context unknowns are resolved below. Each decision records what was
chosen, why, and the alternatives rejected.

## R1 — Where does the package/CLI version live?

**Decision**: Centralize a single semantic `<Version>` in
`Directory.Build.props`. Remove the per-project `<Version>` from
`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, and `FS.GG.SDD.Cli` so all three
packages and the CLI inherit one number. Derive `currentGeneratorVersion` from
the same value (via assembly informational version) so the generator version and
package version cannot diverge.

**Rationale**: Today `Artifacts = 0.1.11`, `Commands = 0.1.10`, `Cli = 0.1.10`,
and `currentGeneratorVersion` hardcodes `0.2.0` — four independently-drifting
numbers. FR-003 requires one deterministic, machine-readable version identity;
US1 SC-001 requires a consumer to read it without source diving. A single
inherited source makes the version identity true by construction.

**Alternatives rejected**:
- *Per-project versions kept in sync by review* — already failed in practice
  (the three numbers diverged); not mechanically enforceable.
- *MSBuild `Nerdbank.GitVersioning`/`MinVer` tooling* — adds a dependency and
  build-time nondeterminism risk; SemVer-from-props is enough for this slice and
  the deterministic build is preserved.

## R2 — Is release-readiness a lifecycle command or a check?

**Decision**: A **pure check** (a fold over already-produced artifacts) hosted by
the test suite, plus an authored machine artifact `release-readiness.json`. It is
**not** a new `fsgg-sdd` command and **not** a new lifecycle stage.

**Rationale**: FR-013 forbids a new lifecycle stage or any change to the
`charter … ship` chain. Constitution V exempts "simple pure … validators" from
MVU ceremony. Modeling readiness as a pure projection keeps the surface minimal,
keeps determinism trivial, and avoids adding agent-command surface (Principle
VII). The conformance check reuses the existing artifact read path; no new write
effect is introduced.

**Scope of "check" (confirms analysis A1)**: FR-012 requires *a* check that
reports not-ready by absence; it does **not** require a consumer-runnable CLI
command. This feature deliberately scopes the check to the **maintainer/CI**
audience (the test suite + the static `release-readiness.json` a consumer can
read). A consumer-runnable `fsgg-sdd`-style readiness command is **explicitly
out of scope / deferred** — adding one would reintroduce the lifecycle-command
surface FR-013 forbids and the enforcement role FR-014 reserves for Governance.
The edge case "Unreleasable state" is satisfied because the CI check blocks the
release before it ships.

**Alternatives rejected**:
- *New `fsgg-sdd release` command* — violates FR-013, expands the MVU surface,
  forces new agent-command generation, and implies an enforcement role that is
  Governance-owned (FR-014).
- *Emit `release-readiness.json` from `ship`/`refresh`* — couples a static
  cross-cutting contract to a per-work-item lifecycle write; the release contract
  is repo-level, not work-item-level.

## R3 — Versioning scheme and change-class → bump mapping

**Decision**: Semantic Versioning as the policy basis (consistent with the
constitution's change classification and spec Assumption "Versioning scheme"):

| Change to a public schema / generated-view shape / command-output / CLI surface | Bump | Migration note? |
|---|---|---|
| Backward-incompatible (removed/renamed/retyped field, changed output shape, removed/renamed command or flag, changed exit-code contract) | **major** | **required** |
| Additive backward-compatible (new optional field, new view kind, new command/flag, new optional report field) | **minor** | none |
| Clarifying / no public-contract change (docs, internal refactor, comment) | **patch** | none |

**Pre-1.0 semantics** (current `0.x` line): under SemVer a `0.y.z` line permits
breaking changes on a **minor** bump; the policy states this explicitly so early
adopters are not surprised (spec edge case "Pre-1.0 / initial release
semantics"). A migration note is still required for any breaking change even
pre-1.0.

**Schema-vs-contract divergence** (spec edge case): a generated view's internal
`schemaVersion` and a cross-repo `contractVersion` (e.g. `governance-handoff.json`)
move independently. Mapping: a breaking change to *either* maps to a **major**
package bump; an additive change to either maps to **minor**. The schema
reference records both numbers per contract so the mapping is auditable.

**Rationale**: SemVer is already the constitution's vocabulary; the table makes
the heretofore-implicit rule explicit and testable (US1 SC-001, acceptance
scenarios 2–4).

**Alternatives rejected**: CalVer (loses compatibility semantics consumers need);
ad-hoc per-release judgement (not auditable — fails acceptance scenario 4).

## R4 — Stability classification vocabulary

**Decision**: A three-value classification applied per contract and (where
useful) per field: `Stable` (frozen; change is breaking → major),
`AdditiveOptional` (may gain optional fields under minor; consumers must tolerate
unknown fields), `Experimental` (may change under minor with a note; not yet
frozen).

**Rationale**: FR-004 requires a stability classification; three tiers cover the
real states (frozen / growing / unsettled) without overfitting. Maps cleanly onto
the SemVer table in R3.

**Alternatives rejected**: binary stable/unstable (cannot express
additive-but-frozen, the common case for the readiness views); a five-tier scheme
(unnecessary precision for the current surface).

## R5 — Determinism strategy for baselines and the release contract

**Decision**: Serialize `release-readiness.json` and all golden fixtures through
the existing canonical `Serialization` module (stable key ordering, no clock, no
host path, no ANSI). Lock determinism with double-run byte-identity tests
(SC-005). Produced-artifact fixtures are captured from a real lifecycle fixture
run, not synthesized.

**Rationale**: The existing generated views already exclude clocks/paths/ordering
nondeterminism (verified across 001–017); reusing that path means baselines
inherit determinism for free. FR-008 makes byte-stability mandatory.

**Alternatives rejected**: bespoke serializer for the release contract (risks a
second, divergent canonicalization); approval-test framework dependency
(unnecessary — plain string baselines suffice and match the existing
`PublicSurface.baseline` convention).

## R6 — CLI distribution channel

**Decision**: Package the CLI as a .NET tool: `PackAsTool=true`,
`ToolCommandName=fsgg-sdd`, `PackageId=FS.GG.SDD.Cli`. Installation docs use
`dotnet tool install`. The specific public registry/account, signing, and
trusted-publishing/provenance enforcement are **out of scope** (Governance /
release-ops; spec Assumption "Distribution channel").

**Rationale**: FR-011/SC-007 require a clean-environment install that reaches
`fsgg-sdd ship` with no FS.GG checkout and no Governance runtime. `dotnet tool`
is the standard, cross-platform path for a `.NET` CLI and needs only the metadata
above plus docs. Provenance/publish enforcement is explicitly a different owner
(FR-014).

**Alternatives rejected**: self-contained single-file binaries per OS (heavier,
not needed for this slice); global `dotnet` project reference (requires repo
checkout — fails the clean-environment requirement).

## R7 — Schema reference: catalog scope and "projection, not second source"

**Decision**: The catalog covers exactly the 7 `GeneratedViewKind` outputs
(`WorkModel`, `Analysis`, `Verify`, `Ship`, `Summary`, `AgentCommands`,
`GovernanceHandoff`) and the public `--json` command-output report(s)
(`CommandReport` via `CommandSerialization.serializeReport`). Each entry names its
`schemaVersion` (+ `contractVersion` where applicable), field inventory,
determinism guarantee, stability class, and a back-reference (`ArtifactRef`) to
the authoritative structured contract. The Markdown schema reference and
compatibility matrix are rendered **from** `release-readiness.json`; a test
asserts the doc and the json agree (FR-005/FR-015 — structured wins).

**Rationale**: Bounding the catalog to the enumerable view kinds + the report
surface makes SC-002 ("100% of public outputs documented") checkable by
enumeration rather than judgement. Driving the docs from the json prevents doc
rot (spec edge case "Drift between docs and reality").

**Alternatives rejected**: hand-maintained Markdown table only (drifts — fails
FR-015); documenting authored-source schemas too (out of scope — those are not
"public generated outputs" and FR-013 forbids touching them).

## R8 — Governance boundary

**Decision**: The compatibility matrix records the supported Governance handoff
`contractVersion` range as a declared **string** fact only. No FS.GG.Governance
package reference, no gate/route/profile/freshness computation, no
publish/provenance enforcement appears in any produced artifact. A
boundary-exclusion test asserts the absence of Governance gate vocabulary
(SC-008), mirroring the 017 approach.

**Rationale**: FR-014 and CLAUDE.md's ownership boundary; SDD must build, test,
and install with no Governance runtime (edge case "No Governance installed").

**Alternatives rejected**: importing Governance's `contractVersion` constant
(creates a compile-time dependency the constitution forbids).
