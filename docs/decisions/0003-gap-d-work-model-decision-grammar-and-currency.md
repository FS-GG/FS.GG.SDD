# 0003: Gap D — Converge the Decision Grammar and Retire Work-Model Decoration

## Status

Proposed, 2026-07-09.

Scopes the resolution of **ADR-0002 Gap D** (work-model field semantics,
invariant 5), tracked by epic **#204**. This ADR does not restate Gap D; it
records the decisions that let #204 — and the always-on half of Gap B finding 7
(#262) that is blocked on it — actually close.

## Context

A 2026-07-09 source audit of the four ADR-0002 gap epics found that most of
Gap D's enumerated findings had already landed piece by piece, yet the epic
could not close. The reason is a single **fixpoint blocker** that the earlier
point-fixes routed *around* rather than through.

### The decision-grammar disagreement

Two parsers read the same authored decision line and disagree:

- The **clarify-facts** parser reads the authored, documented form
  `- **DEC-001** [CQ-001] [AMB:AMB-001] [FR-001] [AC-001]:` with a word-boundary
  id match (`EarlyStageAuthoring.fs:940-954`). This is the grammar the clarify
  stage and `.fsgg/early-stage-guidance.md` teach, and it is what the shipped
  example authors (`docs/examples/lifecycle-artifacts/clarifications.md:34,38`).
- **`RequirementModel.parseDecisions`** — the parser that actually populates the
  normalized work model's `decisions` list (`WorkItem.fs:146`) — accepts only the
  bare `- DEC-001: text` form (regex `^\s*-\s*(DEC-\d{3,})\s*:` at
  `RequirementModel.fs:139`). It fails on the leading `**` and on the tags
  standing between the id and the colon.

So a decision authored in the canonical grammar **never enters the work model**.
Any task that types a `DEC-###` reference then points at a decision the model
does not contain → `referenceDiagnostics` raises `unknownReference` +
`workModelInconsistent` (both blocking, `WorkModel.fs:198-206`) → the work model
has blocking diagnostics → `generatedViewPlan` takes its no-write arm.

### Why this blocks two epics

This is precisely why #191's silent-work-model-skip fix (#236) could be turned on
only at verify/ship and not always-on (Gap B finding 7 / #262): an always-on
`workModelNotGenerated` surface at the *authoring* stages would fire on the
project's own canonical example, because the example's decisions don't resolve.
The example is green today only because #231 re-routed its decisions through plan
`PD-###` dispositions so that no task types a raw `DEC-###` — a workaround that
keeps the canonical example from *teaching* the authored `**DEC**` convention it
is supposed to demonstrate.

### The migration precedent that constrains the fix

Gap D's invariant-5 instruction — "apply the reference-set union at the blind
consumers, not the parser" — exists because feature 093 (#164) **tried** unioning
`requirements`/`decisions` into `SourceIds` at the `Task.fs` parser and deferred
it (witness `Task.fs:320-330`, re-stated by #223). `unknownSources`
(`TaskGraphAuthoring.fs:786-794`) gates on `SourceIds`, so a parser-side union
retroactively subjects previously-unvalidated tokens to a blocking gate: a
`tasks.yml` green for months turns exit-1 on the next upgrade with **no
`schemaVersion` signal**. The parser is a pure read and carries no version gate
that could stage such a change. Any change to what the parser accepts must be
weighed against this hazard.

Crucially, the two changes have **opposite** migration directions:

- The rejected #164 parser-union would move workspaces **green → blocking**
  (newly-validated ids that were silently accepted).
- Fixing `parseDecisions` moves workspaces **blocking → green** (decision refs
  that are *currently* failing because the decision was silently dropped now
  resolve). It cannot newly-break a workspace that parses today; the only
  workspaces it touches are ones already broken by, or worked around, this bug.

## Decision

Resolve Gap D in four parts, in order.

### 1. Fix the parser, not the example (the fixpoint blocker → #265)

Converge `RequirementModel.parseDecisions` onto the **authored** grammar so it
accepts `- **DEC-001** [tags]: text` (bold-optional id, tags between id and
colon), matching the clarify-facts parser's word-boundary match. The bug is in
`parseDecisions` being out of step with the canonical authored surface — not in
the example.

Rejected alternative — re-authoring the example to the bare `- DEC-001:` form:
it would abandon the decision-tag contract (`[CQ-…]`/`[AMB:…]`/`[FR-…]`/`[AC-…]`)
that the clarify stage teaches and that `.fsgg/early-stage-guidance.md`
documents, and it would make the canonical example demonstrate a grammar the rest
of the lifecycle does not use. Cheapest to type, wrong to teach.

Rejected alternative — the parser-side reference-set union with a major bump
(option 3): out of scope here. That is the #164 green→blocking migration, which
invariant 5 deliberately keeps at the consumers. This ADR converges a *grammar*,
which is blocking→green, not that union.

Migration handling: because the change is blocking→green it needs no new gate,
but it **does** change generated-view content on workspaces that author `**DEC**`
decisions (agent guidance and analyze disposition sets will now include those
decisions). Ship it with a `docs/release/migrations` note and let the existing
`reportVersion`/`viewVersion` guards (Gap B finding 5, now golden-pinned) move if
the emitted shape changes. No silent behavioral drift.

### 2. Stop zeroing round-trip fields (finding 2 → #266)

`parseWorkModel` still hardcodes `Sources = []` (`WorkModel.fs:794`) and
`GeneratedViews = []` (`WorkModel.fs:881`), so `deriveGuidanceModel.sourceIdentities`
(`WorkModel.fs:957-962`) collapses to a singleton in the `agents`/`refresh`
flow. Round-trip both fields, mirroring the `GovernanceBoundaries` fix already
landed in #242. This is unambiguous corrective work with no design question.

### 3. Remove the unread view digests as decoration (finding 3 → #267)

Six generated views (analysis/verify/ship-handoff/ship-verdict/…) compute an
`outputDigest` (`ViewGeneration.fs:677`, `HandlersVerify.fs:728`,
`HandlersShip.fs:171,211,613`) that is **never read back**; real digest-based
staleness exists only for `work-model.json` (`Serialization.fs:287-323`), and
every downstream view already inherits "stale" transitively via `wmChanged`.

**Decision: remove the computed-but-unread digests and keep the transitive
`wmChanged` cascade as the single currency model.** A value computed and
persisted but never checked is exactly the "decoration masquerading as a
contract" ADR-0002 targets; keeping it invites a future reader to trust a number
nothing maintains. The cascade is already the honest model — make it the *only*
model.

Rejected alternative — persist-and-check each view's `outputDigest` (extend
`outputDigestStale` beyond `work-model.json`): it would add real per-view
staleness (catching a hand-edited or generator-drifted view whose work model did
not change), but at the cost of six new checked contracts and more schema
surface, to detect a case the workspace `surface`/golden gates and re-generation
already cover. If a concrete need for per-view staleness appears later, add it
**deliberately with a reader** rather than leaving today's unread digests as a
standing invitation.

### 4. Retire the verify/ship carve-out (unblocks #262)

Once part 1 lands and the example's decisions resolve cleanly, turn
`workModelNotGenerated` always-on across all six `generatedViewPlan` seams by
removing the `workModelIsConsumedHere` gate (`ViewGeneration.fs:925-937`). This
is the always-on half of Gap B finding 7; it is tracked under #202 as **#262**
and is *blocked-by* part 1. Verify the shipped example still passes its own
lifecycle stages (`ExampleLifecycleContractTests`) with the carve-out gone —
that assertion is the proof the fixpoint is closed, not merely routed around.

### Findings already resolved (no action)

Finding 1 (`GovernanceBoundaries` round-trip, #242), finding 4 (verify's
`affectedSourceIds` union, #189/096), and finding 5 (union at the four blind
read-consumers; `unknownSources` intentionally left `SourceIds`-only as the
write-time gate) are done. #192 (example passes analyze→ship) and #211 (example
is a `plan→tasks` fixpoint, #231) are closed.

## Consequences

- **Gap D closes on a real example.** The canonical example teaches the authored
  `**DEC**` grammar *and* round-trips its decisions into the work model, instead
  of dodging the bug via `PD-###`. The stage-driving
  `ExampleLifecycleContractTests` becomes a genuine regression fence for the
  decision path.
- **#262 unblocks.** The always-on `workModelNotGenerated` surface that Gap B
  deferred can be enabled, because there is no longer a canonical artifact that
  would spuriously trip it.
- **The currency model becomes singular.** After part 3, "is this view stale?"
  has exactly one answer (`wmChanged` transitive), not a second unread per-view
  digest that could disagree.
- **Sequencing.** Part 2 is independent and can land anytime. Part 1 gates part 4.
  Part 3 is independent. Recommended order: 2 and 3 in parallel, then 1, then 4.
- **Migration surface.** Part 1 changes generated-view *content* (not gates) for
  `**DEC**`-authoring workspaces; it ships with a migration note and lets the
  existing version guards move. No green→blocking migration is introduced by this
  ADR — that hazard (the #164 parser-union) remains explicitly out of scope.

## Tracking

Epic **#204** with children **#265** (part 1, blocker), **#266** (part 2),
**#267** (part 3); the carve-out retirement (part 4) is **#262** under epic #202,
blocked-by #265.
