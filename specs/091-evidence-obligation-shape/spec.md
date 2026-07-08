# Feature Specification: Slim the Evidence Declaration Shape (Omit Always-Null Optional Fields)

**Feature Branch**: `091-evidence-obligation-shape`

**Created**: 2026-07-08

**Status**: Draft

**Input**: User description: "fsgg-sdd evidence: when writing work/<id>/evidence.yml, omit the optional declaration fields that are almost always null (syntheticDisclosure, rationale, owner, scope, laterLifecycleVisibility) instead of emitting an explicit `null` line for each. Implements FS.GG.SDD#165 (origin FS.GG.Game workflow-feedback §WD2), sliced to the schema half — the terse `--satisfy` CLI form is deferred because it collides with the touch-set of FS.GG.SDD#163."

## Overview

`fsgg-sdd evidence` scaffolds and re-renders `work/<id>/evidence.yml`. Every evidence
declaration it writes carries five optional scalar/mapping fields that are, in practice, almost
always absent: `syntheticDisclosure`, `rationale`, `owner`, `scope`, and
`laterLifecycleVisibility`. The writer emits each of them as an explicit `null` line regardless.

The result is that a declaration block is dominated by fields that say nothing. The
decision-bearing content — `kind`, `result`, `synthetic`, `artifacts`, `notes` — is a small
minority of the bytes. At the 16–19 obligations per work item observed in the FS.GG.Game
feedback runs, that is roughly 80–95 lines of pure `null` boilerplate in a ~330-line file, and it
was escalated by the third feedback report to "the single highest-value ergonomic fix remaining."

This feature removes those lines. When an optional field has no value, the writer omits the key
entirely rather than writing `null`.

The reader already treats an **absent** key and a **plain `null`** key identically. Both
`tryScalarNonNullAt` (used for `rationale`, `owner`, `scope`, `laterLifecycleVisibility`) and
`parseSyntheticDisclosure` (used for `syntheticDisclosure`) yield `None` in either case. Omission
is therefore *parse-compatible in both directions*: a slimmed file parses to the same
`EvidenceDeclaration` the verbose file parsed to, and an existing verbose file — hand-authored or
written by an older CLI — continues to parse unchanged. No schema version is bumped and no
migration step is required. The one observable consequence is a **one-time normalization diff**:
the first `evidence` run after upgrading rewrites an existing verbose `evidence.yml` without the
`null` lines.

Feature 161's guarantee is preserved and strengthened: a bare `null` is the *absence* of a value
and must never round-trip into the quoted string `"null"`; a *quoted* `"null"` is a real string
value and must survive verbatim.

**Change tier: Tier 1** (artifact-layout change to the authored `evidence.yml` surface). No
`.fsi` change, no persisted schema-version change, no CLI surface change.

## Clarifications

### Session 2026-07-08

- Q: Is omitting an always-null optional key a schema-version-bumping contract change? → A: **No.**
  The `evidence.yml` reader resolves an absent key and a plain-`null` key to the same `None`
  (`tryChild` returns `None` for an absent key; `isPlainNullScalar` maps a bare `null` to `None`).
  Omission is a strict subset of what the parser already accepts, and every previously written
  verbose file still parses. `schemaVersion` therefore stays at its current value. Slimming is a
  *writer* change, not a schema change.
- Q: Which fields are in scope? → A: Exactly the five named in FS.GG.SDD#165 —
  `syntheticDisclosure`, `rationale`, `owner`, `scope`, `laterLifecycleVisibility`. Empty inline
  list fields (`taskRefs: []`, `requirementRefs: []`, …) are **out of scope**: an empty list is a
  distinct, meaningful "no refs" statement that the authoring contract asks the author to fill in,
  and dropping it would hide the field from a human editing the file.
- Q: What happens to a *populated* optional field? → A: It is written exactly as before. Only the
  `None` case is omitted. `syntheticDisclosure` still renders its nested `standsInFor`/`reason`
  mapping when a disclosure is present.
- Q: Does an existing `evidence.yml` containing explicit `null` lines have to be migrated? → A:
  **No migration step.** It parses unchanged. The next `evidence` run re-renders it in the slim
  form; that one-time diff is the whole migration. No `--migrate` flag, no schema gate, no
  Governance coordination.
- Q: Does the re-run byte-idempotence guarantee (issue #161) still hold? → A: **Yes**, and it is
  the primary regression guard. `render(parse(render(x))) == render(x)` for every declaration.
  After the first normalization, `evidence` re-runs are byte-identical.
- Q: Does a quoted `"null"` string value keep its quotes? → A: **Yes.** A quoted `"null"` is a
  real string, parses to `Some "null"`, and re-renders as the quoted string `"null"`. Only the
  `None` case (absent, or bare `null`/`~`/empty) is omitted. This is the exact 161 boundary and it
  is asserted directly.
- Q: Does the terse `fsgg-sdd evidence --satisfy T001=pass` form land here? → A: **No.** It touches
  `src/FS.GG.SDD.Cli/Program.fs`, which FS.GG.SDD#163 has declared in its touch-set
  (ADR-0021 intra-repo parallel work). It is deferred to a follow-up after #163 merges. This
  feature is the schema half only.
- Q: Does the `fs-gg-sdd-evidence` process skill need updating (and hence a `registry/skills.yml`
  sha reconcile)? → A: **No.** The skill documents the `kind`/`result`/`synthetic` satisfaction
  rule and vocabularies; it does not document the five omitted optional fields. Its body is
  unchanged, so its pinned `sha256` in the org registry stays valid.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read a scaffolded evidence.yml without wading through null lines (Priority: P1)

An author runs `fsgg-sdd evidence` on a work item with 16 obligations. The scaffolded
`work/<id>/evidence.yml` shows, for each declaration, only the fields that carry information: its
id, kind, subject, ref buckets, artifacts, result, synthetic flag, and notes. No `rationale: null`,
no `owner: null`, no `scope: null`, no `laterLifecycleVisibility: null`, no
`syntheticDisclosure: null`.

**Why this priority**: This is the entire value of the feature — the dominant per-item authoring
cost identified in both feedback datasets. Delivered alone, it is a complete, useful change.

**Independent Test**: Scaffold a fresh work item, render `evidence.yml`, and assert that none of
the five keys appears in the text while every decision-bearing key still does.

**Acceptance Scenarios**:

1. **Given** a work item whose obligations carry no rationale, owner, scope, later-lifecycle
   visibility, or synthetic disclosure, **When** `fsgg-sdd evidence` writes `evidence.yml`,
   **Then** none of the five optional keys appears anywhere in the file, and each declaration
   still emits `id`, `kind`, `subject`, `result`, `synthetic`, and `notes`.
2. **Given** the same work item, **When** the emitted file is parsed, **Then** every declaration's
   `SyntheticDisclosure`, `Rationale`, `Owner`, `Scope`, and `LaterLifecycleVisibility` are `None`
   — identical to what the verbose form parsed to.
3. **Given** a declaration block in the emitted file, **When** its lines are counted, **Then** it
   is at least five lines shorter than the pre-change rendering of the same declaration.

---

### User Story 2 - A populated optional field is still written (Priority: P1)

An author records a synthetic obligation with a `syntheticDisclosure`, or writes a `rationale` for
a deferral. Re-running `fsgg-sdd evidence` must not eat that content.

**Why this priority**: Omission that also drops *authored* values would be data loss, not
ergonomics. This story is what makes Story 1 safe to ship.

**Independent Test**: Author an `evidence.yml` with each of the five fields populated, re-run
`evidence`, and assert every value survives verbatim.

**Acceptance Scenarios**:

1. **Given** a declaration with `synthetic: true` and a populated `syntheticDisclosure`
   (`standsInFor` + `reason`), **When** `evidence` re-renders the file, **Then** the nested
   `syntheticDisclosure` mapping is written with both child keys intact.
2. **Given** a declaration with a populated `rationale`, `owner`, `scope`, and
   `laterLifecycleVisibility`, **When** `evidence` re-renders the file, **Then** all four scalar
   values are written unchanged.
3. **Given** a declaration whose `rationale` is the *quoted* string `"null"`, **When** `evidence`
   re-renders the file, **Then** the value is still the quoted string `"null"` — it is neither
   omitted nor unquoted (feature 161 boundary).

---

### User Story 3 - Backward compatibility and idempotent re-runs (Priority: P2)

A workspace holds an `evidence.yml` written by an older CLI, full of explicit `null` lines. The
new CLI must read it, and settle to a stable slim form.

**Why this priority**: The file is authored, committed, and read by downstream stages. A parse
regression or a never-settling diff would be worse than the boilerplate.

**Independent Test**: Parse a verbose fixture, assert the declarations match the slim fixture's,
then re-render twice and assert byte-equality.

**Acceptance Scenarios**:

1. **Given** an `evidence.yml` containing explicit `syntheticDisclosure: null`, `rationale: null`,
   `owner: null`, `scope: null`, and `laterLifecycleVisibility: null` lines, **When** it is parsed,
   **Then** it parses successfully and yields the same declarations as the slim form.
2. **Given** that verbose file, **When** `fsgg-sdd evidence` runs once, **Then** the file is
   rewritten in the slim form (the one-time normalization diff) with no diagnostic raised.
3. **Given** an already-slim file, **When** `fsgg-sdd evidence` runs twice, **Then** the two
   outputs are byte-identical (re-run byte-idempotence, issue #161).

---

### Edge Cases

- **All five fields populated on one declaration** — every key is written; nothing is omitted.
- **A declaration with `synthetic: true` but no disclosure** — the `syntheticDisclosure` key is
  omitted. The existing "synthetic evidence requires a disclosure" diagnostic still fires from the
  parsed model, not from the presence of a `null` line; omission must not suppress it.
- **A bare `~` or empty value** (`rationale:` with nothing after it) — parses to `None`, so the key
  is omitted on the next render, exactly as a bare `null` is.
- **A quoted `"null"` value** — a real string, retained and re-quoted.
- **A file where the last emitted key of a declaration was an omitted optional** — the declaration
  block must not end with a blank or trailing-whitespace line, and the next declaration must start
  cleanly. No stray blank lines anywhere in the emitted YAML.
- **A declaration with no optional fields at all** — the rendering must be well-formed YAML with
  `notes:` following `synthetic:` directly.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The evidence writer MUST omit the `syntheticDisclosure` key entirely when the
  declaration's `SyntheticDisclosure` is `None`, rather than writing `syntheticDisclosure: null`.
- **FR-002**: The evidence writer MUST omit the `rationale`, `owner`, `scope`, and
  `laterLifecycleVisibility` keys entirely when their respective values are `None`, rather than
  writing an explicit `null` for each.
- **FR-003**: The evidence writer MUST write each of the five fields unchanged when it holds a
  value, including the nested `standsInFor`/`reason` mapping of a populated `syntheticDisclosure`.
- **FR-004**: The emitted YAML MUST contain no blank line, trailing whitespace, or malformed
  indentation introduced by an omitted key; each declaration block MUST remain well-formed YAML
  that `parseEvidenceArtifact` accepts.
- **FR-005**: The evidence reader MUST continue to parse a document that carries any of the five
  keys with an explicit `null`, `~`, or empty value, yielding `None` — identical to the behavior
  when the key is absent. No schema version is bumped.
- **FR-006**: A `rationale` (or `owner`/`scope`/`laterLifecycleVisibility`) whose value is the
  *quoted* string `"null"` MUST parse to `Some "null"` and MUST re-render as a quoted string —
  never omitted and never unquoted (feature 161).
- **FR-007**: Two consecutive `fsgg-sdd evidence` runs over an already-slim `evidence.yml` MUST
  produce byte-identical output.
- **FR-008**: The `evidence` command's report, diagnostics, exit code, and obligation-disposition
  counters MUST be unchanged by this feature. Only the emitted `evidence.yml` bytes change.
- **FR-009**: Omitting the `syntheticDisclosure` key MUST NOT suppress any diagnostic that depends
  on a synthetic declaration lacking a disclosure; such diagnostics derive from the parsed model,
  not from the key's presence in text.

### Key Entities

- **EvidenceDeclaration**: the per-obligation record in `work/<id>/evidence.yml`. Carries
  decision-bearing fields (`kind`, `result`, `synthetic`, `artifacts`, `notes`, ref buckets) and
  five optional fields (`syntheticDisclosure`, `rationale`, `owner`, `scope`,
  `laterLifecycleVisibility`) whose `None` case this feature stops serializing.
- **Optional-field rendering rule**: the writer-side policy that maps `None` to *no line* and
  `Some v` to the field's existing rendering. Replaces the current `None -> "<key>: null"` rule.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A scaffolded `evidence.yml` with no populated optional fields contains **zero**
  occurrences of `syntheticDisclosure`, `rationale`, `owner`, `scope`, and
  `laterLifecycleVisibility`.
- **SC-002**: Each declaration block shrinks by exactly **5 lines** when all five optional fields
  are `None`; a 16-obligation work item's `evidence.yml` shrinks by **80 lines**.
- **SC-003**: `parseEvidenceArtifact` returns the same `EvidenceDeclaration list` for the verbose
  and slim renderings of the same content — asserted by a direct equality test, not by inspection.
- **SC-004**: Re-running `fsgg-sdd evidence` on a slim file produces byte-identical output
  (existing #161 idempotence test still passes, adapted to the slim expectation).
- **SC-005**: No `.fsi` signature, no `schemaVersion` value, and no `CommandReport` field changes.

## Assumptions

- The `evidence.yml` reader's treatment of absent-key ≡ plain-null is intentional and stable; it is
  relied upon rather than re-implemented. (Verified in
  `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`: `tryChild` / `isPlainNullScalar`.)
- No FS-GG consumer parses `evidence.yml` by key presence rather than by value. The org contract
  registry (`FS-GG/.github` `registry/`) holds `dependencies.yml`, `repos.yml`, and `skills.yml` —
  there is no registered `evidence.yml` obligation-shape contract row to coordinate against, and
  the `fs-gg-sdd-evidence` skill body does not mention the five fields.
- The one-time normalization diff on the first post-upgrade `evidence` run is acceptable and needs
  no flag, gate, or announcement beyond the changelog.
- The terse `--satisfy` authoring form and requirement→file obligation seeding (the other two
  options in FS.GG.SDD#165) are follow-up work, not part of this feature.
