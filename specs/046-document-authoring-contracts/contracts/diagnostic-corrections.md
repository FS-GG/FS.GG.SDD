# Contract: Diagnostic Correction Enrichment

Three diagnostics get a richer `Correction` (and, where noted, no `Message` change). For each:
the **code is invariant**, the **Correction string changes**, and golden fixtures are updated.
Exact wording is finalized in implementation; the contract below fixes the *required content* and
the *invariants*.

## Site 1 — checklist missing-coverage (FR-007)

- **Where**: `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingMid.fs`, `plannedChecklistReviews`,
  the `fail` branch (current site `ParsingMid.fs:163`; re-confirm via T001) of the requirement review.
- **Invariant**: review status stays `fail`, `Blocking = true`, the review still names the
  specific `FR-###`, and the CHK/CR id allocation is unchanged.
- **Today**: `Correction = "Add an acceptance scenario for {FR} or narrow the requirement."`
- **Required content (after)**: must show the **exact expected coverage form** inline, e.g.
  `Add a coverage line for {FR}: "- {FR}: <text> (covers AC-###)" on a single list item
  (a bold "**{FR}**" or a colon-less line is not recognized).`
- **Golden impact**: `ChecklistCommandTests.fs` (+ any work-model/`checklist.md` golden that
  embeds this correction).

## Site 2 — evidence unsatisfied obligation (FR-008)

- **Where**: `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersEvidence.fs`, `evidenceObligations`,
  the obligation `Correction` (~line 183).
- **Invariant**: obligation id/kind/linkage and the `missing` disposition behavior unchanged; the
  obligation still references the linked task.
- **Today**: `Correction = "Add evidence declaration {id} or an accepted deferral linked to
  {task}."`
- **Required content (after)**: must convey **what makes an obligation satisfied** — a matching,
  **non-synthetic** declaration whose `result` is `pass` — e.g. `Add evidence {id} for {task}
  with result: pass and synthetic: false (a synthetic pass does not satisfy it), or an accepted
  deferral.`
- **Golden impact**: `EvidenceCommandTests.fs` (+ `VerifyCommandTests.fs`/work-model goldens that
  surface the obligation correction).

## Site 3 — missingSpecificationIntent (FR-009)

- **Where**: `src/FS.GG.SDD.Commands/CommandReports.fs`, `missingSpecificationIntent` (~line 151).
- **Invariant**: code `missingSpecificationIntent`, severity error, exit unchanged. The
  **Message already names the specific missing facts** (computed in `ParsingEarly.fs`) — keep it.
- **Today**: `Correction = "Provide input with value, scope, and requirement facts before
  creating a new specification."`
- **Required content (after)**: must show the **exact labeled form** the `--input` parser accepts,
  e.g. `Provide --input with labeled facts, one per line: "value: <user value>", "scope: <scope>",
  "requirement: <measurable requirement>".`
- **Note**: No change to `ParsingEarly.fs` fact computation (already correct, R3). No parser
  behavior change.
- **Golden impact**: `SpecifyCommandTests.fs` (+ `CommandReportJsonTests.fs`/`TextProjectionTests.fs`
  if they assert this correction).

## Global invariants (FR-010 / SC-004)

- No diagnostic **code** added, removed, or renamed.
- No JSON **field** added, removed, or renamed; no schema-version bump.
- Severities, blocking flags, exit codes, stream routing (stdout/stderr), and pass/fail outcomes
  unchanged for every affected command in every representative state.
- The only JSON differences are the three enriched `Correction` strings (and they appear
  identically in default JSON, `work-model.json`/readiness views, and `checklist.md`).
- No external provider package id / template id / path / docs URL introduced (FR-012).
