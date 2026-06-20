# Quickstart Validation: Bootstrap and Migration Experience

This guide validates that the Phase 9 bootstrap/migration feature works
end-to-end. It is the validation/run guide for **this feature's** deliverables —
distinct from the shipped consumer `docs/quickstart.md`, which is one of the
deliverables it validates. Implementation detail lives in `tasks.md`; contracts
and entities are referenced, not duplicated.

## Prerequisites

- .NET SDK able to build `FS.GG.SDD.sln` (targets `net10.0`).
- No Governance gate runtime, FS.GG.Rendering package, or monorepo checkout is
  required — that independence is part of what this feature verifies.

## Build

```bash
dotnet build FS.GG.SDD.sln -c Release
```

Expected: clean Release build with no `.fsi`/baseline changes (this feature adds
no public surface).

## Validate the automated lifecycle smoke

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj \
  -c Release --filter "FullyQualifiedName~LifecycleSmoke"
```

Expected outcomes (see [contracts/lifecycle-smoke.md](contracts/lifecycle-smoke.md)
and [contracts/bootstrap-assertions.md](contracts/bootstrap-assertions.md)):

- A disposable project is created and driven `init` → `ship` plus `agents` and
  `refresh`; every stage produces its authored source and generated readiness
  view.
- No `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or `.fsgg/tooling.yml` is
  created or required (family A).
- Two runs over identical inputs yield byte-identical machine-readable readiness
  (family B).
- The emitted next-action chain matches the documented quickstart order
  (family C).
- Commands stay usable with present-but-incomplete Governance files (family D).
- The run needs nothing beyond the SDD projects (family E).

## Validate the full suite stays green

```bash
dotnet test FS.GG.SDD.sln -c Release
```

Expected: the full suite passes, including unchanged `SurfaceBaselineTests`
(no public surface change).

## Capture the real CLI process smoke (readiness evidence)

Run the shipped executable over a throwaway directory, init through ship, JSON
output, with no Governance files present; capture the transcript to
`specs/016-bootstrap-migration/readiness/cli-smoke.txt`. This is real-evidence
for the executable path (Constitution VI), not part of the deterministic
assertion suite.

## Validate the shipped consumer docs

Review the three shipped docs against
[contracts/consumer-docs.md](contracts/consumer-docs.md):

- `docs/quickstart.md`: canonical stage order; per-stage authored source +
  generated view; `agents`/`refresh` currency; readiness artifacts; no-Governance
  prerequisites; generated views framed as outputs (FR-015); FsDocs frontmatter.
- `docs/migration-from-spec-kit.md`: additive setup; artifact mapping table;
  represent-or-defer for no-equivalent artifacts; `specs/`/`.specify/` left
  unchanged; safe to re-apply (FR-007/008/009).
- `docs/adopting-governance.md`: Governance files added after init; usability
  guarantee; the SDD/Governance boundary (FR-010/011/016).
- `docs/index.md` and `README.md` link the new docs.

Confirm the stage order and next-action pointers asserted by the smoke (family C)
match the order documented in `docs/quickstart.md`, so docs and command behavior
cannot drift (FR-014).

## Done when

- The lifecycle smoke and full suite pass.
- The CLI process smoke transcript is captured under `readiness/`.
- The three consumer docs satisfy the consumer-docs contract and are cross-linked.
- No `src/` module, `.fsi` signature, public baseline, or structured schema
  changed.
