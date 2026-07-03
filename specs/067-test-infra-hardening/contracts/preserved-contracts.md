# Preserved Contracts (Tier-2 guardrail)

This feature exposes **no new** external interface. Its "contract" is the set of
existing contracts it MUST leave byte-for-byte unchanged. Any diff here is a
regression, not a feature.

## MUST NOT change

| Contract | How it is pinned | Verification |
|---|---|---|
| Public `.fsi` surface of every `src/**` assembly | The five `PublicSurface.baseline` / `SurfaceBaselineTests` | Baseline tests stay green **without** regeneration |
| Committed surface baselines | `tests/**/PublicSurface.baseline` (×5) | `git diff -- '**/PublicSurface.baseline'` empty post-feature |
| `validation-report` JSON schema + per-cell verdict structure | `ValidateCommandTests`, `Validation.Tests` matrices | Those tests green; schema keys unchanged |
| CLI JSON automation contract (all commands) | Golden/deterministic fixtures, `CommandReportJsonTests` | Green, fixtures unchanged |
| Persisted schemas / provenance (schema v1) | Artifacts tests | Green, no schema-version bump |
| Agent-skill contract, generated views | Existing drift-guard tests | Green |

## MAY change (internal only)

- xUnit collection/parallelization attributes and assembly info.
- Test-support helper locations and duplication (consolidation).
- `tests/fixtures/lifecycle-commands/*` orphan set (wire-in or delete), provided
  no remaining manifest is unconsumed and no *consumed* fixture changes.
- Internal (`.fs`-only, not in any `.fsi`) helpers in
  `src/FS.GG.SDD.Validation/ValidationRunner.fs`: temp-cleanup nesting, the env
  applied by `withPerturbedHost` and the degradation cells.

## The one allowed observable difference

Sensed, already-non-deterministic `validation-report` metadata
(`startedAtUtc` / `durationMs` / `host`) — these are not part of the deterministic
contract (asserted null in tests) and are unaffected in practice by this feature.

## Verification recipe (run at analyze/ship)

```sh
# public surface + golden fixtures unchanged
git diff --stat -- '**/PublicSurface.baseline' 'tests/fixtures' 'src/**/*.fsi'   # expect empty

# baselines assert (not regenerate) green
dotnet test   # all baseline/golden/matrix tests green with FSGG_UPDATE_BASELINE unset
```
