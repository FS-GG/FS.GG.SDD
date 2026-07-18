# Contract: Remediation pointer suffix

The stable, deterministic format of the pointer appended to a covered diagnostic's `Correction`.
This is the contract the guard test and the golden fixtures pin.

## Rendering rules

The pointer targets the **vendored `fs-gg-sdd-*` process skills**, not tool-repo-only docs. Every
scaffolded product vendors the process skills under `.claude/`, `.codex/`, and the neutral `.agents/`
skill roots (byte-identical, drift-guarded), so a skill reference resolves in a scaffold's tree —
whereas `docs/examples/` and `docs/reference/` exist only in this tool's own repo, which left a
scaffold operator following the `fix:` pointer into a dead end (FS.GG.SDD#539). The reference is by
skill **name** (never a `.claude`/`.codex`/`.agents` path), so generic SDD embeds no agent-runtime
literal and one sentence is correct for a Claude, Codex, or neutral `.agents` runtime.

Let the covered id's registry entry be `{ Skill = s; Grammar = g }`, where `<sk>` is a stage skill
name (`fs-gg-sdd-<stage>`) and `<gr>` is a section-anchor slug into the `fs-gg-sdd-authoring-contracts`
skill.

- **Both present** (`s = Some sk`, `g = Some gr`):
  `See the <sk> skill and the grammar under the fs-gg-sdd-authoring-contracts skill (#<gr>).`
- **Skill only** (`g = None`):
  `See the <sk> skill.`
- **Grammar only** (`s = None`):
  `See the grammar under the fs-gg-sdd-authoring-contracts skill (#<gr>).`
- **Neither**: not permitted (guard fails; every covered id has ≥1 target).

The stage skill shows the artifact plus its stage-specific ids and required headings; the
`fs-gg-sdd-authoring-contracts` skill holds the five cross-cutting gating grammars. Stable-id rules
are stage-specific (each stage skill documents its own id prefixes), so a stable-id block carries no
grammar anchor and cites the stage skill alone.

## Composition with the existing correction

The suffix is appended to the existing correction with a single separating space; the existing
correction is otherwise byte-unchanged. Example (`missingClarificationAnswer`):

- **Before**: `Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity.`
- **After**: `Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity. See the fs-gg-sdd-clarify skill and the grammar under the fs-gg-sdd-authoring-contracts skill (#4-clarify-decision-tag-resolution-used-by-clarify).`

## Invariants (guard-enforced — FR-006 / US3)

1. **Coverage**: every id in `RemediationPointers.registry` renders a non-empty suffix.
2. **Skill resolves**: every cited `<sk>` skill has a `SKILL.md` present on disk under at least one
   vendored agent-skill root (`.claude/`, `.codex/`, or `.agents/`).
3. **Anchor resolves**: every cited `<gr>` is in the set of GitHub slugs computed from the live
   `##`/`###` headings of the `fs-gg-sdd-authoring-contracts` skill.
4. **Determinism (FR-007)**: the suffix contains no timestamp, absolute path, machine name, or
   other environment-dependent content; identical inputs render byte-identical output on every OS. A
   grammar anchor legitimately carries a section number (e.g. `#3-specify---input-…`), so the suffix
   is not required to be digit-free — determinism is that it is a pure function of the static
   registry (no absolute path, POSIX separators only).
5. **Containment for covered diagnostics**: for each covered id, the constructed diagnostic's
   `Correction` ends with the rendered suffix.
6. **Non-interference (FR-008)**: for every diagnostic id **not** in the registry, the constructed
   `Correction` is byte-identical to its pre-feature text (no accidental suffix — it names no
   `fs-gg-sdd-*` skill).
7. **Keys are real ids**: every registry key appears as a quoted id literal in
   `DiagnosticConstructors.fs`, so a typo or a renamed/removed id (which would silently attach a
   pointer to nothing) fails the build. Anchor resolution skips fenced code blocks in the
   `fs-gg-sdd-authoring-contracts` skill, so a slug that only matches an embedded example heading is
   not accepted.

## Projection invariants (FR-003 / FR-009 / SC-005)

- The pointer is carried only in `Correction`; the diagnostic JSON has no new keys.
- `--json` renders the `Correction` string with the pointer included (the default, and the machine
  contract). `--text` (counters summary) and `--rich` (counters + a severity/id/message table)
  do **not** render per-diagnostic corrections, so they are byte-unchanged by this feature.
- `fsgg-sdd lint` / `--explain` (feature 076) renders its fix + grammar pointer from its **own**
  defect model, not from the appended Correction. This feature does not re-plumb lint. As of
  FS.GG.SDD#539 the two intentionally **diverge**: the remediation pointer now cites the vendored
  `fs-gg-sdd-authoring-contracts` skill (resolvable in a scaffold), while lint's `GrammarPointer`
  still cites `docs/reference/authoring-contracts.md` (tool-repo-only). Reconciling lint's pointer
  onto the same skill surface is tracked as a separate follow-up (lint carries the identical
  dead-end in a scaffolded product); the grammar **section** each cites still corresponds.
- The only JSON byte deltas versus pre-feature output are the `correction` values of covered
  diagnostics; non-covered diagnostics' JSON is unchanged.

## Non-goals

- No new `Diagnostic` field, output stream, exit code, or persisted-schema version.
- Corrections outside the covered set are not required to carry a pointer and are not modified.
