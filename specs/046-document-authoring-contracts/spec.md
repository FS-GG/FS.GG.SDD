# Feature Specification: Document Load-Bearing Authoring Contracts & Self-Correcting Diagnostics

**Feature Branch**: `046-document-authoring-contracts`

**Created**: 2026-06-30

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → resolved to FS-GG/FS.GG.SDD#38 (`[cross-repo] Undocumented authoring contracts force decompilation (coverage line + evidence.yml)`), the next non-blocked SDD-owned item on the org Coordination board (status **Backlog**, phase **P2 SDD**, severity 🔴🔴🟠, parent epic FS-GG/.github#74 — TestSpec tutorial framework feedback). Scope confirmed with the requester: **documentation + diagnostics** (issue fixes 1–3); the optional coverage-parser relaxation (fix 4) is **out of scope** and the parsing grammars stay strict and unchanged.

## Context

A consumer agent ran the full SDD lifecycle as a first-time author (`fsgg-sdd` 0.2.1)
and hit two **load-bearing authoring contracts that are documented nowhere** — they
were only recovered by decompiling the shipped CLI. Both were re-verified against SDD
source during this investigation:

- **The requirement→acceptance coverage line.** `checklist` blocks `plan` until every
  `FR-###` has acceptance coverage, but nothing tells the author what *establishes*
  coverage. Two different parsers read the spec: a loose id-scan (`\bFR-\d{3,}\b`,
  which feeds CHK items) and a strict reference-scan
  (`^\s*-\s*(FR-\d{3,})\s*:` on the same line, which feeds coverage). The only accepted
  coverage shape is a plain list item with the id, a literal colon, and the acceptance
  reference on the same line — e.g. `- FR-001: W/S move the left paddle. (covers AC-002)`.
  Natural authoring forms (`**FR-001** …`, bracketed AC tags, `(covers AC-###)` without
  the leading `- FR-###:`) satisfy the loose scan but are invisible to the strict one, so
  the author sees the requirement counted yet uncovered with no explanation.
  *Verified:* `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Specification.fs` —
  `requirementReferences` (regex `^\s*-\s*(FR-\d{3,})\s*:\s*(.+)$`, same-line `AC-`/`US-`
  scan).

- **The `evidence.yml` vocabulary and the "satisfied" rule.** `evidence` emits
  obligations as `missing` and blocks `verify` until they are authored, but the valid
  `kind`/`result` values and the rule that makes an obligation count as satisfied are
  documented nowhere. An obligation is **satisfied** only when a matching declaration has
  a result that normalizes to `pass` **and** is **not** synthetic.
  *Verified:* `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs` (the
  `pass`/`synthetic` disposition ladder) and
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs` (`EvidenceKind`:
  `implementation | verification | review | generated-view | synthetic | deferral |
  note | missing`; unrecognized kinds silently fall back to `verification`).

Two further papercuts compound the pain:

- **Name collision on "evidence."** A scaffolded product can ship its *own* unrelated
  "evidence" documentation (an external provider's build/visibility concept). An author
  reading that doc is actively misled into thinking it describes SDD's lifecycle
  `evidence.yml`. SDD owns disambiguating **its own** evidence concept by name in its own
  authoring surface and diagnostics; it does not own or edit the external provider's docs.

- **Opaque `--input` intent validation.** `specify --input "<intent>"` fails with
  `missingSpecificationIntent` ("missing required facts: user value, scope, measurable
  requirement") regardless of prose, and the diagnostic never says **which** of the three
  facts it failed to find.

The cost is concrete: a first-run author cannot get through `checklist` and `verify`
without reverse-engineering the tool. This feature closes that gap two ways — by
**publishing the authoring contracts** as durable reference documentation, and by making
the **diagnostics self-correcting** so a failure shows the author the exact expected form
inline. Per the requester's scope decision, the parsing grammars themselves are **not
relaxed** in this feature; the contracts are documented and surfaced exactly as they are.

This is a **Tier 1** change — a command-output contract change. No public
structured-artifact **schema** changes (no JSON field is added, removed, or renamed) and
no parsing grammar changes. The enriched `message`/`correction` **string values** for the
three diagnostics *do* appear in the default JSON (both are serialized fields) and in the
persisted `checklist.md`; those golden fixtures are updated deliberately as part of this
feature. Diagnostic **codes**, severities, blocking status, exit codes, stream routing, and
the set of pass/fail outcomes are unchanged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author finds the coverage and evidence contracts without decompiling (Priority: P1)

A first-time author working through the lifecycle needs to know, from the project's own
documentation, exactly how to write an acceptance-coverage line and a valid
`evidence.yml` declaration — before they hit the gate, not after reverse-engineering it.

**Why this priority**: This is the load-bearing fix and the reason the issue is severity
🔴🔴🟠. Without published contracts, every new author repeats the decompilation. Reference
documentation alone removes the "forced decompilation" defect even if nothing else ships.

**Independent Test**: Hand the published authoring reference (and quickstart) to someone
who has never seen the CLI internals and have them author a spec coverage line and an
`evidence.yml` declaration that pass `checklist` and `verify` on the first attempt, using
only the documentation.

**Acceptance Scenarios**:

1. **Given** the authoring reference, **When** an author reads the coverage section,
   **Then** it states the exact accepted shape — a list item beginning `- FR-###:` with
   the acceptance reference (e.g. `AC-###`) on the same line — shows a copyable example,
   and explicitly calls out forms that do **not** establish coverage (bold `**FR-###**`,
   bracketed-only tags, a colon-less line), so the author knows why the natural forms fail.
2. **Given** the authoring reference, **When** an author reads the `evidence.yml` section,
   **Then** it lists every valid `kind` value, every meaningful `result` value, the rule
   that an obligation is satisfied only by a non-synthetic `pass`, and what `synthetic`
   and `deferral` declarations do — with a copyable example declaration.
3. **Given** the quickstart, **When** an author follows it end to end, **Then** the
   coverage line and a satisfying `evidence.yml` declaration appear at the lifecycle steps
   where they are first required (`checklist`, `evidence`/`verify`), so the happy path
   never depends on reading the deeper reference.
4. **Given** the documented forms, **When** they are exercised against the live CLI,
   **Then** every documented accepted form is accepted and every documented rejected form
   is rejected — the documentation matches the verified parser behavior, not aspiration.

---

### User Story 2 - A failing gate tells the author the exact form to write (Priority: P2)

When `checklist`, `evidence`/`verify`, or `specify --input` rejects the author's input,
the diagnostic itself should show the exact expected form and name precisely what is
missing, so the author can self-correct from the terminal without consulting docs.

**Why this priority**: Self-correcting diagnostics turn a dead-end into a one-step fix and
catch authors who never opened the reference. High value, but it builds on the contracts
that US1 establishes, so it is P2.

**Independent Test**: Author each failure (an uncovered requirement, an unsatisfied
evidence obligation, an intent string missing one fact), run the corresponding command,
and confirm the diagnostic names the offending item and shows the exact expected form,
such that following the diagnostic verbatim resolves the failure.

**Acceptance Scenarios**:

1. **Given** a requirement with no coverage line, **When** `checklist` runs, **Then** the
   diagnostic names the specific `FR-###` and shows the exact expected form (e.g. write it
   as `- FR-001: <text> (covers AC-002)`), rather than only reporting a missing-coverage
   count.
2. **Given** an unsatisfied evidence obligation, **When** `evidence`/`verify` runs,
   **Then** the diagnostic states what makes an obligation satisfied (a non-synthetic
   `pass` declaration matching the obligation) so the author knows what to author next.
3. **Given** `specify --input "<intent>"` that is missing one of the three required facts,
   **When** it fails with `missingSpecificationIntent`, **Then** the diagnostic names
   **which** fact(s) (user value, scope, measurable requirement) it could not find, not
   just the generic three-part list.
4. **Given** any of these enriched diagnostics, **When** the command is run in the default
   JSON projection, **Then** the diagnostic **code**, exit code, stream routing, and
   overall outcome are unchanged from today — only the human-readable correction text is
   richer (no automation-contract regression).

---

### User Story 3 - "Evidence" is unambiguous in the SDD authoring surface (Priority: P3)

An author who has seen an unrelated "evidence" document in a scaffolded product needs the
SDD documentation and diagnostics to make clear, by name, which "evidence" the lifecycle
means, so the two concepts are never conflated.

**Why this priority**: Disambiguation prevents a wrong-doc rabbit hole, but it only matters
once the SDD evidence contract is itself documented (US1). It is the smallest of the three
slices, hence P3.

**Independent Test**: Search the SDD authoring documentation for "evidence" and confirm
the lifecycle concept is consistently named and distinguished from any externally shipped
"evidence" concept, with a note that a scaffolded product may carry an unrelated document
of the same name.

**Acceptance Scenarios**:

1. **Given** the SDD authoring documentation, **When** an author reads about evidence,
   **Then** the lifecycle `evidence.yml` concept is named distinctly and a note warns that
   a scaffolded product may ship a separate, unrelated "evidence" document that does not
   describe this contract.
2. **Given** the boundary that SDD does not own external provider documentation, **When**
   this feature ships, **Then** disambiguation lives entirely within SDD's own authoring
   surface and diagnostics — no external provider's package name, path, or document is
   referenced or modified.

---

### Edge Cases

- A requirement appears in prose with a bold id (`**FR-001**`) but no `- FR-001:` line:
  it is counted by the loose scan yet uncovered by the strict scan. The reference and the
  diagnostic must both explain this exact mismatch (it is the trap that triggered the
  issue), even though the grammar is not being relaxed.
- An `evidence.yml` declaration uses an unrecognized `kind`: the documentation must note
  the silent fallback to `verification` so the author is not surprised by a kind that
  "works" but is not what they wrote.
- A `synthetic` declaration with `result: pass`: documentation and diagnostics must make
  clear this does **not** satisfy the obligation (it is disclosed as standing in for real
  evidence), so an author does not believe a synthetic pass closes the gate.
- An intent string missing two or three facts: the enriched `--input` diagnostic must name
  all the missing facts, not just the first.
- Output is non-interactive / redirected / color-disabled: enriched correction text must
  carry the same facts in plain form (the diagnostics are content, not rich presentation).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The project MUST publish durable authoring-reference documentation that
  states the exact accepted form of the requirement→acceptance coverage line: a list item
  beginning `- FR-###:` with the acceptance reference on the same line, including at least
  one copyable example.
- **FR-002**: The coverage documentation MUST explicitly enumerate common forms that do
  **not** establish coverage (bold `**FR-###**` ids, bracketed-only acceptance tags, and a
  line lacking the leading `- FR-###:` colon form) and explain why, so authors understand
  the loose-scan vs. strict-scan distinction.
- **FR-003**: The project MUST document the `evidence.yml` vocabulary: every valid `kind`
  value, the meaningful `result` values, and the rule that an obligation is satisfied only
  by a matching **non-synthetic** declaration whose result normalizes to `pass`.
- **FR-004**: The evidence documentation MUST describe the behavior of `synthetic` and
  `deferral` declarations (that a synthetic `pass` does not satisfy an obligation) and note
  that an unrecognized `kind` silently falls back to `verification`.
- **FR-005**: The quickstart MUST show a valid coverage line and a satisfying `evidence.yml`
  declaration at the lifecycle steps where each is first required, so the happy path does
  not depend on the deeper reference.
- **FR-006**: All documented accepted/rejected forms MUST match the verified live CLI
  behavior; the feature MUST include a check that keeps the documentation honest against
  the parsers (documentation drift from behavior is a defect).
- **FR-007**: The `checklist` missing-coverage diagnostic MUST name the specific
  uncovered `FR-###` and show the exact expected coverage form inline.
- **FR-008**: The `evidence`/`verify` unsatisfied-obligation diagnostic MUST convey what
  makes an obligation satisfied (a non-synthetic `pass` declaration matching the
  obligation).
- **FR-009**: The `specify --input` `missingSpecificationIntent` diagnostic MUST name
  which of the three required facts (user value, scope, measurable requirement) were not
  found, rather than emitting the generic list unconditionally.
- **FR-010**: Enriched diagnostics MUST NOT change diagnostic **codes**, severities,
  blocking status, exit codes, stream routing, the JSON **field set** (no field added,
  removed, or renamed), or the set of pass/fail outcomes. The enriched `message`/
  `correction` **string values** legitimately change in the default JSON and in
  `checklist.md`; those changes MUST be captured by deliberately updated golden fixtures
  and confined to the three diagnostics in scope.
- **FR-011**: SDD's authoring surface MUST name the lifecycle `evidence.yml` concept
  distinctly and warn that a scaffolded product may ship a separate, unrelated "evidence"
  document; disambiguation MUST stay within SDD-owned documentation and diagnostics.
- **FR-012**: The feature MUST NOT introduce any external provider's package id, template
  id, file path, or documentation URL into generic SDD behavior or docs, and MUST NOT
  relax or otherwise change the coverage or evidence parsing grammars.
- **FR-013**: Claude and Codex agent guidance MUST be updated equivalently wherever this
  authoring guidance is surfaced to agents, keeping the two surfaces aligned.

### Key Entities *(include if feature involves data)*

- **Coverage line**: An authored spec list item that links a functional requirement to its
  acceptance scenario(s) and is the sole input recognized by the coverage (strict-scan)
  parser. Attributes: requirement id, same-line acceptance reference(s), free text.
- **Evidence declaration**: An authored `evidence.yml` entry. Attributes relevant to
  authoring: `kind` (from the fixed vocabulary), `result` (normalized; `pass` is
  satisfying), `synthetic` flag, subject/obligation linkage. Disposition: satisfied only
  when a matching declaration is a non-synthetic `pass`.
- **Authoring diagnostic**: A diagnostic emitted by `checklist`, `evidence`/`verify`, or
  `specify --input`. Stable attributes: code, severity, blocking status, exit code.
  Mutable attribute in this feature: human-readable correction text.
- **Authoring reference**: The durable SDD documentation surface that publishes the
  coverage and evidence contracts (quickstart entry points plus a deeper reference).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A first-time author who has read only the published authoring reference can
  write a coverage line and an `evidence.yml` declaration that pass `checklist` and
  `verify` on the first attempt, with zero need to inspect CLI internals.
- **SC-002**: 100% of the coverage and evidence contracts that previously required
  decompilation to discover (the `- FR-###:` coverage form, the `kind`/`result`
  vocabulary, and the non-synthetic-`pass` satisfaction rule) are documented in the
  authoring reference.
- **SC-003**: For each of the three failure points (uncovered requirement, unsatisfied
  evidence obligation, intent missing a fact), following the on-screen diagnostic verbatim
  resolves the failure without consulting any other source.
- **SC-004**: The default JSON **structure** (field set, diagnostic codes, severities,
  blocking, exit codes, stream routing, pass/fail outcomes) is unchanged for every affected
  command in every representative state; the only JSON differences are the intended
  `message`/`correction` string values for the three enriched diagnostics, each captured by
  an updated golden fixture, and no unrelated golden regresses.
- **SC-005**: The documentation-vs-behavior check fails if any documented accepted form is
  rejected by the live parser, or any documented rejected form is accepted — so the
  reference cannot silently drift from the tool.
- **SC-006**: No external provider package id, template id, path, or documentation URL
  appears anywhere this feature adds or changes, and no parsing grammar changes.

## Assumptions

- "Next SDD-owned item" resolves to FS-GG/FS.GG.SDD#38: it is the lowest-numbered,
  non-blocked, SDD-owned Backlog item (severity 🔴🔴🟠), ahead of the other two
  non-blocked SDD items from the same epic (#39 lifecycle/CLI papercuts, #40 early-stage
  agent guidance bootstrap).
- Scope is documentation + diagnostics (issue fixes 1–3). The optional coverage-parser
  relaxation (fix 4 — accept bold ids / bracketed AC tags) is **out of scope** and tracked
  separately; this feature documents and surfaces the grammars exactly as they are.
- The misleading "evidence" document the issue cites is shipped by an external scaffold
  provider's product, which SDD does not own or edit; SDD's remedy is to disambiguate its
  own concept by name within its own authoring surface (FR-011).
- The verified source facts (coverage regex, evidence satisfaction ladder, `EvidenceKind`
  vocabulary, silent `verification` fallback) reflect `fsgg-sdd` 0.2.1 and were
  re-confirmed against `main` during specification; the plan will re-verify exact line
  references before changing diagnostic text.
- Enriched correction text is plain content carried in the existing diagnostic structure;
  it is not rich/Spectre presentation and therefore not excluded from deterministic
  contracts the way `--rich` rendering is.
