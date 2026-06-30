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
