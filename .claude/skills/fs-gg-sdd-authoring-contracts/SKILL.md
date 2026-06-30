---
name: fs-gg-sdd-authoring-contracts
description: Reference for the three load-bearing FS.GG SDD authoring grammars that silently block a stage if mis-formatted — the FR→AC checklist coverage line, the evidence.yml kind/result/synthetic satisfaction rule, and the specify --input intent facts. Use when a checklist/evidence/specify stage blocks unexpectedly.
---

# Authoring Contracts (the gating grammars)

Three SDD inputs are **load-bearing**: a small grammar decides whether the tool
accepts what you authored, and a subtly wrong form produces a blocking gate. This
skill is the quick reference; the durable, drift-guarded source is
`docs/reference/authoring-contracts.md` (every example below is run through the
**live parser** on each build by `AuthoringDocsContractTests`, so it cannot drift).

If a stage blocks and you "know" the content is right, it is almost always one of
these three grammars.

## 1. Checklist coverage line (used by `checklist`)

A functional requirement is **covered** only when a strict-scan parser finds a list
item that leads with `- FR-###:` and carries its acceptance reference **on the same
line**:

- literal `- `, then `FR-` + **three or more digits** (case-insensitive), then a
  literal `:`, then prose, with `AC-###` (optionally `US-###`) on the same line.

**Accepted:**

```text
- FR-001: W/S move the left paddle. (covers AC-002)
- FR-014: Ball serves toward the loser. (Stories: US-003; Acceptance: AC-009)
```

**Counted but NOT covered** (a loose scan `\bFR-\d{3,}\b` lists it, so it looks
present but establishes no coverage):

```text
**FR-001** W/S move the left paddle. (AC-002)     ← bold id, not a "- FR-001:" item
- FR-001 — moves the paddle (AC-002)              ← no colon after the id
(covers AC-002)                                    ← AC ref on its own line
```

See [[fs-gg-sdd-checklist]].

## 2. `evidence.yml` satisfaction (used by `evidence`/`verify`)

> An obligation is **satisfied** only by a matching declaration whose `result` is
> `pass` **and** whose `synthetic` is `false`.

- `synthetic: true` + `result: pass` → disposition **synthetic**, does not satisfy.
- `result: deferred` (or `kind: deferral`) → accepted deferral, does not satisfy.
- `result: fail | missing | stale | blocked` → not satisfied.

**`kind`:** `implementation · verification · review · generated-view · synthetic ·
deferral · note · missing` (`generatedview` also accepted). **An unrecognized
`kind` silently becomes `verification`** — spell it exactly.
**`result`:** `pass · fail · deferred · missing · stale · advisory · blocked`.

**Satisfies:**

```yaml
schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject: { type: task, id: T001 }
    artifacts: [tests/Product.Tests/InputMapTests.fs]
    result: pass
    synthetic: false
```

See [[fs-gg-sdd-evidence]].

## 3. `specify --input` intent facts (used by `specify`)

`specify` drafts a spec only when `--input` supplies three labeled facts; an
unlabeled line is read as the user value only, so free prose reports `scope` and
`measurable requirement` missing.

```text
value: A player can rally against a second human at one keyboard.
scope: Two-player local volley; no AI opponent, no online play.
requirement: A rally of 20 consecutive volleys completes without a dropped frame.
```

See [[fs-gg-sdd-specify]].

## Why these are strict

SDD's doctrine is that **structured artifacts are the machine contract** — the
strict scans exist so coverage and evidence are facts a tool can trust, not prose
a reader has to interpret. The strictness is the feature; these few forms are the
price.

## Related

- [[fs-gg-sdd-checklist]], [[fs-gg-sdd-evidence]], [[fs-gg-sdd-specify]],
  [[fs-gg-sdd-lifecycle]].

## Sources

- `docs/reference/authoring-contracts.md` (whole file; drift-guarded by
  `AuthoringDocsContractTests`).
