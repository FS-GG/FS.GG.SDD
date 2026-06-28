# Quickstart: Typed Registry Validator

Runnable validation scenarios proving the feature end-to-end. Implementation lives in
`tasks.md`; this is the run/verify guide. See
[contracts/registry-document.md](./contracts/registry-document.md) and
[contracts/cli-registry-validate.md](./contracts/cli-registry-validate.md) for the rule
and entrypoint contracts.

## Prerequisites

- .NET SDK (`net10.0` band) and the repo restored: `dotnet restore`.
- A vendored fixture copy of the real registry file at the test fixtures path (a task
  vendors it from `FS-GG/.github` → `registry/dependencies.yml`).

## Scenario 1 — Canonical file validates clean (SC-001, FR-008)

```sh
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate path/to/dependencies.yml
echo "exit=$?"
```

**Expected**: exit `0`; default/`--json` `CommandReport` reports `Valid` with **zero**
diagnostics. No bare-integer version (`1`,`2`), `1.x` range, prerelease version
(`0.1.52-preview.1`), or repo→repo dependency edge is flagged.

## Scenario 2 — Pure validator over the real fixture (Contracts test, no I/O)

```sh
dotnet test tests/FS.GG.Contracts.Tests --filter RegistryDocument
```

**Expected**: `Registry.validateDocument <parsed-real-fixture>` returns `Valid`. This
proves parity with `scripts/validate-registry.py` on the canonical content (SC-005).

## Scenario 3 — No false positives, no masked defects (SC-002, FR-003/004/005/006)

For each previously-false-positive class, a good case passes and a paired broken case still
fails (asserted in `RegistryDocumentTests.fs`):

| Class | Good (no diagnostic) | Broken (correct diagnostic) |
|---|---|---|
| repo-id edge | `from: sdd, to: governance` | `from: sdd, to: nope` → `UnknownComponent` |
| bare-integer version | `version: "1"` | `version: "1.2.x.4"` → `MalformedVersion` |
| `N.x` range | `range: "1.x"` | `range: "??"` → `MalformedVersion` |
| prerelease | `version: "0.1.52-preview.1"` | `version: "abc"` → `MalformedVersion` |
| consumers | `consumers: [templates]` | `consumers: [ghost]` → `UnknownComponent` |
| duplicate id | unique ids | two contracts with one id → `DuplicateComponent` |
| missing field | all required present | drop a contract `owner` → `MissingField` |

```sh
dotnet test tests/FS.GG.Contracts.Tests --filter RegistryDocument
```

**Expected**: every good case yields no diagnostic; every broken case yields exactly its
rule kind — detection is not regressed by removing the false positives.

## Scenario 4 — Safe load failure (FR-001, US1-S3, Constitution VIII)

```sh
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate does-not-exist.yml; echo "exit=$?"
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate path/to/not-yaml.txt; echo "exit=$?"
```

**Expected**: exit `1`; a single clear load/parse diagnostic (`MalformedDocument` class),
**not** a stack trace or a cascade of misleading content diagnostics.

## Scenario 5 — Determinism (SC-004, FR-007)

```sh
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate path/to/dependencies.yml --json > a.json
dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate path/to/dependencies.yml --json > b.json
diff a.json b.json && echo "deterministic"
```

**Expected**: byte-identical output across runs — suitable for a CI `--exit-code` gate.

## Done / acceptance

- Scenarios 1–5 pass.
- `dotnet build` of `FS.GG.Contracts` shows **no** new package dependency (BCL-only intact).
- Follow-up (cross-repo, not this repo): FS-GG/.github#18 swaps `scripts/validate-registry.py`
  for `fsgg-sdd registry validate` and flips coherence id `registry-validator-typed` to
  `coherent: true`.
