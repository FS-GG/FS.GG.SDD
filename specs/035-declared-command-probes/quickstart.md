# Quickstart: Declared-or-Default Acceptance Build/Run Probes

Validation guide for the declared-or-default probe behavior. See
[`data-model.md`](./data-model.md) for types and
[`contracts/probe-command.md`](./contracts/probe-command.md) for the resolution
contract.

## Prerequisites

- .NET SDK (`net10.0`), repo at `/home/developer/projects/FS.GG.SDD`.
- No network and **no** `FSGG_SDD_ACCEPTANCE_REGISTRY` for the offline checks
  below — the new declared-command coverage is offline by design.

## Scenario 1 — Default path is observably unchanged (P1 · FR-005 / SC-001)

The default offline inner loop stays green; the network-gated composition facts
skip with the registry unset.

```bash
dotnet test FS.GG.SDD.sln
```

**Expected**: build succeeds; all offline tests pass; the
`composition-acceptance` facts report **Skipped** (registry unset). No
`composition-acceptance.json` is written.

## Scenario 2 — Pure resolver: declared beats default, blank → default (P1/P2 · SC-002 / FR-010)

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~ProbeResolutionTests"
```

**Expected** (these spawn **no** process):

- `resolveBuildCommand (Some {Executable="mybuild"; Arguments=["--fast"]}) root`
  ⇒ `{ Executable="mybuild"; Arguments=["--fast"]; WorkingDirectory=root }` —
  never `dotnet` (SC-002).
- `resolveBuildCommand None root` ⇒ `dotnet build` at root.
- `resolveRunCommand None root` over a product with one project ⇒
  `dotnet run --project <discovered>`.
- A `Some` with a whitespace `Executable` resolves to the **default** (FR-010).

## Scenario 3 — Deterministic run-project discovery (FR-008)

**Expected**: `discoverRunnableProject root` returns the same project across
repeated calls over the same product (ordinal-sorted-first); an empty product ⇒
`None`, and the default run probe then yields a diagnosed not-started
`ProbeResult` (no silent pass, no hang).

## Scenario 4 — Declared command executes through the real edge (P2 · FR-003 / FR-006)

A synthetic declared command (deterministic exit, generic tooling only — no
provider identity) is run through the actual probe edge.

**Expected**: the probe invokes the synthetic command, honors the build timeout /
run grace+overall bounds, and returns a `ProbeResult` reflecting the synthetic
command's exit — proving FR-003 end-to-end without a real provider.

## Scenario 5 — Failure modes are diagnosed, never hang (FR-007 / SC-005)

**Expected**: a missing-executable declared command ⇒ `{ Started=false;
ExitCode=-1; … }`; a non-zero exit ⇒ `{ Started=true; ExitCode≠0; Diagnostic=… }`;
a hanging command ⇒ killed at its bound with a timeout diagnostic. Each is a
distinct diagnosed non-zero outcome.

## Scenario 6 — Harness stays provider-agnostic (P3 · FR-009 / SC-003)

```bash
dotnet test FS.GG.SDD.sln --filter "FullyQualifiedName~acceptance project carries no Governance reference"
```

**Expected**: passes — no Governance/rendering identity in `AcceptanceSupport.fs`;
the only command tokens in the defaults are `dotnet`.

## Scenario 7 (optional) — Real provider, unchanged verdict (SC-001)

Only with an external registry available (network):

```bash
FSGG_SDD_ACCEPTANCE_REGISTRY=/path/to/providers.yml \
  dotnet test FS.GG.SDD.sln --filter kind=composition-acceptance
```

**Expected**: the emitted `composition-acceptance-result` verdict is identical in
pass/fail to the pre-change harness (zero regression).
