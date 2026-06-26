# Feature Specification: Strip redundant `private` + give `failwith` escapes context

**Feature Branch**: `028-strip-private-failwith`

**Created**: 2026-06-26

**Status**: Draft

**Input**: User description: "next item in @docs/reports/2026-06-26-074428-refactor-analysis.md" — the
refactor roadmap's only remaining 🔴 row, **R7 — Strip redundant `private`; fix `failwith` context**
(§2 redundant `private` modifiers, §6 partial-function escapes).

## Change Tier

**Tier 2 (internal change).** Implementation cleanup only. No public API, schema, generated-view,
command, artifact-layout, or agent-skill contract changes. Every public `.fsi` signature and every
`PublicSurface.baseline` stays byte-identical; deterministic JSON/text output stays byte-identical.
This row is the roadmap's `Severity: Low · Risk: None · Payoff: Noise removal`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Redundant `private` no longer implies a visibility decision the `.fsi` already made (Priority: P1)

A maintainer reading an `.fsi`-guarded module sees a `let private foo`/`type private`/`module private`
binding and has to reason about whether that `private` is load-bearing. In every one of these modules
the signature file is already the sole arbiter of public surface, so any binding the `.fsi` does not
re-export is *already* inaccessible to consumers — the `private` adds nothing but a misleading hint that
a visibility choice was made in the `.fs`. The constitution's Principle III ("Visibility Lives in
`.fsi`, Not in `.fs`") names exactly this: top-level visibility modifiers in `.fs` files are not the
visibility policy. This story removes that noise so the `.fsi` is unambiguously the one place visibility
is decided.

**Why this priority**: Highest reader-confusion-per-byte and directly codified by Principle III. It is
the larger of the two cleanups (~81 sites) and fully mechanical, so it carries the lowest risk and the
clearest payoff.

**Independent Test**: Grep the affected `.fsi`-guarded modules for `let private`/`type private`/
`module private`; confirm zero remain where the binding's inaccessibility is already guaranteed by the
signature file. Build is green and the full suite passes, proving no removed `private` was actually
controlling reachable visibility.

**Acceptance Scenarios**:

1. **Given** an `.fsi`-guarded module whose `.fsi` omits binding `x`, **When** the redundant `private` on
   `x` in the `.fs` is removed, **Then** the assembly still compiles, `x` remains absent from the public
   surface (`.fsi` and `PublicSurface.baseline` byte-identical), and all tests pass.
2. **Given** a `private` binding whose removal *would* change intra-assembly resolution (e.g. a name that
   would then collide with, or shadow, a sibling binding in an `[<AutoOpen>]` module), **When** the
   cleanup runs, **Then** that `private` is **retained** because the build/test gate proves it is
   load-bearing, not redundant.
3. **Given** the whole cleanup, **When** the Release build runs, **Then** no new warning category appears
   and the existing FS3261/FS0025 ratchet (0 sites) is still satisfied.

---

### User Story 2 - Partial-function escapes carry context or become provably total (Priority: P2)

The codebase prizes total-function discipline, yet ~9 `failwith` / `Result.defaultWith failwith` /
`Option.defaultWith (fun () -> failwith …)` sites convert a `Result.Error`/`None` into a raw exception
inside otherwise-total code. Each is a "can't-happen-by-construction" invariant — the inputs are built
from internal format strings (`sprintf "EV%03d"`, `sprintf "T%03d"`, fixed artifact paths) or
already-validated work ids — but the throw discards all context: the thrown message is just the inner
error string with no `workId`, path, or offending value, and the partiality is invisible at the call
site. This story makes each escape either explicitly unreachable-by-construction *with context* in the
thrown message, or — where a site is genuinely reachable on bad input — threads the `Result` to a
diagnostic instead of throwing.

**Why this priority**: Lower volume and lower reader-confusion than Story 1, but it removes a latent
discipline gap (partiality in total code) and improves the failure message if an invariant is ever
violated. Sequenced second because it requires per-site judgment rather than a uniform mechanical sweep.

**Independent Test**: For each remaining throwing site, confirm the thrown message names the offending
value/identifier/path (not a bare inner error string), or that the site has been replaced by a threaded
diagnostic. The suite passes unchanged, proving no behavior on the reachable (happy) paths changed.

**Acceptance Scenarios**:

1. **Given** a `failwith message`/`Result.defaultWith failwith` whose value is unreachable by
   construction, **When** it is rewritten, **Then** it uses an explicit context-bearing form (e.g.
   `failwithf`/`invalidOp` naming the constructed id/path and the underlying error) and the happy-path
   output is byte-identical.
2. **Given** a site that can actually fail on malformed external input, **When** it is rewritten, **Then**
   the `Result.Error` is threaded into a diagnostic on the normal report path rather than thrown — and
   this conversion is only made where it does **not** change tool-visible output for currently-passing
   fixtures (otherwise the site stays a context-bearing throw and any output-changing conversion is
   recorded as out of scope).
3. **Given** the full set of escapes, **When** the suite runs, **Then** all prior tests pass and any
   newly-total path that previously threw is covered by an assertion that it no longer throws.

---

### Edge Cases

- **`[<AutoOpen>] module internal` workflow children (no own `.fsi`).** The R2 split produced
  `module internal` files under `CommandWorkflow/` with no sibling `.fsi` (e.g. `HandlersShip.fs:80`
  `let private parseShipReadinessFacts`). Here `private` is *not* automatically redundant: it controls
  cross-file visibility *within* the assembly across the sibling `AutoOpen` modules, which the namespace's
  `internal` scoping does not. Such a `private` is removed only when the build proves it changes nothing;
  otherwise it is retained. The redundancy guarantee of Story 1 applies cleanly to modules backed by an
  actual `.fsi`.
- **`private` that prevents a name collision or shadow.** Covered by Story 1 Scenario 2 — retained.
- **A `failwith` whose conversion to a diagnostic would alter golden/snapshot output.** Out of scope for
  this row; the site keeps a context-bearing throw (Story 2 Scenario 2).
- **A `private` binding referenced only within its own module.** Removing `private` is still safe (the
  `.fsi` keeps it off the public surface); it is removed as redundant.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The product MUST remove every redundant top-level `private` modifier (`let private`,
  `type private`, `module private`) from `.fs` bindings in modules backed by an explicit `.fsi`
  signature file, where the `.fsi` already makes the binding inaccessible to consumers.
- **FR-002**: The cleanup MUST retain any `private` whose removal would change compilation or
  intra-assembly visibility/resolution (name collision, shadowing, or cross-file exposure inside an
  `[<AutoOpen>] module internal`). Such retained sites MUST be the exception, justified by a failing
  build/test if removed, not by preference.
- **FR-003**: Removing redundant `private` MUST NOT change any public surface: every public module's
  `.fsi` and every `PublicSurface.baseline` MUST remain byte-identical.
- **FR-004**: Each remaining partial-function escape (`failwith`, `Result.defaultWith failwith`,
  `Option.defaultWith (fun () -> failwith …)`) in `src` MUST be either (a) unreachable by construction
  and rewritten to a context-bearing form whose message names the offending identifier/path/value and the
  underlying error, or (b) where reachable on malformed external input, replaced by a `Result`/diagnostic
  threaded onto the normal report path.
- **FR-005**: Any conversion of a throw to a threaded diagnostic MUST NOT change tool-visible output for
  currently-passing fixtures; where it would, the site MUST instead keep a context-bearing throw and the
  output-changing conversion is deferred (recorded, not performed).
- **FR-006**: The change MUST be behavior-preserving end to end: the existing test suite passes unchanged
  and deterministic `--json` and `--text` output for the representative commands (`charter`, `analyze`,
  `refresh`) stays byte-identical.
- **FR-007**: The Release build MUST stay green with zero errors and no new warning category, no `#nowarn`
  is introduced, and the existing scoped `WarningsAsErrors=FS3261;FS0025` ratchet (0 sites) MUST still
  hold.
- **FR-008**: The work MUST update the R7 row and its status-detail entry in
  `docs/reports/2026-06-26-074428-refactor-analysis.md` to ✅ with landed evidence, and flip the
  aggregate line to `7 / 7 complete`.

### Key Entities

- **Redundant `private` site**: a `(let|type|module) private` binding in an `.fs` whose enclosing module
  has an `.fsi` that omits it — i.e. visibility is already fully decided by the signature file.
- **Partial-function escape**: a call site converting `Result.Error`/`None` into a thrown exception
  (`failwith`/`Result.defaultWith failwith`/`Option.defaultWith … failwith`) inside otherwise-total code.
- **Surface baseline**: the `PublicSurface.baseline` capture that, with the `.fsi` files, pins the public
  contract this change must hold byte-stable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero redundant `private` modifiers remain in `.fsi`-guarded `src` modules (baseline: 80
  `(let|type|module) private` sites across 8 `.fsi`-guarded files, plus 1 gate-evaluated edge-case site
  in the non-`.fsi` `HandlersShip.fs` — 81 candidate sites across 9 files total); any residual `private`
  is provably load-bearing (removing it fails the build or a test).
- **SC-002**: Every `failwith`/`Result.defaultWith failwith`/`Option.defaultWith … failwith` site in
  `src` (baseline: 9 sites across 5 files) is either rewritten to a context-bearing form or replaced by a
  threaded diagnostic — zero bare inner-error-string throws remain.
- **SC-003**: 100% of the existing test suite passes with no test removed or weakened, plus any new
  totality/no-throw assertion added for a now-total path.
- **SC-004**: The public contract is byte-stable: a diff of every `.fsi` and every
  `PublicSurface.baseline` against the merge base is empty.
- **SC-005**: Deterministic command output is byte-stable: `--json`/`--text` for the representative
  commands (`charter`, `analyze`, `refresh`) is byte-identical to the merge base.
- **SC-006**: The Release build is green with no new warning category and the FS3261/FS0025 ratchet still
  reports 0 sites.
- **SC-007**: The refactor roadmap shows R7 ✅ with evidence and `7 / 7 complete`.

## Assumptions

- "Redundant" is defined by the signature file: in an `.fsi`-guarded module, any binding the `.fsi` does
  not re-export is already inaccessible, so its `private` adds no visibility decision. This is the §2
  premise and aligns with constitution Principle III.
- The current escape inventory (verified against `main` on 2026-06-26) is 9 sites:
  `ParsingTasks.fs:91,96,101`, `HandlersEvidence.fs:220,259`, `ReleaseContract.fs:266,451`,
  `SchemaVersion.fs:166`, `ValidationRunner.fs:642` — all "can't-happen-by-construction" invariants today
  (inputs are internal format strings / pre-validated work ids), so FR-004(a) is the expected treatment
  for all of them and FR-004(b) is a contingency, not a planned behavior change.
- The §2/§6 line numbers in the analysis report predate the R2/R3 splits; this spec is grounded in the
  current tree, where `private` sites live in `ValidationRunner.fs` (33), `ReleaseContract.fs` (20),
  `Cli/Rendering.fs` (8), `WorkModel.fs` (7), `GovernanceHandoff.fs` (5), `ValidationHarness.fs` (3),
  `LifecycleArtifacts/Verify.fs` (3), `HandlersShip.fs` (1), `SchemaVersion.fs` (1).
- No external consumers depend on these internal bindings, so the only binding gate is the existing test
  suite plus byte-identical `.fsi`/baseline/JSON output (consistent with how R2/R4/R5/R6 were gated).
- This row is independent of any other roadmap work and is fully shippable on its own.
