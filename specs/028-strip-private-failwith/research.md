# Phase 0 Research: R7 — redundant `private` + `failwith` context

All Technical Context items were resolvable from the constitution, the spec, and the
current tree; there are **no open NEEDS CLARIFICATION**. This file records the decisions
that shape Phase 1.

## D1 — What makes a `private` "redundant"

- **Decision**: In an `.fsi`-guarded module, any binding the `.fsi` does not re-export is
  already inaccessible to consumers, so a top-level `let/type/module private` on it encodes
  no visibility decision and is removable. 8 of the 9 inventory files have a sibling `.fsi`
  (verified) and are in scope for mechanical removal.
- **Rationale**: Constitution Principle III — the `.fsi` is the sole public-surface policy;
  `.fs` modifiers are not visibility policy. Removing them makes III literally true in the
  source.
- **Alternatives considered**: (a) Keep `private` as "defense in depth" — rejected: it is
  the exact reader-confusion Principle III names, and the `.fsi` already provides the guard.
  (b) Add `private` everywhere for consistency — rejected: inverts the principle.

## D2 — The one file without an `.fsi` (`HandlersShip.fs`)

- **Decision**: Treat `HandlersShip.fs:` `let private parseShipReadinessFacts` as the
  documented edge case, **not** an automatic removal. It lives in an
  `[<AutoOpen>] module internal` workflow child (R2 split) with no own `.fsi`; here `private`
  controls cross-file visibility *within* the assembly across sibling AutoOpen modules, which
  namespace-level `internal` does not. Remove it only if the build proves removal changes
  nothing; otherwise retain (FR-002).
- **Rationale**: Spec Edge Case #1 + FR-002. The redundancy guarantee (D1) only holds for
  modules backed by an actual `.fsi`.
- **Alternatives considered**: Add an `.fsi` for the workflow child — rejected: out of scope
  (Tier 2, no surface change), and R2 deliberately left these children `.fsi`-less.

## D3 — How redundancy is *proved* per site (the binding gate)

- **Decision**: A removed `private` is proven redundant iff after removal: (1) the solution
  compiles; (2) the full test suite passes; (3) every `.fsi` and every `PublicSurface.baseline`
  diffs empty vs the merge base; (4) deterministic `--json`/`--text` for representative
  commands diffs empty. Any site failing (1)–(4) is load-bearing → restore the `private` and
  record it as a justified retention.
- **Rationale**: No external consumers depend on these internal bindings (Assumption 4), so
  the suite + byte-identical baselines/output is a sufficient and the same gate used by
  R2/R4/R5/R6. Intra-assembly hazards (collision, shadow under AutoOpen) surface as a build
  error or a changed test — exactly what the gate catches.
- **Alternatives considered**: Per-site manual reachability analysis — rejected as
  unnecessary given a deterministic build/test gate that is cheaper and authoritative.

## D4 — `failwith` treatment policy

- **Decision**: For all 9 sites apply **FR-004(a)**: rewrite to a context-bearing throw whose
  message names the constructed identifier/path/value *and* the underlying error string. Use
  `failwithf`/`invalidOp` (or `Result.mapError`-then-`failwith` with an enriched message);
  keep the throw — these are can't-happen-by-construction invariants, so converting to a
  threaded diagnostic (FR-004(b)) is unnecessary and would risk output drift. FR-004(b) stays
  a contingency: only if a site is found reachable on malformed external input *and* threading
  the `Result` leaves tool-visible output byte-identical (FR-005). None of the 9 are expected
  to qualify; any that would change output stays a context-bearing throw and the conversion is
  recorded as out of scope.
- **Rationale**: Spec Assumption 2 + Story 2. The inputs are internal format strings
  (`sprintf "EV%03d"`, `"T%03d"`), fixed artifact paths (`CommandSerialization.fs`, evidence/
  tasks paths), self-serialized inventory re-parsed in the same function, or pre-validated
  work ids / just-built report models. The happy path never throws; the rewrite only improves
  the message on the impossible branch, so happy-path output stays byte-identical (FR-006).
- **Per-site message intent**:
  - `ParsingTasks.fs:91/101` — name the constructed id (`EV%03d`/`T%03d` value) + inner error.
  - `ParsingTasks.fs:96`, `HandlersEvidence.fs:220` — name the artifact path + inner error.
  - `HandlersEvidence.fs:259` — name the offending `workId` + inner error.
  - `ReleaseContract.fs:266` — name the `CommandSerialization.fs` artifact path + inner error.
  - `ReleaseContract.fs:451` — name the path being parsed back + inner error.
  - `SchemaVersion.fs:166` — name the generator component + inner error.
  - `ValidationRunner.fs:642` — name the validation stage ("report not built after BuildReport")
    rather than the bare `"report not built"`.
- **Alternatives considered**: (a) Blanket convert to threaded diagnostics — rejected:
  changes shape/output of currently-passing fixtures with no behavior benefit (the branches are
  unreachable). (b) Leave bare throws — rejected: SC-002 requires zero bare inner-error-string
  throws.

## D5 — No new warnings / no `#nowarn`

- **Decision**: Do not introduce `#nowarn`; keep `Directory.Build.props` untouched
  (`WarningsAsErrors=FS3261;FS0025`, `TreatWarningsAsErrors=false`). Removing `private` can
  surface FS1182 (unused) or shadowing warnings on a load-bearing site — that is the gate
  signal to retain the `private`, not to suppress.
- **Rationale**: FR-007 + SC-006. The ratchet must still report 0 FS3261/FS0025 sites.
- **Alternatives considered**: Suppress incidental warnings — rejected: would mask a
  load-bearing `private`.

## D6 — Verification commands (feeds quickstart.md)

- **Decision**: Gate locally with `dotnet build -c Release FS.GG.SDD.sln`,
  `dotnet test FS.GG.SDD.sln`, `git diff --stat` over `**/*.fsi` and
  `**/PublicSurface.baseline` (must be empty), and a captured-output diff of representative
  `fsgg-sdd` commands (`charter`/`analyze`/`refresh`) in `--json` and `--text` vs merge base.
- **Rationale**: Directly realizes SC-003 through SC-006 with deterministic, scriptable checks.

## D7 — Report update (FR-008 / SC-007)

- **Decision**: Flip the R7 row (`docs/reports/2026-06-26-074428-refactor-analysis.md:345`)
  and its `🔴 R7` status-detail entry (~line 450) to ✅ with landed-commit evidence, and set
  the aggregate line (~455) to `7 / 7 complete · 0 in progress · 0 not started`.
- **Note**: The aggregate currently reads `5 / 7` while R1–R6 are all ✅ — it is stale versus
  the just-landed R6 (commit `eed6bd5`). The R7 update corrects it straight to `7 / 7`, which
  is the accurate post-R7 state; no separate R6 correction is needed.
- **Rationale**: FR-008, SC-007.
