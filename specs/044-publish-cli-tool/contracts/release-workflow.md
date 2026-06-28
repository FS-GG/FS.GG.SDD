# Contract: `release.yml` two-package publish workflow

The external interface this feature exposes is a **CI/release-engineering contract**, not an F#
API or `.fsgg` schema. It extends the feature-039 single-package producer
(`specs/039-publish-contracts-package/contracts/release-workflow.md`) to publish **two** packages
in one run: `FS.GG.Contracts` (unchanged behavior) and `FS.GG.SDD.Cli` (the `fsgg-sdd` dotnet
tool, new). This document is the authoritative description for the two-package producer; the YAML
in `.github/workflows/release.yml` is its implementation.

File: `.github/workflows/release.yml` (edited; feature 039 created it).

## Supersession (the one delta from feature 039)

Feature 039's conformance check **C2** — "a mismatched version-bearing tag fails loudly" — is
**generalized**: the tag is now checked against *both* version lines and must match **at least
one** (see "Version-resolution contract"). This is the minimal change required for the repo to cut
a product-line release (`v0.2.0`) while `FS.GG.Contracts` stays on its own `1.1.0` line. Every
other feature-039 contract clause (triggers, gating, idempotency, least-privilege creds,
canonical-repo guard, single-package-scope-per-pack, dry run) is preserved. See research Decision 2
for the FR-014 reconciliation.

## Triggers (unchanged from 039)

```yaml
on:
  release:
    types: [published]
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        description: "Explicit FS.GG.Contracts version to publish. Omit for a pack-only dry run."
        type: string
        required: false
```

The `version` input remains **Contracts-scoped** (research Decision 3). The CLI always tracks its
evaluated `<Version>`. Dual-trigger + concurrency-serialization notes from 039 are unchanged.

**Dispatch coupling note (A3)**: because the `push` flag is shared and the only explicit input is
Contracts-scoped, there is no `workflow_dispatch` path that pushes *only* the CLI — a
push-enabling dispatch always (re)asserts Contracts at `version` and publishes the CLI at its
evaluated version in the same run. This is benign: a re-publish of an already-present Contracts
version is an idempotent `--skip-duplicate` no-op (FR-008). A future `cli_version` input could
decouple the two (deferred, research Decision 3); until then, to publish a new CLI version via
dispatch, supply the current Contracts `version` and let the CLI publish its evaluated value.

## Jobs and gating contract

| Job | Runs when | Contract |
|-----|-----------|----------|
| `resolve-versions` | `github.repository == 'FS-GG/FS.GG.SDD'` | evaluate both `<Version>`s; apply the at-least-one-line tag guard; output `contracts_version`, `cli_version`, `push`. |
| `contracts-tests` | same repo guard | locked restore + `dotnet test tests/FS.GG.Contracts.Tests/...  -c Release`. Gate for `publish-contracts` (unchanged from 039). |
| `cli-tests` | same repo guard | locked restore + `dotnet test tests/FS.GG.SDD.Cli.Tests/... -c Release`. Gate for `publish-cli` (new). |
| `publish-contracts` | repo guard, `needs: [resolve-versions, contracts-tests]` | pack+push `FS.GG.Contracts` at `contracts_version`. |
| `publish-cli` | repo guard, `needs: [resolve-versions, cli-tests]` | pack+push `FS.GG.SDD.Cli` at `cli_version`. |

- Top-level `permissions: { contents: read }`.
- Each publish job adds `permissions: { contents: read, packages: write }` (least-privilege).
- Fork events never satisfy the repo guard ⇒ no publish (FR-009).
- If **either** publish job fails (other than a skipped duplicate), the run fails (FR-012).

## Version-resolution contract (`resolve-versions`, outputs `contracts_version`, `cli_version`, `push`)

Both versions are the **evaluated fsproj `<Version>`** via `dotnet msbuild <proj> -getProperty:Version`:
- Contracts: `src/FS.GG.Contracts/FS.GG.Contracts.fsproj` (→ `1.1.0`, fsproj override).
- CLI: `src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj` (→ `0.2.0`, inherited product line).

| Event | `contracts_version` | `cli_version` | `push` | Failure mode |
|-------|---------------------|---------------|--------|--------------|
| `workflow_dispatch`, `version` non-empty | `strip-v(inputs.version)` | evaluated CLI | `true` | — |
| `workflow_dispatch`, `version` empty | evaluated Contracts | evaluated CLI | **`false`** | — (intentional dry run, FR-004) |
| `release: published` | evaluated Contracts | evaluated CLI | `true` | version-bearing tag matching **neither** evaluated version ⇒ **fail** (FR-005); either evaluated version empty ⇒ **fail** (FR-006) |
| `push: tags v*` | evaluated Contracts | evaluated CLI | `true` | same guards as `release` |

`strip-v(x)` removes one leading `v`. **At-least-one-line guard**: on a real event with a
version-bearing tag (`^v?[0-9]+\.[0-9]+\.[0-9]+`), the stripped tag MUST equal `contracts_version`
**or** `cli_version`, else fail. A non-version-bearing tag is fine (the fsprojs are authoritative).
Each package then publishes its own resolved version regardless of which line the tag matched.

## Pack + push contract (per publish job)

```
restore (locked, once) ─► dotnet pack <project> -c Release -p:Version=$VER --no-restore -o artifacts/packages
                       ─► assert artifacts/packages/*.nupkg non-empty            (FR-007)
   if push == true     ─► dotnet nuget push "artifacts/packages/*.nupkg" \
                            --source https://nuget.pkg.github.com/FS-GG/index.json \
                            --api-key ${{ secrets.GITHUB_TOKEN }} --skip-duplicate
```

- Single explicit project per pack ⇒ single-package scope (FR-001).
- `--skip-duplicate` ⇒ idempotent re-publish (FR-008).
- `${{ secrets.GITHUB_TOKEN }}` + `packages: write` ⇒ least-privilege, no PAT (FR-002).
- Any push failure other than a skipped duplicate fails the run (FR-012).
- `push == false` skips the push step entirely (dry run, FR-004).

## CLI self-containment requirement (FR-010)

The `FS.GG.SDD.Cli` tool package MUST bundle its full runtime closure — including the
`RegistryDocument` YAML loader from `FS.GG.SDD.Artifacts` and `YamlDotNet` — so that, once
installed, `fsgg-sdd registry validate <path>` runs with **no FS.GG.SDD source checkout**. This is
a property of `dotnet pack` on a `PackAsTool` project and is verified by the offline
pack→install→run smoke (conformance C6 below; runnable form in `quickstart.md`).

## Feed visibility requirement (FR-011)

The `FS.GG.SDD.Cli` org package MUST be **public** (as `FS.GG.Contracts` is) so consumer CI
restores it with a run-scoped `GITHUB_TOKEN`. First publish defaults to private; visibility is set
once via the package settings (operational step in `quickstart.md`).

## Out of scope / unchanged (FR-014)

The `FS.GG.Contracts` publish keeps its version source, gating, idempotency, least-privilege creds,
canonical-repo guard, and single-package scope — only the cross-line tag failure is relaxed
(Supersession). No `.fsgg` schema, contract surface, contract version, or CLI command behavior
changes; the CLI fsproj is already `PackAsTool`/`ToolCommandName=fsgg-sdd` and needs no edit. The
cross-repo registry record and the `.github` coherence-gate wiring are owned by FS-GG/.github#49.

## Conformance checks (verification anchors)

- **C1** — manual dry run (`workflow_dispatch`, no `version`) packs both, pushes nothing.
- **C2** — a real event with a version-bearing tag matching neither line fails loudly; a tag
  matching either line publishes both packages at their own evaluated versions.
- **C3** — re-running a published version (either package) completes and pushes no duplicate.
- **C4** — fork event / failing `contracts-tests` or `cli-tests` ⇒ the corresponding push never runs.
- **C5** — feed query lists both `fs.gg.contracts` and `fs.gg.sdd.cli` at their published versions.
- **C6** — offline pack→install→run smoke: the packed CLI tool, installed to a throwaway
  `--tool-path` with no feed, runs `fsgg-sdd registry validate` to success on a well-formed fixture
  and to a non-zero exit on a malformed one (self-containment, FR-010).

See `quickstart.md` for the runnable form of each.
