# SDD928 provider/catalog plan

Transport revalidation repair: use the existing custody and opened-file verification seam; retire
the synchronous read buffer inside its registered worker under the original cleanup end and project
revalidation to a boolean before completing its task. Keep the first captured payload as authority
and preserve KnownFiles binding checks. Measure actual owned allocation sizes and retained capture
task results using bounded fixtures; no transport/profile/schema/version change is selected.

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


## Selected-template data transport follow-up

The accepted Templates441/SDD928 join extends only the private data transport:
select one exact descriptor TemplateId from a multi-template archive, reject alias/identity
ambiguity and nested content roots, and admit inert classifications/descriptions, literal
text/string and single-valued choices with boolean requiredness. One static source mapping
uses only the closed exclusion forms of the selected payload. The sole generated form is
a declared parameter followed by literal hyphen-to-underscore replacement; no regex engine,
conditions, file renames, callbacks or dependency acquisition are admitted. Generated values
are payload substitutions, never CLI parameters. SDK metadata/cache association must match
the same declaration, and the complete generated payload must match captured expected bytes.

Public signatures, descriptor/schema contracts, Linux custody, original budgets and atomic
no-replace publication remain unchanged. Compile Commands immediately after integration;
focused pure archive/cache controls precede the separately selected real Node CLI vertical.
Identifier grammar, portable tool carrier and other-language evidence remain explicit gaps.
No package publication/default activation is selected by this source window.


## SDD1115: literal template parameter transport

Tier1 transport correction, with existing FSI/API/schema shapes unchanged. Extend
the existing public-caller semantic tests before implementation, then repair both
legacy and catalog argument encoding. Legacy planned/failure display derives from
original RunProcess argv in the handler; no global process renderer change.

Focused semantic tests use Commands.Tests ScaffoldCommandTests and
CatalogScaffoldRuntimeTests. Real legacy theories carry tier=slow; the maintained
catalog CLI driver consumes generate-literal-parameters fixture requests for each
case: existing, missing, spaces, empty, equals, option, unicode, default, override.
The generator command is `dotnet fsi --exec scripts/fixtures/catalog-cli-fixture.fsx
generate-literal-parameters <Commands.Tests.dll> <fresh-input-root> <unused-target>
<manifest.json> <case>`. Each selected runtime uses the existing bounded harness
and separately admitted exact SDK10.0.401 profile; skipped native facts and pure
Python controls are not runtime evidence. Retain the original split-encoding causal
failure and exact source/input/exit/EOF/staging facts for both production callers.

No build/native execution is selected by these source edits. Required full source
CI remains; its disposable compatibility packages are not retained release custody.
No version, release, publication or installed adoption is selected.

## Catalogue skill digest reconciliation

Use the existing FileSnapshot.RawBytes in the shared Doctor/Upgrade drift calculation. Tagged
raw declarations use exact SHA-256; bare legacy declarations keep SkillMirror canonical hashing.
Do not strip prefixes or rewrite provenance. Verify LF, CRLF, UTF-8 BOM, byte tampering, missing
raw snapshots and legacy normalization, including the real filesystem read seam and both callers.
Source acceptance does not establish public SDD successor publication or fresh installed doctor coherence.


## Workspace execution contract source boundary — 2026-10-27

Incorporate the accepted October21 design through the
[owning roadmap's correspondence](../../docs/roadmaps/sdd-928-provider-catalog.md#workspace-execution-contract-incorporation--2026-10-27),
under retained owner `sdd_provider_catalog`. This is design incorporation into the existing provider
cohort, with Programme/root retaining the Coordination/Governance/Templates/Wizard join.
Reuse Coordination's protected
[`OrdinaryAssignment` source contract](https://github.com/FS-GG/FS.GG.Coordination/blob/69a582f98813922ff8801f84d7f7980fb1b77f84/src/FS.GG.Coordination.Core/OrdinaryAssignment.fsi):
`WorkspaceExecutionContract` plus `Requirements`, `ExecutionProfile.Capabilities` and independently
supplied `AuthoritativeObligations` completeness. No additional aggregate or wire format is selected.

The producer mapping must preserve accepted WorkspaceModel/change semantics, pinned catalog/provider
and effective prepared configuration, exact per-repository/component revisions and source hashes,
declared tool/command/capability/evidence/skill obligations, and separate observed capability evidence.
A successful descriptor resolution proves declarations only. Independently supplied expected population
and complete authoritative obligations must not be inferred from the candidate being verified.

The next contract/consumer design must make these cases explicit:

| Case | Required design disposition |
| --- | --- |
| Maintained framework repository | Resolve its maintained workspace/work-model inputs without requiring product scaffolding or a fabricated board issue. |
| Generated product | Bind actual effective catalog/provider/provenance inputs and accepted workspace identity; retain SDD-owned and provider-owned boundaries. |
| Multiple languages/components/repositories | Preserve component roots, declarations and each exact revision/source closure; refuse unsupported consumer projection rather than collapsing to a single revision. |
| Local-only work | Retain Coordination's local work identity and the same lifecycle; do not invent board membership or claim current production projection support. |
| Missing, stale or contradictory model/source/obligation inputs | Keep the affected result incomplete or conflicting, with its evidence owner; never substitute empty requirements or a remembered revision. |
| Declared capability without current observed supplier evidence | Retain the requirement and withhold dependent execution; do not label a tool available because a descriptor names it. |
| Unsupported consumer/protocol projection | Refuse that projection while retaining the complete domain value; do not add a parallel decoder or compatibility runtime. |

Before implementation or serialization, Root must identify the authoritative SDD producer and
completeness manifest, exact source/freshness joins, Governance semantic/evidence bindings and the
Templates/Wizard consumer revisions. The selected domain contract already exists; its representation
does not by itself settle those producer/consumer decisions or prove that a runtime enforces them.
Later implementation needs its own feature contract, focused positive/refusal controls and source review;
no API, schema, dependency, decoder, executable recipe or runtime change is made by this plan update.

Retain accepted public2.5.3/source PR1142 and author qualification evidence. Governance423(b), actual
contained consumer/builder acceptance and all remaining #928 acceptance stay open. SDD Atlas record
`sdd/workspace-execution-correspondence` records this design boundary with immutable source citations;
it does not establish installed qualification. Coordination Atlas record
`coordination/ordinary-assignment-source-boundary` has valid functional provenance at
`309046c178a79dc05ba052daf3bd50e19a136cdf`; all five cited paths and hashes verify after fetching
that immutable commit. The protected69a582f source remains the selected shared contract reference.
An earlier local object-acquisition failure did not establish a missing source path or citation defect.
