# Quickstart / Validation: scaffold name → valid F# identifier

Runnable scenarios that prove feature 080 end-to-end. See [spec.md](./spec.md) for the FR/SC ids,
[data-model.md](./data-model.md) for the resolution flow, and [contracts/](./contracts/) for the
descriptor field and derivation signature.

## Prerequisites

- .NET SDK (`net10.0`); repo restored (`dotnet restore --locked-mode`).
- For the real-provider smoke only: `FSGG_SDD_ACCEPTANCE_REGISTRY` pointing at a feed serving the
  updated `rendering` provider (network-gated; skipped otherwise).

## Scenario 1 — Derivation is correct and deterministic (offline, unit)

Covers FR-001/003, SC-005.

```sh
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter FullyQualifiedName~FsharpIdentifier
```

Expected: `Roquelike-DungeonCrawler → RoquelikeDungeonCrawler`; `Acme.Foo` and `Acme` unchanged;
`---`/`""` → `Unrepresentable`; idempotence + no-op-on-valid properties pass.

## Scenario 2 — Both params forwarded; raw name preserved (offline, handler)

Covers FR-004/005/006/007, US1-scenario-1, US2.

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold
```

Expected, against a fixture registry declaring `nameParameter` + `identifierParameter`: the
planned/forwarded `dotnet new` args contain **both** `--<name> Roquelike-DungeonCrawler`
(verbatim) and `--<identifier> RoquelikeDungeonCrawler`; the recorded `EffectiveParameters`
(provenance + report) contain both, deterministically ordered; provenance schema stays `1`.

## Scenario 3 — Author override + unrepresentable name (offline, handler)

Covers FR-008 (override wins) and FR-009 (block, exit 1).

Expected: passing `--param <identifier>=Explicit` yields `Explicit` forwarded (no derivation);
a name of `---` blocks with `scaffold.nameUnrepresentable`, exit 1, a `NextAction`, and a not-run
summary (no provenance claiming success).

## Scenario 4 — Deterministic gate stays green offline (SC-006)

```sh
dotnet test FS.GG.SDD.sln -c Debug
```

Expected: full offline suite green; the composition-acceptance facts self-skip (registry unset).

## Scenario 5 — Real-provider build + test smoke (network-gated, SC-001/002/004)

Covers FR-011. Runs in CI `composition-acceptance.yml` (nightly / dispatch) or locally with the
registry set.

```sh
export FSGG_SDD_ACCEPTANCE_REGISTRY=<feed>
dotnet test FS.GG.SDD.sln --filter "kind=composition-acceptance"
```

Expected (once FS.GG.Rendering has adopted the sink symbol — research D8): scaffolding a
hyphenated/misspelled product name against the real `rendering` provider produces a workspace
where `dotnet build` **and** `dotnet test` are green — zero `FS0010`. Until Rendering adopts, this
lane is the concrete red→green signal that closes the cross-repo request.

## Manual end-to-end (optional)

```sh
mkdir /tmp/roque && cd /tmp/roque
fsgg-sdd scaffold --provider rendering --param productName=Roquelike-DungeonCrawler
dotnet build && dotnet test
```

Expected: builds and tests green; `.fsgg/scaffold-provenance.json` `effectiveParameters` shows the
raw name and the derived `rootNamespace`; generated `.fsproj`/paths keep the hyphenated name.
