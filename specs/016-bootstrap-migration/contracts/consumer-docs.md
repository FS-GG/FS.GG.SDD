# Contract: Consumer Documentation Surfaces

This feature ships three first-party SDD documentation surfaces under `docs/`.
They are authoring surfaces (Constitution II): they describe the existing
`fsgg-sdd` lifecycle and never become a second source of truth. Each must satisfy
the invariants below; the lifecycle smoke and quickstart validation pin the
behavioral claims to real command output.

## Shared rules

- FsDocs-style frontmatter consistent with `docs/initial-implementation-plan.md`
  (`title`, `category`, `categoryindex`, `index`, `description`).
- Repository-relative paths use `/`.
- No FS.GG.Rendering package names, templates, or docs URLs in generic content
  (CLAUDE.md boundary rule); no assumption of a monorepo checkout or runtime
  templates.
- Generated readiness views are described as outputs whose currency comes from
  `fsgg-sdd refresh`, not from file presence (FR-015).
- Governance references are advisory compatibility facts; the docs never claim
  SDD evaluates or enforces Governance routing, freshness, profiles, gates,
  audit, or release decisions (FR-016).

## `docs/quickstart.md` — Consumer quickstart

Purpose: take a new user from `fsgg-sdd init` through `fsgg-sdd ship` for one
work item with no Governance installed.

Required sections:

- Prerequisites: the SDD CLI; explicit statement that no Governance gate runtime,
  FS.GG.Rendering package, or monorepo checkout is required.
- `fsgg-sdd init`: what skeleton is created (`.fsgg` config, `work/`,
  `readiness/`, agent guidance targets) and the next action.
- Lifecycle in canonical order — charter → specify → clarify → checklist → plan →
  tasks → analyze → evidence → verify → ship — with, for each stage, the authored
  source written and the generated readiness view refreshed or reported, and the
  command's emitted next action.
- Cross-cutting generators: where `fsgg-sdd agents` and `fsgg-sdd refresh` bring
  agent guidance and `readiness/<id>/summary.md` to currency
  (`nextLifecycleCommand = None` for both).
- Result: the SDD-owned readiness artifacts (`work-model.json`, `analysis.json`,
  `verify.json`, `ship.json`, `summary.md`, `agent-commands/`), and the pointer
  to the Governance-owned protected-boundary handoff as an optional next step.

Invariants: FR-001, FR-002, FR-003, FR-014, FR-015, FR-013.

## `docs/migration-from-spec-kit.md` — Migration guide

Purpose: additive, non-destructive adoption of native SDD artifacts from an
existing standard Spec Kit project.

Required sections:

- Starting point: an existing Spec Kit project with `specs/` and `.specify/`.
- Additive setup: run `fsgg-sdd init` to create `.fsgg`, `work/`, `readiness/`
  without touching `specs/` or `.specify/`.
- Artifact mapping table: existing Spec Kit feature artifacts (`spec.md`,
  `plan.md`, clarifications, checklist, `tasks.md`, evidence) → native
  `work/<id>/` authored sources, authored through the `fsgg-sdd` commands.
- No-equivalent handling: represent in the nearest SDD source or explicitly
  defer; never delete authored Spec Kit content.
- Coexistence: standard Spec Kit remains a valid workflow after migration; the
  steps are safe to re-apply.

Invariants: FR-007, FR-008, FR-009, FR-012.

## `docs/adopting-governance.md` — Optional Governance adoption note

Purpose: document Governance as an optional, additive layer after SDD init.

Required sections:

- After `fsgg-sdd init`, Governance owners may add `.fsgg/policy.yml`,
  `.fsgg/capabilities.yml`, and `.fsgg/tooling.yml`.
- Usability guarantee: every SDD lifecycle command stays usable whether those
  files are present, absent, or incomplete.
- Boundary: SDD reports readiness; Governance owns routing, effective-evidence
  freshness, profiles, gates, audit, and release enforcement; SDD does not
  evaluate or enforce them.

Invariants: FR-010, FR-011, FR-016.

## Cross-links

- `docs/index.md` gains links to all three new docs.
- `README.md` Workflow section links the quickstart and migration guide.
- No existing documentation content is removed or restructured.
