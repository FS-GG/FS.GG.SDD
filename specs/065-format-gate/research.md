# Research: Format gate (feature 065)

This feature inherits its architecture from **feature 064, research Decision 3**
(`.editorconfig` as the Fantomas config, pinned tool installed out-of-manifest,
one-time layout-only reformat). That decision is not re-litigated here. Research
below resolves the two items feature 065's spec deliberately left to planning
(Assumptions): the **pinned Fantomas version** and the **`fsharp_*` key set**,
plus the mechanical unknowns of the out-of-manifest install and job placement.

## Decision 1 — Pin Fantomas `7.0.5`

**Decision**: Pin Fantomas to **`7.0.5`** (latest stable on nuget.org at
2026-07-03; the `7.0.x` line the 064 measurement already used). CI installs it to
a repo-local tool path; contributors install the same version by the documented
command.

**Rationale**: The gate's verdict must be deterministic across a contributor
machine and CI, which is only true if both run the identical formatter version
(SC-004). Fantomas' output changes across majors and occasionally across minors,
so an unpinned `dotnet tool install fantomas` (which floats to latest) would make
the gate non-reproducible and could reformat differently than the tree was
cleaned with. `7.0.5` is the current stable head of the `7.0.x` line, and the
064 churn measurement ("171/223 under Fantomas v7 defaults") was taken against
v7, so pinning within v7 keeps the measured scale honest.

**Alternatives considered**:
- *Float to latest (`dotnet tool install fantomas` no `--version`)*: **rejected**
  — non-deterministic verdict; a later Fantomas release could redden a
  previously-clean tree with no repo change.
- *Pin an older v6*: **rejected** — v6 reads `.editorconfig` too, but the 064
  churn baseline is v7; pinning v6 would re-measure the reformat and diverge from
  the settled design.

## Decision 2 — Install out-of-manifest to a repo-local tool path, roll-forward enabled

**Decision**: In the CI `format` job, install Fantomas with
`dotnet tool install fantomas --version 7.0.5 --tool-path <dir>` (a repo-local
directory, e.g. `./.fantomas-tool`, git-ignored / ephemeral in CI), then run
`<dir>/fantomas --check .`. Enable runtime roll-forward
(`--allow-roll-forward`, and/or `DOTNET_ROLL_FORWARD=Major`) so the tool runs on
the `net10.0` runtime the `setup-dotnet@v4` step provides.

**Rationale**: `.config/dotnet-tools.json` is a **managed org file** the
`build-config-drift` gate pins byte-identical to `FS-GG/.github` `dist/dotnet/`
(FR-003 / feature 064 FR-010). Adding Fantomas there would fail that gate. A
`--tool-path` install writes nothing to the managed manifest and nothing to the
committed tree, so the drift gate stays green (SC-003). Roll-forward is needed
because the pinned Fantomas may target an earlier `net`; the CI SDK
(`10.0.x`) ships only the .NET 10 runtime, so a tool built for net8.0/net9.0
needs `--allow-roll-forward` to execute.

**Alternatives considered**:
- *A second nested `.config/dotnet-tools.json` under a subfolder*: possible, but
  `dotnet tool restore` resolution + the drift gate's expectations make an
  explicit pinned `--tool-path` install cleaner and less surprising (064
  Decision 3).
- *Global install (`-g`)*: **rejected** — mutates the runner's global tool store
  (state leak across steps), and is no more hermetic than `--tool-path`.

## Decision 3 — Tune a minimal `fsharp_*` key set to cut churn; accept Fantomas defaults otherwise

**Decision**: Start `[*.fs]`/`[*.fsi]` from Fantomas defaults and tune only a
**small** set of `fsharp_*` keys where the deliberate house style diverges from
defaults — primarily `fsharp_max_line_length` (the single highest-churn knob),
and only add further keys if a category of reformat is both large and clearly
against the existing deliberate style. The exact final key set is settled during
implementation by measuring `fantomas --check` churn against candidate configs;
the reformat is applied once the config is chosen.

**Rationale**: 064 measured 77% of files needing reformat under **bare** v7
defaults; much of that is line-length reflow. A tuned `fsharp_max_line_length`
(and a few style keys) demonstrably shrinks the diff where the current style is
intentional, keeping the one-time reformat smaller and easier to review as
layout-only. But the config must stay a *small* deviation from defaults —
over-tuning to freeze every current quirk defeats the point of adopting a
standard formatter.

**Alternatives considered**:
- *Bare Fantomas defaults, no tuning*: acceptable but produces the full 77%
  churn; harder to review and needlessly reflows deliberate constructs.
- *Heavily customised config freezing current layout*: **rejected** — turns the
  formatter into a rubber stamp of today's inconsistencies; defeats
  enforce-a-standard-style.

## Decision 4 — `format` as a non-required job in `gate.yml`

**Decision**: Add `format` as its own job in `.github/workflows/gate.yml`
(alongside `gate`, `build-config-drift`, `api-compatibility-gate`), running on
`ubuntu-latest` with `setup-dotnet@v4` (`10.0.x`), the out-of-manifest Fantomas
install, then `fantomas --check .`. It is **non-required** — never added to the
branch-protection required-checks set — and on failure prints the exact
`fantomas <paths>` reformat command.

**Rationale**: 064 contract C3 places the format gate in `gate.yml`. Non-required
(FR-005) because no runtime behaviour depends on formatting; the tree is kept
clean by the one-time reformat + contributor discipline + advisory signal, not
by hard-blocking merges on a style nit (which would be brittle across Fantomas
minor bumps). Failure output naming the fix command satisfies Constitution VIII
(actionable diagnostics) and FR-004.

## Decision 5 — Prove the reformat is layout-only via the existing suite + baselines

**Decision**: The one-time reformat's safety is evidenced by the **existing**
full solution suite staying green, every golden/deterministic JSON baseline and
every `.fsi` public-surface baseline staying byte-identical, and `fsgg-sdd
validate` staying `overallPassed`. No new xUnit contract tests are added for the
reformat itself; a small **negative check** proves the gate rejects a
mis-formatted tree (FR-004 / SC-001).

**Rationale**: The reformat changes no token, identifier, or behaviour, so the
correct evidence is the *absence* of any baseline/suite change — a golden or
`.fsi` diff attributable to the reformat would itself be the defect (Constitution
III / VI). The only genuinely new behaviour is the gate's reject-path, which
needs its own failing-before/passing-after check.

**Validation**: SC-002 (green suite, zero golden/`.fsi` drift), SC-001 (negative
check fails a mangled tree and names the fix).

## Resolved unknowns

| Spec assumption | Resolution |
|---|---|
| Exact Fantomas version | `7.0.5` (Decision 1) |
| `fsharp_*` key set | Minimal, `fsharp_max_line_length`-led, measured in impl (Decision 3) |
| Install mechanism | `--tool-path` out-of-manifest + roll-forward (Decision 2) |
| Job placement | Non-required `format` job in `gate.yml` (Decision 4) |
| Reformat evidence | Unchanged suite + byte-identical goldens/`.fsi` + `validate` (Decision 5) |
