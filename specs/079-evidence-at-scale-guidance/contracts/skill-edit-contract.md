# Contract: fs-gg-sdd-evidence skill-body edit

The invariants the feature-079 edit MUST preserve. This is a documentation edit to an
SDD-owned authored artifact; the "contract" is the set of guard-enforced properties that keep the
skill coherent across surfaces and the seeding path.

## C1 — Content obligations (verified by inspection)

- **C1.1** The body documents an at-scale classification workflow for an auto-expanded obligation
  graph, distinguishing real pass (`result: pass`, `synthetic: false`) from deferral, keyed on the
  carried `requirementRefs`/`planDecisionRefs` (no `tasks.yml` title-join). *(FR-001, FR-002)*
- **C1.2** The body states a deferral is first-class and honest — not a failure — that a shippable
  work item may carry deferrals, and that a deferral is preferable to a synthetic pass. *(FR-003)*
- **C1.3** The body documents a bulk-authoring pattern using only already-shipped affordances
  (`evidence --from-tests`, the origin refs) with an explicit honesty caveat (never a blanket
  `pass`). *(FR-004)*
- **C1.4** The body introduces no unshipped CLI flag, evidence field, `kind`/`result` value,
  output stream, or exit code, and reinforces the `result: pass ∧ synthetic: false` rule. *(FR-005)*
- **C1.5** The body stays coherent with sibling skills — links `fs-gg-sdd-authoring-contracts`,
  `fs-gg-sdd-verify`, `fs-gg-sdd-tasks` rather than restating divergently. *(FR-010)*

## C2 — Mirror parity (guard-enforced)

- **C2.1** `.claude/skills/fs-gg-sdd-evidence/SKILL.md` and `.codex/skills/fs-gg-sdd-evidence/
  SKILL.md` are byte-identical. *(FR-006)* — enforced by the skill-mirror guard.

## C3 — Manifest pin (guard-enforced)

- **C3.1** `.agents/skills/skill-manifest.json`'s `fs-gg-sdd-evidence` `sha256` equals the
  CRLF→LF-normalized `sha256` of the on-disk body; `fsgg-sdd registry skill-manifest --check`
  exits 0. *(FR-007)* — enforced by `ProcessSkillManifestTests` + the `--check` path.
- **C3.2** The manifest row set and schema (v1) are unchanged; only the `fs-gg-sdd-evidence`
  `sha256` value differs. *(FR-009)*

## C4 — Seeding & drift guards stay green (guard-enforced)

- **C4.1** The embedded resource `SeededSkill.fs-gg-sdd-evidence` matches the on-disk canonical
  body after rebuild, so `init`/`scaffold` seed the improved guidance. *(FR-008)*
- **C4.2** The agent-surface-drift, skill-mirror, and process-skill-manifest guards all pass.
  *(FR-008, SC-005)*
- **C4.3** `SeededSkills.skillNames` is unchanged (no skill added/removed). *(FR-009)*

## C5 — No collateral change

- **C5.1** No `fsgg-sdd` command behavior, persisted schema, field, flag, output stream, or exit
  code changes; no golden fixture changes outside the manifest `sha256`. *(FR-005, SC-005)*
- **C5.2** Other skills' bodies and `sha256` rows are unchanged.

## Verification map

| Contract | How verified |
|---|---|
| C1.1–C1.5 | manual inspection against quickstart checklist |
| C2.1 | `diff .claude/.../SKILL.md .codex/.../SKILL.md` (empty) + skill-mirror guard |
| C3.1 | `fsgg-sdd registry skill-manifest --check` exit 0; `sha256sum` cross-check |
| C3.2, C4.3 | `ProcessSkillManifestTests` (row set == `skillNames`); manifest schema unchanged |
| C4.1, C4.2 | full `dotnet test` — drift-guard suite green |
| C5.1, C5.2 | `git diff --stat` scoped to the three surfaces + spec docs; no other churn |
