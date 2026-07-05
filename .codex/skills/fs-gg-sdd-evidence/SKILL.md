---
name: fs-gg-sdd-evidence
description: Stage 8 of the FS.GG SDD lifecycle — fsgg-sdd evidence authors work/<id>/evidence.yml declaring how each obligation is satisfied. Carries the load-bearing satisfaction rule (only result:pass AND synthetic:false satisfies) and the kind/result vocabularies. Use after implementing, before verify.
---

# Evidence (stage 8)

`evidence` is where you declare what **proves** each obligation your tasks created
— after the code and tests exist. It is one of the two load-bearing authoring
contracts: a subtly wrong declaration leaves an obligation unsatisfied even though
the work is done.

## Command

```text
fsgg-sdd evidence --work <id>
fsgg-sdd evidence --work <id> --from-tests <path>   # pre-map new obligations to a proving test file
```

## Produces / consumes

- **Consumes:** `readiness/<id>/analysis.json` and the task obligations.
- **You author:** `work/<id>/evidence.yml`.
- **Tool refreshes:** `readiness/<id>/work-model.json`.
- **Next:** `verify` ([[fs-gg-sdd-verify]]).

## Auto-scaffolded obligations carry their origin refs

Each scaffolded obligation already records the `requirementRefs` and `planDecisionRefs` it
descends from — routed from the originating task's source lineage (so a `PD-###` plan-decision
obligation carries its `PD-###` **and** the `FR-###` it traces to). You can classify each
obligation honestly from its `evidence.yml` entry alone, with **no join back to `tasks.yml` by
task title**. (Other ref buckets stay empty on scaffolds; add them by hand if you want.)

`--from-tests <path>` additionally seeds each **newly scaffolded** obligation with a
verification-kind source pointing at `<path>` (a declared pointer; its existence is checked at
`verify`). It is inert without the flag. Neither behavior overwrites an obligation you have
already authored (no-clobber).

## The satisfaction rule (load-bearing)

> An obligation is **satisfied** only by a matching declaration whose `result` is
> `pass` **and** whose `synthetic` is `false`.

- `synthetic: true` + `result: pass` → disposition **synthetic**: discloses a
  stand-in, does **not** satisfy.
- `result: deferred` (or `kind: deferral`) → an accepted **deferral**, not a
  satisfaction.
- `result: fail | missing | stale | blocked` → not satisfied.

## Vocabularies

- **`kind`:** `implementation · verification · review · generated-view ·
  synthetic · deferral · note · missing` (`generatedview` also accepted). An
  **unrecognized `kind` silently becomes `verification`** — a typo does not fail
  the build, it just records a different kind than you wrote. Spell it exactly.
- **`result`:** `pass · fail · deferred · missing · stale · advisory · blocked`
  (trimmed and lowercased before matching).

## Example: a declaration that SATISFIES

```yaml
schemaVersion: 1
evidence:
  - id: EV001
    kind: verification
    subject:
      type: task
      id: T001
    artifacts: [tests/Product.Tests/InputMapTests.fs]
    result: pass
    synthetic: false
```

## Example: declarations that DO NOT satisfy

```yaml
schemaVersion: 1
evidence:
  - id: EV001
    kind: synthetic
    subject: { type: task, id: T001 }
    result: pass
    synthetic: true       # discloses a stand-in → disposition synthetic, NOT satisfied
  - id: EV002
    kind: verification
    subject: { type: task, id: T002 }
    result: fail
    synthetic: false      # not satisfied
```

A subject may be a `task` or a `requirement` (e.g.
`subject: { type: requirement, id: FR-002 }`); `artifacts`/`source` point at the
real test or proof, and `note` documents context.

## Disambiguation

This lifecycle `evidence.yml` is the **SDD evidence contract**. A product
scaffolded by SDD may ship a *separate, unrelated* "evidence" document of its own
(e.g. a rendering product's visual-evidence report) — that is **not** this file.

## Real-vs-synthetic discipline

Prefer real filesystem/process/schema artifacts. When a synthetic stand-in is
unavoidable, set `synthetic: true` and disclose what real path it stands in for —
the tool will correctly record it as not-satisfying rather than letting a stand-in
masquerade as proof.

## Pitfalls

- A typo in `kind` (e.g. `test`) silently becomes `verification` — your declared
  kind is lost. Use the exact vocabulary.
- Marking work `synthetic: true` and expecting it to satisfy — it never does.

## Next

- `verify` — evaluate verification readiness over your evidence: [[fs-gg-sdd-verify]].

## Related

- [[fs-gg-sdd-authoring-contracts]] (full grammar + drift guard),
  [[fs-gg-sdd-tasks]] (the obligations), [[fs-gg-sdd-lifecycle]].

## Sources

- `docs/reference/authoring-contracts.md` (§ evidence.yml declarations);
  `docs/quickstart.md`.
