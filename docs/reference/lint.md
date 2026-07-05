# `fsgg-sdd lint` — pre-flight authoring lint

`fsgg-sdd lint <artifact>` statically pre-flights a single authored SDD artifact for the
load-bearing authoring-grammar defects **before** a lifecycle stage would block on them. It is
read-only, is **not** a lifecycle stage (`nextLifecycleCommand Lint = None`), writes nothing, and
emits no readiness/state file. Feature 076; source: the TD1 field-feedback report `FEEDBACK.md`
§3.1/§3.6/§4.2.

## Surfaces

| Surface | Use |
|---|---|
| `fsgg-sdd lint <artifact>` | Pre-flight one authored artifact by path. |
| `fsgg-sdd <stage> --explain` | Non-blocking dry run of the same checks against the stage's own artifact (advances no state, mutates nothing). Stage verbs that accept `--explain`: `charter`, `specify`, `clarify`, `checklist`, `plan`, `tasks`, `evidence`. |

Output follows the shared three-projection contract — `--json` (default, the automation
contract), `--text`, `--rich` — with precedence `--rich > --text > --json`. `--rich` is a pure
projection (adds/drops no facts, changes no JSON byte) and degrades to zero-ANSI when
non-interactive / `NO_COLOR` / `TERM=dumb`.

## Recognized artifacts (FR-002)

Kind is auto-detected from the front-matter `stage:` value first, then the filename/extension:
`charter.md`, `spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`,
`evidence.yml`. An unrecognized path is reported as unusable input (exit 2), not a crash.

## Defect classes

Lint reuses the **live stage parsers** — it derives no new grammar, so it cannot diverge from
what the stage enforces. It surfaces the four load-bearing classes, each with the parser's fix
hint and a pointer to the grammar of record (`docs/reference/authoring-contracts.md`):

| Class | What it catches | Grammar anchor |
|---|---|---|
| `coverageLine` | a Functional-Requirements bullet shaped like a coverage line but missing its stable `FR-###` id (the silent "counted but uncovered" trap) | `#acceptance-coverage-line` |
| `missingDecisionTag` | a blocking remaining ambiguity not resolved by an `[AMB:AMB-###]`-tagged decision | `#clarify-decision-tag-resolution` |
| `frontMatter` | a required per-stage front-matter field absent/invalid | `#per-stage-front-matter` |
| `duplicateId` | the same stable id declared twice | `#per-stage-front-matter` |

Scope: lint is **single-artifact**. Cross-artifact FR→AC coverage reconciliation is the job of
`checklist` / `analyze`, not lint. All reported defects are errors (there is no warning severity);
conditions that are explicitly not defects (e.g. optional `sha256:` Source-Snapshot digests) are
never reported.

## Exit codes (FR-011)

`lint` and `<stage> --explain` use a bespoke polarity so the pre-flight is CI-usable and can tell
"found defects" from "couldn't run":

| Code | Meaning |
|---|---|
| `0` | clean — the artifact has zero defects |
| `1` | defects found in a well-formed artifact |
| `2` | unusable input — the artifact is missing, unreadable, an unrecognized kind, or no `<artifact>` was supplied |

This is the opposite of the shared lifecycle exit mapping (where `2` is the tool-defect class);
every other command keeps that shared mapping. See ADR/feature-076 plan Complexity Tracking.

## JSON shape

The `lint` block on the command report carries `artifactPath`, `kind`, `outcome`
(`clean`/`defectsFound`/`unusableInput`), and an ordered `defects[]` — each with `class`, `id`,
`severity`, `location`, `message`, `correction` (the fix hint), and, for the four grammar classes,
a `grammarPointer` (`doc` + `anchor` + optional `exampleTag`). Output is deterministic: identical
artifact bytes yield byte-identical JSON, defects ordered by `(line, column, id)`.

## Examples

```sh
fsgg-sdd lint work/042-thing/clarifications.md          # pre-flight before running clarify
fsgg-sdd lint work/042-thing/checklist.md --text
fsgg-sdd clarify --explain --work 042-thing             # same checks, in-stage, non-blocking
```
