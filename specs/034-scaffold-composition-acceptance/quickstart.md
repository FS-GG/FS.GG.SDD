# Quickstart: Scaffold Composition Acceptance

Validates the feature end to end. The acceptance is **opt-in and network-gated** — it does
nothing (skips) unless you point it at an external rendering registry.

## Prerequisites

- .NET SDK capable of building/running the produced UI app (`net10.0`).
- For the real-provider path only: network + package-feed access, and an **author-supplied**
  registry (`.fsgg/providers.yml`) whose `rendering` entry resolves the real published rendering
  template. This file is **external** — it is not in this repo (FR-009).

## 1. Offline inner loop stays green (US3 / SC-003) — no network

```bash
# FSGG_SDD_ACCEPTANCE_REGISTRY is unset → the acceptance skips; everything else passes offline.
dotnet test FS.GG.SDD.sln
```

Expected: all existing suites pass; the `FS.GG.SDD.Acceptance.Tests` facts report **Skipped**
(not passed, not failed). No network is touched. The deny-list guard
(`ScaffoldGuardTests`, now scanning the acceptance project too) passes — no rendering identifier
appears in SDD source or the acceptance code.

## 2. Run the real-provider acceptance (US1) — network required

```bash
export FSGG_SDD_ACCEPTANCE_REGISTRY=/abs/path/to/author/.fsgg/providers.yml
dotnet test FS.GG.SDD.sln --filter kind=composition-acceptance
```

Expected on a healthy run: verdict **pass**. The acceptance, from an empty dir, runs
`scaffold --provider rendering --param lifecycle=sdd` against the real provider and asserts:

- the externally-owned runnable app **and** the SDD skeleton (`init` effects) **and**
  `.fsgg/constitution.md` are all present;
- the produced app **builds** (`dotnet build`) and **runs** (bounded `dotnet run`);
- a git repo was initialized at the product root (or skipped-non-fatal) and every produced
  `.sh` is executable;
- `.fsgg/scaffold-provenance.json` partitions provider paths as `generatedProduct` and marks no
  skeleton/constitution path `generatedProduct`;
- `refresh` regenerates only SDD-owned views and leaves the app code byte-unchanged;
- the scaffold `--json` report's `outcome` is the success outcome (complete).

A result document (`composition-acceptance.json`) records the verdict + per-fact booleans.

## 3. Provenance + refresh partition (US2)

Covered by the same run: inspect the emitted result's `facts.provenancePartitioned` and
`facts.refreshExcludes`, or read the product's `.fsgg/scaffold-provenance.json` directly and run
`fsgg-sdd refresh` to confirm the app code is untouched. See
[contracts/acceptance-protocol.md](./contracts/acceptance-protocol.md).

## 4. Unavailable provider → honest SKIP (SC-004)

With the env set but the feed unreachable / template version unresolvable:

```bash
FSGG_SDD_ACCEPTANCE_REGISTRY=/path/to/registry-pointing-at-unreachable-feed \
  dotnet test FS.GG.SDD.sln --filter kind=composition-acceptance
```

Expected: verdict **skip-unavailable** (test reports Skipped) — never a false PASS and never a
FAIL of SDD itself (FR-008/SC-004).

## 5. Determinism (SC-005)

Run step 2 twice with the same inputs and an available provider; diff the two result documents
with the `sensed` block normalized to null — the deterministic bodies are **byte-identical**.

## 6. Scheduled CI

`.github/workflows/composition-acceptance.yml` runs step 2 on a schedule and via
`workflow_dispatch`, with `FSGG_SDD_ACCEPTANCE_REGISTRY` provided by the workflow — separate from
the offline inner loop.

## What success proves (SC-006)

A single PASS/FAIL/SKIP verdict for the real `rendering` + `lifecycle=sdd` composition, with no
manual inspection — closing the P2 SDD epic's deliverable: *confirm the composition path through
SDD's provider wrapper*.
