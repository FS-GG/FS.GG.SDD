# Quickstart / Validation Guide: Default Starter Selection

Runnable scenarios that prove the feature end-to-end. Details live in
[data-model.md](./data-model.md) and [contracts/](./contracts/).

## Prerequisites

- .NET `net10.0` SDK; repo restored/built (`dotnet build`).
- Offline by default — Stories 1–2 touch no network. Story 3 is opt-in/network-gated.

## Scenario 1 — Declared default applied when the author omits the parameter (US1, FR-001/FR-003)

Fixture: `tests/fixtures/scaffold-provider/registries/default-declaring.providers.yml` (a
non-required parameter with a declared `default`, value-agnostic).

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests \
  --filter "FullyQualifiedName~Scaffold&FullyQualifiedName~DefaultApplied"
```

Expected:
- Provider invoked with the declared default value for the parameter.
- Scaffold JSON `scaffold.effectiveParameters` and `.fsgg/scaffold-provenance.json`
  `effectiveParameters` both record the declared default (sorted by key).

## Scenario 2 — Explicit `--param` overrides the default (US2, FR-002/FR-003)

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests \
  --filter "FullyQualifiedName~Scaffold&FullyQualifiedName~Override"
```

Expected: provider invoked with the author's value (not the default); provenance + report record
the override value as effective.

## Scenario 3 — Default value changes with a data-only registry edit (US1 scenario 3, SC-001)

Change only the `default:` in the fixture registry and re-run Scenario 1's test against the edited
fixture. Expected: the new default is forwarded and recorded — **no generic SDD source changed**.

## Scenario 4 — Provenance round-trip + goldens (FR-003)

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests \
  --filter "FullyQualifiedName~ScaffoldProvenance"
```

Expected: `serialize >> tryParse` preserves `effectiveParameters`; a v1 document without the field
parses to `[]`; byte-exact provenance golden matches.

## Scenario 5 — Edge cases (precedence, required, blank)

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests --filter "FullyQualifiedName~Scaffold"
```

Expected: required-with-default still surfaces `scaffold.providerParamMissing` when omitted; a
blank/whitespace default is surfaced, not masked; values forwarded verbatim.

## Scenario 6 — Boundary grep guard (FR-004 / SC-003)

```bash
dotnet test --filter "FullyQualifiedName~Boundary|FullyQualifiedName~GrepClean"
# or the repo-wide guard the feature adds:
! grep -rEn "\bgame\b|\bapp\b" src tests/FS.GG.SDD.Commands.Tests tests/fixtures/scaffold-provider \
    | grep -iv "application\|happ\|mapp" # illustrative; the test encodes the precise guard
```

Expected: zero occurrences of `game`, `app`-as-starter, rendering package/template ids, paths, or
docs URLs in generic SDD source and generic-contract tests/fixtures.

## Scenario 7 — Real-provider default-starter acceptance (US3, FR-006/FR-007) — opt-in

Offline (gating env unset) — must report Skipped, no network:

```bash
dotnet test tests/FS.GG.SDD.Acceptance.Tests
```

Network-gated (against the real published registry):

```bash
FSGG_SDD_ACCEPTANCE_REGISTRY=/path/to/rendering.providers.yml \
  dotnet test tests/FS.GG.SDD.Acceptance.Tests \
  --filter "FullyQualifiedName~Composition"
```

Expected (gated): the fixed composition scaffold runs with **no** explicit starter parameter, the
produced product builds, and the verdict is `pass` (GREEN) — exercising the registry's declared
default by reference, never by name.

## Done when

- Scenarios 1–6 pass offline; Scenario 7 is Skipped offline and `pass` when gated.
- `docs/release/schema-reference.md` + `docs/reference/authoring-contracts.md` document the
  selection contract (a provider author can follow it without reading source — SC-005).
- Cross-repo response posted on FS-GG/FS.GG.SDD#44 redirecting the literal `app → game` flip to
  FS.GG.Templates (FR-009).
