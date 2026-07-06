# Feature Specification: Dev-Repo Provenance

**Feature Branch**: `085-dev-repo-provenance`

**Created**: 2026-07-06

**Status**: Draft

**Input**: User description: "A repo that develops a framework itself — not a product scaffolded from a template — is initialized with `fsgg-sdd init`, which seeds the `.fsgg/` lifecycle skeleton but writes **no** `.fsgg/scaffold-provenance.json`. Because provenance is exclusively a `scaffold` artifact built from a resolved provider descriptor, such a hand-`init`'d 'dev-repo' has no anchor: `doctor`/`upgrade` hit the `HasProvenance = false` branch and report 'no scaffold provenance — nothing to reconcile', so they can never reconcile the seeded skeleton or the CLI axis. Define a provider-less 'dev-repo' provenance shape that `init` writes, so the whole reconciliation model engages on a dev-repo. It must not require a provider or template pin, must stay schema v1 (additive), must not leak into `scaffold` (which writes its own provider provenance) or into `upgrade`'s no-clobber re-seed, and must keep `init` byte-deterministic. Forcing workload: FS.GG.Game (ADR-0022, P3) is `init`'d this way and dogfoods the shape."

## Clarifications

### Session 2026-07-06

- Q: How is a dev-repo distinguished from a scaffolded product in the provenance document? → A: By a dedicated `outcome` value, `devRepoInit`, with empty provider/template fields — expressible without any structural change, so the record stays schema v1. `ScaffoldProvenance.isDevRepo` is the single predicate; readers key off it.
- Q: What does the dev-repo record's `producedPaths` contain? → A: Exactly the coherent seeded skeleton (`Drift.expectedArtifactPaths` — the 16 `fs-gg-sdd-*` process skills × three roots, `.fsgg/early-stage-guidance.md`, `.gitignore`), owner `sdd`, no digest. The document is a truthful self-describing manifest of what `init` produced; it stays the one canonical set `doctor`/`upgrade` already reconcile against.
- Q: Should `init` writing provenance change what `scaffold` or `upgrade` write? → A: No. The write is bound to the `init` command only, not to the shared `initEffects` skeleton seeder that `scaffold` (skeleton seed) and `upgrade` (no-clobber re-seed) reuse. Scaffold still writes its own provider provenance; upgrade re-seeds only the missing seeded set. The provenance file is the reconciliation *anchor*, so it is deliberately excluded from `Drift.expectedArtifactPaths`.
- Q: What CLI-axis behavior does a dev-repo get? → A: `coherentByAbsence` — a dev-repo declares no provider minimum, so nothing forces an upgrade. The recorded `generator` still audits the CLI version that init'd the repo.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - `doctor`/`upgrade` engage on a dev-repo (Priority: P1)

A maintainer developing an FS-GG framework component `init`s the repo with `fsgg-sdd` and later runs `doctor` (or `upgrade`). Instead of the dead-end "no scaffold provenance — nothing to reconcile", the commands recognize the repo as a provider-less dev-repo: they report it as tracked (has provenance), reconcile the seeded `fs-gg-sdd-*` skeleton (re-seeding any missing copy), and report the CLI axis as coherent-by-absence — all without naming a provider or a template.

**Why this priority**: This is the whole feature — closing the provider-less provenance gap so a dev-repo is a first-class, reconcilable lifecycle state rather than an untracked hole.

**Independent Test**: `init` a clean repo, delete one seeded skill copy, run `doctor`: it reports `HasProvenance = true`, no provider, `coherentByAbsence` CLI axis, and a `WouldApply` artifact re-seed naming the missing copy. `upgrade --yes` re-seeds it; a subsequent `doctor` reports coherent.

**Acceptance Scenarios**:

1. **Given** a freshly `init`'d repo with the full seeded skeleton present, **When** the maintainer runs `doctor`, **Then** the summary has `HasProvenance = true`, no provider name, CLI axis `coherentByAbsence`, a `NoTarget` template re-pin, and `IsCoherent = true` (exit 0).
2. **Given** a freshly `init`'d repo with one seeded skill copy removed, **When** the maintainer runs `doctor`, **Then** the summary is not coherent and previews a `WouldApply` artifact re-seed listing exactly the missing copy — still with no provider named.
3. **Given** the same repo, **When** the maintainer runs `upgrade --yes`, **Then** the missing copy is re-seeded (no-clobber) and a subsequent `doctor` reports coherent, with the template re-pin remaining `NoTarget`.

---

### User Story 2 - `init` writes a deterministic dev-repo anchor without disturbing `scaffold` (Priority: P1)

`init` writes `.fsgg/scaffold-provenance.json` as a provider-less dev-repo document — byte-identical across runs at a fixed CLI version — and this must not change what `scaffold` writes (its own provider provenance) or what `upgrade` re-seeds.

**Why this priority**: The anchor is only trustworthy if it is deterministic and if it does not corrupt the two commands that reuse the skeleton-seeding seam. A dev-repo document leaking into a scaffolded product's provenance is a defect.

**Independent Test**: Run `init` into two clean roots and confirm the two provenance files are byte-identical. Run `scaffold` and confirm its provenance is the provider document (`providerSucceeded`, provider/template populated), not a dev-repo document, and that the seeded skeleton it writes is byte-identical to init's apart from the provenance anchor.

**Acceptance Scenarios**:

1. **Given** two clean roots, **When** `init` runs into each, **Then** `.fsgg/scaffold-provenance.json` is byte-identical between them (no clock, no absolute path, sorted `producedPaths`).
2. **Given** a scaffolded product, **When** its provenance is read, **Then** it is a provider document (`isDevRepo = false`, provider/template populated), unaffected by feature 085.
3. **Given** an `upgrade` re-seed of a scaffolded or dev repo, **When** it re-materializes missing artifacts, **Then** it writes only the missing seeded set and never a dev-repo provenance document as a side effect.

## Requirements *(mandatory)*

- **FR-001**: `fsgg-sdd init` MUST write `.fsgg/scaffold-provenance.json` as a provider-less dev-repo document: `outcome = devRepoInit`, empty `providerName`/`providerContractVersion`/`templateRef`, `requiredMinimumCliVersion = null`, `generator` = the producing CLI version.
- **FR-002**: The dev-repo document's `producedPaths` MUST be exactly the coherent seeded skeleton (`Drift.expectedArtifactPaths`), owner `sdd`, no digest.
- **FR-003**: The document MUST stay schema v1 (additive) — existing readers parse it and unknown-key/absent-field back-compat is preserved.
- **FR-004**: `ScaffoldProvenance.isDevRepo` MUST be the single predicate distinguishing a dev-repo document from a provider document.
- **FR-005**: `doctor`/`upgrade` MUST engage on a dev-repo document (`HasProvenance = true`): reconcile the seeded artifact axis, report the CLI axis as `coherentByAbsence` (absent a provider minimum), and preview the template re-pin as `NoTarget`; the reported provider MUST be `None` (never an empty string).
- **FR-006**: The provenance write MUST be bound to the `init` command only — NOT to the shared `initEffects` seam reused by `scaffold` and `upgrade`. `scaffold` MUST still write its own provider provenance; `upgrade` MUST re-seed only the missing seeded set.
- **FR-007**: `init` MUST stay byte-deterministic — the dev-repo document is byte-identical across runs at a fixed CLI version.
- **FR-008 (versioning)**: Additive-optional ⇒ schema stays v1; **minor** package bump (`versioning-policy.md`). No cross-repo handoff `contractVersion` (scaffold-provenance has `ContractVersion = None`), so only the package/registry coherence checklist applies.

### Key Entities

- **Dev-repo provenance document** — a `ScaffoldProvenanceRecord` with `Outcome = devRepoOutcome ("devRepoInit")`, empty provider/template, `producedPaths` = the seeded skeleton (owner `sdd`). The reconciliation anchor for a provider-less repo.

## Success Criteria *(mandatory)*

- **SC-001**: A hand-`init`'d repo is reconcilable — `doctor`/`upgrade` engage and reconcile the seeded skeleton instead of reporting "nothing to reconcile".
- **SC-002**: The public surface adds only `devRepoOutcome`, `isDevRepo`, `devRepoRecord`; schema stays v1; no existing provenance consumer breaks.
- **SC-003**: `scaffold` and `upgrade` behavior is unchanged (their suites stay green); the only init-vs-scaffold skeleton comparisons updated are those that must exclude the intentionally-different provenance anchor.
- **SC-004**: `init` provenance is byte-identical across runs.

## Out of Scope

- A `provider: none` descriptor or any provider-registry change — a dev-repo has no provider at all, not a null one.
- `refresh` semantics changes — the dev-repo record's `producedPaths` are excluded from the provider product-skill union (namespace filter) and require no refresh rework.
- Consumer scaffolding / the `game` scaffold provider (ADR-0022 P6).
