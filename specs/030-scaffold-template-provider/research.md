# Phase 0 Research: Scaffold Runnable Products via Template Providers

All NEEDS CLARIFICATION from Technical Context are resolved below. Decisions 1–4 were
confirmed interactively with the maintainer on 2026-06-26 after surveying the existing
FS.GG.Rendering repo.

## Existing-state findings that shaped the decisions

- `fsgg-sdd init` already writes exactly the SDD skeleton (`.fsgg/`, `work/`,
  `readiness/`, `AGENTS.md`, `CLAUDE.md`) via `Foundation.fs:initEffects` and refuses
  unsafe authored overwrites (`CommandEffects.canOverwrite`). It is the no-provider path.
- The report machinery is a single `CommandReport` projected three ways
  (`CommandSerialization` JSON, `CommandRendering` text, `Cli/Rendering` rich) with
  deterministic ordering; new commands plug in by extending the DU + report fields.
- `ArtifactOwner.GeneratedProduct` **already exists** (`ArtifactRef.fs:6-10`,
  serialized `"generatedProduct"`) but is unused — it is the natural ownership marker
  for provider-produced runtime files.
- `nextLifecycleCommand` (`CommandTypes.fs:491`) returns `None` for the cross-cutting
  `Agents` and `Refresh` commands — the precedent scaffold follows (FR-015).
- **FS.GG.Rendering already ships a working `dotnet new` template** (`.template.config/
  template.json`, identity `FS.GG.UI.Template`, short name `fs-gg-ui`, source
  `template/base/`) that produces a runnable Elmish/MVU + SkiaSharp/OpenGL app via
  `dotnet new fs-gg-ui --name MyApp && dotnet run`. It has **no** SDD awareness, no
  request/response contract, and no provider executable. The harness exe under
  `tools/Rendering.Harness` *runs/validates* apps; it does not scaffold.

The last finding is decisive: the reference provider's real, already-built
materialization engine is `dotnet new`. That makes a `dotnet new` wrapper the
lowest-friction path to an actually-runnable Rendering product while a thin descriptor
keeps the generic contract clean.

## Decision 1 — CLI surface: a dedicated `scaffold` command

**Decision**: Add `fsgg-sdd scaffold` as a new `SddCommand`, not a flag on `init`.

**Rationale**:
- Keeps `init` **byte-identical**, so SC-003 ("no-provider output equivalent to today's
  init, verified byte-for-byte") holds without touching the init path at all.
- Gives provider options (`--provider`, `--param`, `--force`) a clean home without
  widening `init`'s contract.
- Spec §Assumptions explicitly leaves this to the plan; both satisfy requirements.

**Alternatives considered**:
- *`init --provider …`*: smaller DU, but every provider flag becomes part of `init`'s
  contract, and byte-equivalence of the no-provider path must be re-argued per change.
  Rejected for contract hygiene.

**Lifecycle posture**: `nextLifecycleCommand Scaffold = None` (cross-cutting, like
`Agents`/`Refresh`), satisfying FR-015 ("not a new lifecycle stage … MUST NOT … emit a
lifecycle successor of its own"). The scaffold report's `NextAction` is also `None`; the
`ScaffoldSummary` carries an **informational** note that the SDD skeleton is ready and
the lifecycle begins at `charter`. This guides the author without inserting scaffold into
the canonical `charter → ship` chain or making anything point to `charter` except `init`.

## Decision 2 — No-provider behavior: `scaffold` requires `--provider`

**Decision**: `scaffold` with no `--provider` is a clean, actionable error
(`scaffold.providerMissing`) that points the author to `fsgg-sdd init` for skeleton-only.

**Rationale**:
- Keeps the two commands' purposes crisp: `init` = skeleton, `scaffold` = skeleton + a
  named runtime provider.
- FR-005/US2 ("SDD stays useful with no provider") are delivered by `init`, which stays
  untouched — the no-provider contract has a clear owner.
- Avoids two divergent skeleton-only code paths.

**Alternatives considered**:
- *`scaffold` aliases init when no provider*: convenient one-command-to-learn, but
  duplicates init's no-provider guarantee on a second surface and blurs intent. Rejected.

**Consequence for ordering of effects**: even though a provider is required, scaffold
still **establishes the skeleton first**, then invokes the provider (FR-004). If provider
resolution/validation fails *before* invocation, the skeleton is already created and the
report says so explicitly ("skeleton created, provider not run") — never presenting an
incomplete scaffold as complete (FR-009).

## Decision 3 — Invocation mechanism: a generic `dotnet new` wrapper

**Decision**: SDD invokes a provider by resolving a **provider descriptor** to a
`dotnet new` template id + source + declared contract version + parameter spec, then
shelling `dotnet new <templateId> -o <targetDir> -p:<key>=<value> …` (with `--force`
only when the author opts in), and diffing the target directory to enumerate produced
paths.

**Rationale**:
- The reference provider already *is* a `dotnet new` template; this reuses it directly
  with no new provider executable required in the common case.
- The mechanism stays **generic**: SDD code references no template id, package id, path,
  or docs URL — every provider-specific value comes from the author-/provider-supplied
  descriptor (`.fsgg/providers.yml` and/or `--provider`/`--param`). SC-005 is
  grep-verifiable.

**Alternatives considered**:
- *Process + JSON wire contract* (provider is any executable speaking versioned JSON):
  the most language-agnostic and the cleanest "provider declares its own contract"
  story, but it requires building a brand-new provider executable in FS.GG.Rendering
  before anything runs end-to-end. Rejected as higher friction given Rendering's
  existing `dotnet new` asset. (Kept as the documented evolution path — see *Contract
  version handshake* below — should non-.NET providers ever be needed.)
- *.NET plugin assembly* (reflection-loaded interface): couples every provider to .NET
  and to SDD's assembly versioning; least aligned with "external, optional,
  language-agnostic". Rejected.

**Contract version handshake** (the one wrinkle of the wrapper model): `dotnet new`
has no native slot for "the SDD contract version I implement." So the *provider-authored
descriptor* declares it, and SDD validates that declared version against its supported
range **before** invoking (`scaffold.providerVersionUnsupported`, no invocation on
mismatch — US3 scenario 2). The descriptor is provider-owned (shipped with / alongside
the template, referenced by the author), so it is still *the provider* declaring its
contract, just via a manifest rather than a wire response. The generic contract is
defined in [contracts/template-provider-contract.md](./contracts/template-provider-contract.md)
independent of `dotnet new`, so a future process+JSON provider can satisfy the same
contract without changing SDD's report or provenance shapes.

**Produced-path determination**: scaffold snapshots the target directory (via
`EnumerateDirectory`) immediately before invoking the provider, runs the provider,
snapshots after, and computes `producedPaths = after − before − (paths SDD itself
created in the skeleton step)`. This is robust to whatever files the template writes and
needs no cooperation from `dotnet new` beyond its exit code.

**Outcome mapping**:
| `dotnet new` exit | producedPaths | Outcome | Notes |
|---|---|---|---|
| 0 | non-empty | `Succeeded` | normal path |
| 0 | empty | `Succeeded` + info `scaffold.providerEmpty` | "successful-but-empty" (edge case), distinct from failure |
| ≠ 0 | any | `Blocked` + `scaffold.providerFailed` (exit code 2, provider defect) | partial producedPaths still listed (FR-009) |

## Decision 4 — Reference provider scope: fixture in SDD + real adapter in Rendering

**Decision**: SDD ships an **in-repo fixture provider** — a tiny real local `dotnet new`
template under `tests/fixtures/scaffold-provider/` with variants (ok / empty /
fails-midway / declares-bad-version / writes-into-`.fsgg`) — to prove the generic
contract and all failure modes with **real** process+filesystem evidence (constitution
VI, no mocks). Separately, the **real** runnable provider is delivered in the
FS.GG.Rendering repo by authoring a provider descriptor over its existing `fs-gg-ui`
template and validating an end-to-end `fsgg-sdd scaffold` → build → run there.

**Rationale**:
- FR-014 requires only that the reference provider be *demonstrable* against the contract
  *without* Rendering specifics in generic SDD. The in-repo fixture satisfies the
  contract tests; the Rendering adapter satisfies the "real runnable product" demo.
- Keeps the SDD test suite hermetic and green without the .NET template engine or native
  GL/HarfBuzz assets present (CI-friendly), while still delivering a genuinely runnable
  Rendering product to authors.

**Alternatives considered**:
- *Fixture only*: would leave authors without a real runnable provider — falls short of
  the feature's headline value (SC-001/SC-002 demonstrated only synthetically). Rejected
  per maintainer direction.

**Boundary**: the Rendering adapter work is tracked as a cross-repo workstream (its own
tasks touch `FS.GG.Rendering/`). The SDD spec's success criteria that require a *runnable*
product (SC-001/SC-002) are verified by the Rendering-repo end-to-end test referenced
from [quickstart.md](./quickstart.md); SDD's own CI verifies the generic contract via the
fixture provider.

## Decision 5 — MVU edge for external process I/O

**Decision**: Add a `RunProcess of command: string * args: string list * workingDir: string`
case to `CommandEffect`, interpreted at the existing edge in `CommandEffects.fs` using
`System.Diagnostics.Process`, capturing exit code + stdout + stderr into
`CommandEffectResult`. `DryRun` short-circuits the process (plans the effect, reports it,
runs nothing).

**Rationale**: Scaffold is a stateful, I/O-bearing workflow → constitution V mandates the
MVU boundary. Keeping process invocation as an effect (not an inline call in a handler)
preserves the pure `plan`/`update` transition and the single real-I/O edge.

**Alternatives considered**: inline `Process.Start` inside the scaffold handler — rejected;
it would push real I/O out of the edge interpreter and break the boundary + testability.

## Decision 6 — Provenance location & refresh exclusion

**Decision**: Provenance is a **project-level** artifact `.fsgg/scaffold-provenance.json`
(schema v1), because scaffolding bootstraps a project, not a per-work-item readiness
view. Produced files are marked `owner: generatedProduct`. `fsgg-sdd refresh` reads the
provenance file and treats every listed path as **externally owned** — excluded from
staleness classification and never regenerated (FR-007 / SC-007).

**Rationale**: aligns with where init writes project config (`.fsgg/`); reuses the
existing `GeneratedProduct` owner; gives refresh a single authoritative source for "hands
off these paths." Determinism: provenance produced-path list is sorted, no clock/abs-path.

**Alternatives considered**: per-work-item `readiness/<id>/scaffold-provenance.json` —
rejected; scaffolding is not scoped to a work item and runs before any work item exists.

## Risks & mitigations

| Risk | Mitigation |
|---|---|
| `dotnet` / template engine absent in environment | Sense + `scaffold.providerUnavailable` diagnostic (provider defect class, degrades, no crash — constitution VIII). |
| Template writes into `.fsgg/`/`work/`/`readiness/` | Post-invocation guard over producedPaths → `scaffold.providerWroteSddTree` (provider defect); report partial state (FR-011/FR-009). |
| Provider stdout leaks non-determinism into JSON | stdout/stderr captured but **excluded** from the deterministic contract; surfaced only in diagnostic text. |
| Cross-repo drift (Rendering descriptor vs SDD contract) | Contract is versioned; Rendering declares the version; SDD validates range; mismatch is an explicit diagnostic, not a silent break. |
| Accidental Rendering specifics in SDD | SC-005 grep test in the Artifacts/Commands suite asserts zero `fs-gg-ui`/`FS.GG.UI`/Rendering-docs-URL occurrences in generic source + generic-contract tests. |
