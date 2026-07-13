# Implementation Plan: Observed Run Receipts

**Spec**: `specs/102-observed-run-receipts/spec.md` · **Issue**: FS.GG.SDD#350 · **ADR**: ADR-0035

## Architecture

The constitutional constraint decides the shape. `Artifacts` performs **no I/O** (Constitution V), so
the parse must be a pure fold over *text*, and the read must be a `ReadFile` effect interpreted at the
`CommandEffects` edge — the same seam feature 099 used for `missingCitedArtifacts`' injected `exists`.

```
CLI  --from-tests <path>
  │
  ├─ HandlersEvidence.testReportReadEffects   (plan: ReadFile <path>, first wave —
  │                                             the path is request data, not artifact data,
  │                                             so unlike #349 it needs no second wave)
  │      └─ containment refused BEFORE planning  → evidence.testReportPathEscape, no effect
  │
  ├─ CommandEffects.interpret                 (the ONLY I/O; missing file → Snapshot = None)
  │
  ├─ Artifacts.TestReport.parse : source -> text -> Result<ObservedRun, string>   (PURE)
  │      └─ TRX | JUnit, counts derived, digest = SchemaVersion.sha256Text text
  │
  └─ HandlersEvidence.recordObservedRun       (stamp receipt on real-pass `verification` decls)
```

Nothing else in the pipeline learns a new concept. `isObserved` — which #398 left as "the one place
that learns to say `true`" — reads the recorded receipt, and the counters, projections, and committed
verdict that already consume it move on their own.

## Key decisions

**D1 — Parse over text, not over a file.** `TestReport.parse` takes the report's text and returns a
`Result`. It is total, deterministic, and I/O-free, so it is unit-testable without a filesystem and
cannot violate Constitution V. The edge supplies the text.

**D2 — Derive `outcome` from counts; validate it on read.** A recorded receipt cannot be
self-inconsistent, because `outcome` is computed (`failed = failures + errors`, `passed` iff
`failed = 0`) rather than copied from the report's own summary field, which runners disagree about.
But an *authored* evidence.yml can carry a hand-written receipt, so the consistency rule is enforced
where the artifact is read — `observedRunInconsistency` — and blocks.

**D3 — Reuse `SchemaVersion.sha256Text`, do not hash raw bytes.** It normalises CRLF→LF, which is what
every other digest in this product does. A receipt whose digest flips between Windows and Linux CI
would make the field useless for exactly the audience it is for.

**D4 — The receipt's `source` is a cited path.** Adding it to `Evidence.citedArtifactPaths` makes the
feature-099 cascade probe it for free: a report deleted after recording turns its obligation `invalid`
at `verify`, the merge boundary. One line, no new gate, and it is what "compare against reality"
means.

**D5 — Stamp only real-pass `verification` declarations.** A suite run discharges test obligations. It
says nothing about a review, a deferral, or a judgement call, and stamping those would manufacture the
appearance of observation — the overclaim ADR-0035 warns against.

**D6 — Touch no disposition.** Stage 2. The ladder, severities, readiness, and exit codes are
untouched, so this cannot break an existing consumer, and stage 3 remains a small, deliberate flip.

## Change set

| Area | File | Change |
|---|---|---|
| Codec | `ArtifactCodec.fs`/`.fsi` | add `intScalar` (additive; the codec has no int field type) |
| Model | `Evidence.fs`/`.fsi` | `ObservedRun` type; `EvidenceDeclaration.ObservedRun`; codec fields; `isObserved`; `observedRunInconsistency`; `citedArtifactPaths` += source |
| Parse | `TestReport.fs`/`.fsi` (new) | pure TRX + JUnit parse → `ObservedRun` |
| Record | `HandlersEvidence.fs` | plan `ReadFile` for `--from-tests`; parse; stamp receipts; three diagnostics |
| Diagnostics | `Diagnostics` | `evidence.testReportUnparseable`, `evidence.observedRunInconsistent`, `evidence.testReportPathEscape` |
| Docs | `docs/release/schema-reference.md` | document `observedRun` |

## Verification plan

- **Unit (Artifacts)**: TRX parse, JUnit parse, unparseable, self-inconsistent, digest stability
  (CRLF≡LF), codec round-trip, `isObserved` truth table, `obligationIsObserved` no-laundering.
- **Command (Commands)**: `--from-tests` records receipts; idempotent; stamps only real-pass
  `verification`; absent/escaping/unparseable report blocks and records nothing.
- **Invariant**: `supported == selfAttested + observed` on a mixed fixture, in `verify.json` and
  `ship.json`; `readiness` unchanged vs the receipt-free fixture (FR-010 — the regression guard that
  proves stage 2 gates nothing).
- **Surface**: hand-authored `.fsi` updated; `PublicSurface.baseline` test.
