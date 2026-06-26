# Contract: Template Provider (generic, schema-versioned)

**Contract version**: `1.0.0` · **Status**: Draft · **Owner**: FS.GG.SDD (generic)

This is the **generic** agreement between SDD and any template provider. It is defined
independently of any invocation transport so that today's `dotnet new` wrapper and a
possible future process+JSON provider both satisfy the *same* contract, report, and
provenance shapes. It contains **no** provider-specific identifier, template id, path, or
docs URL (FR-002 / SC-005).

## Identity & selection (FR-001, FR-003)

A provider is identified by a **descriptor** the author/provider supplies — never by SDD
code. The descriptor is resolved from `.fsgg/providers.yml` (by `--provider <name>`)
and/or command options. See
[providers-descriptor.schema.md](./providers-descriptor.schema.md).

A provider MUST declare:
- the **contract version** it implements (semver string);
- a **template reference** opaque to SDD (a `dotnet new` template id + source today);
- its **parameters** — each `key`, whether `required`, and an optional `default`.

## Inputs SDD provides (the Scaffold Request)

| Input | Meaning |
|---|---|
| target directory | absolute path the provider materializes into |
| named parameters | `key=value` map (descriptor defaults overlaid with `--param`) |
| supported contract range | the version range SDD will accept; provider's declared version must fall in it |
| force flag | whether the author opted into overwriting a non-empty target |

`dotnet new` realization: SDD first acquires/refreshes the template
(`dotnet new install <source>` then a best-effort `dotnet new update` to upgrade an
already-installed, e.g. NuGet-sourced, package to its latest version — its result is
ignored, since up-to-date/offline is not a failure), then invokes
`dotnet new <templateId> -o <targetDir> -p:<key>=<value> …` (`--force` only when the
author opted in). A non-`dotnet new` provider may realize these inputs by any
transport, provided it honors the same outputs below.

## Outputs the provider yields (the Scaffold Result)

| Output | How SDD obtains it (wrapper model) |
|---|---|
| produced paths | before/after directory diff, minus SDD's own skeleton writes |
| outcome | from process exit code (0 + paths = ok; 0 + none = empty; ≠0 = failed) |
| (optional) diagnostics text | captured stdout/stderr, surfaced only in diagnostic messages |

A provider MUST NOT be required to cooperate beyond producing files and an exit code; SDD
derives produced paths itself, so the contract holds for opaque templates.

## Compatibility rules (FR-008, US3)

1. **Version**: SDD validates the descriptor's declared `contractVersion` against its
   supported range **before** invoking. Out of range → no invocation;
   `scaffold.providerVersionUnsupported`.
2. **Unknown provider**: a `--provider` name with no descriptor → no invocation;
   `scaffold.providerUnknown` (distinguished from a hard failure: skeleton is still
   reported as created, provider not run).
3. **Missing required parameter**: declared `required` param with no value → no
   invocation; `scaffold.providerParamMissing` (surfaces the *declared* missing keys; SDD
   never guesses).
4. **Provider failure**: nonzero exit / process error → `scaffold.providerFailed`
   (provider-defect class, exit 2), partial produced paths still reported.
5. **Empty success**: zero produced paths with success exit → `scaffold.providerEmpty`
   (info), explicitly distinct from failure.

## SDD-owned guarantees (the invocation protocol)

- **Skeleton first** (FR-004): SDD establishes the lifecycle skeleton (init effects,
  unchanged) before invoking the provider.
- **No overwrite by default** (FR-010): a non-empty target aborts with
  `scaffold.targetCollision` (per-path) unless `--force` is given.
- **SDD-tree protection** (FR-011): any produced path under `.fsgg/`, `work/`, or
  `readiness/` → `scaffold.providerWroteSddTree`; reported, not silently accepted.
- **Provenance** (FR-006): SDD writes `.fsgg/scaffold-provenance.json` marking produced
  paths `generatedProduct` (externally owned).
- **Refresh exclusion** (FR-007): SDD's generated-view currency never regenerates or
  flags provenance-listed paths.
- **Determinism** (FR-012): the JSON report + provenance are byte-stable; provider
  stdout/stderr is excluded from the deterministic contract.
- **No provider knowledge in SDD** (FR-002): every provider-specific value originates in
  the descriptor or the provider, verified by the SC-005 grep test.

## Conformance checklist for a provider

- [ ] Ships/points to a descriptor declaring `contractVersion`, `templateId`, `source`,
      `parameters`.
- [ ] Declares all required parameters (so SDD can pre-validate).
- [ ] Materializes only into the target directory; writes nothing under `.fsgg/`,
      `work/`, `readiness/`.
- [ ] Returns a nonzero exit on failure; zero on success (including empty).
- [ ] Carries no dependency on SDD internals; remains usable standalone.

## Evolution note

Should non-.NET providers be required, a process+JSON transport can be added as a second
realization of this same contract: the descriptor would name an executable; SDD would
pass the Scaffold Request as versioned JSON on stdin and read the Scaffold Result as
versioned JSON on stdout. The report, provenance, diagnostics, and outcome state machine
are transport-independent and would not change.
