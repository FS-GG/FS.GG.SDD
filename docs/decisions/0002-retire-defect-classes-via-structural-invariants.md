# 0002: Retire Defect Classes via Structural Invariants

## Status

Proposed, 2026-07-08.

## Context

Between 2026-06-27 and 2026-07-08 the repository sustained a steady arrival of
~12–15 issues per week with no sign of decay. A review on 2026-07-08 (five
parallel source audits over the parse/render, report/version, CLI/effects,
work-model, and test-suite layers) reached a specific conclusion:

- The architecture is sound and was **not** the source of the defects. The
  MVU pure-core/effect-edge boundary is real and mechanically verifiable
  (zero IO/clock/env in the Commands core outside `CommandEffects.fs`),
  layering is strictly acyclic, `.fsi` discipline holds, and serialization is
  deterministic. The 2026-06-26 and 2026-07-02 reviews reached the same
  verdict, and the team has acted on both (the 6,838-line god module was
  eliminated, warnings driven ~290 → 2, the per-PR test gate wired up in #65,
  and the test-infra hazards of #75 largely remediated).

- **Every open non-epic issue maps to one of four structural gaps.** The
  defects are not independent; each gap is a *generator* that manufactures a
  family of near-identical bugs at every seam it touches. Point-fixing one
  instance leaves the same hole open at the other seams, which is why the
  arrival rate does not decay.

The four gaps, and the issues they have already produced:

| Gap | Root cause | Filed issues |
|---|---|---|
| **A. Authored-artifact round-trip** | Every YAML emitter is hand-written string interpolation living in a different assembly from its parser, with no shared field list, no codec, and no round-trip test. Only 6 of ~123 scalar reads are null-aware. | #161, #180, #181, #182 — grouped in **#201** |
| **B. Diagnostic / report / version contract** | The `--json` report is ~422 hand-written `Utf8JsonWriter` calls decoupled from the record type; diagnostics can be computed then dropped before the report; `outcome`/exit derive from a list; version strings are hand-maintained literals; `jsonInventory` freezes only top-level keys. | #183, #191, #193, #198 — grouped in **#202** |
| **C. CLI parsing & path containment** | No argument grammar (unknown tokens are silently ignored); no shared containment primitive at the request→effect boundary — `escapesRoot` exists but is wired to exactly one of ~six user-influenced path inputs. | #185, #196 — grouped in **#203** |
| **D. Work-model field semantics** | Reference sets (`SourceIds`/`Requirements`/`Decisions`) are consumed by five consumers that each pick a different subset; `parseWorkModel` silently zeroes fields (`GovernanceBoundaries`, `Sources`) on round-trip. | #189, #192 — grouped in **#204** |

The audits additionally found ~18–20 **unfiled** bugs of these same four
classes already present in the tree (see the grouped tracking issues that
reference this ADR), two of them firing on every artifact re-run. This is the
expected shape of a systematic-defect signature: not a design that needs
replacing, but a design missing a small number of guardrails.

A clean rewrite was considered and rejected. It would regenerate the same
architecture (because the architecture is correct), discard 224 commits and
~1,050 tests of accumulated correct behavior and every golden that encodes
hard-won lifecycle semantics, and — decisively — would fix none of the four
gaps unless the missing invariants were consciously introduced, which can be
done far more cheaply against the existing tree.

## Decision

Treat the four gaps as a single coordinated workstream whose goal is to
convert each recurring bug family into a **compile-time or property-time
impossibility**, rather than continuing to point-fix instances. We adopt five
structural invariants and one process change. Each invariant is applied *once*
at the seam that generates the family, not per-instance.

1. **Authored artifacts round-trip through a single field-list-driven codec
   (Gap A).** Replace the ~20 hand-written emitters and ~123 hand-written
   reads with one read/write pair (or codec) per authored artifact, so the
   parsed field set and the emitted field set cannot diverge. Add an
   FsCheck round-trip property `render(parse(x)) = x` per artifact. Reads that
   gate on `Option.isNone` must use the null-aware reader. This makes the
   #180/#181/#182 class unrepresentable and is delivered first, as feature 097.

2. **The serialized report shape is pinned at full depth, and version literals
   are tied to it (Gap B).** Add a golden of the emitted JSON key set at full
   depth for the command-report and every catalog contract, and a test that
   fails when a serializer field changes without `reportVersion`/`viewVersion`
   moving. `jsonInventory`'s top-level-only comparison is the current hole;
   this closes #198 and its unfiled generalizations to the other contracts.

3. **Diagnostics deduplicate at the assembly seam, and computed-blocking
   diagnostics must reach the report (Gap B).** Deduplicate structurally at
   `ReportAssembly.buildReport` (closing the #193 *class*, not just its
   instance), and surface `blockingModelDiagnostics` in the dropped arm of
   `ViewGeneration.generatedViewPlan` so a blocking work model can never report
   success with an unwritten view (#191), fixed once to cover all six caller
   seams.

4. **One containment primitive guards every user-influenced path, and unknown
   tokens are rejected (Gap C).** Generalize the existing, correct
   `escapesRoot` predicate (it tests the raw string before normalization) to
   every relative path that reaches `fullPath` — `sourceRoot`, `baselineRoot`,
   `validate --out`, the registry-subcommand paths — and add an unknown-token
   reject pass to the request assembler. Closes #185, #196, and four unfiled
   items together.

5. **The reference-set union is reconciled at the blind consumers, and
   `parseWorkModel` stops zeroing fields (Gap D).** Apply the
   `SourceIds ∪ Requirements ∪ Decisions` union at the consumers that need it
   (not at the parser, which would migrate silent to blocking on upgrade), and
   stop discarding `GovernanceBoundaries`/`Sources` on the work-model
   round-trip so the governance handoff stops shipping an empty
   `governedReferences`. Closes #189 and the related unfiled erasure.

**Process change.** Add one real-binary end-to-end lifecycle test
(charter→ship over a real workspace through the actual `fsgg-sdd` binary; the
current "full lifecycle" tests re-implement the run loop in
`TestSupport.runRequest`), drive the shipped worked example through the
lifecycle stages rather than the parser alone (#192), and wire `fsgg-sdd
validate` into the per-PR gate. This converts "found in the field weeks later"
into "found on the PR" for all four classes.

Each invariant follows the constitution's Spec → FSI → Semantic Tests →
Implementation order and is specified as its own feature. This ADR is the
umbrella; the four grouped tracking issues (one per gap) enumerate the concrete
unfiled findings and reference this decision.

## Consequences

- The four gaps become the tracked unit of work. A fix is "done" when the
  invariant is in place at the generating seam and a test makes the class
  unrepresentable — not when a single instance stops reproducing.
- Feature 097 (the authored-artifact codec) is the one place a subsystem-level
  rewrite is justified; it replaces the hand-written serialization layer for
  authored artifacts while leaving the generated-view serializers, the MVU
  core, and the CLI host untouched.
- The change classification (constitution §Change Classification) applies:
  invariant 2 touches the `reportVersion` contract (Breaking on the field
  removal it corrects), invariant 5 touches the governance-handoff contract,
  and both owe release notes and baseline updates.
- Several fixes are behavior-changing for workspaces that currently rely on a
  hole (out-of-root baselines under invariant 4; bare-null synthetic
  disclosure under invariant 1). Each such change is scoped to its own feature
  with its own migration note, not folded into an additive change.
- The invariants are independent enough to be sequenced against the existing
  intra-repo overlap protocol (ADR-0021); the grouped issues carry their
  `Paths:` touch-sets for that purpose.
