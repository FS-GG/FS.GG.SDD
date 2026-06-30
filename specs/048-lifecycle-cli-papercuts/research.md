# Phase 0 Research: Lifecycle/CLI Semantics Papercuts

All five papercuts were located in code before planning. This document records
the decision, rationale, and rejected alternatives for each, plus the resolution
of the spec's one explicitly-open question (§3.2 (a) vs (b)).

---

## D1 — §3.1 checklist stale rows: purge-and-re-derive on stale re-run

**Decision**: When the checklist source snapshot is stale on re-run
(`sourceSnapshotStale`, `ParsingMid.fs:305-315`), **discard the previously-derived
result rows and re-derive every row from the current `spec.md`/clarification
sources** — the same derivation used by the fresh `checklistTemplate` path
(`plannedChecklistReviews` with no `existing` filter) — and rewrite the
`## Source Snapshot` digests. Authored, human-written sections of `checklist.md`
are preserved (`ensureChecklistSections`); only machine-derived result rows are
purged and regenerated. When the snapshot is **not** stale, the existing
preserve path is unchanged and the run still reports `noChange` with
byte-identical output.

**Rationale**: Today the re-run path (`ParsingMid.fs:368-417`) reuses prior rows
via an `existingSourceIds` filter and only *appends* a `stale:` row
(`appendStaleChecklistResult`), so a corrected-but-still-failing source keeps its
old `fail` rows and the run reports `succeededWithWarnings` (only warning-severity
diagnostics are emitted). Re-deriving against the current snapshot is the only way
to guarantee "zero rows derived from the superseded snapshot" (SC-001) without the
author deleting `checklist.md`. It also correctly handles the partial-fix edge
case: every row is re-evaluated, so a still-failing requirement stays `fail` while
a now-passing one flips to `pass`.

**Alternatives rejected**:
- *Append-only + louder warning* (keep current behavior, escalate severity):
  rejected — stale `fail` rows still survive in `checklist.md`, violating FR-001/SC-001.
- *Require manual deletion* (status quo): rejected — explicitly the papercut.

**Determinism note**: re-derivation is a pure function of current source bytes, so
identical inputs produce byte-identical rows (FR-012). The existing test
`checklist appends safe missing requirement item and marks prior result stale`
(`ChecklistCommandTests.fs:186-200`) encodes the *old* append-stale behavior and
must be rewritten to assert purge-and-re-derive.

---

## D2 — §3.2 specify silent no-op: document-by-reporting (resolves the open (a)/(b))

**Decision**: Adopt a **document-by-reporting** resolution. `specify` already
re-parses the live `spec.md` into its `SpecificationSummary` on every run
(`ParsingEarly.fs:489-528` builds the summary from the on-disk snapshot), so the
report's requirement/ambiguity facts *already* reflect edited content. We make the
outcome **truthful and explicit**: when `specify` re-runs on an existing spec and
makes no authored write, the report carries a deterministic statement (a
`NextAction`/advisory fact) that **`specify` promotes only the first draft**, that
**`spec.md` is authored and read live by downstream stages** (`clarify`,
`checklist`, …), and points the author at the next stage. `specify` never clobbers
authored edits.

**Rationale**: The spec's Assumptions default to option (a) "re-ingest the changed
file," but Phase 0 showed `specify` has **no** upstream source other than the user
intent and `spec.md` is itself the output — there is no well-defined "re-ingest"
action distinct from what already happens (the summary is re-parsed live), and
fabricating a digest/snapshot for an authored output would either be circular or
risk violating the "specify promotes the first draft" invariant the spec flags as
the trigger for option (b). The constitution (II, VII) already makes the live
`spec.md` authoritative for downstream stages. The actual harm in §3.2 is
**perception** — a bare `NoChange` reads as "my edit was ignored." A deterministic
report statement closes exactly that gap and satisfies FR-002's "author always
knows" and SC-002 under either reading of (a)/(b).

**Alternatives rejected**:
- *(a) literal re-ingest with a new spec source-snapshot/digest*: rejected as
  over-engineered and architecturally circular (the digest would cover the authored
  output itself); risks the first-draft invariant for no behavioral gain over
  live-read + reporting.
- *Re-promote status / rewrite `spec.md` on edit*: rejected — clobbers authored
  content and contradicts "promotes the first draft."

**Scope note**: This is a reporting change only; the WriteFile `NoChange`
classification for an unchanged authored file is correct and stays. Determinism
holds — the statement is a pure function of run state.

---

## D3 — §3.3 ambiguity disclaimer: recognize a no-outstanding sentinel

**Decision**: Teach the `## Ambiguities` parse to recognize a **no-outstanding
sentinel** so a "none outstanding" note never becomes a blocking item, whether
written as prose or as a bullet. Two cooperating sites in `Specification.fs` change:
(1) `missingIdDiagnostics` (`:84-102`) must **not** demand an `AMB-###` id on a
disclaimer line; (2) the id extraction (`:176`) already only matches `AMB-###`, so
a disclaimer with no id yields no `AmbiguityId` — the fix is purely making the
disclaimer exempt from the "every bullet needs an id" rule. Recognized sentinel
forms follow the existing convention in `Internal.fs:211-218`
(`parseNonEmptySectionLines` already skips blank lines and lines starting with
`No ` case-insensitively); we extend that to cover the obvious bullet gesture
(`- None outstanding`, `- No open questions`, `- No material ambiguities …`).

**Rationale**: Mirroring the existing `StartsWith "No "` convention keeps the rule
discoverable and consistent rather than inventing a new sanctioned token. Genuine
ambiguities are unaffected: a `- AMB-001 …` bullet still parses to a real
`AmbiguityId`, `clarify` still synthesizes a blocking question, and
`BlockingAmbiguityCount > 0` still blocks (FR-004). The mixed-content edge case (a
disclaimer plus a real `AMB-001` bullet) works because only the disclaimer line is
exempted; the real bullet still parses and blocks.

**Alternatives rejected**:
- *A dedicated machine token* (e.g. a front-matter `ambiguities: none` flag):
  rejected — less discoverable than the natural prose/bullet gesture the spec asks
  to support; would require a schema change.
- *Treat any bullet as a note unless it has an id*: rejected — that would silently
  drop genuine ambiguities authored without an id (which today correctly error via
  `missingSpecificationId`).

**Exact sentinel grammar** is a Phase 1 contract detail
([ambiguity-disclaimer.md](./contracts/ambiguity-disclaimer.md)); the invariant is:
prose-or-bullet "no outstanding" ⇒ section is empty-of-questions and non-blocking;
any line bearing an `AMB-###` ⇒ a real ambiguity.

---

## D4 — §3.4 staleGeneratedView: mirror the generation source set in the currency check

**Decision**: In `existingGeneratedViewDiagnostic` (`ViewGeneration.fs:445-474`),
build `currentSnapshots` (`:452-461`) from the **exact same authored source set**
that `workModelSnapshots` (`:476-502`) uses to *generate* the work model — i.e.
add `planPath` and `charterPath` (currently omitted) alongside the `.fsgg/*.yml`,
spec, clarification, checklist, tasks, and evidence snapshots already present.

**Rationale**: Phase 0 proved the advisory is **not** caused by writing
`verify.json`/`ship.json` — readiness `.json` files are filtered out of work-model
sources (`WorkItem.fs:158-164`) and there is **no mtime logic** anywhere; staleness
is digest/version-based (`checkGeneratedWorkModelCurrency`, `Serialization.fs:263-289`).
The real cause is a snapshot-set mismatch: the generated `work-model.json` records
`plan.md` (and `charter.md`) among its sources, but the currency check omits them,
so `sourceStale`'s "stored source absent from current set → stale"
branch (`Serialization.fs:248-253`) fires on every run. Mirroring the source set
gives apples-to-apples digest inputs: a real authored edit (e.g. a changed
`spec.md` digest) still flags via `sourceStale` (FR-007), while the phantom
missing-plan staleness disappears (FR-005/006). The unit fixture that passes the
*full* snapshot set already reports a clean model
(`GeneratedModelCurrencyTests.fs` `valid-work-item`), confirming the fix direction.

**Alternatives rejected**:
- *Suppress `staleGeneratedView` whenever the stage wrote a readiness file*:
  rejected — it would mask genuine upstream staleness (violates FR-007) and encodes
  the reporter's incorrect mental model rather than the true cause.
- *Stop regenerating the work model in verify/ship*: rejected — out of scope and
  unrelated to the false advisory.

**Blast-radius note**: the seam is shared by `tasks`/`analyze`/`evidence`/`verify`/
`ship`, so the fix removes the false advisory everywhere; spec acceptance is scoped
to verify/ship (FR-005/006). The existing `ship` test asserts a `"advisory"`
disposition with a comment documenting the self-clearing warning
(`ShipCommandTests.fs:80-83`) and must be rewritten to expect a clean `shipReady`.

---

## D5 — §3.5 --help: additive `help` report field + static flag-metadata table

**Decision**:
1. **Flag metadata** — introduce a new public `CommandHelp` module in
   `FS.GG.SDD.Commands` (testable without the CLI, beside the `commandName`/
   `parseCommand` registry) declaring: global flags (`--root`, `--work`, `--title`,
   `--input`, `--dry-run`, `--force`, `--no-update`, `--provider`, `--param`,
   `--json`/`--text`/`--rich`) and the accepted flags + one-line description per
   command, covering the 14 `SddCommand` cases **and** the CLI-level peers
   (`version`, `validate`, `registry`). No such metadata exists today.
2. **Report representation** — project help through the existing `CommandReport`
   three ways by adding an additive `Help: HelpSummary option` field, serialized by
   a `writeHelp` that follows the established optional-summary convention (object
   when present, `null` when absent — exactly like `writeScaffold`). `renderText`
   emits help lines when present; `--rich` derives from the text projection.
3. **Dispatch** — add a help branch in `Program.run` as a peer of
   `--version`/`validate`: top-level `--help`/`-h`/`help` (no command) → top-level
   help (envelope `Command = Init`, mirroring `printUnknown`'s precedent; scope
   recorded in `HelpSummary`); `<command> --help`/`-h` → parse the command, detect
   help in `rest`, render that command's flag listing. Help builds its report via
   `buildReport` (no diagnostics, no changes → `NoChange` outcome → exit 0) and
   routes to **stdout** through `resolve format (detectCapabilities()) report`.

**Rationale for always-emitted `help` (vs omitted-when-None)**: the codebase's
established pattern adds every optional summary as an always-present field that is
`null` when absent (`writeScaffold`/`writeRefresh`), the release catalog lists each
as an `additiveOptional` jsonField, and the schema-reference conformance test
enforces **"no documented field absent"** — i.e. fields are always present. Adding
`help` the same way is consistent, keeps the conformance model simple, and is the
exact path by which `scaffold`/`refresh` were added. FR-010's "changing no JSON
byte relative to the projection contract" is satisfied as the standard projection
invariant (one canonical JSON; `--text`/`--rich` add/drop no facts and change no
JSON byte). The cost — `"help": null` added to every command's JSON golden — is
mechanical and expected for an additive field; `schemaVersion` stays `1`,
`stability: additiveOptional`.

**Edge-case rules** (from spec Edge Cases / FR-011):
- `fsgg-sdd --help --json` → top-level help, JSON projection.
- `fsgg-sdd verify --help` → verify flag listing, exit 0; never executes verify.
- `fsgg-sdd frobnicate` → `unknownCommand` (unchanged); `fsgg-sdd frobnicate --help`
  → **`unknownCommand`** (unknown command detection wins — you cannot help on a
  command that does not exist), preserving FR-011.

**Alternatives rejected**:
- *Omitted-when-None `help`*: rejected — would require special-casing the
  "no documented field absent" conformance test and diverges from the scaffold/refresh
  convention for no real benefit (an additive null field is deterministic and
  byte-stable run-to-run).
- *Bespoke help renderer outside `CommandReport`* (as `validate` does with
  `ValidationContracts`): rejected — FR-010 requires help to use the same
  `CommandReport` three-way projection as other commands.
- *Add a `Help` case to `SddCommand`*: rejected — help is "not a command" (spec);
  a new case would churn `commandName`/`commandStage`/`parseCommand`/
  `nextLifecycleCommand`/`outcomeValue` and the surface baseline. The `Command = Init`
  envelope + `HelpSummary.scope` captures top-level help without a DU change.

---

## Cross-cutting invariants confirmed in Phase 0

- **No mtime anywhere** — all staleness is digest/version-based; safe to reason
  about purely from content.
- **`outcome` / exit codes** (`CommandReports.fs:1286-1296`, `:1368-1374`):
  any error → `Blocked`/exit 1 (provider-defect ids → 2); any warning →
  `SucceededWithWarnings`/exit 0; no changes → `NoChange`/exit 0.
- **No Governance touch** — none of the five fixes reads, writes, or versions a
  Governance contract; the Coordination item stays `Repo Scope: sdd` (FR-013).
- **Determinism harness** — `tests/FS.GG.SDD.Validation.Tests/DeterminismMatrixTests.fs`
  and `CommandReportJsonTests.fs` guard byte-stability; the `help` golden and the
  rewritten checklist/ship goldens must be regenerated through them.
