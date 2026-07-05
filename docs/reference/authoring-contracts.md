---
title: Authoring Contracts
category: SDD
categoryindex: 6
index: 20
description: The load-bearing authoring contracts an SDD author must satisfy — the requirement→acceptance coverage line, the evidence.yml kind/result vocabulary and satisfaction rule, and the specify --input intent facts.
---

# Authoring Contracts

Three SDD inputs are **load-bearing**: a small grammar decides whether the tool
accepts what you authored. Getting any of them subtly wrong produces a blocking
gate whose remedy used to require reading the CLI source. This page publishes the
exact accepted forms so you never have to decompile `fsgg-sdd` to clear a gate.

The canonical examples below are tagged so a drift-guard test
(`AuthoringDocsContractTests`) runs every accepted/rejected example through the
**live parser** on each build. If the tool's behavior and this page ever
disagree, the build fails — these contracts cannot silently drift.

> **Early-stage authoring.** A scaffolded product also carries
> `.fsgg/early-stage-guidance.md` (seeded by `fsgg-sdd init`), which restates the
> §1.1 coverage line and §1.2 evidence rule below alongside the per-stage commands,
> required headings, and stable-id formats for `charter`/`specify`/`clarify`/`checklist`
> — the guidance available before a work model exists.

## Acceptance coverage line

`fsgg-sdd checklist` marks a functional requirement **covered** only when a
*strict-scan* parser finds a list item that leads with `- FR-###:` and carries an
acceptance reference (`AC-###`, and optionally a `US-###`) **on the same line**.

- The list item must start with a literal `- `, then the id, then a literal `:`.
- The requirement id is `FR-` followed by **three or more digits**; matching is
  case-insensitive.
- The acceptance reference(s) must sit on that same line. They are collected as
  the requirement's coverage.
- There must be prose after the colon.

A separate *loose scan* (`\bFR-\d{3,}\b`) lists the requirement as an item, which
is why a requirement written in a non-matching form is **counted but reported
uncovered** — the form looks present but establishes no coverage.

Copyable accepted forms (each line establishes coverage):

```text coverage:accepted
- FR-001: W/S move the left paddle. (covers AC-002)
- FR-014: Ball serves toward the loser. (Stories: US-003; Acceptance: AC-009)
```

Forms that **do not** establish coverage (counted by the loose scan, invisible to
the strict scan):

```text coverage:rejected
**FR-001** W/S move the left paddle. (AC-002)
- FR-001 — moves the paddle (AC-002)
(covers AC-002)
```

- `**FR-001** …` — a **bold** id is not the required `- FR-001:` list item.
- `- FR-001 — moves the paddle …` — no **colon** after the id.
- `(covers AC-002)` on its own line — the acceptance reference is not on the
  `- FR-001:` line.

## `evidence.yml` declarations

> **Disambiguation.** This `evidence.yml` is the SDD **lifecycle** evidence
> contract — the authored declarations that satisfy the obligations
> `fsgg-sdd evidence`/`fsgg-sdd verify` emit. A product scaffolded by SDD may
> ship a *separate, unrelated* "evidence" document of its own that does **not**
> describe this contract; it is not the file documented here. When this page says
> "evidence," it always means the lifecycle `evidence.yml`.

Each entry under `evidence:` declares a `kind` and a `result`.

**`kind` vocabulary:** `implementation` · `verification` · `review` ·
`generated-view` · `synthetic` · `deferral` · `note` · `missing`
(`generatedview` is also accepted for `generated-view`). An **unrecognized
`kind` value silently becomes `verification`** — there is no error, so a typo
does not fail the build, it just records a different kind than you wrote.

**`result` vocabulary:** `pass` · `fail` · `deferred` · `missing` · `stale` ·
`advisory` · `blocked` (trimmed and lowercased before matching).

**Satisfaction rule.** An obligation is **satisfied** only by a matching
declaration whose `result` is `pass` **and** whose `synthetic` is `false`.

- `synthetic: true` with `result: pass` → disposition `synthetic`. A synthetic
  pass **discloses a stand-in and does not satisfy** the obligation.
- `result: deferred` (or `kind: deferral`) → disposition `deferred` — an accepted
  deferral, not a satisfaction.
- `result: fail`, `missing`, `stale`, `blocked` → not satisfied.

A copyable declaration that **satisfies** its obligation:

```yaml evidence:satisfied
schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    artifacts: [tests/FS.GG.SDD.Commands.Tests/EvidenceCommandTests.fs]
    result: pass
    synthetic: false
```

Declarations that **do not** satisfy (a synthetic pass and an outright fail):

```yaml evidence:unsatisfied
schemaVersion: 1
evidence:
  - id: EV001
    kind: synthetic
    subject:
      type: task
      id: T001
    result: pass
    synthetic: true
  - id: EV002
    kind: verification
    subject:
      type: task
      id: T002
    result: fail
    synthetic: false
```

## `specify --input` intent facts

`fsgg-sdd specify` drafts a new specification only when `--input` supplies three
facts: a **user value**, a **scope**, and a **measurable requirement**. The
parser reads `label: value` lines:

- `value: <user value>`
- `scope: <scope>`
- `requirement: <measurable requirement>`

An **unlabeled** line is read as the user value. Free prose with no labels
therefore yields only the user value, and `specify` reports `scope` and
`measurable requirement` as missing. Supply all three on their own labeled lines:

```text
value: A player can rally against a second human at one keyboard.
scope: Two-player local volley; no AI opponent, no online play.
requirement: A rally of 20 consecutive volleys completes without a dropped frame.
```

## Empty-section disclaimers (clarify & checklist)

Two lifecycle sections are scanned for outstanding work by **bullet**, so how you
write "there is nothing left" matters. The rule is the same for both:
a bullet is a **disclaimer** (contributes nothing) when — after stripping the
leading `- `/`*` — its text is empty, is the whole word `none` (optionally
qualified: `None.`, `None — all resolved`), or is a `No …` sentence naming an
outstanding noun (`findings`, `ambiguities`, `clarifications`, `issues`, …).
Anything else is a real, outstanding item.

Two disciplines keep genuine bullets from being mistaken for a placeholder: the
whole word `none` is required (so `Nonexistent flag behavior is unclear` stays a
real item), and a `No …` disclaimer must name an outstanding noun (so
`No tests cover FR-003` and `No decision yet on AMB-001` stay real, blocking
items — they are not placeholders).

### Clarify `## Remaining Ambiguity`

`fsgg-sdd clarify` counts a line under `## Remaining Ambiguity` as a **blocking
ambiguity** when it carries an `AMB-###`/`CQ-###` id and is neither a disclaimer
nor marked `deferred`/`non-blocking`. To say none remain, write a disclaimer —
do **not** re-list the resolved ids as bullets, because a bullet that names an
`AMB-###` is otherwise read as still-unresolved.

Copyable forms that leave **zero** blocking ambiguities:

```text remaining-ambiguity:disclaimer
- None. AMB-001, AMB-002, AMB-003 resolved above.
- No remaining ambiguities; AMB-001 resolved.
```

Forms that are counted as a **blocking** ambiguity (each blocks `checklist`/`plan`):

```text remaining-ambiguity:blocking
- AMB-001: The scoring rule is unresolved.
- No decision yet on AMB-001.
```

- `- None. AMB-001…` — a `none` disclaimer; the resolved ids it names do not
  block.
- `- AMB-001: …` — a real, unresolved ambiguity.
- `- No decision yet on AMB-001.` — a `No …` line naming no outstanding noun, so
  it is a genuine open item, not a placeholder.

### Checklist `## Blocking Findings`

`fsgg-sdd checklist` counts a bullet under `## Blocking Findings` as a finding
that blocks `plan` unless it is a disclaimer. A bare `- None.` is therefore safe.

Copyable forms that record **no** blocking finding:

```text blocking-findings:disclaimer
- None.
- No blocking findings.
```

Forms that record a real blocking finding (each blocks `plan`):

```text blocking-findings:finding
- No tests cover FR-003.
- Requirement FR-001 is missing acceptance coverage.
```

> **Reaching `checklistReady`.** There is no manual status transition to author. A
> clean `fsgg-sdd checklist` review writes `status: checklistReady` directly (an
> unclean one writes `needsCorrection`). If `plan` reports *"Checklist status '…'
> is not checklistReady"*, clear the blocking findings and **re-run
> `fsgg-sdd checklist`** — it re-promotes the status; do not hand-edit it.

## Clarify decision-tag resolution

`fsgg-sdd clarify` resolves an ambiguity carried from the spec only when its
`AMB-###` id appears on a **`DEC-###` line** under `## Decisions` **or**
`## Accepted Deferrals`, **and** that ambiguity is not left as a blocking bullet
under `## Remaining Ambiguity`. Both halves matter: the decision line is what
attaches the resolution; the `## Remaining Ambiguity` section is what the tool
counts for blocking (see the empty-section rule above).

The tool writes — and you should author — the canonical tagged form
`[AMB:AMB-###]` on the decision line. The bracket-with-prefix is a **convention**,
not a parser requirement: id extraction scans the raw line for a bare `AMB-###`
token, so `[AMB:AMB-001]`, `[AMB-001]`, and a bare `AMB-001` are all equivalent.
Use `[AMB:AMB-001]` — it is unambiguous and matches every generated artifact.

A concrete decision that **resolves** its ambiguity (parses with zero blocking
ambiguities, and the decision carries the AMB id):

```markdown clarify-decision:resolved
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
sourceSpec: work/001-authoring-contracts-guard/spec.md
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Decisions
- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]: The serve targets the prior-rally loser.

## Remaining Ambiguity
- None. AMB-001 resolved above.
```

An **accepted deferral** resolves the ambiguity too — recorded, not dropped — as
its own uniquely-id'd `DEC-###` under `## Accepted Deferrals`:

```markdown clarify-decision:deferred
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
sourceSpec: work/001-authoring-contracts-guard/spec.md
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Accepted Deferrals
- **DEC-002** [CQ-002] [AMB:AMB-002]: A match-end/win condition is deferred to a later work item — recorded, not dropped.

## Remaining Ambiguity
- None. AMB-002 accepted as a deferral above.
```

**An answer alone does not resolve.** Keying the answer under `## Answers` by its
`CQ-###`/`AMB-###` id produces an answer fact, but resolution is carried by the
decision **tag**, not the answer. The file below answers the question yet leaves
`AMB-001` listed under `## Remaining Ambiguity` with no tagged decision, so the
ambiguity stays blocking:

```markdown clarify-decision:answer-does-not-resolve
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
sourceSpec: work/001-authoring-contracts-guard/spec.md
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Answers
- CQ-001 → the serve goes to the player who lost the prior rally (resolves AMB-001).

## Remaining Ambiguity
- AMB-001: Serve target after a point is unresolved.
```

**Declare each `DEC-###` id exactly once.** Any line under `## Decisions` or
`## Accepted Deferrals` whose leading id is a `DEC-###` is a **declaration**; the
two sections are pooled, so declaring the same id in both raises
`duplicateClarificationId` (surfaced at the artifact layer as `duplicateIdentifier`).
Mentioning a `DEC-###` elsewhere — in `## Answers`, `## Remaining Ambiguity`, or
`## Lifecycle Notes` — is a safe **reference**. This file declares `DEC-002` twice
and is rejected:

```markdown clarify-dup:rejected
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
sourceSpec: work/001-authoring-contracts-guard/spec.md
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Decisions
- **DEC-002** [CQ-002] [AMB:AMB-002]: A match-end condition is deferred.

## Accepted Deferrals
- **DEC-002** [CQ-002] [AMB:AMB-002]: A match-end condition is deferred — recorded, not dropped.

## Remaining Ambiguity
- None. AMB-002 resolved above.
```

## Per-stage front matter

Every authored lifecycle artifact opens with a YAML `---` front-matter block (the
`tasks.yml`/`evidence.yml` artifacts carry the same scalars as a whole-document
header). A stage blocks with an *incomplete/malformed front matter* diagnostic
only when a field it actually **gates on** is absent. Other fields the templates
include are **defaulted** by the parser — their absence never blocks, though
authoring them keeps the artifact self-describing.

| Stage | Artifact | Gating fields (absence → blocked) | Defaulted (present in template, not gating) |
|---|---|---|---|
| charter | `charter.md` | `schemaVersion, workId, title, stage, changeTier, status` | — (charter is strict) |
| specify | `spec.md` | `schemaVersion, workId, stage` | `title`, `changeTier`→`tier1`, `status`→`draft`, `publicOrToolFacingImpact` |
| clarify | `clarifications.md` | `schemaVersion, workId, stage, sourceSpec` | `title`, `changeTier`, `status`→`needsAnswers`, `publicOrToolFacingImpact` |
| checklist | `checklist.md` | `schemaVersion, workId, stage, sourceSpec, sourceClarifications` | `title`, `changeTier`, `status`→`needsReview`, … |
| plan | `plan.md` | `schemaVersion, workId, stage, sourceSpec, sourceClarifications, sourceChecklist` | `title`, `changeTier`, `status`→`planned`, … |
| tasks | `tasks.yml` | `schemaVersion` (workId derivable from path) | `stage`→`Tasks`, `status`→`tasksReady`, source paths |
| evidence | `evidence.yml` | `schemaVersion`, a valid `workId` | `status`→`draft`, source paths |

Value rules the parser enforces:

- **`stage`** is the only closed vocabulary — one of `charter`, `specify`,
  `clarify`, `checklist`, `plan`, `tasks`, `analyze`, `implement`, `evidence`,
  `verify`, `ship`. Any other token fails to parse.
- **`schemaVersion`** major must be `1`.
- **`changeTier`** and **`status`** are **free strings** — the parser does not
  validate them against a fixed set (`tier1`/`tier2` and the status words above are
  conventions and defaults, not enforced vocabularies). Do not expect a
  "bad tier"/"bad status" rejection; there is none.

A minimal `clarify` front matter — only the four gating fields, `title`/`changeTier`/
`status` omitted — parses cleanly:

```markdown front-matter:clarify-minimal
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
sourceSpec: work/001-authoring-contracts-guard/spec.md
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Remaining Ambiguity
- None.
```

Drop a gating field — here `sourceSpec` — and the same stage blocks as incomplete:

```markdown front-matter:clarify-missing-required
---
schemaVersion: 1
workId: 001-authoring-contracts-guard
stage: clarify
---

## Source Specification
- work/001-authoring-contracts-guard/spec.md

## Remaining Ambiguity
- None.
```

### Source Snapshot digests are optional — and not a clarify concept

`clarifications.md` has **no** `## Source Snapshot` section and no `sha256:` field.
Source Snapshot lines (with an optional `sha256:`) belong to `checklist`/`plan`,
and the `tasks`/`evidence` artifacts carry an optional `sources[].digest`. Where a
digest is accepted it is **optional**, captured only when it is exactly 64 hex
characters, and used solely for **staleness detection** — a non-conforming
placeholder is silently ignored, never a blocking error, and a real digest is
never required to author a stage. The tool writes real digests; you are not
obligated to.

## Provider default-starter selection

`fsgg-sdd scaffold` selects a product *starter* through a provider-declared scaffold
parameter, never a first-class "profile" concept. A "starter" is just a parameter the
external template uses to pick what to generate; the **default starter** is that
parameter's `default` in the provider-owned `.fsgg/providers.yml` registry. SDD forwards
the effective value verbatim as `--<key> <value>` and never interprets, validates, or
enumerates the allowed starters — the provider owns which starters exist and which is
default.

Declare a default starter on the parameter (provider/author owns this registry):

```yaml
schemaVersion: 1
providers:
  - name: <provider>
    contractVersion: "1.0.0"
    templateId: <template-id>
    source: <template-source>
    parameters:
      - key: <starterKey>     # the parameter your template uses to select a starter
        required: false       # a default takes effect only for non-required/omitted params
        default: <value>      # the DEFAULT STARTER — forwarded when --param <starterKey> is omitted
```

Selection precedence (what `fsgg-sdd scaffold` does):

1. **Author omits the parameter** → the declared `default` is forwarded as
   `--<starterKey> <value>`; the author lands on the provider's intended default with no
   extra flags (FR-001).
2. **Author passes `--param <starterKey>=<other>`** → `<other>` is forwarded and the
   declared default is **not** applied; the explicit choice always wins (FR-002).
3. The **effective value** (declared default or override) is recorded in
   `.fsgg/scaffold-provenance.json` and the scaffold report `effectiveParameters`, so the
   chosen starter is auditable and the product reproducible (FR-003).

To **change** the default starter, edit the `default:` value in the registry. The next
unchanged `fsgg-sdd scaffold` run forwards the new default — **zero lines of generic SDD
code change**. The previous starter stays explicitly selectable via
`--param <starterKey>=<previous>`.

Boundaries: a `default` does **not** make a `required` parameter optional — an omitted
required value still surfaces `scaffold.providerParamMissing`. A blank/whitespace
`default` is surfaced as a blank declaration, never a silently invented value. The
canonical rendering registry (with its real default starter) is owned by FS.GG.Templates
and consumed only through the versioned provider contract and the network-gated
composition-acceptance — generic SDD carries no provider-specific starter value.
