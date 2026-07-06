# Contract: Lifecycle-Status Footer

Three projections of one fact. **JSON is authoritative**; text and rich add/drop no facts.

## 1. JSON contract (`--json`, default) — authoritative

Additive object `lifecycleStatus`, always present on every command report.

```
lifecycleStatus: object (required)
  workId:           string | null          # resolved work id, null if none in scope
  isLifecycleStage: boolean                # false for init/agents/refresh/scaffold/doctor/upgrade/lint/validate
  currentOrdinal:   integer | null         # 1..10 for a lifecycle stage; count-of-done for cross-cutting; null if no position
  totalStages:      integer                # constant 10
  outcome:          string                 # echo of report.outcome
  nextCommand:      string | null          # next lifecycle command name, or null at terminal/all-done
  stages:           array[10] of object    # canonical order, ordinal 1..10
    command:        string                 # stage command name (charter … ship)
    ordinal:        integer                # 1..10
    state:          "done"|"current"|"next"|"pending"|"blocked"
```

Contract rules:
- **Additive**: `schemaVersion` stays `1` (Stable). `reportVersion` → `"1.1.0"`. Field recorded in `docs/release/schema-reference.md` inventory and `release-readiness.json`. Consumers MUST tolerate its presence; consumers that ignore it are unaffected.
- **Byte-stable**: field ordering and formatting deterministic; identical on-disk state + command ⇒ identical bytes.
- **No failure sub-object**: on a blocked outcome, the explanation/options are NOT added here — they are read by the text/rich projections from the report's existing `diagnostics` and `nextAction` (which are already in this same JSON).

## 2. Plain-text footer (`--text`) — deterministic projection

Appended as the final block of `renderText`, after the `nextAction:`/help lines. Exact, color-free, width-independent. Golden-fixture covered.

Non-blocked example:

```
lifecycle: 3/10 clarify (current) · work=084-lifecycle-status-footer · outcome=succeeded
stages: charter=done specify=done clarify=current checklist=next plan=pending tasks=pending analyze=pending evidence=pending verify=pending ship=pending
next: fsgg-sdd checklist
```

Cross-cutting command example (`refresh`, not a lifecycle stage):

```
lifecycle: 6/10 done · work=084-lifecycle-status-footer · outcome=succeeded · (refresh is not a lifecycle stage)
stages: charter=done specify=done clarify=done checklist=done plan=done tasks=done analyze=pending evidence=pending verify=pending ship=pending
next: fsgg-sdd analyze
```

Blocked example — explanation + options derived from existing `diagnostics` + `nextAction`:

```
lifecycle: 3/10 clarify (blocked) · work=084-... · outcome=blocked
stages: charter=done specify=done clarify=blocked checklist=pending … ship=pending
blocked: clarify
why: <blocking diagnostic .Message>
fix: <blocking diagnostic .Correction>          # already carries the remediation-pointer sentence
options: fsgg-sdd clarify                        # nextAction.Command; plus any requires: <artifact>
```

No-work-id example (`init` or unresolved):

```
lifecycle: 0/10 · work=none · outcome=succeeded
stages: charter=pending specify=pending … ship=pending
next: fsgg-sdd charter
```

Text rules:
- Final element of the output (nothing after it) — FR-001.
- Carries the same facts as the JSON `lifecycleStatus`; the blocked-only `why:`/`fix:`/`options:` lines are drawn verbatim from `diagnostics`/`nextAction` already present in json/text.
- Exact spacing/tokens are pinned by a golden fixture (rich output is not).

## 3. Rich footer (`--rich`) — presentation projection

Appended as the final element of `renderRichTo` (after the Next-action callout). A Spectre panel: a stage rail with each stage colored by state, a summary line (`N of M · stage · outcome · work id`), and on a blocked outcome a red-emphasized failure sub-block with the `why`/`fix`/`options` (same facts as text).

Color mapping (presentation only; excluded from golden contracts):

| State | Style |
|---|---|
| done | green |
| current | bold cyan |
| next | yellow |
| pending | dim/grey |
| blocked | bold red |

Rich rules:
- Adds/drops **no** facts vs text/json (FR-010). Color and box layout only.
- **Degrades by construction**: `Rendering.resolve` calls `renderRichTo` only when interactive + color-enabled; otherwise it emits `renderText` (which now includes the plain footer). So a redirected/`NO_COLOR`/`TERM=dumb` run yields the byte-identical text footer with zero color/box control sequences (FR-009, SC-008).

## Parity obligation (tested)

For any command run, the set of facts {work id, per-stage states, current ordinal, total, outcome, next command, and — on failure — the explanation/options} MUST be identical across json, text, and (interactive) rich. A fact present in one projection and absent in another is a defect.
