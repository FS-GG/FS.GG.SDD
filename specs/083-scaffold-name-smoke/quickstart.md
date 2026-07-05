# Quickstart — Composition Smoke: Hyphenated Scaffold Name

How to run and validate the feature-083 guard. Prerequisite: .NET 10 SDK; for the gated run, a
registry file that resolves the real published provider (as the CI workflow provides).

## 1. Offline inner loop (default — the gated fact self-skips)

```sh
dotnet test tests/FS.GG.SDD.Acceptance.Tests   # FSGG_SDD_ACCEPTANCE_REGISTRY unset
```

Expected: the new gated fact reports **Skipped** (discovery-time static skip); the always-on
offline companion (request forwards the descriptor-resolved name key + hyphenated value, names
no rendering token) **passes**. No network touched. (Spec SC-003 / FR-004.)

## 2. Gated composition smoke (network — the real guard)

Point the env at a registry that resolves the real provider, then run only the
composition-acceptance lane (as the workflow does):

```sh
export FSGG_SDD_ACCEPTANCE_REGISTRY=/path/to/real-provider-registry.yml
dotnet test FS.GG.SDD.sln --filter "kind=composition-acceptance"
```

Expected: `hyphenated scaffold name builds and tests green` **passes** — the real provider is
scaffolded with `Roquelike-DungeonCrawler`, the produced product's `dotnet build` and
`dotnet test` both exit 0, and the composition verdict is `pass`. If the provider is
unreachable, the fact resolves `skip-unavailable` (not a failure). (Spec SC-001 / FR-002/FR-005.)

This is the run that turns **FS.GG.SDD#150** green (spec SC-005). CI drives it on the nightly
schedule, on `composition-registry-updated` dispatch, and on manual `workflow_dispatch`.

## 3. Prove the guard actually fences the regression (SC-002)

Temporarily revert feature 080's derivation (template the raw name into identifier contexts),
then re-run step 2. Expected: `dotnet build` fails with the `FS0010: Unexpected keyword 'open'`
hyphen-in-namespace class of error and the fact **fails** naming the build diagnostic — the
guard demonstrably catches the *Hollow Depths* footgun. Restore the derivation; the fact goes
green again.

## 4. Confirm no schema / CI drift

```sh
dotnet test tests/FS.GG.SDD.Acceptance.Tests --filter "FullyQualifiedName~result schema golden"
```

Expected: the `composition-acceptance-result` v1 golden and determinism facts stay green — the
new fact added no schema field (spec FR-008). Confirm `.github/workflows/composition-acceptance.yml`
still selects with `--filter "kind=composition-acceptance"` and gained no new job (spec FR-007).

## Notes

- An empty-but-green `dotnet test` in the produced product (exit 0, zero tests) satisfies the
  pass condition — the fact proves the produced test project compiles and runs, not a test count.
- The hyphenated name value is a generic author token; the parameter **key** is resolved from
  the registry descriptor at run time, so generic SDD carries no rendering identity (FR-006).
