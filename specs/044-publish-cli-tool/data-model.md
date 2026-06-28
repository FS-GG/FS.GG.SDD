# Phase 1 Data Model: two-package release producer

This feature has no `.fsgg` schema and no F# data types of its own. The "entities" here are the
**release-engineering objects** the producer reasons over — the packages, their version
authorities, and the publish-decision state. They define the contract in
`contracts/release-workflow.md`.

## Entities

### Published package (×2)

| Field | Contracts | CLI |
|-------|-----------|-----|
| `PackageId` | `FS.GG.Contracts` | `FS.GG.SDD.Cli` |
| Kind | library | dotnet tool (`PackAsTool=true`, `ToolCommandName=fsgg-sdd`) |
| Project | `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` | `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` |
| Version authority | evaluated fsproj `<Version>` (`1.1.0`, fsproj override) | evaluated `<Version>` (`0.2.0`, inherited from `Directory.Build.local.props`) |
| Version line | org-shared contracts line | SDD product line |
| Test gate | `FS.GG.Contracts.Tests` | `FS.GG.SDD.Cli.Tests` |
| Runtime closure | self (library) | **bundles** Artifacts (YAML loader) + Commands + Validation + Contracts + Spectre.Console + YamlDotNet |
| Feed visibility | public (existing) | public (set once on first publish, FR-011) |

**Validation rules**:
- Evaluated `<Version>` MUST be readable on a real publish event, else fail (FR-006).
- `dotnet pack` MUST yield a non-empty `*.nupkg` set, else fail (FR-007).
- The CLI package MUST run `fsgg-sdd registry validate <path>` standalone after install, with no
  SDD source present (FR-010) — verified by the offline smoke (`quickstart.md`).

### Version-resolution state (shared `resolve-versions`)

Inputs: `github.event_name`, `inputs.version`, `github.event.release.tag_name`,
`github.ref_name`, both evaluated `<Version>` values.

Outputs: `contracts_version`, `cli_version`, `push` (shared boolean).

| Event | `contracts_version` | `cli_version` | `push` | Failure mode |
|-------|---------------------|---------------|--------|--------------|
| `workflow_dispatch`, `version` non-empty | `strip-v(inputs.version)` | evaluated CLI `<Version>` | `true` | — |
| `workflow_dispatch`, `version` empty | evaluated Contracts `<Version>` | evaluated CLI `<Version>` | **`false`** (dry run) | — |
| `release: published` | evaluated Contracts `<Version>` | evaluated CLI `<Version>` | `true` | version-bearing tag matching **neither** evaluated version ⇒ **fail**; either evaluated version unreadable ⇒ **fail** |
| `push: tags v*` | evaluated Contracts `<Version>` | evaluated CLI `<Version>` | `true` | same guards as `release` |

`strip-v(x)` removes one leading `v`. The version-bearing tag is a **coherence check** (must
match at least one live line, Decision 2), never the version source.

### Publish decision (per package, gated)

```
gate tests green ─► pack <project> -p:Version=<resolved> ─► assert *.nupkg non-empty (FR-007)
   if push==true ─► nuget push --skip-duplicate (idempotent, FR-008) to the org feed
```

- Canonical-repo guard `github.repository == 'FS-GG/FS.GG.SDD'` on every job ⇒ no fork publish
  (FR-009).
- Least-privilege: only publish jobs gain `packages: write`; push uses the run-scoped
  `GITHUB_TOKEN`, no PAT (FR-002).
- A non-duplicate push failure fails the run; either publish job failing fails the run (FR-012).

## State transitions (a release run)

```
event ─► resolve-versions ──┬─► contracts-tests ─► publish-contracts ─┐
                            └─► cli-tests       ─► publish-cli        ─┴─► run result
                                                                          (fails if EITHER fails)
```

Dry run (`push==false`): both publish jobs pack + assert, neither pushes.

## Out of scope / unchanged

No `.fsgg` schema, contract surface, contract version, or CLI command behavior changes. The CLI
fsproj is already `PackAsTool`/`ToolCommandName=fsgg-sdd`; no fsproj edit is required. The
cross-repo registry record and the `.github` coherence-gate wiring are owned by FS-GG/.github#49,
not here.
