# Quickstart / Validation: CLI Version Coherence in Scaffold Provenance

Feature: `052-cli-version-coherence`. Runnable scenarios proving the feature end-to-end. See
`spec.md` for acceptance criteria, `data-model.md`/`contracts/` for shapes.

## Prerequisites

- .NET 10 SDK; `dotnet build FS.GG.SDD.sln`.
- Offline inner loop only (no network / no real provider needed — use the in-repo fixture
  provider used by `ScaffoldCommandTests`).

## Build & test

```bash
dotnet build FS.GG.SDD.sln
dotnet test tests/FS.GG.Contracts.Tests            # Fsgg.Version grammar + Registry delegation
dotnet test tests/FS.GG.SDD.Artifacts.Tests        # provenance serialize/tryParse + back-compat
dotnet test tests/FS.GG.SDD.Commands.Tests         # scaffold advisory + provenance golden
dotnet test tests/FS.GG.SDD.Cli.Tests              # three-projection parity
```

## Scenario A — Provenance records both facts (US1, SC-001)

1. Provider registry declares `minimumCliVersion: "0.3.0"`.
2. `fsgg-sdd scaffold --provider <fixture>` into an empty dir.
3. **Expect** `.fsgg/scaffold-provenance.json` contains `generator.version` (CLI used) **and**
   `requiredMinimumCliVersion: "0.3.0"`, side by side. `schemaVersion` still `1`.
4. Scaffold twice → provenance byte-identical (US1 scenario 3).

## Scenario B — No provider minimum degrades cleanly (US1-2, US2-4, SC-003)

1. Provider registry omits `minimumCliVersion`.
2. Scaffold. **Expect** `requiredMinimumCliVersion: null` (not fabricated); **no**
   `scaffold.cliBehindMinimum` advisory in any projection.

## Scenario C — Behind minimum emits the advisory (US2, SC-002/SC-004)

1. Registry declares `minimumCliVersion` **above** the installed CLI version (e.g. `"99.0.0"`).
2. Scaffold. **Expect**, in `--json`, `--text`, and `--rich`:
   - exactly one `scaffold.cliBehindMinimum` (severity `info`) naming installed, minimum, gap;
   - `nextAction.actionId == "reseedSeededSkills"` pointing at `fsgg-sdd init` re-seed;
   - scaffold **completes**, `outcome` unchanged, **exit code identical** to an at/above run
     (SC-004).

## Scenario D — At/above minimum is silent (US2-2, SC-003)

1. `minimumCliVersion` `<=` installed version (e.g. `"0.0.1"` or exact equal).
2. Scaffold. **Expect** no CLI-staleness advisory (equal is coherent — boundary is "behind").

## Scenario E — Malformed minimum is surfaced, not dropped (Edge, D6)

1. `minimumCliVersion: "not-a-version"`.
2. Scaffold. **Expect** a `scaffold.providerMinimumMalformed` **warning**;
   `requiredMinimumCliVersion: null`; **no** `scaffold.cliBehindMinimum`; scaffold still
   succeeds with unchanged exit code.

## Scenario F — Re-seed remedy is real (US3, SC-006)

1. From a scaffold missing the seeded skills (e.g. produced by an old CLI), **upgrade the CLI**,
   then run `fsgg-sdd init` in place.
2. **Expect** the 15 `fs-gg-sdd-*` skills (`.claude/skills/…`, `.codex/skills/…`) and
   `.fsgg/early-stage-guidance.md` re-materialized, no author-modified file clobbered
   (idempotent/no-clobber). Confirm the advisory's next-action and the docs name this path and
   note `fsgg-sdd refresh` does **not** re-seed.

## Scenario G — Value-agnostic (SC-005)

```bash
git grep -nE 'fs-gg-ui-template|rendering.*template|0\.3\.0' -- src/ | grep -v tests/
```
**Expect** no provider-specific package id, template id, path, or version literal introduced by
this feature in generic SDD `src/`.

## Determinism / regression

- `dotnet test` scaffold JSON golden is byte-stable across runs (no clock/root).
- Old provenance without `requiredMinimumCliVersion` parses (field → `None`).
- Rich output has zero `` when redirected/non-interactive.
