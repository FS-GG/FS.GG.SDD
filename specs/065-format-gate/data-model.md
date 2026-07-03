# Data Model: Format gate (feature 065)

This feature adds **no runtime data model, schema, or persisted artifact**. Its
"entities" are repo-owned configuration and CI surfaces. They are captured here
for completeness; there are no F# types, no JSON schemas, and no golden fixtures
introduced.

## Config / CI entities

### `.editorconfig` (repo root) — new

The single source of both editor configuration and Fantomas configuration
(Fantomas 6+ has no separate `fantomas.json`).

| Field / section | Meaning | Constraint |
|---|---|---|
| `root = true` | terminates `.editorconfig` inheritance at repo root | present |
| general whitespace keys (`indent_style`, `indent_size`, `end_of_line`, `insert_final_newline`, `charset`, `trim_trailing_whitespace`) | baseline editor behaviour for the tree | consistent with existing file style |
| `[*.fs]` / `[*.fsi]` section | Fantomas house style | contains the tuned `fsharp_*` keys |
| `fsharp_max_line_length` | primary churn-control knob | tuned to minimise reformat diff (research Decision 3) |
| other `fsharp_*` keys | small deliberate deviations from Fantomas defaults | kept minimal |

State: does not exist today → created once. No versioning; it is authoring
config, not a machine contract.

### `format` CI job (in `.github/workflows/gate.yml`) — new

| Field | Value / meaning | Constraint |
|---|---|---|
| job id | `format` | new job under `jobs:` |
| `runs-on` | `ubuntu-latest` | matches sibling jobs |
| required? | **no** | never in branch-protection required checks (FR-005) |
| Fantomas version | `7.0.5` | pinned (research Decision 1) |
| install target | repo-local `--tool-path` dir | NOT `.config/dotnet-tools.json` (FR-003) |
| check command | `fantomas --check .` over tracked F# | fails on non-clean tree (FR-004) |
| failure output | names `fantomas <paths>` fix command | actionable (FR-004 / Constitution VIII) |

### Managed org file invariant — unchanged (guard)

| File | Invariant |
|---|---|
| `.config/dotnet-tools.json` | byte-identical to `FS-GG/.github` `dist/dotnet/`; Fantomas is NOT added (FR-003 / SC-003) |
| `Directory.Build.props`, `Directory.Packages.props` | unchanged |

### Existing F# tree — reformatted once (layout-only)

| Aspect | Before | After | Invariant |
|---|---|---|---|
| 172 `.fs` + 50 `.fsi` tracked files | not fantomas-clean | fantomas-clean | layout-only; no token/identifier/behaviour change |
| golden / deterministic JSON baselines | — | — | byte-identical (SC-002) |
| `.fsi` public-surface baselines | — | — | byte-identical (SC-002) |
| full test suite | green | green | stays green (SC-002) |
| `fsgg-sdd validate` | `overallPassed` | `overallPassed` | unchanged (research Decision 5) |

## Contributor documentation entity — new/updated

The pinned-Fantomas install + run + fix commands are documented for contributors
(FR-007) in the repo's developer docs (e.g. `DEVELOPING.md`), reproducing the CI
commands verbatim so a contributor's local verdict matches CI (SC-004/SC-005).
