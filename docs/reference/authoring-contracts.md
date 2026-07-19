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
acceptance reference (`AC-###`, and optionally a `US-###`) **on the same physical
line**.

- The list item must start with a literal `- `, then the id, then a literal `:`.
- The requirement id is `FR-` followed by **three or more digits**; matching is
  case-insensitive.
- The acceptance reference(s) must sit on that same **physical** line. They are
  collected as the requirement's coverage.
- There must be prose after the colon.

> **Keep the whole `- FR-###: … (covers AC-###)` on one physical line.** The scan
> reads a single line — it does not join a soft-wrapped bullet's continuation lines.
> If your editor wraps a long requirement across several physical lines and the
> `(covers AC-###)` marker lands on line two, the FR is reported **uncovered** even
> though the marker is right there. This is deliberate: coverage is an *explicit
> declaration on the FR line*, not an `AC-###` inferred from anywhere in the
> requirement's prose. Disable soft-wrap (or turn off "reflow paragraph") for the
> requirement line, or write the reference immediately after the id.

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
- FR-001: W/S move the left paddle, and the ball serves
  toward the loser when a point is scored. (covers AC-002)
```

- `**FR-001** …` — a **bold** id is not the required `- FR-001:` list item.
- `- FR-001 — moves the paddle …` — no **colon** after the id.
- `(covers AC-002)` on its own line — the acceptance reference is not on the
  `- FR-001:` line.
- The last two lines are **one soft-wrapped bullet**: a `- FR-001:` line whose
  `(covers AC-002)` marker wrapped onto the continuation line. Neither physical
  line establishes coverage — the first has no `AC-###`, the second is not a
  `- FR-###:` item — so the FR is reported uncovered. Keep the marker on the same
  physical line as the id.

## Functional-requirement classification

`fsgg-sdd` reads an **opt-in classification facet** off the functional-requirement
line (ADR-0048). A brace-delimited token from a **closed set** — currently just
`{gameplay}` — on the `- FR-###:` line tags that requirement with the class, and the
generated work model carries it as `requirements[].classification`, where the
downstream per-requirement gate and Governance select on it. An unannotated FR is
*unclassified* (the empty set).

- The vocabulary is a **closed set**: `{gameplay}`. It is **case-insensitive**
  (`{Gameplay}` is the same facet) and recognized only in **brace form** — a bare
  `gameplay` in prose is not an annotation.
- Write the token on the FR line, after the colon, alongside the coverage marker.
  Classification is **orthogonal to coverage**: the token does not change whether the
  line establishes coverage, and the coverage marker does not change the class.
  > **Keep it after the colon.** The `- FR-###:` grammar requires the colon
  > immediately after the id (see *Acceptance coverage line* above), so a token placed
  > *before* the colon (`- FR-001 {gameplay}: …`) makes the line parse as no
  > requirement at all — the FR then reports **uncovered**, not merely unclassified.
- The facet is **additive and non-blocking**, so every pre-ADR-0048 specification
  stays valid unchanged: an FR with no recognized token is unclassified, and a brace
  token that is **not** in the closed set (a typo, or braces used incidentally in
  prose) is **ignored**, never an error.

Copyable forms that classify the requirement `{gameplay}` (each also establishes its
coverage):

```text classification:gameplay
- FR-001: W/S move the left paddle. {gameplay} (covers AC-002)
- FR-014: Ball serves toward the loser. {gameplay} (Stories: US-003; Acceptance: AC-009)
```

Forms that leave the requirement **unclassified**:

```text classification:unclassified
- FR-002: The main menu lists saved games. (covers AC-003)
- FR-003: Difficulty is configurable. {difficulty} (covers AC-004)
```

- `- FR-002: …` names no brace token → unclassified.
- `- FR-003: … {difficulty} …` names a brace token that is **not** in the closed set
  → ignored, so the requirement stays unclassified.

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

## Stable id declarations

Every stable id (`FR-###`, `AC-###`, `US-###`, `AMB-###`, `CQ-###`, `DEC-###`,
`CHK-###`, `PD-###`, `T-###`, `EV-###`, work id, …) is **declared once** — at the
bullet that introduces it. Declaring the same id twice in one artifact is a
`duplicate…Id` block (the generic parser id is `duplicateIdentifier`, remapped per
stage to `duplicateSpecificationId` / `duplicateClarificationId` /
`duplicateChecklistId` / `duplicatePlanId` / `duplicateTaskId` / `duplicateEvidenceId` /
`duplicateWorkId`). Referencing an already-declared id **inside another bullet's prose**
is fine and is *not* a re-declaration — but note the clarify trap below, where a
`DEC-###` id mentioned inside *Accepted Deferrals* prose was historically miscounted;
the rule is one declaration site per id, references are unlimited.

`fsgg-sdd lint <artifact>` reports a duplicate declaration as a `duplicateId` defect
and points here.

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

## Framework-API references (`plan` / `analyze`)

Feature 105 (design of record ADR-0004) turns a framework-package API citation from
an un-checked backtick token into a **resolvable** reference the tool can check at
plan time. On a `## Contract Impact` line a `framework:` token declares a **use**; on
an `## Accepted Deferrals` line a `blocked-on-framework:` token declares an **absence
claim**:

```
- framework: <PackageId>[@<version>]#<symbol>
- CR-003 blocked-on-framework: <PackageId>[@<version>]#<symbol>
```

- `<PackageId>` is the NuGet id (e.g. `FS.GG.UI.SkiaViewer`); `#<symbol>` is the
  module-qualified `val`/member (e.g. `SkiaViewer.runAppWithPersistence`); `@<version>`
  is **optional** and, when omitted, defaults to the Central Package Management pin in
  `Directory.Packages*.props`. The version is single-sourced from the pin so a reference
  never duplicates (nor drifts from) the pinned version.
- A `framework:`/`blocked-on-framework:` keyword present with a token that is **not**
  this grammar — no `#symbol`, or an empty `PackageId` — is a **blocking**
  `malformedFrameworkReference` at `plan`, never a silent non-match. A mis-typed
  reference reading as "no reference" is exactly the failure mode this defeats.

`analyze` resolves each reference against the pinned package's **committed captured
surface** — `docs/dependency-surface/<PackageId>/<version>.json`, produced by
`fsgg-sdd dependency-surface --update` from the real restored package (never a vendored
`.fsi` snapshot). The verdicts are symmetric, and fail-open:

| reference | symbol in the real surface? | verdict |
|---|---|---|
| a `framework:` **use** | yes | passes |
| a `framework:` **use** | no | **blocks** — `frameworkApiDangling` (blocked on a framework change) |
| a `blocked-on-framework:` **deferral** | yes | **blocks** — `frameworkApiDeferralContradicted` (the deferral's premise is false) |
| a `blocked-on-framework:` **deferral** | no | passes — the deferral is legitimate |
| any | no capture / unresolvable version | **advisory** — `frameworkApiSurfaceUnavailable` (exit 0) |

The last row is the settled severity policy: **block on real contradictions, advise
when blind.** "Could not look" — no capture committed, or the run cannot resolve the
version — is never rendered as a negative verdict (org ADR-0002 / #266). This defeats
both the genuinely dangling reference and the inverse false alarm (a real API mis-read
as absent from a stale local view — the RM2 incident this feature was filed for).

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

## Derived task skills (`project.testFramework`, `project.implementSkill`)

`tasks` stamps a `requiredSkills:` list onto each task it derives. Two of those skills are
**declared by the workspace**, in `.fsgg/project.yml`, rather than fixed by the tool:

| Declaration | Used by | Neutral default when absent |
| --- | --- | --- |
| `project.testFramework` | verification-obligation (`VO-###`) tasks | `automated-tests` |
| `project.implementSkill` | requirement, clarification-decision, and plan-decision tasks | `implementation` |

```yaml
schemaVersion: 1
project:
  id: my-product
  defaultWorkRoot: work
  testFramework: expecto
  implementSkill: speckit-implement
```

Both are optional, trusted verbatim (SDD keeps no allow-list), and normalized the same way —
trimmed, lowercased, internal whitespace runs collapsed to `-`, so `My Custom Runner` becomes
`my-custom-runner`. A missing, empty, or whitespace-only value declares nothing and degrades to
the neutral default; no other task category's skills are affected by either declaration.

Before FS.GG.SDD#310 the implement skill was the hardcoded literal `speckit-implement` — SDD's
own authoring toolchain leaking into every consumer's task graph. Declare `implementSkill` to
name the skill your agents actually have.

## The visual-inspection obligation (`project.visualSurface`)

Every obligation SDD derives descends from a lifecycle fact id — an `FR-###`, a `DEC-###`, a
`PD-###`, a contract or migration id. The graph is therefore **closed over requirements**, and a
defect that lives in the *conjunction* of two requirements is in no requirement at all. A spec that
puts a wall at `y = 16`, an opaque status band over `y ∈ [0, 48]`, and draws the band last is
internally satisfiable, fully covered, and renders an invisible ball. No requirements-derived gate
can reach it.

A workspace whose product renders something a human must look at declares so:

```yaml
schemaVersion: 1
project:
  id: my-product
  defaultWorkRoot: work
  visualSurface: true
```

The declaration is a **boolean, and value-agnostic** — you decide what a visual surface is (a scene
graph, a terminal frame, a plot); SDD never learns why. It is optional; absent, blank, non-boolean,
or in an unparseable config, it reads `false` and nothing changes.

When it is `true`:

| Stage | Effect |
| --- | --- |
| `checklist` | one **advisory** (non-blocking) review row prompting for the between-requirements defect class — draw order vs geometry, overlapping bands, z-order vs collision bounds |
| `tasks` | one derived task `Inspect a rendered frame`, `requiredSkills: [<implement skill>, visual-inspection]`, empty `sourceIds:` (it descends from no fact id — that is the point), and one `requiredEvidence` obligation |
| `evidence` / `verify` | that obligation is satisfied only by `result: pass` ∧ `synthetic: false` ∧ **a named rendered artifact** |

The artifact is an `artifacts:` entry, or a `sourceRefs[]` entry carrying a `path` or a `uri`. A
non-synthetic `pass` that names neither blocks with `evidence.missingVisualInspectionArtifact`: it
asserts that someone looked at a frame that does not exist. A `synthetic: true` pass never satisfies
it, as for every other obligation, and an accepted deferral (with all four deferral fields) is a
first-class outcome that the artifact rule does not touch.

SDD owns the obligation, never the renderer: it ships no `render` command, embeds no rendering API,
and does not check that the named file is an image.

**Migration.** Additive and opt-in; the schema stays v1. A workspace that never declares
`visualSurface` sees no change to any artifact. Withdrawing the declaration drops the derived task
under the existing orphan rule, so remove its `evidence.yml` entry — the same cleanup a folded
`PD-###` requires (see below).

## Plan decisions that mirror a requirement earn no task of their own

`plan` auto-derives exactly one `PD-###` per functional requirement, and that derived decision
mirrors the requirement's own refs. `tasks` therefore does **not** emit a separate
`Implement plan decision PD-001` task over the same `FR-001` / `AC-001` set — it *folds* the
`PD-001` id into the requirement task's `sourceIds:`, which is what disposes it:

```yaml
tasks:
  - id: T001
    title: Implement requirement FR-001
    requirements: [FR-001]
    sourceIds: [AC-001, FR-001, PD-001]   # PD-001 folded in — no duplicate task
```

The rule: a `PD-###` whose refs are **subsumed** by some requirement task's refs is folded into
that task. A `PD-###` that refs anything no requirement task covers — an accepted deferral, a
contract, a decision you wrote by hand — still earns its own task. A `PD-###` with no refs at
all is subsumed by nothing and also keeps its task.

Folding, not dropping, is the point. A `PD-###` is a **required disposition**: `analyze` demands
that some task dispose it. Removing the duplicate task without carrying its id forward would
block `analyze` with `missingDisposition` two stages later.

The practical consequence for authors: a work item with *n* requirements produces *n* tasks for
them, not *2n*. If you are migrating an existing `tasks.yml`, the surviving tasks keep their
`T###` ids (the merge matches on title), the folded ones disappear, and any `evidence.yml`
declaration whose `subject.id` names a folded task must be removed — its obligation is now
carried by the requirement task's own evidence entry.

## Regeneration semantics (re-running `checklist` and `tasks`)

`checklist.md` and `tasks.yml` are **generated gate artifacts** you also author against.
Re-running their stage regenerates the tool-owned content from the current sources and
preserves your authored content — it never re-ingests its own prior output as input
(feature 082 / FR-002).

All seven lifecycle artifacts work this way, and every command report names it: each is
reported with write kind `hybridArtifact` and ownership `hybrid` (FS.GG.SDD#308). The
remaining write kinds are `generatedView` (the tool alone produces it), `structuredSource`
and `agentGuidance` (seeded once, never clobbered), and `authoredSource` — which the tool
**never** writes. An effect that claims `authoredSource` over existing content is refused by
the interpreter, so a stage cannot acquire the ability to replace your prose by mis-tagging
a write.

- **Tool-owned content is re-derived every run, never re-ingested.** `checklist`
  recomputes its `Checklist Items` / `Review Results` / `Accepted Deferrals` /
  `Blocking Findings` rows (and refreshes `Source Snapshot`) from the current spec/clarify
  facts on every run — so a `CHK-###` blocking row clears as soon as the source no longer
  justifies it (e.g. once the FR is covered in `spec.md`), with no file to delete. `tasks`
  re-derives the whole task graph, so a newly-added plan decision disposition appears
  immediately instead of the run reporting the graph "stale" and leaving it unchanged.
- **Authored content is preserved.** `checklist`'s authored sections (`Advisory Notes`,
  `Lifecycle Notes`, the `Source Specification`/`Source Clarifications` mirrors) are left
  untouched. In `tasks.yml`, a task's advanced `status` (`inProgress`/`done`/`skipped`),
  its `owner`, any **hand-added disposition refs** you wrote (`requirements` /
  `decisions` / `sourceIds`, e.g. `decisions: [DEC-001]` to record a decision's
  disposition), and any **hand-added `requiredSkills`** are carried onto the re-derived
  task, and a wholly hand-authored task is kept when it uniquely covers a live disposition
  the derivation cannot. Authored refs and tasks whose sources are gone — or already covered
  by derivation — are dropped, so nothing stale accumulates.

  `requiredSkills` is **unioned**, not replaced: the derived skills always reappear
  alongside the ones you added. There is no id universe to check a skill against, so unlike
  the ref fields nothing is ever dropped from it as dead — including a skill that a *previous*
  CLI derived. If you change `project.implementSkill`, the old value stays on tasks that
  already carry it until you remove it by hand.
- **A task's refs: all three fields are authored, and the parser reads each verbatim.** Write the
  typed fields. They carry the FR/DEC distinction `analyze`'s disposition relationships rely on,
  and they are what a human reads. You write `sourceIds:` by hand for a reference the typed fields
  cannot express: an acceptance scenario `AC-###`, a checklist result `CR-###`, a generated-view
  impact `GV-###`, a plan contract reference `PC-###`, a plan decision `PD-###`, a plan migration
  note `PM-###`, a scope boundary `SB-###`, a verification obligation `VO-###`. (Names per
  `Identifiers.create*`; the checklist *item* is `CHK-###`.)

  **`sourceIds` is not a derived superset.** `tasks.yml`'s parser stores each of the three fields
  exactly as authored; it unions nothing. Each *consumer* unions the three as it needs them —
  `analyze`'s disposition set, `evidence`'s scaffolded ref buckets, `verify`'s `affectedSourceIds`,
  and the generated agent guidance's `relatedIds` all read
  `sourceIds ∪ requirements ∪ decisions`. The union deliberately lives at the consumer and **not**
  at the parser. Each field already answers to its *own* gate, against its *own* set of known ids:

  | field | gate | where |
  |---|---|---|
  | `sourceIds:` | `unknownTaskSourceReference` | the `tasks` stage (`taskValidationDiagnostics`) |
  | `requirements:` / `decisions:` | `unknownReference` + `workModelInconsistent` | work-model generation (`WorkModel.referenceDiagnostics`) |

  Unioning on parse would fold the typed fields into `sourceIds` and so subject them to the *tasks*
  gate **as well** — a second validation, against a different known-id set, that they have never
  faced. That turns an untouched, previously-green `tasks.yml` red with no `schemaVersion` change to
  explain it. It was tried and reverted once; do not re-derive it.

  The generator **does** restate ids across fields, and always writes the `sourceIds:` key. A
  derived requirement task carries `requirements: [FR-001]` *and* `sourceIds: [AC-001, FR-001]`
  (`TaskGraphAuthoring.requirementTasks` passes `requirement :: acceptance` as the source ids);
  a derived clarification-decision task carries the same `DEC-###` in both `decisions:` and
  `sourceIds:`. The consumer-side unions dedupe, so the restatement is invisible downstream —
  but do not read a task's `sourceIds:` as "only the ids the typed fields cannot express."
  An explicit entry you write is always kept; the derivation only ever adds.
- **Coverage lives in `spec.md`.** The `(covers AC-###)` declaration a checklist verdict
  reads is the `spec.md` requirement-reference line (see *Acceptance coverage line* above),
  **not** a line in `checklist.md`. Fix a "missing acceptance coverage" verdict there.
- **Hand-edited generated rows are reclaimed.** Tool-owned rows are not an authoring
  surface: if you edit one by hand (e.g. inject a task dependency), the next run overwrites
  it with the derived value.
- **Idempotence.** With unchanged sources and no authored edits, a re-run is byte-identical
  and reports `noChange`. (A file produced by an older CLI may be canonicalized once on the
  first re-run, then stabilizes.)
- **The only re-run that blocks** is the `<!-- fsgg-sdd: unsafe-overwrite -->` opt-out,
  whose `unsafeOverwrite` diagnostic names the exact file to delete and command to re-run —
  no supported recovery ever requires guessing an `rm`.
