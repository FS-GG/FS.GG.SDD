# Verification evidence — `FS.GG.SDD#865`

Two things are recorded here because neither is visible from a green suite: that every gate this change
adds **can fail**, and that the two occurrences the item names are actually unblocked by it.

Everything below was run from the item's worktree with the CLI built from this branch
(`src/FS.GG.SDD.Cli/bin/Debug/net10.0/FS.GG.SDD.Cli`, `toolVersion 1.1.0`).

## 1. Gate-inversion evidence

A gate asserted only to pass on good input has not been tested. Each row below mutates **one** source
expression, rebuilds, and runs the two test classes that cover the record channel
(`FS.GG.SDD.Artifacts.Tests.RecordReceiptTests`, 16 tests;
`FS.GG.SDD.Commands.Tests.RecordDischargedObligationTests`, 11 tests). A **green baseline is asserted
before every mutation**, so each red is attributable to that mutation and not to a tree that was
already broken.

| # | Mutation | Observed red |
|---|---|---|
| I1 | `HandlersVerify`: the `unrecorded` arm's `&& recordDischarged` guard replaced by `&& false`, making the arm unreachable | 2 failed — `a record obligation resting on result pass alone does not satisfy and cannot ship`, `an unmet record obligation and an unmet test obligation are named by their own diagnostics` |
| I2 | `Evidence.recordReceiptInconsistency` short-circuited to `None` (every receipt coherent) | 9 failed — all eight `RecordReceiptTests` refusal cases plus `a malformed receipt is reported as invalid, naming the field` |
| I3 | `HandlersEvidence.recordReceiptIsCurrent` short-circuited to `true` | 1 failed — `editing the decision record after the receipt turns it stale at verify and at ship` |
| I4 | `Evidence.citedArtifactPaths`: the `decision` locator arm disabled with `&& false` | 6 failed — including `deleting the decision record turns the obligation invalid through the cited-artifact cascade` and the positive ship fixture |
| I5 | `Evidence.obligationDischarged` collapsed to `obligationIsObserved` (dispatch removed) | 4 failed — including `an observed run never discharges a record-class obligation` |
| I6 | `HandlersVerify`: `recordRequirement` written as the constant `false` | 2 failed — `verify writes recordRequirement onto both disposition arrays`, and the class-partition test |
| I7 | `HandlersShip`: `unrecordedIds` replaced by the empty list (no partition by class) | 1 failed — `an unmet record obligation and an unmet test obligation are named by their own diagnostics` |
| I8 | `Evidence.recordReceiptInconsistency`: the `decision` locator's `"://"` scheme guard removed | 1 failed — `each kind rejects a locator in another kind's form` |
| I9 | `HandlersEvidence`: the evidence-stage `recordReceiptInvalid` diagnostic dropped | 1 failed — `a malformed receipt is reported as invalid, naming the field, rather than as a missing record` |

**I8 was not a hypothetical.** The guard did not exist when the test was first written: `citedPathIsContained`
answers "does this path escape the repository?", and an `https://…` URI carries no `..` and is not
rooted, so containment alone **accepted** it. The test failed on its first run, and the guard is that
failure's fix. `citedArtifactPaths` carries the same clause for the same reason — without it a
`decision` receipt naming a URI would be cited as a local path, and the ladder's `artifactNotFound` arm
(which sits above the receipt arms) would report "artifact not found" for what is really "your locator
is the wrong kind".

Two harness properties are worth recording, because either one silently corrupts this evidence:

* **`touch` after restoring the file.** `cp`/`mv` restores the original mtime, which is *older* than the
  build that ran over the mutated file, so MSBuild treats the assembly as up to date and the next case
  runs against the previous mutation's binaries. A first pass without this produced reds attributable to
  nothing, including an `Artifacts` failure under a `Commands`-only mutation.
* **Assert the baseline green before each mutation**, for the same reason in the other direction.

### Repair 1 — the boundary this change decalibrated, and the guard that now holds it

Critic `heron-d9ac` measured (M1) that `min-equal.providers.yml` still declared `minimumFsggSdd.version:
"1.0.0"` after this change moved the installed version to `1.1.0`. The equality test
(`ScaffoldCliCoherenceTests.equal to minimum emits no cliBehindMinimum advisory`) was therefore running
the strictly-**above** case — a second copy of what `min-satisfied` already covers — and `Assert.Empty`
cannot tell the two apart.

The repair is a **fixture** re-anchor to `1.1.0`, not an assertion change: weakening or re-scoping the
assertion would keep the test green while leaving the boundary untested, which is the failure itself
rather than a fix for it.

Reproduced on both axes — boundary intact/broken × fixture `1.0.0` (pre-repair) / `1.1.0` (repaired),
where "boundary broken" is `HandlersScaffold.fs:368` `| Some -1` widened to `| Some -1 | Some 0`:

| boundary | fixture | `equal to minimum …` |
|---|---|---|
| intact | `1.1.0` (repaired) | **pass** — correct behaviour preserved |
| **broken** | `1.0.0` (pre-repair) | **pass** — the surviving inversion, reproduced |
| **broken** | `1.1.0` (repaired) | **FAIL** — the gate is restored |

The middle row is the point: the same mutation that the suite absorbed silently before this repair is
caught after it, and nothing about the assertion changed between them.

**A guard was added so the calibration cannot drift silently again.** `min-equal` and `min-behind` are
hand-anchored to the installed version and nothing re-anchors them when `<Version>` moves; when they
drift, *neither* of the tests that use them fails, because both assert only on the presence or absence
of an advisory. `the min-equal and min-behind fixtures stay anchored to the installed version` compares
each fixture's declared minimum against `currentGeneratorVersion ()` through the **same**
`Fsgg.Version.compare` the production arm calls, so the fixtures are calibrated against the rule rather
than against a literal the test happens to agree with. It is inverted by decalibrating the fixture it
guards:

| guard | fixture | result |
|---|---|---|
| present | `1.0.0` (decalibrated) | **FAIL** |
| present | `1.1.0` | **pass** |

This drift had already happened once before this PR — `min-equal` sat a patch below the installed
`1.0.1` on `main` — which is why the remedy is a check rather than a comment.

## 2. Re-check of the two measured occurrences (item acceptance criterion 5)

Both were re-checked against the change in a **clean `FS-GG/.github` worktree at `origin/main`**
(`45bbaf9f`), driven by this branch's CLI with `--root`. Nothing in `.github` was modified: the worktree
is a throwaway checkout, and no commit, push, or board write was made against it.

### `.github#2380` — feedback report materialization

| step | result |
|---|---|
| `verify` as committed | **blocked**, `needsVerificationCorrection`, `verify.unobservedRequiredTest`; 10 self-attested, 4 observed |
| tag the ten as `record-discharge` in `tasks.yml`, `verify` | **blocked**, and the diagnostic is now `verify.unrecordedRequiredRecord` naming exactly `EV001, EV002, EV005, EV006, EV007, EV008, EV009, EV010, EV013, EV014` |
| re-derive (`refresh`, `analyze`, `evidence`), add a `recordReceipt` to each of the ten, `verify` | **`verificationReady`**, 0 blocking, **0 self-attested, 14 observed** |
| `ship` | **`shipReady`**, 0 blocking |

The ten obligations named by the new diagnostic are *exactly* the ten its own
`work/2380-feedback-report-materialization/lifecycle-status.md` identifies as documentation and routing
claims — "there is no suite for prose". Its first `verify` line reproduced here matches that file's
recorded state, which is what makes the second line a measurement rather than a demonstration.

### `.github#2545` — Rendering-owned product skill channel

| step | result |
|---|---|
| `verify` as committed | **blocked**, `verify.unobservedRequiredTest`; 5 self-attested, 19 observed |
| tag `EV007, EV009, EV011, EV013, EV024` as `record-discharge`, re-derive, add receipts, `verify` | **`verificationReady`**, 0 blocking, **0 self-attested, 24 observed** |
| `ship` | **`shipReady`**, 0 blocking |

Those five are the ones that item's `lifecycle-status.md` records as admitting no honest receipt: a route
decision, two decisions, a generated-view claim, and one set of filed rows.

### What this does and does not show

It shows that the change **removes the structural block**: both items reach `ship` once their record
obligations declare their class and name their records.

It does **not** discharge either item's obligations. The receipts used in the re-check carry a
`statement` that says `"Re-check only: a placeholder record standing in for the durable artifact <id>
really rests on."`, and they all point at the item's own `spec.md` rather than at the real decision
record, filed row, or commit each obligation rests on. Naming the real artifact is the authoring work
those items own, and this note deliberately does not pretend to have done it. Neither row was reopened
or edited; whether to reopen them is a judgement for whoever owns them.

## 3. This package as its own fixture

`work/865-record-discharged-obligations` is itself record-discharged in part, and its back half was run
with the changed tool:

* `verify` → `verificationReady`, **30 supported, 30 observed, 0 self-attested**, 0 blocking.
* `ship` → `shipReady`, 0 blocking.
* Eight of the 30 — `EV011, EV012, EV013, EV014, EV025, EV026, EV027, EV030` — carry
  `recordRequirement: true` in the committed
  `readiness/865-record-discharged-obligations/verify.json`, reached `supported` with `observed: true`,
  and their `TD-` mirrors reached `satisfied`. Before this change those eight could not have reached
  `observed` by any route, and the package could not have reached `ship`.

The other 22 carry ordinary `observedRun` receipts, recorded from two committed lane reports:
`readiness/865-record-discharged-obligations/artifacts.trx` (101 tests) and `…/commands.trx` (128
tests). They are **two** receipts rather than one because a receipt names a run, and attaching a run
that did not execute an obligation's proving tests is the misattachment `.github#2380` refused; each
declaration carries the receipt of the lane that actually ran its cited tests.

### The stale-record rule fired on this package, and was repaired rather than suppressed

Adding FR-012 moved `plan.md`, and `EV026`/`EV027` bind that file's bytes. Their receipts went stale
exactly as designed, and were re-stamped from the record's current bytes after confirming the record
still establishes each receipt's `statement`. That is the intended workflow for an edited record: read
it again, confirm, re-stamp — not delete the binding.

## 4. One defect found while doing this, filed rather than folded in

Adding a requirement to a package that ALREADY has an `evidence.yml` deadlocks: `refresh` cannot bring
`work-model.json` to currency because a declared source (`evidence.yml`) lacks a declaration for the
newly-minted obligation, and `evidence` refuses to write one because analysis is not
`implementationReady` — which it cannot be until the work model is current. Neither command names the
other's precondition; `refresh.malformedSource` reports the *specification* as malformed, which is a
hard-coded placeholder in `HandlersRefresh` rather than the source actually at fault.

The escape used here was to hand-seed a `result: missing` declaration for the new obligation, after
which `refresh` reported the ordinary `staleView` and the lifecycle proceeded. That is a workaround, not
a fix, and the defect is filed at its cause rather than repaired inside this item — it is a distinct
cause from the verify gate's vocabulary and touches a different handler.
