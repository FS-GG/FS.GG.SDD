# Phase 0 Research: CLI Version Coherence in Scaffold Provenance

Feature: `052-cli-version-coherence` · Date: 2026-07-01

All decisions below resolve the spec's open planning choices (the "Exact
classification is a planning decision" edge cases) against the actual codebase.
Nothing here is speculative — each cites the load-bearing source location.

---

## D1 — The producing CLI version is already recorded (FR-001)

**Decision**: Treat the existing `generator.version` in the provenance record as the
authoritative "CLI version used". Do not add a second CLI-version field or re-derive it.

**Rationale**: `ScaffoldProvenanceRecord.Generator: GeneratorVersion` (`{ Id; Version }`)
is already serialized as the `generator` object (`ScaffoldProvenance.fs:41-44`). The value
is `request.GeneratorVersion` (`HandlersScaffold.fs:241`), populated by the CLI edge from
`SchemaVersion.currentGeneratorVersion()` (`Program.fs:129,188`), which reads
`AssemblyInformationalVersionAttribute` off the Artifacts assembly, strips the `+<sha>`
suffix, and falls back to `"0.2.1"` (`SchemaVersion.fs:149-169`). FR-001 is therefore about
*not dropping/duplicating* an existing fact, not adding one.

**Alternatives rejected**: A new `cliVersion` field — redundant with `generator.version` and
would create two sources of truth (violates Principle II).

---

## D2 — Where the provider-declared minimum comes from (FR-002 / FR-009)

**Decision**: Add one **optional** scalar to the provider registry entry, read
value-agnostically via `tryScalarAt`. Proposed YAML key: `minimumCliVersion`. Model it as
`ProviderDescriptor.MinimumCliVersion: string option` in `Fsgg.Provider`
(`FS.GG.Contracts`), defaulting to `None` when absent.

**Rationale**: `ProviderDescriptor` is explicitly an "additive superset" designed to gain
new declared fields (`Provider.fsi:22-31`); the parser already reads optional scalars with
`tryScalarAt` (`Config.fs:73-104,171-205`). SDD reads the raw string and never embeds a
concrete value → FR-009 / SC-005 satisfied by construction. The concrete minimum value is
delivered by the sibling epic-#85 Templates/registry work (spec Assumptions); this feature
only reads it, so it is independently shippable and degrades to "no minimum" when absent.

**Cross-repo coordination**: The `minimumCliVersion` key name and its placement in the
registry schema are a shared provider-contract detail. Per the `cross-repo-coordination`
protocol and epic FS-GG/.github#85, the key name must match what Templates writes into
`providers/rendering.providers.yml`. Plan proposes `minimumCliVersion`; final key is
confirmed via the shared contract issue before merge. Reading is forward-compatible either
way (unknown keys ignored today).

**Alternatives rejected**: A new top-level registry section — unnecessary; the value is a
per-provider fact and belongs on the provider entry. A typed/parsed value in the registry —
SDD must stay value-agnostic; keep it a raw string and parse only at comparison time.

---

## D3 — Version grammar & comparison (Assumption: "same grammar the registry uses")

**Decision**: Introduce a small **public** `Fsgg.Version` module in `FS.GG.Contracts`
exposing a `major.minor.patch` parse + total order, and use it from the scaffold handler.
Refactor `Registry.fs`'s existing *private* `SemVer`/`tryParseSemVer`/`compareSemVer`
(`Registry.fs:67-134`) to delegate to it, so there is exactly one version grammar in the repo.

**Rationale**: A complete `major.minor.patch` parser/comparator already exists but is
`private` to `Registry` (BCL-only, no third-party dependency). The spec Assumption says this
feature "does not introduce a new version format" and "uses the same version grammar the
registry/provider contract already uses". Extracting the existing engine into one shared
public module honors that literally and avoids a second, subtly-different parser.

**API shape** (see `contracts/version-compare.md`): `Fsgg.Version.tryParse: string -> Version option`
and `Fsgg.Version.compare: string -> string -> int option` (returns `None` when *either*
side is unparseable, so callers degrade honestly rather than assert a false ordering).

**Alternatives rejected**: Reusing the scaffold `contractMajor` helper
(`HandlersScaffold.fs:39-49`) — it only inspects the major component and would misjudge
`0.2.x` vs `0.3.x`. Adding a NuGet SemVer package — violates the BCL-only precedent and adds
a dependency for a three-integer compare.

---

## D4 — Boundary semantics: "behind" (FR-004 / FR-006, Edge "exactly equal")

**Decision**: Emit the staleness advisory **iff** `compare installed minimum = Some -1`
(strictly less). Equal (`Some 0`) or greater (`Some 1`) → no advisory. `None` (either side
unparseable) → no staleness advisory (see D6/D7).

**Rationale**: Spec Edge Case: "CLI version exactly equal to the minimum … no advisory
(boundary is 'behind', not 'at or below')". Matches FR-006 ("at or above … no advisory").

---

## D5 — The advisory is a non-blocking `DiagnosticInfo` (FR-004/FR-005)

**Decision**: New diagnostic code **`scaffold.cliBehindMinimum`**, severity
`DiagnosticInfo`, defined in `Diagnostics.fs` alongside the other `scaffold.*` codes.

**Rationale**: Blocking is determined solely by `DiagnosticError`
(`hasBlocking`, `Diagnostics.fs:301-302`); `Info` never blocks and never changes the exit
code (exit map at `CommandReports.fs:1473-1482` only escalates on `Blocked` + provider-defect
ids). The three existing non-blocking scaffold advisories (`providerEmpty`,
`repoInitSkipped*`, `scriptsNotMadeExecutable`) are already `DiagnosticInfo` in
`Diagnostics.fs` — this follows that precedent exactly, giving FR-005 by construction
(exit code and success classification unchanged). It appears automatically in the JSON
diagnostics array and the rich Diagnostics table; text shows the count (see D9).

**Message content (FR-004, "how far behind")**: names the installed version, the required
minimum, and the gap, e.g. *"Installed fsgg-sdd 0.2.1 is behind the provider-declared minimum
0.3.0 (behind by 1 minor version)."* The gap phrase is derived deterministically from the
parsed component difference. The diagnostic's `Correction` carries the remedy pointer (D8).

**Alternatives rejected**: `DiagnosticWarning` for staleness — the doctrine is
report-readiness-not-enforce and the board item explicitly chose warn-not-fail as an
advisory; `Info` is the right non-alarming severity and keeps `SucceededWithWarnings` off the
happy path. A hard error — explicitly out of scope (spec Assumptions).

---

## D6 — Malformed / unparseable provider minimum (Edge Case)

**Decision**: New diagnostic **`scaffold.providerMinimumMalformed`**, severity
`DiagnosticWarning` (non-blocking), emitted when `minimumCliVersion` is present but
`Fsgg.Version.tryParse` returns `None`. In that case: record the provenance
`requiredMinimumCliVersion` as **absent/null** (do not persist an invalid version into the
machine contract), and **skip** the staleness comparison (no `scaffold.cliBehindMinimum`).

**Rationale**: Spec: a malformed minimum "is provider/registry input, not author input … must
not silently drop the coherence check; it surfaces the malformed-minimum condition rather than
treating it as 'no minimum'." A `Warning` surfaces it visibly without blocking (warnings don't
set `hasBlocking`, so exit code and success are unchanged) and without polluting the record's
version field. This distinguishes malformed provider input from author input per Principle
VIII, while honoring "never report an incomplete scaffold as complete" (unchanged — this does
not make the scaffold incomplete).

**Alternatives rejected**: Treating malformed as "no minimum" — spec forbids silent drop.
Making it a blocking error / exit 2 — a malformed *optional* coherence hint should not fail an
otherwise-complete scaffold; the provider still instantiated correctly.

---

## D7 — CLI version cannot be determined (Edge Case)

**Decision**: Record whatever `generator.version` resolves to (never fabricate); if it is
blank/unparseable, `Fsgg.Version.compare` returns `None` and the staleness comparison is
skipped (no advisory). No new diagnostic.

**Rationale**: `currentGeneratorVersion()` always yields a value (real informational version
or the `"0.2.1"` fallback), so in practice the installed version is always present and
parseable; this branch is defensive. The `compare … = None` path already gives honest
degradation — record the fact, skip the comparison, assert no false ordering.

---

## D8 — The re-seed remedy the advisory points at (FR-008 / US3 / SC-006)

**Decision**: The advisory's next-action pointer and docs name **`fsgg-sdd init`** (the reused
seeding effects) as the supported re-seed path — run **after upgrading the `fsgg-sdd` CLI** —
which idempotently, no-clobber re-materializes the 15 seeded `fs-gg-sdd-*` skills and
`.fsgg/early-stage-guidance.md`. Structured `NextAction` `ActionId = "reseedSeededSkills"`,
`Command = Some Init`, `RequiredArtifacts` naming the skill subtrees + guidance file,
`BlockingDiagnosticIds = []`.

**⚠ Correction to the spec's parenthetical (FR-008 / US3)**: The spec text says the remedy is
"`refresh-agents` / the seeding effects". Verified against the code, **`refresh` does NOT
re-seed** — `HandlersRefresh.fs:27` regenerates only work-model-derived views
(`work-model, analysis, verify, ship, governance-handoff, agent-commands, summary`) and emits
no `SeededSkills`/`earlyStageGuidance` writes; it only *reads* provenance to exclude
provider-produced files. The actual re-seed is `initEffects` (`Foundation.fs:373-390`),
reused by `init` and `scaffold`, guarded idempotent/no-clobber by `canOverwrite`
(`CommandEffects.fs:42-48`: absent → write, byte-identical → no-op, `AgentGuidanceTarget`
present & differing → refuse). This matches CLAUDE.md ("Scaffold delivers all of these via the
reused `init` effects … `refresh` never regenerates them"). So the correct, truthful remedy is
**upgrade the CLI, then re-run `fsgg-sdd init`** — which is within FR-008's own wording ("the
seeding effects"). The docs will state this and explicitly note `refresh` does not re-seed.
This is surfaced in the completion report for the user to confirm.

**Rationale**: Pointing an author at `refresh` would be a dead end — it would not restore the
missing skills. The seeding effects (`init`) are the only path that re-materializes them, and
they are safe to re-run over an existing scaffold.

**Alternatives rejected**: Building a new remediation/auto-upgrade command — explicitly out of
scope (spec). Pointing at `refresh-agents` verbatim — factually wrong (would not re-seed).

---

## D9 — Three-projection surfacing (FR-007)

**Decision**: (a) The advisory rides the existing `Diagnostic` + `NextAction` machinery, so it
appears in JSON (diagnostics array + `nextAction`) and rich (Diagnostics table + next-action
callout) automatically; the text projection shows it via the existing `diagnostics: N` count +
`nextAction: <ActionId>` lines. (b) The new provenance fact is surfaced on the report by adding
`RequiredMinimumCliVersion: string option` to `ScaffoldSummary` and emitting it in the JSON
scaffold block (`CommandSerialization.fs:294-325`) and a `renderText` line
(`CommandRendering.fs:196-216`); rich then derives it for free from the text `key: value`
split (`Rendering.fs:92-94`).

**Rationale**: JSON is the contract; text/rich are pure projections that add/drop no facts
(`Rendering.fs:89-94`). This is exactly how the existing early-stage advisory
(`agents.earlyStageGuidance`) surfaces across all three (model in
`EarlyStageProjectionTests.fs`). Rich is presentation-only and excluded from golden contracts.

---

## D10 — Schema versioning & the contract change (FR-003)

**Decision**: `requiredMinimumCliVersion` is **additive-optional**; the
`scaffold-provenance` schema **stays v1**. `tryParse` defaults it to `None` for records
written before it (exactly the pattern already used for `effectiveParameters`,
`ScaffoldProvenance.fs:102-110`). Serialize it always-present as string-or-null, placed
immediately after the `generator` object so the two facts sit side by side (US1). Document
the field in `docs/release/schema-reference.md` (the declared-exception section) and add a
migration note under `docs/release/migrations/` following the feature-050 precedent
(`migrations/README.md:44-50`). Per `versioning-policy.md:66` an additive schema change is a
**minor** package bump; scaffold-provenance carries `ContractVersion = None`
(`Schemas.fs:172`) so **no** cross-repo handoff-contract coordination is triggered — only the
registry/package coherence checklist (`contracts-version-bump-checklist.md`) applies to the
minor bump.

**FS.GG.Contracts mirror**: `ScaffoldProvenanceSchema` (`Schemas.fs:83-90`) is a cross-repo
shape mirror that already omits `effectiveParameters` (pre-existing drift). Adding
`RequiredMinimumCliVersion` there is a coherence nicety, not a correctness requirement (no
code validates the serializer against it). Plan includes it as an **optional** coherence task
and records the pre-existing `effectiveParameters` gap as a known issue; it does not expand
scope to fully reconcile the mirror.

**Alternatives rejected**: A v2 schema bump — the change is purely additive; existing readers
that ignore unknown keys keep parsing (spec Edge "Pre-existing provenance consumers"). Omitting
the key when unset — always-present-null keeps the JSON shape stable and matches the
`effectiveParameters: []` precedent.

---

## D11 — MVU placement (Constitution V)

**Decision**: Compute CLI-coherence as a **pure** function
`cliCoherenceDiagnostics (descriptor) (request) -> Diagnostic list` (and the derived
`requiredMinimumCliVersion` value) inside the scaffold `update`, merged into
`model.Diagnostics` wherever the descriptor is resolved (dry-run and real paths) so the
advisory appears in every outcome; thread the resolved `requiredMinimumCliVersion` into
`provenanceWriteEffect`. No new `Effect` / edge I/O — both inputs (installed version via
`request.GeneratorVersion`, provider minimum via the already-parsed descriptor) are in hand.

**Rationale**: The comparison is pure; the only I/O (provenance write) is an existing
`WriteFile` effect. Keeps the MVU boundary clean (Principle V) and the logic unit-testable
without fixtures.

---

## Change tier & constitution posture

**Tier 1 (contracted change)**: touches the `scaffold-provenance` schema, the provider
registry/contract read, command diagnostic + `NextAction` output, and agent-facing docs/skill
guidance. Requires spec, plan, tasks, `.fsi` updates, `PublicSurface.baseline` refresh, tests,
docs, and a migration note. No constitution violations → no Complexity Tracking entries.
