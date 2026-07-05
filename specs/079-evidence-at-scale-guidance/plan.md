# Implementation Plan: fs-gg-sdd-evidence skill — honest partial/at-scale evidence, bulk authoring, deferrals-first-class

**Branch**: `079-evidence-at-scale-guidance` | **Date**: 2026-07-05 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/079-evidence-at-scale-guidance/spec.md`

## Summary

The `fs-gg-sdd-evidence` process skill's authored body is
`.claude/skills/fs-gg-sdd-evidence/SKILL.md`, mirrored byte-identically to
`.codex/skills/fs-gg-sdd-evidence/SKILL.md`, compiled into `FS.GG.SDD.Commands` as the embedded
resource `SeededSkill.fs-gg-sdd-evidence` (linked from the `.claude` file — see
`src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs`), and pinned by a `sha256` in the
process manifest `.agents/skills/skill-manifest.json`. This feature is a **documentation-only**
edit: it adds three pieces of guidance to that body — (1) an at-scale classification workflow for
an auto-expanded obligation graph (the TD1 18→85 case), (2) an explicit "deferrals are
first-class, not failures" framing that reinforces the `result: pass ∧ synthetic: false`
satisfaction rule, and (3) a bulk-authoring pattern grounded only in already-shipped affordances
(`evidence --from-tests`, the carried `requirementRefs`/`planDecisionRefs` origin refs from
feature 077, ordinary scripted edits) — then re-pins the three derived surfaces.

**Approach**: edit the canonical `.claude` body; copy it byte-for-byte over the `.codex` mirror;
regenerate the manifest `sha256` with `fsgg-sdd registry skill-manifest --write`; rebuild so the
embedded resource re-links; run the drift-guard suite (agent-surface parity, skill mirror, process
skill manifest) to green. No source code, `.fsi`, schema, field, flag, output-stream, or
exit-code change; the skill *set* is unchanged (no row added/removed), so `SeededSkills.skillNames`
and the manifest's row set stay put — only the `fs-gg-sdd-evidence` body bytes and its `sha256`
change. The guidance describes only behavior `fsgg-sdd` already ships (FR-005).

## Technical Context

**Language/Version**: F# on .NET (`net10.0`) for the guarded assembly/tests; the artifact under
change is a Markdown `SKILL.md` (YAML frontmatter + Markdown body).

**Primary Dependencies**: no new packages. Touch points are the authored skill file, its `.codex`
mirror, the `SeededSkills` embedded-resource wiring in `FS.GG.SDD.Commands` (unchanged — it links
the `.claude` file by relative path), the `fsgg-sdd registry skill-manifest` generator
(`src/FS.GG.SDD.Cli/RegistrySkillManifest.fs`), and the existing drift-guard tests.

**Storage**: three in-repo derived surfaces re-pinned to the edited body —
`.codex/skills/fs-gg-sdd-evidence/SKILL.md` (byte-identical mirror), the compiled embedded
resource (re-links on build), and the `fs-gg-sdd-evidence` `sha256` row in
`.agents/skills/skill-manifest.json`. No DB, no schema version bump.

**Testing**: xUnit. Guards that must stay green (no new tests expected — the existing guards
already cover the invariants this edit must preserve):
`tests/FS.GG.Contracts.Tests/AgentSurfaceDriftTests.fs` (agent-surface parity),
`tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs` (`.claude`≡`.codex`), and
`tests/FS.GG.SDD.Commands.Tests/ProcessSkillManifestTests.fs` (manifest `sha256`/row set / a
`--check` equivalence). These are the FR-006/FR-007/FR-008 enforcement. Manual content
verification (FR-001..FR-005) is by inspection against the quickstart checklist.

**Target Platform**: Linux/macOS/Windows CLI (`fsgg-sdd`) and the seeded agent surfaces
(Claude/Codex/neutral `.agents`) of scaffolded workspaces.

**Project Type**: single-project CLI + libraries (`src/FS.GG.SDD.*`) with SDD-owned agent-skill
skeleton under `.claude` / `.codex` / `.agents`.

**Performance Goals**: N/A — a documentation edit; no runtime path changes. Guards run bounded
file reads + a `sha256`.

**Constraints**: `.claude`≡`.codex` byte-identical (FR-006); manifest `sha256` == `sha256sum` of
the on-disk body (FR-007); no CLI/schema/field/stream/exit change and no unrelated golden churn
(FR-005/FR-008/SC-005); guidance must describe only already-shipped behavior (FR-005); stay
coherent with sibling skills, linking not restating (FR-010). Frontmatter `description` stays a
single line within existing conventions if touched at all (Edge Cases).

**Scale/Scope**: one authored file edited; two derived surfaces re-pinned (mirror + manifest row);
one rebuild to re-link the embedded resource. No new files. No new tests anticipated (existing
guards suffice); if the manifest `--check` guard is not already wired, a one-line assertion is
added rather than a new suite.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Spec → FSI → Semantic Tests → Implementation** — N/A to code: no `.fs`/`.fsi` changes. The
  authored-source → generated-view discipline still holds: `SKILL.md` is authored source; the
  `.codex` mirror, embedded resource, and manifest `sha256` are its derived, guarded projections.
  PASS.
- **II. Structured Artifacts Are the Machine Contract** — the manifest `sha256` (the machine
  contract over the skill body) is regenerated deterministically from the authored Markdown; the
  guards enforce the projection. PASS.
- **III. Visibility Lives in `.fsi`** — no code surface changes. PASS (N/A).
- **IV. Idiomatic Simplicity** — the change is the minimal one that satisfies the issue: edit the
  one authored body and re-pin its derived surfaces. No new module, flag, or field (rejected by
  FR-005/FR-009). PASS.
- **V. Elmish/MVU boundary** — no stateful/I/O workflow added. PASS (N/A).
- **VI. Test Evidence Is Mandatory** — real evidence is the green drift-guard suite over the edited
  body + the manifest `--check` equivalence; content requirements verified by inspection against
  the quickstart. No synthetic stand-ins. PASS.
- **VII. Agent And Human Workflows Share One Contract** — the whole point: the improved body seeds
  byte-identically to Claude, Codex, and neutral `.agents` surfaces via the unchanged seeding path;
  the mirror guard enforces `claude ≡ codex`. PASS.
- **VIII. Observability And Safe Failure** — no failure surface changes; the guards fail-closed on
  any mirror/manifest drift. PASS.

**Change Classification**: documentation / authored-SDD-owned skeleton content — no schema or
contract version bump (persisted schemas and the skill-manifest schema stay v1; only a `sha256`
value changes). No gate violations; Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/079-evidence-at-scale-guidance/
├── plan.md              # This file
├── research.md          # Phase 0 output — surface/guard mechanics + content sourcing
├── data-model.md        # Phase 1 output — the edited body's guidance blocks + derived surfaces
├── quickstart.md        # Phase 1 output — how to verify the edit end-to-end
├── contracts/           # Phase 1 output — the invariants the edit must preserve
│   └── skill-edit-contract.md
├── checklists/
│   └── requirements.md  # from /speckit-specify
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
.claude/skills/fs-gg-sdd-evidence/SKILL.md      # canonical authored body — EDITED
.codex/skills/fs-gg-sdd-evidence/SKILL.md       # byte-identical mirror — RE-SYNCED
.agents/skills/skill-manifest.json              # process manifest — fs-gg-sdd-evidence sha256 RE-PINNED

src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs   # embedded-resource wiring — UNCHANGED (re-links on build)
src/FS.GG.SDD.Cli/RegistrySkillManifest.fs              # manifest generator — UNCHANGED (invoked with --write)

tests/FS.GG.Contracts.Tests/AgentSurfaceDriftTests.fs    # guard — must stay green
tests/FS.GG.Contracts.Tests/SkillMirrorTests.fs          # guard — must stay green
tests/FS.GG.SDD.Commands.Tests/ProcessSkillManifestTests.fs  # guard — must stay green
```

**Structure Decision**: single-project layout; the feature edits one authored file under the
SDD-owned `.claude` skill skeleton and re-pins its two derived surfaces. No new source directories.

## Complexity Tracking

> No Constitution Check violations — this section intentionally left empty.
