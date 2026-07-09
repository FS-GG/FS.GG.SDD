# Architecture Decision Records — index

This repo cites ADRs in two distinct series. A bare `ADR-####` citation is ambiguous
unless you know which series it belongs to, so this index resolves every citation to its
defining document.

## Two ADR series (mind the numbering collision)

| Series | Home | Numbering | What it records |
|---|---|---|---|
| **Repo-local** | `docs/decisions/` (this folder) | `0001`–`0003` | Decisions internal to **FS.GG.SDD** |
| **Org (cross-repo)** | [`FS-GG/.github/docs/adr/`](https://github.com/FS-GG/.github/tree/main/docs/adr) | `0001`–`0027`+ | Decisions spanning more than one FS-GG repo |

The two series **number independently and collide on `0001`–`0003`**: repo-local
[`0002`](0002-retire-defect-classes-via-structural-invariants.md) ("Retire Defect Classes")
is a different document from org `0002` ("Composition by scaffold"). Inside this repo,
`ADR-0001`/`ADR-0002`/`ADR-0003` mean the **repo-local** records below; **every other
`ADR-####` citation refers to the org series** and resolves via the table further down.

## Repo-local ADRs (`docs/decisions/`)

| ADR | Title |
|---|---|
| [0001](0001-separate-sdd-product.md) | Separate SDD Product |
| [0002](0002-retire-defect-classes-via-structural-invariants.md) | Retire Defect Classes via Structural Invariants |
| [0003](0003-gap-d-work-model-decision-grammar-and-currency.md) | Gap D — Converge the Decision Grammar and Retire Work-Model Decoration |

New repo-local decisions continue this sequence. Cross-repo decisions belong in the org
series (open a PR against `FS-GG/.github`), not here.

## Org ADRs cited from this repo

These are cited across this repo's code, skills, specs, migration notes, and `CLAUDE.md`
but define nothing here — their home is [`FS-GG/.github/docs/adr/`](https://github.com/FS-GG/.github/tree/main/docs/adr)
(authoritative index: the [org ADR README](https://github.com/FS-GG/.github/blob/main/docs/adr/README.md)).

| ADR | Title | Home |
|---|---|---|
| 0004 | SDD owns the `lifecycle=sdd` constitution, shipped at `.fsgg/constitution.md` | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0004-constitution-ownership-for-lifecycle-sdd-products.md) |
| 0005 | `.fsgg/` slot ownership — SDD owns `project.yml`, Governance owns `governance.yml` | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0005-fsgg-slot-ownership-sdd-project-governance-governance.md) |
| 0006 | `.github` owns the org-shared .NET build config; `RestoreLockedMode` gates on `GITHUB_ACTIONS` (unified CI gate) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0006-org-shared-dotnet-build-config-and-unified-restore-locked-mode-gate.md) |
| 0007 | `FS.GG.Governance.ReferenceGateSet` version-derivation rule | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0007-reference-gate-set-package-version-derivation.md) |
| 0008 | The `fsgg-sdd` CLI is a first-class member of the coherent set (orchestrator axis) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0008-fsgg-sdd-cli-first-class-member-of-coherent-set.md) |
| 0009 | The `fsgg-sdd` CLI is the single orchestrator — detect-and-remediate (doctor/upgrade) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0009-cli-single-orchestrator-detect-and-remediate.md) |
| 0011 | Every agent-skill root carries the full skill union; `fsgg-sdd` owns the mirror | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0011-agent-skill-roots-full-union-orchestrator-owned-mirror.md) |
| 0012 | Dual-publish FS-GG packages to nuget.org alongside GitHub Packages | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0012-dual-publish-to-nuget-org.md) |
| 0013 | Publish to nuget.org via Trusted Publishing (OIDC) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0013-trusted-publishing-oidc-for-nuget-org.md) |
| 0014 | Skill vendoring & mirroring — one manifest, one materialize-and-verify | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0014-skill-vendoring-one-manifest-one-materialize-verify.md) |
| 0017 | Org skill registry + condition-aware `materializes-when` on the manifest | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0017-skill-registry-condition-aware-materialization.md) |
| 0018 | Transient vs durable SDD artifact taxonomy (regenerable output gitignored by role) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0018-transient-durable-sdd-artifact-taxonomy.md) |
| 0019 | Org repo roster registry (`registry/repos.yml`) + coordination kit | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0019-org-repo-roster-registry-and-coordination-kit.md) |
| 0021 | Parallel intra-repo work — claim lock, git worktree, `Paths:` touch-set | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0021-parallel-intra-repo-work-claim-worktree-touchset.md) |
| 0022 | Extract FS.GG.Game as an SDD-driven component | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0022-extract-fs-gg-game-as-an-sdd-driven-component.md) |
| 0025 | First-class shipped surface-mutation event | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0025-first-class-shipped-surface-mutation-event.md) |
| 0026 | Committed compact ship verdict (feature 092) | [org](https://github.com/FS-GG/.github/blob/main/docs/adr/0026-committed-compact-ship-verdict.md) |

The org series is authoritative and grows independently; consult the
[org ADR README](https://github.com/FS-GG/.github/blob/main/docs/adr/README.md) for its
complete, current list (including any not yet cited here).
