# Contract: `lint` CLI surface + `<stage> --explain`

Command family `fsgg-sdd`. This contract defines the invocation, flags, and exit codes. It is
Tier 1 (new verb + new flag + exit semantics).

## `fsgg-sdd lint <artifact>`

Read-only pre-flight of a single authored SDD artifact. Not a lifecycle stage
(`nextLifecycleCommand Lint = None`); writes nothing; emits no readiness/state file.

```
fsgg-sdd lint <artifact-path> [--json | --text | --rich]
```

- `<artifact-path>` — **required** positional. A path to one authored artifact
  (`charter.md`, `spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`,
  `evidence.yml`). Kind is auto-detected (front-matter `stage:` first, else filename/extension).
- Output flags follow the shared precedence `--rich > --text > --json > default(json)`. `--rich`
  is a pure projection (adds/drops no facts, changes no JSON byte, degrades to zero-ANSI when
  non-interactive / `NO_COLOR` / `TERM=dumb`).

### Behavior

- Runs the **live stage parser(s)** for the detected kind and reports every `Error`-severity
  diagnostic, classified into `{CoverageLine, MissingDecisionTag, FrontMatter, DuplicateId}`, each
  with the parser's `Correction` fix hint and a grammar pointer (see `lint-report.md`).
- Reports **all** applicable defects in one run (FR-014); never stops at the first.
- An artifact too malformed to parse yields a single `Parse` defect (FR-015), not a cascade.
- Non-defects (optional `sha256:` digests, empty-section disclaimers) are never reported (FR-017).

### Exit codes (FR-011 — bespoke `exitCodeForLint`)

| Code | Condition |
|---|---|
| `0` | artifact is well-formed and has **zero** defects (clean) |
| `1` | artifact is well-formed but has **one or more** defects |
| `2` | input is **unusable** — path missing/unreadable, or artifact kind not recognizable |

This polarity is lint-specific (peer-verb bespoke mapping); all lifecycle stages keep the shared
`exitCodeForReport` (where `2` = tool defect). Documented divergence: see plan Complexity Tracking.

## `fsgg-sdd <stage> --explain`

Non-blocking dry run: runs the **same** lint checks against the stage's own artifact.

```
fsgg-sdd clarify   --explain [--json|--text|--rich]
fsgg-sdd checklist --explain [...]
fsgg-sdd specify   --explain [...]
fsgg-sdd plan      --explain [...]
# (any front-matter/grammar-bearing stage)
```

- Reports the identical defect list `lint` would for that stage's artifact.
- **Advances no state and mutates nothing** (no `WriteFile`/readiness effect); `NextAction = None`.
- Shares the same `0/1/2` exit mapping as `lint` (non-blocking = no mutation, *not* always-0).

## Determinism (FR-012/SC-005)

For identical artifact bytes, the JSON report is byte-identical across runs; defects are ordered
`(Location.Line, Location.Column, Diagnostic.Id)`. No timestamps/env values in the report.

## Out of scope

- Cross-artifact FR→AC reconciliation (that is `checklist`/`analyze`) — lint is single-artifact.
- Linting a whole work item in one invocation (possible additive follow-up).
- Any mutation, auto-fix, or state advancement.
