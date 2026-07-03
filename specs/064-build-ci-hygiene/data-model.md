# Data Model: Build/CI hygiene (feature 064)

This feature has **no runtime data model** — it changes no schema, no persisted
artifact, and no `fsgg-sdd` type. The "entities" here are the build/CI
configuration surfaces it touches and the invariants each must hold. Captured so
the task graph and verification can reference them precisely.

## Configuration surfaces

| Surface | Ownership | Change | Invariant |
|---|---|---|---|
| `nuget.config` | repo-owned | `<clear/>` + explicit sources + `packageSourceMapping` | Restore is machine-independent; fork-friendly (no token source) |
| `packages.lock.json` (×11) | generated, committed | none directly; hashes become machine-independent | Unmodified after a clean hermetic restore; all agree on one FSharp.Core version+hash |
| `.editorconfig` | repo-owned (NEW) | add whitespace + `[*.fs]`/`[*.fsi]` Fantomas keys | Is the sole Fantomas 6 config; drives the format gate |
| `Directory.Build.local.props` | repo-owned | widen `$(WarningsAsErrors)` (append) | Canonical `NU1603;NU1608` + local `FS3261;FS0025` preserved; current tree clean |
| `Directory.Build.props` / `Directory.Packages.props` / `.config/dotnet-tools.json` | **managed (org)** | **none** | Byte-identical to `FS-GG/.github`; `build-config-drift` gate green |
| `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` | repo-owned | `+ <RollForward>` | Packed tool declares a roll-forward policy |
| `.github/actions/locked-restore/action.yml` | repo-owned (NEW) | composite locked-restore | Single definition; input `target`; canonical NU1603 message |
| `.github/workflows/{gate,release,composition-acceptance}.yml` | repo-owned | cache + composite + gates | All `setup-dotnet` cached; all 5 restore steps use the composite; format + tool-smoke gates present |
| `**/*.fs`, `**/*.fsi` | product source | layout-only Fantomas reformat | No token/identifier/signature/behaviour change; goldens byte-identical |

## Locked-restore composite action (interface)

The one behavioural "contract" this feature introduces (fully specified in
`contracts/ci-hygiene-contract.md`):

- **Input**: `target` (string, default `FS.GG.SDD.sln`) — the solution or single
  `.fsproj` to restore.
- **Behaviour**: `dotnet restore <target> --locked-mode`; on failure emit the
  canonical `::error::` message naming the `--force-evaluate` regenerate command,
  then exit non-zero.
- **Invariant**: byte-identical enforcement and message for all five callers; only
  `target` varies.

## Warning-ratchet state (transition)

```text
before:  WarningsAsErrors = NU1603;NU1608 (canonical) ; FS3261;FS0025 (local)   TWAE=false
after:   WarningsAsErrors = <before> ; <maximal already-clean set>              TWAE=false
         (or, iff tree is clean: TWAE=true in local props)
```

Transition rule: a warning class is added **only if** its current count is zero
(measured, Decision 4). Adding a class must never redden the current build.

## Non-entities (explicitly unchanged)

- No `fsgg-sdd` command, report, or JSON contract.
- No `.fsi` public-surface declaration (baselines unchanged).
- No golden/snapshot fixture.
- No managed org build file.
