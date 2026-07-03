---
description: "Task list for feature 065 — format gate"
---

# Tasks: Format gate

**Input**: Design documents in `/specs/065-format-gate/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/format-gate-contract.md, quickstart.md

**Tier**: Tier 2 (build / tooling / CI / repo-config). No public API, schema,
generated-view, command, or agent-skill contract change. The reformat touches
source but is layout-only.

**Story shape**: US1 (the gate) and US2 (the provably-layout-only reformat) are
**co-P1** — neither ships alone. The gate can only pass once the tree is clean,
and the tree is cleaned with the config the gate enforces. So the MVP is US1 **+**
US2 together; the phase order below reflects that hard coupling (config →
reformat → gate → docs). No xUnit tests are added: the reformat's safety is the
*absence* of any suite/baseline change, and the only new behaviour (the gate
reject-path) is covered by a negative check.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: no dependency on another incomplete task in this phase (different file)
- **[Story]**: `[US1]` gate / `[US2]` reformat / `[SETUP]` / `[POLISH]`

---

## Phase 1: Setup — pin Fantomas and author the config (Shared)

**Purpose**: Establish the pinned formatter and the `.editorconfig` that both the
reformat (US2) and the gate (US1) depend on.

- [X] T001 [SETUP] Install pinned Fantomas `7.0.5` out-of-manifest locally:
  `dotnet tool install fantomas --version 7.0.5 --tool-path ./.fantomas-tool --allow-roll-forward`.
  Confirm `.config/dotnet-tools.json` is **unmodified** (`git status` clean for
  it) — the load-bearing invariant (research Decision 2, FG-2).
- [X] T002 [SETUP] Author the repo-root `.editorconfig` with `root = true`,
  general whitespace keys, and a `[*.fs]`/`[*.fsi]` section starting from
  Fantomas defaults (FG-1, data-model entity `.editorconfig`).
- [X] T003 [SETUP] Measure reformat churn: run `./.fantomas-tool/fantomas --check .`
  and record how many files/what categories are flagged; tune
  `fsharp_max_line_length` (and only clearly-justified further `fsharp_*` keys) in
  `.editorconfig` to minimise the diff where the existing style is deliberate
  (research Decision 3). Iterate T002/T003 until the config is settled.

**Checkpoint**: `.editorconfig` is final; churn is understood and minimised.

---

## Phase 2: User Story 2 — the tree is fantomas-clean, provably layout-only (P1)

**Goal**: Reformat the tracked F# tree once with the settled config and prove the
change is layout-only.

**Independent Test**: after the reformat, the full suite is green and every
golden/`.fsi` baseline is byte-identical; `fsgg-sdd validate` stays
`overallPassed`.

- [X] T004 [US2] Apply the one-time reformat as its own commit:
  `./.fantomas-tool/fantomas .` across the tracked `src/**` and `tests/**`
  `.fs`/`.fsi` tree (FG-5, FR-006).
- [X] T005 [US2] Prove suite green: `dotnet test FS.GG.SDD.sln -c Debug` passes
  (SC-002).
- [X] T006 [P] [US2] Prove zero golden/deterministic drift:
  `git diff --stat -- '*.json'` shows **no** golden/baseline changes attributable
  to the reformat (SC-002).
- [X] T007 [P] [US2] Prove signature stability:
  `git diff -- '*.fsi'` shows whitespace/layout-only diffs with **zero**
  declaration changes; public-surface baselines byte-identical (Constitution III,
  SC-002).
- [X] T008 [US2] Prove `validate` unchanged:
  `dotnet run --project src/FS.GG.SDD.Cli -- validate --json` still reports
  `"overallPassed":true` (research Decision 5).

**Checkpoint**: tree is clean and the reformat is evidenced as layout-only.

---

## Phase 3: User Story 1 — formatting is enforced by a non-required gate (P1)

**Goal**: Add the CI gate that fails a non-clean PR and names the fix, without
touching the managed manifest.

**Independent Test**: a mangled file makes `fantomas --check` non-zero with the
fix hint; a clean tree passes; `.config/dotnet-tools.json` stays byte-identical.

- [X] T009 [US1] Add a **non-required** `format` job to
  `.github/workflows/gate.yml`: `runs-on: ubuntu-latest`, `setup-dotnet@v4`
  (`10.0.x`), out-of-manifest install
  (`dotnet tool install fantomas --version 7.0.5 --tool-path … --allow-roll-forward`),
  then `fantomas --check .`; on failure print the `fantomas <paths>` fix command
  (FG-2/3/4, FR-002/004/005). Do **not** add the job to branch-protection required
  checks. **FR-005 guard**: confirm the repo's required-checks set is explicitly
  enumerated (not a wildcard / "all jobs must pass" policy) so adding `format`
  cannot silently make it blocking; if it is a wildcard policy, note the residual
  risk in the PR.
- [X] T010 [US1] Verify the managed-manifest invariant: after the job's install
  step, `.config/dotnet-tools.json` is byte-identical to the `FS-GG/.github` org
  source and the `build-config-drift` job stays green (FG-2, SC-003).
- [X] T011 [US1] Negative check (gate reject-path): mangle a file
  (`printf '\n\n   let   x=1\n' >> src/FS.GG.SDD.Cli/Program.fs`), run
  `./.fantomas-tool/fantomas --check .`, confirm non-zero exit **and** that the
  output names the reformat command, then restore the file (FG-3, SC-001). This is
  the one new-behaviour test.

**Checkpoint**: the gate rejects a dirty tree, passes a clean one, and leaves the
managed manifest untouched.

---

## Phase 4: Polish — document and validate end-to-end

- [X] T012 [P] [POLISH] Document the pinned install / `--check` / fix commands in
  `DEVELOPING.md`, verbatim to CI so a contributor's local verdict matches CI
  (FG-6, FR-007, SC-004/005).
- [X] T013 [POLISH] Run the full `quickstart.md` validation sequence end-to-end
  (FG-1..FG-6) and confirm every expected outcome.
- [X] T014 [POLISH] Add `./.fantomas-tool/` to `.gitignore`, then remove the local
  install artifact from the working tree, so nothing beyond `.editorconfig`,
  `gate.yml`, `DEVELOPING.md`, `.gitignore`, and the reformat lands.

---

## Dependencies & Execution Order

### Phase dependencies

- **Phase 1 (Setup)**: no deps — start immediately. T002/T003 iterate together.
- **Phase 2 (US2 reformat)**: depends on the settled `.editorconfig` from Phase 1.
- **Phase 3 (US1 gate)**: depends on Phase 2 (the gate must pass on the clean tree
  in CI; adding the job before the reformat would redden `main`).
- **Phase 4 (Polish)**: depends on Phases 2–3.

### Why the stories are ordered, not parallel

US1 and US2 are co-P1 but **not** independently deployable: US1's gate presupposes
US2's clean tree, and US2's reformat uses US1's config. They ship in one PR
(roadmap #85 §1). Within phases, `[P]`-marked verification tasks (T006/T007,
T012) run in parallel.

### Parallel opportunities

- T006 and T007 (golden-diff and `.fsi`-diff checks) run in parallel after T004.
- T012 (docs) can be drafted in parallel with Phase 3.

---

## Implementation Strategy

**MVP = US1 + US2 together** (single reviewable PR):

1. Phase 1 — settle `.editorconfig` + pinned Fantomas, minimise churn.
2. Phase 2 — reformat once; prove layout-only (green suite, byte-identical
   goldens/`.fsi`, `validate` unchanged).
3. Phase 3 — add the non-required `format` gate; negative-check it; confirm the
   managed manifest is untouched.
4. Phase 4 — document + full quickstart validation.

Stop-and-validate after Phase 2: if any golden/`.fsi`/suite/`validate` evidence is
**not** clean, the reformat is not layout-only — fix before adding the gate.

## Notes

- Never mark a task `[X]` on failing evidence. A golden or `.fsi` declaration diff
  attributable to the reformat is a **defect**, not an acceptable change — narrow
  the `.editorconfig` or investigate, do not update the baseline.
- Elmish/MVU (Constitution V): **N/A** — no runtime code paths added; this is
  build-time tooling policy over existing source.

## Evidence (implementation run, 2026-07-03)

- **Config**: `.editorconfig` (defaults + `indent_size=4`, `max_line_length=120`,
  matching detected repo conventions: all-LF, 4-space, no tabs) + `.fantomasignore`
  (scopes to authored source; excludes `obj/`/`bin/`/generated `.fs`). Fantomas
  pinned **7.0.5**. Line-length tuning was measured to barely affect churn (repo p90
  line length = 91), so the config stays close to defaults per research Decision 3.
- **Reformat scale** (T004): 172 files formatted, 0 errored; diff +10801/−3378 across
  171 `.fs`/`.fsi` + `scripts/prelude.fsx`.
- **T005** suite green: **873 passed, 0 failed**, 3 network-gated skips (Contracts 86,
  Artifacts 175, Acceptance 33, Validation 18, Cli 87, Commands 474).
- **T006** zero golden/fixture `.json` changed (Fantomas formats only F#).
- **T007** `.fsi`: 21 files reflowed; **0 genuine token changes** after normalizing
  comment-whitespace and single-line record `;` separators; public-surface baseline
  tests pass.
- **T008** `fsgg-sdd validate`: `summary.overallPassed = true` (332 passed, 0 failed).
- **T010** `.config/dotnet-tools.json` byte-identical (SHA `834850b6…`, git clean) —
  the `--tool-path` install never touches the managed manifest.
- **T011** negative check: mangled tree → `fantomas --check` exit 1, flagged file
  named, fix hint emitted; clean after restore → exit 0.

### Disclosure (Principle V — environment workaround, not synthetic test evidence)

The dev-container reproduces the known `NU1403` `FSharp.Core` **lockfile hash
divergence** ([[nuget-lockfile-hash-divergence]]): locked restore fails locally with
a contentHash that differs from CI's. To run the suite locally I restored with
`-p:RestoreForceEvaluate=true` (rewriting 11 `packages.lock.json` hashes to the local
value), then **reverted all lockfiles** (`git checkout -- '**/packages.lock.json'`)
so no dev-container hash is committed. The test evidence itself is real (real suite,
real filesystem/process). CI's locked restore uses the authentic committed lockfiles
unchanged. Call this out in the PR description.
