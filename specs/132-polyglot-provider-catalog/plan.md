# SDD928 provider/catalog plan

Use sibling `Fsgg.ProviderCatalog` in the BCL-only shared Contracts leaf. Write signature,
public API semantic tests, then body. The pure Artifacts parser reuses the hardened YAML reader
and returns catalog-specific typed diagnostics; it does not broaden global SchemaVersion.
No MVU is necessary because these functions only consume in-memory data.

The prelude/public API tests preceded implementation. Root's bounded native08 qualification passed
all nine phases and 56 tests: 25 Contracts, 30 Artifacts and one legacy scaffold refusal. Both
reflection baselines are the exact root-accepted compiled native05 outputs (95 Contracts additions
and one Artifacts parser addition, with no removals), validated with updates disabled in native08.

Contracts own declarative shape, validation and resolution. Artifacts owns strict document decoding.
Governance #423 owns supported capability meanings, semantic floors and evidence normalization.
Command execution limits (timeout, cost class, environment IDs) remain declarations; C1 does not
claim runtime enforcement. C2 must join them with the admitted executor before invoking anything.

Root selected the additive source version join: Contracts 7.5.2 → 7.6.0 and shared SDD
2.1.0 → 2.2.0. Only local Project dependency lock edges change; external package bytes remain fixed.
Native08 qualifies this source version join. C1 protected landing is accepted through PR1097;
coordinated publication remains pending.
No feed or consumer pin is changed here. Legacy published consumers remain compatible.


## SDD928-C2.1 — read-only catalog preparation

Add explicit digest-checked `catalog inspect`, canonical semantic digest projections and closed schema-2 provenance reading/ownership projection. Preserve C1 parser/resolve and every legacy constructor. No runtime Governance reference or invocation is introduced. Inspection reports prepared declarations only, reads the explicitly selected local file and never writes or probes tools. `scaffold --catalog` refuses as unavailable before effects.

Verify raw/semantic tamper, sorted declarations versus literal argv, Unicode/control byte goldens, explicit parameter identities/defaults, schema-1 parity, complete synthetic schema-2 ownership correspondence, and malformed/unsupported provenance blocking refresh/lifecycle mutation. Real executable provider qualification remains C2.2 under root admission.

## C2.2 local candidate source window

Reuse the frozen owning roadmap transport/admission/staging/provenance/WorkspaceModel design.
The actual Config archive is source dbbffb5, version 0.3.0, SHA256
997573453a0c42c3d0714321641ea254d0d4707ea520b6f68dedbb5f4a3bc8a8. Its isolated producer
suite (83 tests), single pack and package-only synthetic consumer passed; publication is pending.
Do not copy its resolver or reference sibling Governance source. Source Contracts7.6 retains the
legacy command shape, but native binary compatibility is an explicit first prelude qualification.
Keep local candidate dependency selection explicit; unavailable public dependencies cannot land
as a released default. No provider/template native execution precedes its own concrete admission.

Order: spec and policy/admission/MVU signatures -> compile connected implementation and
public API callers immediately -> focused semantic tests -> one real CLI creation and principal
failure cases. Repeat compilation after small implementation changes. Actual Config/Contracts7.6
compatibility is checked against the selected package, without postponing unrelated compilation.
Keep original failures, actual process observations and cleanup separate from semantic acceptance.
Run focused contracts/workflow/source-bundle/CLI/ownership parity checks and required coherent
exact-head checks before delivery; only actual observed fixture acceptance can close C2.2.

## C2.2 first-host mechanism addendum

The explicit catalog route adds `--transport-executable <fully-qualified-file>`. The host supplies
LocalLinux selection independently of provider bytes and policy probe IDs. Unsupported host,
CI/Release requirements, missing ABI/exports or unqualified transport behavior refuse before launch.
The first profile uses Linux-x64/glibc, pidfd_spawn and held directory descriptors. Atomic
renameat2 NOREPLACE publishes into the held parent object and preserves an appearing target.
The supported cooperative namespace excludes adversarial pathname ABA, hostile mounts and escaping
descendants; pathname relocation can leave requested-path commit Unknown. No sandbox is claimed.

The caller holds an opaque Operation before any acquisition. Pure prepare performs no native
observation; run consumes it once. Every phase ceiling includes cancellation/drain/cleanup, with
1MiB per stream/2MiB whole-phase output and separately bounded8MiB raw transport state. Unsettled
launch, readers, children or commit remain owned and block retry/commit/deletion. CLI reports the
original failure/unknown once, then passively retains the owner; bounded reporting does not promise
bounded process exit. Late facts do not upgrade the report or renew filesystem cleanup authority.

Compile the integrated CLI and run ordinary semantic tests as soon as signatures and their
callers are connected. Native ABI/export/offset checks and SDK procfd-root/cache/alias checks
remain focused prerequisites for the affected runtime operations, not for compilation. Reuse
unchanged qualification evidence only for its actual source and environment; rerun changed
helpers and their affected integrations. Use the maintained execution harness below for these
checks and the real CLI fixture. No alternate product executor is introduced.


## C2.2 selected exact tool-manifest join

Within the existing source reservation, the catalog edge reuses shared toolManifestText and
mergeToolManifestText for only `.config/dotnet-tools.json`. Retain immutable captured original
bytes/hash and separately computed expected final bytes/hash. The catalog-only wrapper rejects
invalid UTF8/JSON and duplicate object property names before the shared merge. Conflicting owned
selections refuse; already complete selections retain original formatting without a write.
Absent seed writes are SDD-owned; co-tenant hybrid merges keep whole-file provider ownership.

The pure transition recognizes only that exact additional path, with all unrelated payload and
process/tool/archive facts preserved. The actual edge must verify shared expected bytes/hash
under the original physical file identity before WorkspaceComposed and commit. Source controls
cover shared merge/no-rewrite/conflict/duplicate behavior and exact-path correspondence; real
fixture controls must cover actual readback, original versus final provenance, substitution and
first-refusal cleanup. Source preparation does not qualify those physical controls or close C2.2.


## Current execution plan — 2026-10-09

The [roadmap's cooperative local execution profile](../../docs/roadmaps/sdd-928-provider-catalog.md#cooperative-local-execution-profile--2026-10-09)
is the single definition of the supported failure boundary and validation sequence. It supersedes
older process-only qualification ordering; the product acceptance criteria remain unchanged.

1. Restore actual declared dependencies into the explicit local candidate cache and compile the
   real CLI. Fix compiler errors in small, disjoint file lanes; run affected ordinary tests.
2. Use `scripts/catalog-runtime-validation.py` for bounded compile/test and runtime commands.
   Test changes to that harness once, then rerun the affected integrations; do not copy supervisors
   into each filesystem, child or SDK recipe.
3. Establish a compatible independently owned teardown boundary for future runtime attempts.
   An observed process-lifetime capability alone does not prove the SDK's `/proc` compatibility.
4. Run one actual generic package through CLI preparation, admission, staging, composition,
   provenance readback, atomic publication and cleanup in a disposable workspace. Follow with
   the principal negative cases through that same CLI path.
5. Record source identity, selected archive hashes, command, result and log. Keep current status
   separate from historical attempts. Source edits/reviews use disjoint ownership without CPU
   leases; reserve heavy experiment resources only when their measured needs justify it.

See [validation commands and recovery behavior](../../docs/validation/catalog-runtime.md).
Compilation, helper tests and native fixture acceptance are reported separately. C2.2 remains
open until the real CLI acceptance passes; C3/C4 publication and adoption remain separate.

## C3.1 typed authoring example slice

Implement examples/provider-authoring with IsPackable=false and package references only. Specify
Authoring.fsi before the pure catalog/sealing/admission helpers; compile typed calls immediately
in a disposable copied project with selected private feed and empty cache. The independent policy
is parsed by CatalogScaffoldPolicy and the complete declaration passes the existing adapter.

The sample edge emits the existing request.json manifest and a deterministic archive from explicitly
copied opaque-template data plus unchanged raw test input. Its check entry uses the actual schema2
codec and current driver summary. Program's small init/update/effect boundary selects these sample
operations; no installed command is added. The existing acceptance driver optionally consumes the
producer/check command, preserving its test-helper default and original total budget.

Use retained qualified2.2/publishedContracts7.6/localConfig closure for early compatibility compile;
never interpret it as final2.3/public availability. Final2.3 archive selection, actual CLI→Verify
acceptance and publication remain independent root-owned joins. Determinism and negative input
controls use the actual example executable; reuse unchanged C2.2/C2.3 failure evidence within scope.
