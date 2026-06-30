# Phase 0 Research: Honor Provider-Declared Default Starter Selection

All Technical-Context unknowns are resolved below. No `NEEDS CLARIFICATION` remains.

## D1 — Where the selection mechanism already lives (FR-001 / FR-002)

- **Decision**: Reuse the existing `effectiveParameters` precedence function unchanged;
  this feature adds *recording* and *coverage*, not new selection machinery.
- **Rationale**: `effectiveParameters` (`src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:85-92`)
  builds the base map from each `descriptor.Parameters` entry's `Default` (`spec.Default |> Option.map …`),
  then folds `request.Parameters` over it with `Map.add` — so an author `--param key=value`
  overwrites the declared default (author wins). The effective map is forwarded verbatim as
  `--<key> <value>` pairs (`HandlersScaffold.fs:202-205`). `ProviderParameterSpec.Default`
  is parsed at `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs:189`
  (`Default = tryScalarAt ["default"] …`). FR-001/FR-002 are met in code.
- **Alternatives considered**: Elevating "profile/default-profile" to a first-class versioned
  contract concept — **rejected** (spec Assumptions): heavier than the board's effort sizing and
  unnecessary since the parameter-`default` mechanism already exists; it would also tempt
  provider-specific naming into generic SDD.

## D2 — The FR-003 gap and how to close it (effective-params recording)

- **Decision**: Add an additive **EffectiveParameters** field to both
  `ScaffoldProvenanceRecord` (durable produced artifact) and `ScaffoldSummary` (report),
  emitted in json/text/rich, ordered deterministically by key.
- **Rationale**: Today the effective map is only rendered into the *dry-run* planned-command
  string (`plannedCreateCommand`, `HandlersScaffold.fs:219-228`); a real success writes
  provenance (`provenanceWriteEffect`, `:232-242`) and the report summary
  (`terminalSummary` `:259-270`) **without** the param values. `ScaffoldProvenanceRecord`
  (`src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs:15-22`) and `ScaffoldSummary`
  (`src/FS.GG.SDD.Commands/CommandTypes.fsi:328-339`) carry no params field. Story 1 scenario
  3 changes the registry default over time, so provider/templateRef alone cannot reconstruct
  which starter a past run used — recording the *effective value* is the only reading under
  which FR-003's "the chosen starter is auditable" holds. **Confirmed with the spec author.**
- **Resolution of the spec's internal tension**: FR-008's parenthetical ("additive
  verification + documentation over an existing mechanism") and the Assumptions section
  understated the work; they are reconciled as: *exit codes, stream routing, and all existing
  fields are unchanged; exactly one additive field is introduced on the scaffold provenance
  record and report.* FR-008's byte-level guarantee is scoped accordingly in data-model.md.
- **Alternatives considered**:
  - *Provenance-only (leave report JSON byte-identical)* — preserves FR-008 literally but
    splits the audit surface; rejected in favor of a single coherent contract per the author's
    choice to honor FR-003 on both surfaces.
  - *Tests + docs only, no recording* — rejected: leaves "auditable" unsatisfiable after a
    default flip.

## D3 — Schema version / migration posture (Tier 1)

- **Decision**: **Additive within provenance schema v1.** `EffectiveParameters` is an optional
  object; `serialize` always writes it (possibly empty); `tryParse` defaults it to empty when
  absent so pre-existing provenance files still parse. `schemaVersion` stays `1`.
- **Rationale**: `SchemaVersion.isSupported version = version.Major = 1`
  (`src/FS.GG.SDD.Artifacts/SchemaVersion.fs`), `supportedRange = "1"`. An additive,
  backward-compatible field needs no major bump; existing readers that ignore unknown keys are
  unaffected, and our `tryParse` is tolerant of the field's absence. A migration note is still
  required (Tier 1) and recorded under `docs/release/migrations/`.
- **Alternatives considered**: Bump to schema v2 — rejected as backward-incompatible signaling
  for a purely additive optional field; reserved for a breaking change.

## D4 — Composition-acceptance default-starter path (FR-006 / FR-007)

- **Decision**: Drive the fixed real-provider scaffold with **no** explicit starter parameter
  through the existing network-gated harness, asserting the produced product builds and the
  verdict is `pass` (GREEN). Reference the registry's declared default *by behavior*, never by
  name.
- **Rationale**: The single seam is `scaffoldRequest` in
  `tests/FS.GG.SDD.Acceptance.Tests/AcceptanceSupport.fs:125-128` (today
  `Parameters = ["lifecycle","sdd"]`). Omitting a starter param there exercises whatever default
  the real `0.1.54-preview.1` Templates-owned registry declares. The verdict vocabulary is
  `pass | fail | skip-unavailable` (`CompositionResult.fs:28-31`), gated by
  `FSGG_SDD_ACCEPTANCE_REGISTRY` via `RequiresRegistryFactAttribute`
  (`AcceptanceSupport.fs:63-69`); unset ⇒ Skipped, no network (FR-007). The result document's
  hard-coded `inputs.params` (`CompositionResult.fs:169-174`) and the byte-exact golden
  (`CompositionAcceptanceTests.fs:407-441`) move in lockstep with any `scaffoldRequest` change.
- **Alternatives considered**: A new dedicated acceptance project — rejected; feature 041
  already wired the harness, lane, and gating. Reuse it.

## D5 — Boundary preservation (FR-004 / SC-003)

- **Decision**: Keep generic SDD source and generic-contract tests/fixtures free of `game`,
  `app`-as-starter, rendering package/template ids, paths, and docs URLs; add a repository-wide
  grep guard test asserting zero occurrences.
- **Rationale**: feature 030 FR-002/SC-005 boundary, re-asserted here. The new fixture
  registry uses a neutral parameter key and an abstract default value (e.g. `variant` with
  `default: alpha`) — purely illustrative, no rendering meaning. The real `game`/`app` flip is a
  data edit in the Templates-owned `providers/rendering.providers.yml` and is redirected
  cross-repo (FR-009), never landed in this repo.
- **Alternatives considered**: Asserting the boundary only by review — rejected; an automated
  grep guard is the durable, regression-proof contract (SC-003).

## D6 — Documentation surface (FR-005 / SC-005)

- **Decision**: Document parameter `default` semantics and `--param` override precedence
  value-agnostically in `docs/release/schema-reference.md` (the `.fsgg/providers.yml` and
  `.fsgg/scaffold-provenance.json` entries) and in `docs/reference/authoring-contracts.md`
  (how a provider author declares and changes a default starter with no SDD code change).
- **Rationale**: Today only the spec contract
  `specs/038-retype-provider-contracts/contracts/provider-registry-encoding.md` documents the
  `parameters[].default` encoding; no *published* doc explains the default-apply +
  `--param`-wins precedence or the provenance recording. SC-005 requires a provider author to
  determine this from docs alone. Both Claude and Codex agent surfaces consume these docs, so
  one edit keeps the workflows aligned (Constitution VII).
- **Alternatives considered**: Documenting in the seeded `.fsgg/early-stage-guidance.md` —
  rejected; that file covers pre-work-model lifecycle stages, not the provider/scaffold contract.

## D7 — Cross-repo redirect of the literal flip (FR-009)

- **Decision**: Post a cross-repo response on FS-GG/FS.GG.SDD#44 stating SDD's in-boundary
  half is delivered and the literal `app → game` default is a data edit FS.GG.Templates must
  land in `providers/rendering.providers.yml` for the `fs-gg-ui-template` contract at
  `0.1.54-preview.1`; track via the `cross-repo-coordination` protocol.
- **Rationale**: `game`/`app` cannot enter generic SDD (FR-004). The observable production flip
  depends on Templates, not on SDD code; it is not a blocker for SDD's mechanism-lock and
  documentation deliverables (spec Assumptions).
- **Alternatives considered**: Editing the rendering registry from this repo — rejected;
  violates the ownership boundary and FR-004.

## Open risks

- None blocking. The composition-acceptance default-starter assertion only goes GREEN against
  the real provider once Templates lands `default` for the starter param; until then FR-007
  keeps it Skipped offline and FR-006 is exercised by-reference when the gating env + registry
  are present. SDD's deliverables (D2/D3/D5/D6) do not depend on that landing.
