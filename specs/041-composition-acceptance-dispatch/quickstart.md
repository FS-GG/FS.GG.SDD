# Quickstart / Validation: Composition-Acceptance Consumes the Dispatched Registry

Runnable scenarios that prove the consumer half works. References the
[dispatch contract](./contracts/registry-dispatch.md), [data-model](./data-model.md), and the
unchanged 034
[acceptance-protocol](../034-scaffold-composition-acceptance/contracts/acceptance-protocol.md).

## Prerequisites

- .NET SDK `10.0.x`; `bash` available (Linux/macOS, or the CI `ubuntu-latest` runner).
- Repo checked out at root (`FS.GG.SDD.sln` present).

## Scenario 1 — Resolver source selection & verbatim materialization (offline, US1 / FR-002/004/005)

The deterministic resolver is unit-tested through a real shell and real temp files — no network.

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~RegistryResolverTests"
```

**Expected**: green. The facts assert:
- **Dispatch source**: given the event name `repository_dispatch` and non-empty
  `registry_content` (incl. multi-line YAML + special chars), the resolver writes the bytes
  **verbatim** to an ephemeral file, prints that path, exits 0; the file's first-12 sha256 matches
  the advertised `registry_sha256_12`.
- **Manual input overrides secret**: given both a `registry_path` input and a secret, the input path
  is selected (precedence 1).
- **Secret fallback**: given only the secret, its content is materialized.
- **Fail closed (FR-005)**: given the dispatch event with empty/missing content, the resolver exits
  **non-zero** with a diagnostic — no path printed, no silent skip.

## Scenario 2 — Offline inner loop unaffected (US3 / FR-007 / SC-004)

```bash
dotnet test FS.GG.SDD.sln          # no FSGG_SDD_ACCEPTANCE_REGISTRY set
```

**Expected**: green; every `RequiresRegistryFact` composition-acceptance fact reports **Skipped**; no
network touched; wall-clock unchanged versus before this feature (the resolver tests are cheap, local
process spawns).

## Scenario 3 — Dispatch end-to-end against a live registry (US1+US2 / FR-001/006/008/010)

Simulates the producer dispatch locally (no secret edit). Use a registry file that resolves the real
published rendering template (the same external registry the nightly uses).

```bash
# Point the resolver at a real registry's content as if dispatched:
export RUNNER_TEMP="$(mktemp -d)"
export GITHUB_EVENT_NAME=repository_dispatch
export FSGG_DISPATCH_REGISTRY_CONTENT="$(cat /path/to/rendering.providers.yml)"
export FSGG_DISPATCH_REGISTRY_SHA256_12="<advertised-12-char-sha>"

# Resolve + export, then run the unchanged acceptance facts:
eval "$(scripts/workflows/resolve-acceptance-registry.sh --print-env)"   # sets FSGG_SDD_ACCEPTANCE_REGISTRY
dotnet test FS.GG.SDD.sln --filter "kind=composition-acceptance"
```

**Expected**: the acceptance materializes that exact content, resolves the real provider from it, and
runs the **identical** facts as the secret path (skeleton, constitution, build, run, git, chmod,
provenance partition, refresh exclusion, completeness). With the merged Rendering root-build wrappers,
the build and run facts pass and the verdict is **pass** (green). The drift signal (12-char sha) is
echoed for traceability.

> The exact env-var names the script reads are defined in
> `scripts/workflows/resolve-acceptance-registry.sh`; in CI they are wired from
> `${{ github.event.client_payload.* }}`. Adjust the names above to match the script.

## Scenario 4 — CI verification (the real path)

1. **Manual**: Actions → *composition-acceptance* → *Run workflow* with a `registry_path` input
   pointing at a checked-out registry file → the acceptance runs against it (manual override).
2. **Dispatch** (once the org App is provisioned): editing the Templates registry sends
   `composition-registry-updated`; SDD's workflow triggers, materializes the content, runs the facts,
   and records the drift sha in the **Step Summary**.
3. **Scheduled**: the nightly run continues to use the secret unchanged.

**Expected (SC-001/SC-002/SC-006)**: a Templates registry change is tested by SDD within one dispatch
cycle with zero SDD secret edits (drift = 0); the scheduled run reaches a passing verdict; every
dispatch-sourced run records the content hash it tested.

## Identity-leak check (SC-003 / FR-003)

```bash
# No rendering package id / template id / path / docs URL anywhere in SDD source or results:
git grep -nI -e 'rendering.providers.yml content tokens' -- ':!specs/**' ':!docs/**' || true
```

**Expected**: no rendering identity token appears in committed SDD source, the resolver, or any
produced result document — the registry remains the only identity channel, and it is never committed.
