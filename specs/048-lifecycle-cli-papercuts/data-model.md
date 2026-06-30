# Phase 1 Data Model: Lifecycle/CLI Semantics Papercuts

This feature changes **re-evaluation semantics and reporting** over existing
entities and adds one new report payload (`HelpSummary`). No lifecycle artifact
schema version changes; the only structured-contract change is the additive
`help` jsonField on the command report.

---

## 1. Checklist result row + source-snapshot (§3.1)

**Existing types** (unchanged shape):
- `ChecklistReviewResult` / `ChecklistItem` / `ChecklistSourceSnapshot`
  (`Checklist.fsi:23-57`) — the per-requirement verdict (`pass` | `fail` |
  `acceptedDeferral` | `stale`) and the recorded source digests.
- `PlannedChecklistReview` (`ParsingMid.fs:89-96`) — an in-flight derived row.
- `ChecklistSummary` (`CommandTypes.fs:123-137`) — report summary, incl.
  `FailedBlockingCount`, `StaleResultCount`.

**Behavioral change (re-derivation rule)**:

| Re-run condition | Old behavior | New behavior |
|---|---|---|
| Source snapshot **current** | preserve rows, `noChange` | unchanged: preserve rows, `noChange`, byte-identical |
| Source snapshot **stale** | preserve old rows, append one `stale:` row, `succeededWithWarnings` | **purge all machine-derived result rows, re-derive from current sources, rewrite `## Source Snapshot`** |

**Invariants**:
- Authored (human-written) `checklist.md` sections are preserved
  (`ensureChecklistSections`); only the derived result rows are purged.
- After a stale re-run, **no row whose review was made against the superseded
  snapshot survives** (SC-001).
- Partial fix: each requirement is re-evaluated independently — still-failing →
  `fail`, now-passing → `pass` (Edge Case: partial source change).
- Re-derivation is a pure function of current source bytes (determinism, FR-012).

---

## 2. Specify report statement (§3.2)

**Existing types** (unchanged): `SpecificationFacts` / `SpecificationFrontMatter`
(`Specification.fsi:12-38`), `SpecificationSummary` (`CommandTypes.fs:101-110`),
`NextAction` (`CommandTypes.fs:348-354`).

**Behavioral change**: on a `specify` re-run that makes no authored write (the
spec already exists and is section-complete), the report MUST carry a deterministic
statement that `specify` promotes only the first draft and that `spec.md` is read
live by downstream stages. Realized as a `NextAction` (or an advisory fact)
populated whenever `Command = Specify` and the outcome is `NoChange` over an
existing spec.

**Authoritative-source rule** (Constitution II): the live `spec.md` is
authoritative for downstream stages; `specify` does not re-promote status or
rewrite authored content. The report never asserts the edit was "ingested by
specify" — only that it is consumed downstream.

**Invariant**: in 100% of edited-then-re-run cases the report either reflects the
new content (it already re-parses the summary) **or** explicitly states where the
file is read live — never a bare, ambiguous `NoChange` (SC-002).

---

## 3. Ambiguity item + no-outstanding sentinel (§3.3)

**Existing types** (unchanged): `AmbiguityId = { Value: string }`
(`Identifiers.fs:25`); `SpecificationFacts.AmbiguityIds` (`Specification.fsi:35`);
`RemainingAmbiguity` / `ClarificationFacts.BlockingAmbiguityCount`
(`Clarification.fs:67-73`, `:84`).

**New concept — no-outstanding sentinel** (parse-time classification, no stored
type): a line under `## Ambiguities` that expresses "none outstanding."

| Line under `## Ambiguities` | Bullet? | Carries `AMB-###`? | Classification |
|---|---|---|---|
| `No material ambiguities recorded.` | no | no | sentinel → non-blocking (today: OK) |
| `- None outstanding` / `- No open questions` | yes | no | **sentinel → non-blocking (NEW)** |
| `- AMB-001 open: …` | yes | yes | real ambiguity → blocks (unchanged) |
| `- needs an answer` | yes | no | missing-id error (unchanged) |

**Recognition rule**: a line is a sentinel iff (after trimming an optional leading
`- `) it matches the existing disclaimer convention (`StartsWith "No "`,
case-insensitive) or an explicit "none outstanding" phrasing. Sentinels are
exempted from `missingIdDiagnostics`' "every bullet needs an `AMB-###`" rule and
yield no `AmbiguityId`.

**Invariants**:
- A spec whose `## Ambiguities` is only sentinels ⇒ `AmbiguityIds = []` ⇒ `clarify`
  proceeds, no block (FR-003, SC-003).
- Any line bearing an `AMB-###` ⇒ a real `AmbiguityId` ⇒ `clarify` still blocks
  (FR-004); a sentinel *alongside* a real bullet does not suppress the real one
  (Edge Case: mixed content).

---

## 4. Work-model currency snapshot-set parity (§3.4)

**Existing types** (unchanged): `GeneratedViewState` / `GeneratedViewCurrency`
(`CommandTypes.fs:46-52`, `:93-101`); `SourceIdentity`/source-digest comparison in
`checkGeneratedWorkModelCurrency` (`Serialization.fs:263-289`).

**Behavioral change**: the currency-check input set
(`existingGeneratedViewDiagnostic.currentSnapshots`, `ViewGeneration.fs:452-461`)
MUST equal the generation input set (`workModelSnapshots`, `:476-502`) restricted
to authored sources. Concretely, add `planPath workId` and `charterPath workId` to
`currentSnapshots`.

**Source-set definition** (authoritative, from `WorkItem.fs:158-164`): a work-model
*source* is any input snapshot that is **not** `*.json` and **not** `manifest.yml`.
Therefore readiness outputs (`verify.json`, `ship.json`, `work-model.json`) are
never sources and can never drive staleness.

**Staleness predicate** (unchanged): `generatorStale || sourceStale || outputDigestStale`.

**Invariants**:
- Clean verify/ship run (no authored source changed since the work model was
  generated) ⇒ **no** `staleGeneratedView` (FR-005/006, SC-004).
- A genuinely changed authored source (different digest, or a recorded source now
  absent) ⇒ `staleGeneratedView` still emitted (FR-007).

---

## 5. HelpSummary + flag metadata (§3.5)

**New report type** — `HelpSummary` (added to `CommandTypes.fs`/`.fsi`):

```fsharp
type HelpFlag =
    { Name: string            // e.g. "--work" / "-h" alias listed with its primary
      Argument: string option // e.g. Some "<id>" for value-taking flags, None for switches
      Description: string }

type HelpCommandEntry =
    { Name: string            // command token, e.g. "verify", "validate"
      Description: string }   // one-line summary

type HelpScope =
    | TopLevel                // `fsgg-sdd --help`
    | Command of string       // `fsgg-sdd <command> --help`

type HelpSummary =
    { Scope: HelpScope
      Usage: string                       // canonical usage line
      Commands: HelpCommandEntry list     // populated for TopLevel; [] for Command scope
      GlobalFlags: HelpFlag list
      CommandFlags: HelpFlag list }       // [] for TopLevel; the command's flags for Command scope
```

**New `CommandReport` field**: `Help: HelpSummary option` (additive). Serialized by
`writeHelp` following the `writeScaffold` convention (object when `Some`, `null`
when `None`) so the field is always present — consistent with the catalog's
"documented field always present" model.

**New module** — `CommandHelp` (`CommandHelp.fs`/`.fsi`), pure static data +
builders:

```fsharp
val globalFlags: HelpFlag list
val commandEntries: HelpCommandEntry list          // 14 SddCommand + version/validate/registry
val commandFlags: SddCommand -> HelpFlag list       // accepted flags per lifecycle command
val topLevelHelp: GeneratorVersion -> HelpSummary
val commandHelp: SddCommand -> HelpSummary
```

**Determinism**: all help content is static (no clock/path/env), so default/`--json`
and `--text` output is byte-identical across runs (FR-012).

**State / dispatch transitions** (in `Program.run`):

| Argv | Result |
|---|---|
| `--help` / `-h` / `help` (no command) | top-level `HelpSummary` (scope `TopLevel`), exit 0, stdout |
| `<known> --help` / `<known> -h` | command `HelpSummary` (scope `Command`), exit 0, stdout |
| `<known>` (no help flag) | unchanged command execution |
| `<unknown>` (± `--help`) | `unknownCommand`, exit 1 (FR-011) |
| `--help --json` / `--help --text` / `--help --rich` | help, selected projection (rich degrades to text when non-interactive/color-disabled) |

---

## Catalog / schema-reference impact

- `docs/release/release-readiness.json` — add a `help` entry to the
  `command-report (--json)` `inventory[]`, `kind: jsonField`,
  `stability: additiveOptional`.
- `docs/release/schema-reference.md` — document the additive `help` field so the
  conformance test (doc ⇄ JSON ⇄ produced artifact) agrees.
- No `readiness/<id>/` artifact schema changes; no new declared exception.
