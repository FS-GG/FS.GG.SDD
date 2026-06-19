# FS.GG.SDD Design And Implementation Plan

## Purpose

FS.GG.SDD is the spec-driven development lifecycle product for FS.GG. It turns
project intent into typed lifecycle artifacts that humans, agents, CLIs, and
optional governance gates can share.

The product boundary is deliberate:

| Repository | Owns |
|---|---|
| `FS.GG.SDD` | SDD lifecycle artifacts, normalized work model, generators, lifecycle CLI, agent commands/skills. |
| `FS.GG.Governance` | Rule engine, evidence freshness, routing, profiles, ship/verify/release enforcement. |
| `FS.GG.Rendering` | Rendering runtime, controls, templates, product docs, product tests. |

Markdown remains useful for authoring. Schema-versioned structured artifacts are
the machine contract.

## Product Model

The lifecycle stages are:

| Stage | Purpose | Primary artifact |
|---|---|---|
| Project init | Create the SDD project skeleton and policy pointers. | `.fsgg/project.yml` |
| Charter | Establish project identity, principles, and boundaries. | `work/<id>/charter.md` plus structured metadata |
| Specify | Capture user value, scope, user stories, requirements, and acceptance criteria. | `work/<id>/spec.md` |
| Clarify | Record answers to material ambiguity. | `work/<id>/clarifications.md` |
| Checklist | Validate requirements quality before planning. | `work/<id>/checklist.md` |
| Plan | Record architecture, contracts, risks, and verification strategy. | `work/<id>/plan.md` |
| Tasks | Create the typed implementation graph. | `work/<id>/tasks.yml` |
| Analyze | Check cross-artifact consistency. | `readiness/<id>/analysis.json` |
| Implement | Execute tasks and record evidence. | `work/<id>/evidence.yml` |
| Verify | Validate selected local or CI checks. | `readiness/<id>/verify.json` |
| Ship | Produce merge-boundary readiness for Governance or CI. | `readiness/<id>/ship.json` |

## Source And Generated Artifacts

Authored sources:

- `.fsgg/project.yml`
- `.fsgg/sdd.yml`
- `.fsgg/agents.yml`
- `work/<id>/charter.md`
- `work/<id>/spec.md`
- `work/<id>/clarifications.md`
- `work/<id>/checklist.md`
- `work/<id>/plan.md`
- `work/<id>/contracts/`
- `work/<id>/tasks.yml`
- `work/<id>/evidence.yml`

Generated views:

- `readiness/<id>/work-model.json`
- `readiness/<id>/analysis.json`
- `readiness/<id>/agent-commands/`
- `readiness/<id>/summary.md`
- optional Governance-facing route/evidence/audit inputs

Generated views are outputs. A generated view that is stale relative to its
sources must be reported as stale; its presence is not proof of currency.

## Integration With Governance

Initial SDD work must not depend on Governance. The integration contract comes
later:

- SDD emits normalized work-model and readiness JSON.
- Governance reads those artifacts as inputs.
- Governance decides route, profile, evidence freshness, and gate enforcement.
- SDD remains usable without Governance installed.

## Implementation Plan

Progress markers:

- [x] Scaffold empty repository with Spec Kit metadata, constitution, docs, and
  Claude/Codex guidance.
- [x] Create GitHub repository under `FS-GG`.
- [x] Update FS-GG org profile/site to list SDD as a separate product.
- [x] Copy development-relevant Governance and org reference docs into
  `docs/reference/`.

### Phase 1: Artifact Model

- [ ] Specify schema versions for `.fsgg/project.yml`, `.fsgg/sdd.yml`, and
  `.fsgg/agents.yml`.
- [ ] Define `WorkId`, `Stage`, `RequirementId`, `DecisionId`, `TaskId`,
  `EvidenceId`, and artifact references.
- [ ] Define conflict diagnostics for missing ids, unknown references, stale
  generated views, duplicate ids, and prose/structured mismatch.
- [ ] Add F# signatures before implementation.
- [ ] Add tests for schema validation and deterministic normalized output.

### Phase 2: Normalized Work Model

- [ ] Parse authored lifecycle artifacts into a `WorkModel`.
- [ ] Emit `readiness/<id>/work-model.json` with source digests and diagnostics.
- [ ] Define deterministic ordering and stable JSON contracts.
- [ ] Report stale generated models.
- [ ] Document migration behavior for schema changes.

### Phase 3: Lifecycle Commands

- [ ] Add `fsgg-sdd init`.
- [ ] Add `fsgg-sdd charter`.
- [ ] Add `fsgg-sdd specify`.
- [ ] Add `fsgg-sdd clarify`.
- [ ] Add `fsgg-sdd checklist`.
- [ ] Add `fsgg-sdd plan`.
- [ ] Add `fsgg-sdd tasks`.
- [ ] Add `fsgg-sdd analyze`.
- [ ] Keep command state behind MVU boundaries.

### Phase 4: Agent Commands And Skills

- [ ] Generate Claude command/skill guidance from the same lifecycle model.
- [ ] Generate Codex skill guidance from the same lifecycle model.
- [ ] Keep generated agent content marked as generated.
- [ ] Add stale-agent-command diagnostics.
- [ ] Verify Claude and Codex guidance remain behaviorally equivalent.

### Phase 5: Governance Integration

- [ ] Define SDD readiness JSON consumed by FS.GG.Governance.
- [ ] Add optional Governance adapter or contract tests.
- [ ] Keep Governance absent from the required local workflow.
- [ ] Document version compatibility and failure modes.

### Phase 6: Bootstrap Experience

- [ ] Add project templates for a new SDD-governed product skeleton.
- [ ] Ensure generated projects can continue with standard Spec Kit.
- [ ] Provide migration guidance from existing Spec Kit projects.
- [ ] Add quickstart docs and smoke tests.

### Phase 7: Release Readiness

- [ ] Add package identity and versioning policy.
- [ ] Add release checklist.
- [ ] Add docs for CLI installation and agent setup.
- [ ] Add compatibility matrix for Spec Kit and Governance versions.

## Acceptance Bar

FS.GG.SDD is useful when a new project can:

1. Initialize an SDD skeleton.
2. Author lifecycle artifacts in Markdown and structured files.
3. Produce a deterministic normalized work model.
4. Generate Claude and Codex guidance from the same contract.
5. Run lifecycle commands without Governance installed.
6. Optionally expose readiness artifacts that Governance can inspect.
7. Detect stale generated views.
8. Evolve schemas with explicit migration notes.
