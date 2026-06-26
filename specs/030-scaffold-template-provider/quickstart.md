# Quickstart / Validation: Scaffold Runnable Products via Template Providers

Runnable validation scenarios proving the feature works end-to-end. Details live in
[contracts/](./contracts/) and [data-model.md](./data-model.md); this file is the run
guide, not an implementation.

## Prerequisites

- .NET SDK (`net10.0`), repo built: `dotnet build FS.GG.SDD.sln -c Release`
- For the **real Rendering** scenario only: the FS.GG.Rendering `fs-gg-ui` template
  installed (`dotnet new install <FS.GG.Rendering source>`) and its native GL/HarfBuzz
  assets available. SDD's own suite does **not** need these.

## Scenario A — fixture provider, happy path (US1 / SC-001)

Uses the in-repo fixture `dotnet new` template (no Rendering specifics).

```sh
# arrange a registry pointing --provider at the fixture template
TMP=$(mktemp -d)
cat > "$TMP/.fsgg/providers.yml" <<'YAML'   # (after init creates .fsgg)
schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: tests/fixtures/scaffold-provider/ok
    parameters:
      - { key: productName, required: true }
YAML

dotnet run --project src/FS.GG.SDD.Cli -- scaffold \
  --root "$TMP" --provider fixture --param productName=Acme --json
```

**Expected**: exit 0; report `outcome: succeeded`; `scaffold.providerName = fixture`;
`producedPaths` lists the fixture's files; `ChangedArtifacts` shows them with
`ownership: generatedProduct`; `.fsgg/scaffold-provenance.json` exists and lists the same
paths; SDD skeleton (`.fsgg/`, `work/`, `readiness/`) present.

## Scenario B — no provider → init guidance (US2 / FR-005)

```sh
dotnet run --project src/FS.GG.SDD.Cli -- scaffold --root "$TMP2" --json
```

**Expected**: exit 1; `outcome: blocked`; diagnostic `scaffold.providerMissing` with
correction pointing to `fsgg-sdd init`. Separately, `init` output is **byte-identical** to
the pre-feature skeleton (SC-003 golden test).

## Scenario C — failure modes are distinct & actionable (US3 / SC-004)

Drive each fixture variant and assert one distinct diagnostic + a well-defined state:

| Invocation | Expected diagnostic | Exit |
|---|---|---|
| `--provider does-not-exist` | `scaffold.providerUnknown` | 1 |
| provider declaring `contractVersion: "9.0.0"` | `scaffold.providerVersionUnsupported` (not invoked) | 1 |
| omit required `--param productName` | `scaffold.providerParamMissing` | 1 |
| non-empty target, no `--force` | `scaffold.targetCollision` (per-path) | 1 |
| `--provider fails-midway` | `scaffold.providerFailed` (partial paths listed) | 2 |
| `--provider writes-into-fsgg` | `scaffold.providerWroteSddTree` (paths listed) | 2 |

Every case reports `skeletonCreated`/`providerInvoked` truthfully — no incomplete scaffold
shown as complete (FR-009).

## Scenario D — provenance & refresh exclusion (US4 / SC-007)

```sh
# after Scenario A
cat "$TMP/.fsgg/scaffold-provenance.json"        # names provider, contract version, paths, generatedProduct
dotnet run --project src/FS.GG.SDD.Cli -- refresh --root "$TMP" --work <id> --json
```

**Expected**: zero provider-produced paths reported as stale/missing/regenerable; they do
not appear in the refresh generated-view ledger.

## Scenario E — output parity (SC-006)

Run Scenario A with `--json`, `--text`, and `--rich`; assert identical facts across all
three and that `--rich` (when redirected / `NO_COLOR`) is byte-identical to `--text`, and
that the `--json` bytes are unchanged by the rich path.

## Scenario F — real runnable Rendering product (SC-002, cross-repo)

Owned and verified in the **FS.GG.Rendering** repo (not the SDD suite):

```sh
fsgg-sdd scaffold --root ./MyApp --provider <rendering-provider-name> --param productName=MyApp
cd MyApp && dotnet build && dotnet run        # a runnable Elmish/MVU + Skia app
fsgg-sdd charter                              # lifecycle continues over the skeleton (US1 AS3)
```

**Expected**: the product builds and runs with no manual wiring; the SDD lifecycle
proceeds normally. This scenario is the end-to-end proof of SC-001/SC-002 and is asserted
by a Rendering-repo test referenced from this plan.

## Done-when

- [x] A–E pass in the SDD suite against the in-repo `dotnet new` fixture provider
      (real fs + process). Covered by `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`
      (A happy path + `--dry-run`, B no-provider, C all failure modes + repeat-scaffold,
      D provenance + refresh-exclusion) and `tests/FS.GG.SDD.Cli.Tests/ScaffoldParityTests.fs`
      (E json/text/rich fact-parity + `--rich` redirected == `--text` + JSON bytes
      unchanged by the rich path).
- [x] SC-005 grep test: zero Rendering-specific ids/paths/URLs in generic SDD source +
      generic-contract tests — `tests/FS.GG.SDD.Commands.Tests/ScaffoldGuardTests.fs`.
- [ ] **Cross-repo (owned by FS.GG.Rendering, not the SDD suite):** Scenario F — the real
      `fs-gg-ui` provider adapter + descriptor and the build-and-run SC-002 proof — is
      delivered and verified by a Rendering-repo test (FR-014 / SC-002). It is explicitly
      cross-repo-owned, not unverified.
- [x] All changed `PublicSurface.baseline` snapshots (Artifacts) and `docs/release/`
      catalog (`release-readiness.json` `scaffold` field, `schema-reference.md` scaffold
      artifacts, `compatibility-matrix.md` Governance note) updated. Commands/Cli/Validation
      method surfaces are unchanged (new DU cases + record fields only).
