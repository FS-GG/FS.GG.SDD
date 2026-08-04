# 0005: A generated product gets a pinned, opt-in route to the org reference gate set — not a Governance declaration

## Status

Accepted, 2026-08-04. Scoped by FS.GG.SDD#845, which answers
[FS-GG/FS.GG.Governance#385](https://github.com/FS-GG/FS.GG.Governance/issues/385) AC3 under the
decision recorded at
[FS-GG/FS.GG.Governance#386](https://github.com/FS-GG/FS.GG.Governance/issues/386#issuecomment-5176869657).

This is the **SDD half** of a cross-repo contract. The shared decision — which artifact is
authoritative, and that resolution is distribution rather than enforcement — is Governance's, and is
recorded in its repo (`FS.GG.Governance` `docs/decisions/0011-generated-reference-gate-set.md`,
amending its ADR-0009). What is recorded here is only how **FS.GG.SDD** implements its side of that
contract, which is why it is a repo-local record and not an org ADR.

> Citation note: repo-local `0005` is a different document from **org** ADR-0005 (`.fsgg/` slot
> ownership), which this repo also cites. Cite this one by path, never as a bare `ADR-0005`.

## Context

`fsgg-sdd init` owns `.fsgg/` for a generated product. Before this decision it wrote `project.yml`,
`sdd.yml`, `agents.yml`, `constitution.md` and `early-stage-guidance.md` there, and nothing else —
`grep -rn 'WriteFile ".fsgg' src/ --include=*.fs` returned exactly those writes.

The three Governance files are, and remain, present-or-absent facts the tool never produces:

```text
.fsgg/policy.yml        optionalGovernancePolicy        RequiredBySdd = false   State = "notEvaluated"
.fsgg/capabilities.yml  optionalGovernanceCapabilities  RequiredBySdd = false   State = "notEvaluated"
.fsgg/tooling.yml       optionalGovernanceTooling       RequiredBySdd = false   State = "notEvaluated"
```

(`src/FS.GG.SDD.Commands/CommandReports/DiagnosticConstructors.fs`.)

So the generated product — the case that is supposed to be governed from birth — finished `init`
with no governance declaration **and no route to obtain one** except the same manual copy an
ungoverned repository would perform. `docs/adopting-governance.md` documented exactly that manual
step.

Two things changed on 2026-08-04 and are what make this decidable now:

1. The org profile is single-sourced in F#. The published
   `FS.GG.Governance.ReferenceGateSet` YAML is a **generated artifact derived from it**.
2. The package gained a **consumer resolution contract**:
   `buildTransitive/FS.GG.Governance.ReferenceGateSet.targets` defines one explicit MSBuild verb,
   `FsggResolveReferenceGateSet`, that copies the published `.fsgg/` into a consumer. Before this,
   the payload shipped under `contentFiles/any/any/`, which a modern SDK-style `PackageReference`
   does not materialize, and the package carried no `build`/`buildTransitive` targets — there was
   no verb for `init` to emit or invoke.

Three constraints bound any answer:

- **Governance owns its own semantics.** SDD does not parse, evaluate, or author Governance content
  (FR-011). FS.GG.SDD#833 was rejected for duplicating Governance config parsing inside SDD.
- **Enforcement must not depend on reading an installed package.** Governance ADR-0009 §Decision 1
  is preserved, not overturned: a floor a product escapes by deleting a file is not a floor. The
  inherited gate floor stays embedded in the Governance runtime.
- **Declining Governance must stay a supported end state.** SDD's `verify`/`ship` path deliberately
  sees `.fsgg/capabilities.yml` only as an optional presence fact.

## Decision

### D1 — `init` seeds a route, not a declaration

`fsgg-sdd init` writes exactly one new artifact, `.fsgg/governance-resolution.proj`: a minimal
MSBuild project whose only content is a pinned `PackageReference` to
`FS.GG.Governance.ReferenceGateSet`, a destination pointing at the repository's own `.fsgg/`, and a
comment documenting the verb.

It contains **no Governance content of any kind** — no checks, no maturities, no policy, no
capabilities, no tooling. SDD authors none of the resolved profile; every byte of it comes from the
package.

This is the second of the three options FS.GG.SDD#845 AC1 offered, read precisely: emit a minimal
declaration *of the resolution*, and document the verb as the way to adopt the org reference set.
The thing `init` was missing was never a declaration — it was a route.

### D2 — the route is the Governance-owned verb, invoked by the product, never by `init`

Adoption is these two commands, and they are documented in the emitted file itself:

```bash
cd .fsgg
dotnet restore governance-resolution.proj
dotnet msbuild governance-resolution.proj -t:FsggResolveReferenceGateSet
```

`FsggResolveReferenceGateSet` is defined by FS.GG.Governance. SDD deliberately does **not** invent a
second resolution mechanism, does not shell out to it during `init`, and does not wrap it in a new
subcommand. `init` stays offline, deterministic, and free of a network dependency; the product
decides when — and whether — to resolve, and the write lands in its diff where it can be reviewed.

### D3 — the pin is explicit, and lives in the artifact the product owns

The version is one named constant in `Foundation.fs` interpolated into the emitted file. There is no
floating version and no "latest" resolution anywhere on this path. Advancing the constant changes
what **new** products are seeded with; an existing product re-pins by editing its own copy, which is
`StructuredSource` (no-clobber) and is therefore never rewritten underneath it.

### D4 — the file lives in `.fsgg/`, and the destination is overridden

`init` owns `.fsgg/`, and the package's payload (`governance.yml`, `capabilities.yml`, `policy.yml`,
`tooling.yml`, and the controlled-import contract files) collides with **none** of the files `init`
writes there. Keeping the project inside `.fsgg/` also keeps a second buildable project out of the
product root, where `dotnet build`/`dotnet restore` with no argument would then find two.

Two properties are load-bearing rather than defensive:

- `FsggReferenceGateSetDestination` is overridden, because the target's default is
  `$(MSBuildProjectDirectory)/.fsgg`, which from inside `.fsgg/` resolves to `.fsgg/.fsgg`.
- `ManagePackageVersionsCentrally` is set to `false`, because the org-shared build baseline turns
  Central Package Management on repo-wide and a `Version=` attribute under CPM is `NU1008`. Opting
  this one project out is what keeps the pin inside the artifact that carries it (D3).

### D5 — declining is durable

The path is deliberately **absent** from `Drift.expectedArtifactPaths`. `doctor` therefore never
reports it missing and `upgrade` never re-seeds it, so deleting the file is a permanent, supported
answer rather than drift that returns on the next remediation pass. (Most `.fsgg/` init artifacts —
`project.yml`, `sdd.yml`, `agents.yml`, `constitution.md` — are likewise outside that set; the set
reconciles the seeded skills, the early-stage guidance, and `.gitignore`.)

## Consequences

- A generated product now finishes `init` with a **pinned, runnable, reviewable route** to the org
  profile. Proven end to end: from a clean `fsgg-sdd init` in an empty directory, the two documented
  commands produce a `.fsgg/` that FS.GG.Governance's `Config.Loader.loadAndValidate` reports
  `Valid`, with no file copied by hand.
- The `optionalGovernance*` / `notEvaluated` facts stay **literally true**. Seeding a route declares
  nothing; until a product runs the verb, all three files are absent, exactly as before. Those
  constructors are unchanged by this decision.
- A product that declines Governance is unaffected: it ignores or deletes one file (D5), and every
  SDD command behaves identically whether the Governance files are present, absent, or partial.
- Resolution remains **distribution, never enforcement**. Never resolving, or editing or deleting
  what was resolved, changes what a product *declares*; it cannot change what a product *inherits*.
  Nothing in this decision can lower a gate.
- SDD takes on one new cross-repo coupling: the pinned package version. It is a plain constant with
  a regression test asserting it is exact rather than floating, and re-pinning is a one-line change.
  A `ReferenceGateSet` release that moves the payload or renames the verb is a re-pin here, not a
  redesign.
- `init` gains one artifact (`changedArtifacts` 44 → 45) and no new process execution, network
  access, or read frame.

## Verification

- `tests/FS.GG.SDD.Commands.Tests/GovernanceBoundaryCommandTests.fs`, module
  `GovernanceResolutionRouteTests` — the exact non-floating pin, the absence of any authored
  Governance content after `init`, the durability of declining, and (`tier=slow`) the documented
  verb executed against the real published package from a clean `init`.
- The unchanged `GovernanceBoundaryCommandTests` continue to assert `notEvaluated` across the
  lifecycle commands.
