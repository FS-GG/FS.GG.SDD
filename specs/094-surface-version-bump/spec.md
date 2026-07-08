# Feature Specification: Prompt the Coherent-Set Version Bump on a Classified Shipped-Surface Mutation

**Feature Branch**: `094-surface-version-bump`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "Given the additive/breaking classification from `fsgg-sdd surface` (feature 087 / FS.GG.SDD#170), prompt the coherent-set version bump on the producer repo's version axis: additive → minor, breaking → major. Detect-and-remediate, not silent auto-update (ADR-0009). Publish-before-flip (FR-007) still holds. Implements FS.GG.SDD#171, the *version* slice of the shipped-surface-mutation governed event (FS-GG/.github ADR-0025, epic #235, issue #236)."

## Overview

FS-GG/.github **ADR-0025** makes a mutation of an already-shipped public surface a *first-class
governed event* with a four-step pipeline: **detect** → **classify** → **reconcile** → **flag
consumers**. Steps 1 and 2 have shipped in this repo:

- Feature **086** (`fsgg-sdd surface --check/--update`) detects drift — an authored `src/**/*.fsi`
  whose committed baseline under `docs/api-surface/` is missing or differs byte-for-byte.
- Feature **087** classifies that drift — `SurfaceClassification.Verdict` is `breaking` (a member
  removed, renamed, or re-signatured), `additive` (members only added), `cosmetic` (equal member
  sets), or `none` (nothing drifted). It already carries a `RecommendedBump` of
  `major`/`minor`/`none`.

This feature delivers **reconcile step 3a — the version obligation**: when `surface` observes a
classified mutation, it tells the operator what the classification implies for the repository's
**coherent-set version axis**, and what that axis currently reads. The two other reconcile
obligations are owned elsewhere by ADR-0025 and are explicitly out of scope: the
registry/projection/ADR reconcile (3b) and the consumer-impact flag (3c) both belong to the
`.github` slice of #236.

Everything the classification needs already exists. What is missing is (a) reading the version
axis, (b) computing the version the classification implies, and (c) saying so, loudly, without
touching anything.

**Detect-and-remediate, not silent auto-update.** Per ADR-0009 this feature makes **zero writes**
to the version axis under either mode. It reports; the operator confirms and edits. Publish-before-flip
(feature 018 FR-007) is untouched: the package still leads the registry pin.

**No provider literal.** ADR-0025 names concrete axes (`$(FsGgAudioVersion)`, `$(FsGgGameVersion)`)
because it is an org-level document. Generic SDD may embed none of them (constitution, Engineering
Constraints: *"No repo-specific knowledge of FS.GG.Rendering package IDs, templates, or docs URLs
belongs in generic SDD code"*; feature 086 FR-002/FR-014 hold the same line for `surface`'s roots).
The axis is therefore **declared by the workspace** through the `--param` mechanism `surface`
already uses for `sourceRoot`/`baselineRoot`, with a convention default and an honest
`undeterminable` degradation when the default does not resolve.

**Change tier: Tier 1** (command output-contract change: additive fields on the `surface`
`CommandReport` block, one new advisory diagnostic id, two new `--param` keys). No persisted schema
version changes; no lifecycle artifact is written.

## Clarifications

### Session 2026-07-08

- **Q (AMB-001): How does generic SDD locate the version axis without embedding a provider-specific
  property name?** → A: Two new `--param` keys, resolved by the existing `Foundation.surfaceParam`
  helper: `versionAxisFile` (default `Directory.Build.props`) and `versionAxisProperty` (default
  `Version`). The file is read as XML and the **first** `<{versionAxisProperty}>` element's text is
  taken verbatim. MSBuild is **not** evaluated: no import chasing, no property functions, no
  `$(…)` expansion. A consumer whose axis is `$(FsGgAudioVersion)` in `Directory.Build.props` passes
  `--param versionAxisProperty=FsGgAudioVersion`; this repo, whose `<Version>` lives in
  `Directory.Build.local.props`, passes `--param versionAxisFile=Directory.Build.local.props`. The
  defaults are a convention, not a claim about any repo.

- **Q (AMB-002): Does the prompt fire under `--update`, or only `--check`?** → A: **Both.**
  `--update` is precisely the run that *erases* the evidence — it rewrites the baselines so the very
  next `--check` classifies `none`. If the prompt fired only on `--check`, an operator following the
  normal PR workflow (`surface --update`, commit, push) would never see it, and the governed event
  would be silently consumed. `HandlersSurface.computeSummary` already classifies the drift *before*
  planning the baseline writes, so the classification observed at the start of an `--update` run is
  exactly the one to prompt on. Under both modes the prompt describes the drift as observed at run
  start.

- **Q (AMB-003): Is the prompt blocking? Does it change the exit code?** → A: **No, and no.** It is a
  `DiagnosticWarning` with id `surface.versionBumpRequired`. This preserves feature 087's explicit
  contract — *"Advisory — never changes the exit code"* — and matches ADR-0025's *"Classification is
  advisory-but-loud; the operator confirms."* Under `--check` a drifted tree already exits 1 via the
  pre-existing `surface.drift` error, so the prompt rides along on a run that already fails. Under
  `--update` the run exits 0: it is doing the right thing, and failing it would punish the correct
  workflow. Making the prompt an error would also be *dishonest*, per AMB-004.

- **Q (AMB-004): Can `surface` prove the bump has not already been applied in this change?** → A:
  **No — and it must not pretend to.** `surface` sees only the working tree: the current axis value
  and the classification. The previously *published* version lives in the package feed and the
  `.github` registry pin, neither of which SDD reads (that is ADR-0025's reconcile step 3b, owned by
  `.github`). An operator who has already bumped `0.8.0 → 1.0.0` for a breaking change presents an
  axis reading `1.0.0` and a breaking classification — indistinguishable, from the working tree
  alone, from one who has not bumped at all. The report therefore states **facts and an
  implication**, never an accusation: it emits `currentVersion`, `requiredBump`, and
  `suggestedVersion` (= `currentVersion` with `requiredBump` applied), and the diagnostic is worded
  as a prompt to confirm — *"…unless the bump is already applied in this change"*. A
  stateful alternative (recording the axis value into a `docs/api-surface/` sidecar at `--update`
  time, so a later `--check` could *prove* the omission) was considered and **rejected**: it invents
  a new committed artifact and schema that ADR-0025 does not call for, and it duplicates the
  registry's job as the authoritative record of the published version.

- **Q (AMB-005): Should `SurfaceClassify.bumpFor` be unified with the existing
  `ReleaseContract.bumpRule`?** → A: **No.** They look like duplicates and are not. `bumpRule` maps
  `ChangeClass.Clarifying → "patch"`; `bumpFor` maps `cosmetic → "none"`. The domains differ: a
  *clarifying contract change* warrants a patch release, whereas a *cosmetic `.fsi` edit* (a comment,
  a blank line, a reordering — by construction no member-token delta) warrants no release at all.
  Unifying them would either force a comment-only signature edit to demand a patch bump, or corrupt
  the release contract's `Clarifying` rule. The two stay separate; each gains a comment naming the
  other and the reason they differ. This decision removes `ReleaseContract.fs`/`.fsi` from the
  touch-set entirely.

- **Q (AMB-006): What happens when the axis cannot be read?** → A: It degrades **explicitly**
  (constitution, Principle VIII: *"optional integrations degrade explicitly"*), mirroring
  `Fsgg.Version.compare`, which returns `None` rather than assert a false ordering. Three distinct
  `versionAxisState` values are reported — `resolved`, `undeterminable` (the file is absent, or the
  property element is not present in it), and `unparseable` (the property element exists but its text
  is not a `major.minor.patch` triple per `Fsgg.Version.tryParse`). In the latter two, `currentVersion`
  and `suggestedVersion` are absent, the `requiredBump` is still reported (it depends only on the
  classification), and the diagnostic says what could not be resolved and which `--param` would fix
  it. The run never fails *because of* the axis.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The operator is told what the mutation costs (Priority: P1)

An FS.GG.Audio maintainer additively changes an already-published `FS.GG.Audio.Core` signature and
runs `fsgg-sdd surface --check`. Today they are told only that the baseline drifted and that the
change is `additive`. They must then remember, unaided, that an additive mutation of a shipped
surface obliges a **minor** bump on `$(FsGgAudioVersion)`, and go find what that property currently
reads. This story closes that gap: the same run reports the axis, its current value, the required
bump, and the version the bump implies.

**Why this priority**: This *is* the feature. It is the reconcile obligation ADR-0025 assigns to the
publishing layer, and the one whose absence #236 recorded as "the only record that the change was
contract-relevant was prose the operator wrote by hand."

**Independent Test**: Run `surface --check` against a fixture workspace with one drifted `.fsi` that
only adds a member and a `Directory.Build.props` declaring `<Version>0.8.0</Version>`. Assert the
report carries `requiredBump: minor`, `currentVersion: 0.8.0`, `suggestedVersion: 0.9.0`, and one
`surface.versionBumpRequired` warning. Delivers the whole value of the feature on its own.

**Acceptance Scenarios**:

1. **Given** a drifted `.fsi` classified `additive` and an axis reading `0.8.0`, **When** `surface
   --check` runs, **Then** the report carries `requiredBump: minor` and `suggestedVersion: 0.9.0`,
   and a `surface.versionBumpRequired` warning names both.
2. **Given** a drifted `.fsi` classified `breaking` and an axis reading `0.8.0`, **When** `surface
   --check` runs, **Then** the report carries `requiredBump: major` and `suggestedVersion: 1.0.0`.
3. **Given** a drifted `.fsi` classified `cosmetic`, **When** `surface --check` runs, **Then**
   `requiredBump` is `none`, `suggestedVersion` equals `currentVersion`, and **no**
   `surface.versionBumpRequired` warning is emitted.
4. **Given** a coherent tree (classification `none`), **When** `surface --check` runs, **Then**
   `requiredBump` is `none` and no warning is emitted.
5. **Given** any of the above, **When** the run completes, **Then** the exit code is exactly what
   feature 086 produced for that tree — the prompt never changes it.

---

### User Story 2 - `--update` does not silently consume the event (Priority: P1)

The maintainer's actual workflow is `surface --update`, commit, push. That run rewrites the baselines,
so the *next* `--check` sees a coherent tree and classifies `none`. If the prompt fired only on
`--check`, the governed event would be destroyed by the very command the workflow tells the operator
to run.

**Why this priority**: Co-P1 with US1. A prompt the normal workflow never sees is not a prompt.
Without this the feature is a no-op in practice.

**Independent Test**: Run `surface --update` against the US1 fixture. Assert the baselines are
rewritten (unchanged feature-086 behavior), the exit code is 0, **and** the same
`surface.versionBumpRequired` warning and `requiredBump`/`suggestedVersion` fields are present,
derived from the drift as observed at run start.

**Acceptance Scenarios**:

1. **Given** a drifted `.fsi` classified `breaking` and an axis reading `0.8.0`, **When** `surface
   --update` runs, **Then** the baselines are refreshed, the exit code is 0, and the report still
   carries `requiredBump: major` / `suggestedVersion: 1.0.0` with the warning.
2. **Given** the same workspace, **When** `surface --update` is run a second time, **Then** the tree
   is now coherent, the classification is `none`, `requiredBump` is `none`, and no warning is emitted.
3. **Given** an `--update` run, **When** the report is produced, **Then** no write effect targets the
   `versionAxisFile`.

---

### User Story 3 - An unresolvable axis degrades honestly (Priority: P2)

A workspace whose version axis is not at the convention default — or which has no MSBuild version
property at all — must still get the classification and the bump implication, and must be told
precisely what could not be resolved rather than silently receiving a wrong `suggestedVersion` or a
failed run.

**Why this priority**: P2 — the feature is useful without it, but Principle VIII makes explicit
degradation mandatory rather than optional, and this repo's own layout (`<Version>` in
`Directory.Build.local.props`) hits the case immediately.

**Independent Test**: Run `surface --check` against a fixture with a drifted `.fsi` and (a) no
`Directory.Build.props`, (b) a `Directory.Build.props` with no `<Version>` element, and (c) one whose
`<Version>` reads `not-a-version`. Assert `versionAxisState` is `undeterminable`/`undeterminable`/
`unparseable`, `requiredBump` is still reported, `currentVersion`/`suggestedVersion` are JSON `null`,
and the exit code is unchanged in every case.

**Acceptance Scenarios**:

1. **Given** the `versionAxisFile` does not exist, **When** `surface` runs, **Then** `versionAxisState`
   is `undeterminable`, `currentVersion` and `suggestedVersion` are `null` in `--json` and `(none)` in
   `--text`, `requiredBump` is present, and the diagnostic names the `--param versionAxisFile` override.
2. **Given** the file exists but declares no `<Version>` element, **When** `surface` runs, **Then**
   `versionAxisState` is `undeterminable` and the diagnostic names the `--param versionAxisProperty`
   override.
3. **Given** `<Version>not-a-version</Version>`, **When** `surface` runs, **Then** `versionAxisState`
   is `unparseable` and `currentVersion` is `null` — the unparseable text is **not** echoed as if it
   were a version.
4. **Given** any degraded state, **When** the run completes, **Then** the exit code is exactly what
   feature 086 produced for that tree.

---

### User Story 4 - A consumer declares a non-default axis (Priority: P2)

FS.GG.Audio's axis is `$(FsGgAudioVersion)`; FS.GG.Game's is `$(FsGgGameVersion)`. Each declares its
own, and generic SDD learns neither.

**Why this priority**: P2 — this is what makes US1 usable by the repos ADR-0025 actually targets,
but US1 is demonstrable on the convention default alone.

**Independent Test**: Run `surface --check --param versionAxisProperty=FsGgAudioVersion` against a
fixture whose `Directory.Build.props` declares `<FsGgAudioVersion>2.3.1</FsGgAudioVersion>` and a
breaking drift. Assert `currentVersion: 2.3.1`, `suggestedVersion: 3.0.0`. Grep the SDD source tree
for `FsGgAudioVersion` and assert it appears in **no** file under `src/`.

**Acceptance Scenarios**:

1. **Given** `--param versionAxisProperty=FsGgAudioVersion` and an axis reading `2.3.1` with breaking
   drift, **When** `surface` runs, **Then** `currentVersion` is `2.3.1` and `suggestedVersion` is
   `3.0.0`.
2. **Given** `--param versionAxisFile=Directory.Build.local.props`, **When** `surface` runs in this
   repo, **Then** the axis resolves to the `<Version>` declared there.
3. **Given** the SDD source tree, **When** it is searched for any concrete consumer axis name, **Then**
   no `src/**` file contains one.

---

### Edge Cases

- **The axis element appears more than once** (e.g. in two `<PropertyGroup>`s, MSBuild last-write-wins).
  SDD does not evaluate MSBuild, so it takes the **first** occurrence in document order and reports
  `versionAxisState: resolved`. Deterministic and documented; an operator with a conditional axis
  should point `--param versionAxisFile` at the file that declares it unconditionally.
- **The `versionAxisFile` is not well-formed XML.** Treated as `undeterminable`, not a tool defect:
  a malformed props file is the workspace's problem, and `surface` must not fail a surface check over it.
- **The axis text has leading/trailing whitespace, or an XML comment inside the element.** The element's
  text is trimmed before `Fsgg.Version.tryParse`. A comment child does **not** break it:
  `XElement.Value` concatenates only text nodes and ignores comments, so
  `<Version>0.8.0<!-- pinned --></Version>` resolves to `0.8.0`. A *child element*, by contrast, has its
  text concatenated in, which will generally yield `unparseable`.
- **A pre-release/build-metadata version (`1.2.3-beta`, `1.2.3+sha`).** `Fsgg.Version.tryParse` accepts
  only a `major.minor.patch` triple, so these are `unparseable`. The feature does not widen the shared
  version grammar; widening it is a separate, cross-repo change to `FS.GG.Contracts` (ApiCompat-gated).
- **A `missing-baseline` file.** Per ADR-0025 step 1, a surface with no committed baseline is a *new*
  surface, not a mutation. Feature 087 already excludes it from classification, so it contributes no
  `requiredBump`. Unchanged here.
- **`versionAxisFile` pointing outside the workspace root** — either `../other/Directory.Build.props`
  or an absolute `/etc/passwd`. Both rejected as `undeterminable` by a **new** containment check
  (FR-017). No such guard exists today, and the existing roots escape *asymmetrically* (measured, R7):
  a `..` segment escapes reads, enumerates **and** writes, while an absolute path escapes reads and
  enumerates but not writes (`baselinePathFor` normalizes the write target, which strips the leading
  `/`). This feature adds the guard for the param it introduces and does **not** retrofit the two
  existing roots — that is a separate, behavior-changing fix with its own blast radius (see *Deferred*,
  tracked as FS.GG.SDD#185).
- **The classification is `breaking` solely via the `UnparseableFallback` path** (a non-empty `.fsi`
  yielding no member tokens, feature 087 FR-011). The prompt fires with `requiredBump: major`, which is
  the conservative and correct behavior — the operator is already being asked to inspect that file.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `surface` MUST resolve a **version axis** from two `--param` keys — `versionAxisFile`
  (convention default `Directory.Build.props`) and `versionAxisProperty` (convention default
  `Version`) — using the existing `Foundation.surfaceParam` helper.
- **FR-002**: `surface` MUST read the axis by taking the text of the **first** element named
  `{versionAxisProperty}` in `{versionAxisFile}`, in document order, trimmed. It MUST NOT evaluate
  MSBuild: no import chasing, no `$(…)` expansion, no property functions, no conditions.
- **FR-003**: Generic SDD MUST embed no concrete consumer axis name, package id, or repository
  identity. `FsGgAudioVersion`, `FsGgGameVersion`, and every sibling MUST be absent from `src/**`.
- **FR-004**: `surface` MUST derive `requiredBump` from the feature-087 run verdict alone:
  `breaking → major`, `additive → minor`, `cosmetic → none`, `none → none`. This reuses the existing
  `SurfaceClassification.RecommendedBump`; it introduces no second mapping.
- **FR-005**: `surface` MUST compute `suggestedVersion` as `currentVersion` with `requiredBump`
  applied — `major` ⇒ `(M+1).0.0`, `minor` ⇒ `M.(m+1).0`, `none` ⇒ unchanged — and MUST report it as
  *unresolved* whenever `currentVersion` is unresolved.
- **FR-006**: `surface` MUST report `versionAxisState` as exactly one of `resolved`, `undeterminable`
  (file absent, unreadable, not well-formed, outside the root, or property element not present), or
  `unparseable` (property element present, text not a `major.minor.patch` triple).
- **FR-007**: `surface` MUST report `currentVersion` and `suggestedVersion` as unresolved when
  `versionAxisState` is not `resolved`, and MUST still report `requiredBump` (which depends only on
  the classification). *Unresolved* follows each projection's established convention for an optional
  scalar — JSON writes an explicit `null` under a **stable key set** (as `requiredMinimumCliVersion`
  does; the `surface` block's own comment requires "a stable shape" for the automation contract), and
  `--text` always emits the line, rendering the value `(none)` via `defaultArg`. Neither projection
  omits the key. (Feature 091's key-omission rule governs the authored `evidence.yml`, a different
  artifact, and does not apply to the `CommandReport`.)
- **FR-008**: `surface` MUST emit a `surface.versionBumpRequired` **`DiagnosticWarning`** exactly when
  `requiredBump` is `major` or `minor`. It MUST NOT emit one when `requiredBump` is `none`.
- **FR-009**: The `surface.versionBumpRequired` diagnostic message MUST name the classification
  verdict, the resolved axis (`{versionAxisFile}:{versionAxisProperty}`), the required bump, and —
  when resolved — the current and suggested versions. It MUST be worded as a prompt the operator
  confirms, explicitly allowing that the bump may already be applied in the change under review.
- **FR-010**: When `versionAxisState` is not `resolved`, the diagnostic MUST additionally name the
  `--param` overrides that would resolve the axis. It names **both** (`versionAxisFile` and
  `versionAxisProperty`): an absent file and an absent property both resolve to `undeterminable`, so the
  diagnostic cannot tell them apart and offers both rather than guessing.
- **FR-011**: `surface` MUST emit the prompt under **both** `--check` and `--update`, derived from the
  drift as classified at the start of the run (before any baseline write is planned).
- **FR-012**: `surface` MUST make **zero** writes to the `versionAxisFile` under either mode. No
  **mutating** effect (`WriteFile`/`CreateDirectory`/`SetExecutable`) whose target is the version axis
  may be planned. The axis is *read* — that `ReadFile` is the whole mechanism — but never written.
  (ADR-0009: detect-and-remediate, never silent auto-update.)
- **FR-013**: The prompt MUST NOT change `surface`'s exit code, in any state, relative to feature 086 +
  087 behavior for the same tree.
- **FR-014**: The new fields MUST appear in all three `CommandReport` projections — `--json` (the
  automation contract), `--text`, and `--rich` — and MUST be byte-deterministic across runs.
- **FR-015**: `SurfaceClassify.bumpFor` and `ReleaseContract.bumpRule` MUST remain distinct functions.
  Each MUST carry a comment naming the other and stating why they differ (`cosmetic → none` vs
  `Clarifying → patch`). Neither may be re-expressed in terms of the other.
- **FR-016**: `fsgg-sdd surface --help` MUST document the two new `--param` keys and their defaults.
- **FR-017**: A `versionAxisFile` that escapes the workspace root MUST resolve to
  `versionAxisState: undeterminable`, and MUST NOT be read (no `ReadFile` may be planned for it). It
  escapes when the **raw** param is absolute (`Path.IsPathRooted`) or contains a `..` segment. The test
  MUST be applied to the raw param, **not** to `normalizeRelativePath`'s output: normalization ends in
  `.TrimStart('/')`, so a normalize-then-test guard silently admits `/etc/passwd`. The effect carries
  the raw string, and `Path.Combine(root, "/etc/passwd")` returns `/etc/passwd`. The handler MUST also
  verify the resolved path is under the root before trusting the snapshot. No such containment guard
  exists today for `sourceRoot`/`baselineRoot`; this requirement introduces one for `versionAxisFile`
  only.

### Key Entities

- **VersionAxis**: the workspace's declared coherent-set version property. Attributes: `File` (the
  path read), `Property` (the element name), `State` (`resolved` | `undeterminable` | `unparseable`),
  `CurrentVersion` (present only when `resolved`).
- **VersionBumpPrompt**: the reconcile obligation implied by a classified mutation. Attributes:
  `RequiredBump` (`major` | `minor` | `none`), `SuggestedVersion` (present only when the axis is
  `resolved`), and the `VersionAxis` it was computed against. Always present on the `surface` report;
  inert (`none`, no warning) when nothing drifted.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An operator who mutates a shipped surface learns the required version bump and the
  current axis value **from the same command invocation that detects the mutation** — zero additional
  commands, zero recall of the ADR-0025 mapping.
- **SC-002**: The prompt is observed by the normal PR workflow: `surface --update` emits it on the run
  that rewrites the baselines, so no governed event is consumed silently.
- **SC-003**: `grep -rE 'FsGg[A-Za-z]+Version' src/` returns **no** match, and the acceptance for a
  non-default axis passes purely through `--param`. Generic SDD stays value-agnostic (constitution;
  086 FR-002/FR-014).
- **SC-004**: Every `surface` exit code is unchanged from feature 086 + 087 for every tree state — the
  full existing `SurfaceCommandTests` suite passes untouched except for additive assertions.
- **SC-005**: `surface` performs zero writes to any version axis in every test scenario, verified by
  asserting on the planned effect set, not merely on file mtimes.
- **SC-006**: All three projections render the new fields deterministically: two runs over the same
  fixture produce byte-identical `--json` and `--text` output.
- **SC-007**: A workspace with no resolvable axis still receives `requiredBump` and a diagnostic naming
  the override that would fix it — the feature never dead-ends.

## Assumptions

- The **coherent-set version axis is a single MSBuild property in a single file**, per repo. ADR-0025's
  examples (`$(FsGgAudioVersion)`, `$(FsGgGameVersion)`) are all of this shape, as is this repo's
  `<Version>`. A repo with a multi-file or computed axis points `--param versionAxisFile` at the file
  that declares it literally, or accepts `undeterminable`.
- **`Fsgg.Version`'s `major.minor.patch` grammar is sufficient.** Pre-release and build-metadata
  suffixes are out of scope and classify as `unparseable`. Widening the shared grammar is a separate
  cross-repo change to `FS.GG.Contracts` and is ApiCompat-gated.
- **The previously published version is not available to `surface`.** It lives in the package feed and
  the `.github` registry pin — ADR-0025's reconcile step 3b. This feature therefore cannot and does not
  detect an *already-applied* bump (AMB-004).
- **Feature 087's classification is correct and stable.** This feature consumes
  `SurfaceClassification.Verdict` / `.RecommendedBump` and re-derives nothing.
- **`--param` is already parsed generically** by `FS.GG.SDD.Cli/Program.fs`, so no CLI flag-parsing
  change is required (only `--help` text).
- **XML reading is idiomatic enough** to need no constitutional justification under Principle IV: a
  single `XDocument`/`XElement` descendant lookup, no reflection, no type providers, no MSBuild API.

## Out of Scope

- ADR-0025 reconcile **3b** (registry `version` + `via:` pins, `registry/CHANGELOG.md`, the
  `docs/registry/compatibility.md` projection, the ADR) — owned by the `.github` slice of #236.
- ADR-0025 reconcile **3c** (consumer-impact enumeration and board flagging via
  `scripts/fsgg-surface-impact`) — owned by the `.github` slice of #236.
- **Writing** the version bump. This feature prompts; `upgrade` is the only command permitted to mutate
  consumer artifacts for remediation, and extending it to the version axis is not proposed here.
- Detecting an **already-applied** bump (AMB-004), and the `docs/api-surface/` version sidecar that
  would enable it (considered and rejected).
- Any change to the shared `Fsgg.Version` grammar, to `ReleaseContract`, or to the `release-readiness`
  catalog. No new top-level `CommandReport` block is introduced — the new fields nest inside the
  existing `surface` block.

## Deferred

- **Root-containment for `sourceRoot` / `baselineRoot`.** FR-017 guards only the param this feature
  introduces. The two feature-086 roots escape the workspace root today, asymmetrically (R7): a `..`
  segment escapes reads, enumerates **and** `--update` writes; an absolute path escapes reads and
  enumerates (the effect carries the raw param straight into `Path.Combine`) but not writes (which go
  through `baselinePathFor`'s normalization). Retrofitting the guard is behavior-changing for any
  workspace relying on an out-of-root baseline and belongs in its own item — **filed as
  FS.GG.SDD#185**, which lifts and reuses this feature's `escapesRoot` predicate rather than inventing
  a parallel one.
