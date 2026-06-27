# Quickstart: validate the SDD skeleton constitution

Validation guide for `.fsgg/constitution.md` emission. Run from the repo root after implementing
the `initEffects` change. All scenarios use real filesystem fixtures through the public command
surface (no mocks).

## Prerequisites

- .NET SDK (target `net10.0`), `dotnet test` available.
- Build clean: `dotnet build FS.GG.SDD.sln`.

## Run the feature suite

```bash
dotnet test FS.GG.SDD.sln \
  --filter "FullyQualifiedName~InitCommandTests|FullyQualifiedName~ScaffoldCommandTests|FullyQualifiedName~RefreshCommandTests"
```

Expect: green, including the new US1/US2/US3 scenarios and the unchanged dynamic skeleton-set
tests in `ScaffoldCommandTests.fs`.

## Scenario US1 — `init` seeds a valid constitution

```bash
tmp=$(mktemp -d); ( cd "$tmp" && dotnet run --project <repo>/src/FS.GG.SDD.Cli -- init )
cat "$tmp/.fsgg/constitution.md"
```
Expect: a populated constitution (title + principles), **no** `[BRACKET]` tokens, **no**
`FS.GG.SDD`/`FS.GG.Rendering`/`FS.GG.Governance`/provider/template/URL strings. The `init` report
(JSON, default) lists `.fsgg/constitution.md` with `kind: "agentGuidance"`, `ownership:
"authored"`, operation create. (FR-001/002/003/010, SC-001/006)

**Determinism**: run `init` twice into two clean dirs and `diff` the two `.fsgg/constitution.md`
— identical. (FR-007/SC-003)

## Scenario US2 — scaffold delivers it outside provenance

```bash
# using an in-repo test provider registry + lifecycle=sdd, scaffold into a temp dir
# then inspect provenance:
jq '.generatedProduct' "$tmp/.fsgg/scaffold-provenance.json"
```
Expect: `.fsgg/constitution.md` **present in the product** but **absent** from `generatedProduct`.
(FR-004/005, SC-002)

## Scenario US3 — author edits survive re-`init` and `refresh`

```bash
printf '\n# our ratified amendment\n' >> "$tmp/.fsgg/constitution.md"
( cd "$tmp" && dotnet run --project <repo>/src/FS.GG.SDD.Cli -- init )     # re-init
grep -q 'ratified amendment' "$tmp/.fsgg/constitution.md" && echo "preserved on re-init"
# then run refresh for a work item and confirm the constitution is untouched:
( cd "$tmp" && dotnet run --project <repo>/src/FS.GG.SDD.Cli -- refresh <work-id> )
grep -q 'ratified amendment' "$tmp/.fsgg/constitution.md" && echo "preserved on refresh"
```
Expect: both "preserved" lines print. Re-`init` reports the constitution as refused/preserved
(not overwritten); `refresh` does not report it as a generated/stale/external path.
(FR-008/009, SC-004)

## What "done" looks like

- [ ] `InitCommandTests.fs` US1 (exists, populated, placeholder-free, generic, deterministic,
      no-clobber re-run) — green.
- [ ] `ScaffoldCommandTests.fs` US2 (present, absent from `generatedProduct`) + existing dynamic
      skeleton tests — green.
- [ ] `RefreshCommandTests.fs` US3 (author edit untouched; not stale/generated/external) — green.
- [ ] `CommandWorkflowTests.fs` plans the constitution `WriteFile` (clarity assertion) — green.
- [ ] Full `dotnet test FS.GG.SDD.sln` — green; `release-readiness.json` and `PublicSurface.baseline`
      unchanged; `WarningsAsErrors` ratchet still 0.

References: [contracts/init-emission.md](./contracts/init-emission.md),
[contracts/scaffold-provenance.md](./contracts/scaffold-provenance.md),
[contracts/lifecycle-exclusion.md](./contracts/lifecycle-exclusion.md),
[contracts/constitution-content.md](./contracts/constitution-content.md),
[data-model.md](./data-model.md).
