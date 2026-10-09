# SDD #928 provider/catalog delivery

Owner: [FS.GG.SDD#928](https://github.com/FS-GG/FS.GG.SDD/issues/928).
Campaign: `unified-roadmap-20261003`; Unified part: [§9.8 language-independent workspaces](https://github.com/FS-GG/.github/blob/main/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md).
Architecture: [ADR-0092 and linked implementation design](https://github.com/FS-GG/.github/blob/main/docs/adr/0092-descriptor-driven-polyglot-workspace-providers.md).
The original issue and accepted architecture retain authority; this plan adds the first executable window.

Current execution policy: C2.2 follows the [cooperative local profile and delivery sequence](#cooperative-local-execution-profile--2026-10-09).
Compile and test the integrated CLI immediately, then validate one complete generic package path.
Earlier stage plans below retain their product contracts and historical context; their process-only
ordering is superseded by that profile. C2.2 acceptance and C3/C4 publication remain separate.

Reuse the existing schema-v1 Provider records/registry, generic scaffold invocation, default overrides,
legacy F# identifier route, scaffold provenance and reserved-tree/skill-union boundaries. Descriptor
protocol 2.0.0 remains knowledge admission; it is not the new catalog protocol.

- [x] SDD928-C1 — Parse pinned typed catalogs and resolve validated explicit identities — route: routine.
  Depends on accepted ADR-0092; pure declarations need no downstream publication.
  Scope: sibling Contracts/Artifacts ProviderCatalog APIs, focused semantic and golden fixtures,
  text/reflection surfaces and [spec132](../../specs/132-polyglot-provider-catalog/spec.md).
  Acceptance: all five providers remain fixture data, exact versions and invalid defaults refuse,
  explicit identities retain raw bytes, declarations/references remain complete, deterministic
  prepared output, legacy source callers and unsupported-protocol no-effect controls pass.
  Local source preparation does not check this box; root native qualification and merge readback do.

Later outcome outline, under the same issue:

C2 connects generic inspection/scaffold to pinned catalogs, platform/tool admission, validated identity
routing, actual effective provenance and WorkspaceModel, using existing mutation and sole Quint lifecycle
authorities. Invocation requires executable failure/cleanup/no-partial-state/skill-union controls.

C3 delivers the provider-authoring SDK and package-only qualification harness from issue comment
5436166922: minimal provider/examples, deterministic pack/instantiate and actual build/test/package,
wrong-version/tool controls, provenance, cleanup and operational-skill acceptance. Pure fixtures do not
certify providers.

C4 publishes coherent producers and independently verifies exact installed artifacts before Governance
#423/Templates #441 adoption. Package/source/tag/feed byte identity and independent installed consumption
remain mandatory. #928 stays open across this C1 checkpoint.

C1 changes no generated workspace, installed scaffold or lifecycle default. C2 changes only selected new
catalog source behavior; installed adoption follows qualified publication and receiver adoption. Existing
providers retain schema-v1/legacy compatibility. Retained upgrades separately need preview, no-clobber,
original-byte and rollback evidence. This plan does not close V2-LANG or extend its frozen cohort.

Observation: root accepted bounded native08 on 2026-10-05: all nine phases exited naturally with
zero, 56 tests passed (25 Contracts, 30 Artifacts, one legacy scaffold refusal), and source/cache
pins, output census and cleanup passed in 78.867 seconds. Public surfaces retain all existing
members and add 95 Contracts members plus one Artifacts parser member. The exact compiled
baselines were imported and tested with updates disabled. Contracts 7.6.0 and shared SDD 2.2.0
follow their separate additive-version policies; only local project dependency lock edges change.
Catalog schema 2 and descriptor protocol 3.0.0 remain independent of package versions.

C1 protected source delivery is accepted through PR1097 merge `3ebbb191d7c2c60e93d04f7702051c0e079c9f87`, exact-head hosted checks at `bc6634db0f1f6131cbd0f61eb7017152a49b6b39`. C2–C4 and #928 stay open.
No installed provider, invocation, package publication or downstream adoption is certified. Earlier
failed attempts remain failed and retained; their missing process identities remain unknown.

Root telemetry begin failed duplicate facts and has no child token; model/native usage remains
unknown. No usage counter or publication is inferred.


# SDD928 C2/C3: catalog preview to admitted creation

Accepted design baseline, 2026-10-05. Owning repository: FS-GG/FS.GG.SDD; canonical issue: [SDD#928](https://github.com/FS-GG/FS.GG.SDD/issues/928). Current execution follows the cooperative local profile below; this design baseline is not a current-state ledger. Named Unified §9.8 part: [Language-independent workspaces / V2-LANG-01](https://github.com/FS-GG/.github/blob/main/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md#98-feature-parts-and-subroadmap-index). Stage: source integration, before installed adoption. Preserve ADR-0092 and the existing C1, C2, C3, C4 identities.

The first window selected in the 2026-10-05 baseline was **SDD928-C2.1**: an explicit, digest-checked catalog preview with safe provenance readers. It needs no unpublished Governance dependency. The subsequent scaffold window is designed below, but its runtime reference cannot be committed or qualified until an actual Config package has been packed, inspected and selected. Root owns that package join; the existing `sdd_provider_catalog` owner implements the SDD source windows after parent acceptance, using the installed work-roadmap skill and routine route.

## Historical evidence and gaps at the 2026-10-05 baseline

The observations below explain the accepted design. They are historical: the current local
candidate uses the qualified Config archive identified in the feature plan, includes the
provenance readers/source-bundle join, and has exercised the real CLI. Distribution and coherent
delivery remain separate acceptance boundaries.

SDD C1 is delivered by PR1097, merge `3ebbb191d7c2c60e93d04f7702051c0e079c9f87`, with exact-head checks at `bc6634db0f1f6131cbd0f61eb7017152a49b6b39`. Its schema-2 catalog, descriptor protocol exactly `3.0.0`, BCL-only `Fsgg.ProviderCatalog` and Artifacts parser resolve declarations and explicit identities. Existing `specs/132-polyglot-provider-catalog` covers C1 only. Correct the owning roadmap's stale C1 pending/unchecked prose in the C2.1 source outcome; do not re-open or re-qualify C1 as a new item.

Governance Config's whole-request `CapabilityBindings.resolve` is delivered at `09a20140c2a6cf0694b4a14ce896abf8c4674234` (PR442). Its source project version is `0.3.0`; the observed public NuGet archive returned 404. GitHub Packages was not probed. Therefore neither compatible public distribution nor a local qualified artifact is established. Root's retained publication-plan readback identifies an existing locally accepted ReferenceGateSet `1.8.0` content archive, not Config runtime publication. Preserve that archive's original custody; do not silently rebuild or adopt it. The first neutral catalog adoption remains exactly ReferenceGateSet `1.8.0`, contract `1.0.0`; this content archive does not provide executable admission. C1's digest literals are declarations; no existing producer canonicalization algorithm was found in its schema contract.

Reuse the current generic dotnet-template transport, `ScaffoldMutation` ownership checks, SDD skill mirror/materializer authority, and `WorkModelSourceBundle`. Existing scaffold currently installs a source, optionally updates templates, seeds the live skeleton, then instantiates. That order cannot implement the new no-partial-state contract. Existing schema-1 `ScaffoldProvenance.tryParse` has three direct command readers: Foundation's lifecycle selection/diagnostics and HandlersRefresh. All must understand the additive document reader before schema 2 can be emitted. WorkModelSourceBundle currently admits a closed source-path set; provenance is not yet included.

## Frozen source contract

**Entry and compatibility.** Add `fsgg-sdd catalog inspect --catalog <local-file> --catalog-sha256 <raw-sha256> [--provider <id>] [--param key=value]`. Without provider, return sorted provider metadata and typed prompts; parameters require an explicit provider. With provider, resolve exact effective parameters and the three separate identity values using C1. JSON is the deterministic default; text/rich project the same facts and existing color rules. This command returns `prepared`, never admitted/scaffolded/certified. It performs reads only, never tool probes. No fifth auto-discovered `.fsgg` configuration is introduced.

The later invocation is `fsgg-sdd scaffold --catalog ... --catalog-sha256 ... --provider ... --template-archive <local-nupkg> --template-sha256 ... --admission-policy <local-file> --admission-policy-sha256 ... --platform <id> --preflight-timeout-seconds <positive> --scaffold-timeout-seconds <positive>`, plus existing repeatable `--param`, output and dry-run selectors. Policy supplies its own admitted maximum budgets; requested budgets must fit. Dry run uses the same pure preparation and reports observations absent. Absent `--catalog`, existing CLI/API behavior stays on the legacy route; new flags without catalog refuse. In C2.1 `scaffold --catalog` produces a located unavailable-route diagnostic with no effects; only the inspection verb is enabled. No legacy `Provider`, `CommandRequest` or `ScaffoldProvenanceRecord` constructor is extended.

**Types and ownership.** New `FS.GG.SDD.Commands.CatalogScaffoldWorkflow` exposes `CatalogSelection` (catalog bytes, expected raw digest, optional provider and explicit overrides), `CatalogScaffoldRequest` (selection with required provider, target root, pinned archive locator/digest, independently selected policy bytes/digest, selected platform, separate positive preflight/scaffold budgets, dry-run), `prepare : CatalogSelection -> Result<CatalogPreview, Diagnostic list>`, and the later MVU `Model/Msg/Effect/init/update` plus edge interpreter. Public requests, previews and recorded observations confer no execution authority. Keep this host-specific request in Commands, not shared Contracts. C2.1 implements preparation only; it must not add a fake resolver, always-success admission, dormant runtime reference or unimplemented invocation stub.

Artifacts owns new `ProviderCatalogIntegrity` with canonical descriptor/catalog byte projections and `verify : expectedRawDigest:string -> bytes:byte[] -> Result<Fsgg.ProviderCatalog.Catalog, Diagnostic list>`. Raw SHA-256 verifies exact supplied file bytes separately from declared semantic digests. For semantic digests, serialize strict parsed fields as compact UTF-8 JSON without BOM or trailing newline, property order from the C1 schema table; omit only the digest field of the object being hashed. Descriptor digest excludes `descriptorDigest`; catalog digest excludes root `digest` and includes verified descriptor digests. Sort keyed declarations by ordinal ID/key and set-like ID lists ordinally; retain command argument order and string contents, without Unicode normalization. Optional parameter default is emitted as JSON null when absent. Use decimal integers, lowercase JSON booleans and the fixed System.Text.Json default escaping contract, with exact Unicode/control-character byte goldens. Verify every descriptor and catalog digest; formatting-only YAML changes preserve semantic identity but change raw-byte identity. This is an additive C2 verification contract: C1 parse/resolve remains declaration-only and existing fixtures keep their meaning.

Artifacts also owns `CatalogScaffoldProvenance` and `ScaffoldProvenanceDocument`. The latter exposes `Document = Legacy of ScaffoldProvenanceRecord | Catalog of CatalogScaffoldProvenanceRecord`, `parse : string -> Result<Document, Diagnostic list>` and `ownershipProjection : Document -> ScaffoldProvenanceRecord`. Schema-1 parsing/serialization remains unchanged. Schema-2 uses exactly one envelope at the existing `.fsgg/scaffold-provenance.json` path. Its fields are `schemaVersion:2`, generator, declaration, observation, and ownership. Declaration contains raw and semantic catalog digests, catalog/descriptor IDs and revisions, full canonical selected descriptor, effective parameters and distinct identities, archive identity, normalized full admission request, trusted policy identity/digest, evidence-map identity/digest and requested/admitted budgets. Observation contains measured platform, tools, consumed archive digest, transport identity, ordered invocations and actual result plus produced hashes. Ownership contains the complete existing schema-1 ownership categories and lifecycle parameters; it is a projection, never a second independently editable declaration.

The strict schema-2 parser rejects unknown/duplicate properties, unsupported versions, malformed hashes, duplicate/colliding paths, inconsistent declaration/ownership identities or parameters, partial observation and non-success envelopes. It never falls back to schema 1 or absence after a schema-2 error. All malformed/unsupported documents block relevant mutation with a located diagnostic. Roundtrip and ownership projection are public contract tests; synthetic successful records are labeled synthetic. Parsing a coherent record proves structure/correspondence only, not authenticity or fresh execution. The writer is enabled only by the successful C2.2 state transition.

## Admission, transport and evidence joins

SDD `ProviderCapabilityAdmission` is the single adapter into the actual Config package. Its effectful caller receives original normalized declarations and an independently selected policy, resolves the **whole** `ResolutionRequest` again immediately before dispatch, and ignores caller-supplied `ResolvedSet` values. Policy supplies required capability IDs, legitimate semantic-only floors, supported platforms/environments/formats, exact format mappings, budget ceilings and tool-observation specifications. Provider bytes cannot supply or widen these trusted sets. A policy file's digest establishes identity, not trust: the CLI's explicit caller selection is recorded, and managed Governance execution uses its independently configured policy rather than trusting workspace provenance. This creates no new organization policy registry or floors.

Map protocol `3.0.0` to Governance's distinct neutral catalog contract exactly `1.0.0`; do not conflate them. Evidence mapping keys are exact opaque C1 `Format` strings; each maps to explicit `{Id; Version}`, with supported versions checked independently. Missing/conflicting mapping refuses; no string splitting or inferred version. Map only cheap/medium/high/exhaustive cost literals to the corresponding existing DU and exact policy-listed environment IDs to existing environment cases. Convert declared positive timeout and governed relative working/output paths without fallback. Requiredness is the union of policy requirements and provider required flags. Optional unsupported capabilities remain explicitly unsupported/Unknown. Observe all descriptor-declared tool versions for the selected platform, using independently supplied executable, literal version argv and exact bounded parsing specification; a descriptor's version string is never the observation.

Use the selected local nupkg as the opaque dotnet-template transport. Verify bytes, nuspec package identity/version and policy-declared association with the descriptor's opaque TemplateSource; never derive an archive digest from that string. Copy/pin exact archive bytes into an isolated operation directory; install only that file into isolated template-engine state, without update, package discovery or network fallback. Select the exact opaque TemplateId and pass ordinal effective `--key`, value pairs literally; reject transport-option collisions and option-shaped keys/template IDs rather than escaping into a shell. No language switch or F# name transform. The transport runtime and its isolation mechanism must be qualified against an exact observed SDK; unknown isolation semantics refuse before install. No new OS sandbox claim is made.

The MVU phases are prepare/read -> verify policy/catalog/archive -> whole-request resolve -> bounded platform/tool preflight -> stage install/create -> inspect payload and skill union -> compose complete SDD result in staging -> commit -> report. All process operations use the owning process edge with explicit argv, cwd, environment and timeout; capability timeouts do not become scaffold budgets. Preflight and scaffold use separate monotonic whole-operation deadlines (including all children in their phase), bounded output and cancellation; each child receives no more than remaining budget. Environment admission must be enforced by the selected execution profile, not satisfied merely by recognized labels. Cleanup retires children and removes only the owned staging directory. Unknown child termination or commit outcome remains unknown and requires observation before retry.

For this first route require an absent target path and existing parent, with no `--force`; construct the complete workspace in a sibling staging directory, then use an atomic no-replace commit that fails if a target appeared. This deliberate bound makes original target preservation testable and avoids pretending a sequence of file copies is transactional. Existing/nonempty targets are refused without modification; retained upgrades use the separate upgrade contract. Validate physical containment and reject escaping links, reserved `.fsgg`, work/readiness trees, SDD skills and provider-authored mirrors before commit. Reuse SDD init composition and the existing sole skill-mirror authority for the full seeded/provider/driver/product skill union; do not duplicate ownership rules.

Record exact argv values using relative transport paths where possible, and cwd/environment root roles plus relative paths in deterministic provenance. Qualification observes the expanded physical cwd/environment at the real process port and verifies the root-role binding. No clock, random staging name or absolute machine path enters canonical provenance. Runtime diagnostics may retain physical details separately. Byte determinism is promised for identical normalized declarations and identical observed outputs; tool output with volatile bytes cannot be silently normalized into a claimed repeat-identical result. Persist no success envelope on failure; the successful envelope and all produced-path hashes are composed before the atomic commit.

Later Governance Verify receives explicit provider context: original normalized declarations plus this schema-2 source identity and independently supplied policy. Its additive whole-request path uses Config.resolve and typed commands through CommandHost/GateRun to GateExecution, retaining legacy APIs and command-free F#/game behavior. Recompute the new route; legacy cache entries cannot satisfy it. A serialized resolved set or a zero process exit cannot certify evidence. Missing/stale/tampered provenance, mismatched tools, missing outputs and unsupported formats remain refusal/Unknown at their existing consumer. Root owns the companion Governance scope under existing GOV-423-C2, not a duplicate SDD completion checkbox.

## Bounded delivery windows

- [ ] **SDD928-C2.1 — Digest-checked catalog inspection and schema-2 reading** — route: routine; ready now.
  Depends on delivered C1. Extend specs/132's spec/plan/tasks and contracts first, sketch additive signatures, then implement the read-only CLI caller, integrity functions, envelope codec and document/ownership readers. Update the owning roadmap's C1 evidence and append these windows in the same source PR. Acceptance: real file inspection discovers a fifth catalog-only provider with no provider branch; exact identity/default/override preservation; raw/semantic tamper and canonicalization goldens; closed parsers; schema-1 parity; malformed/new-version provenance blocks refresh and cannot silently switch lifecycle; successful schema-2 fixtures preserve all ownership categories. Inspection launches no process and performs no write. This closes only the source preparation window.

- [ ] **SDD928-C2.2 — Admitted, staged catalog scaffold through the real runtime** — route: routine; conditional next window.
  Depends on C2.1 and root's selected actual Config artifact. Implement the adapter and MVU workflow above, explicit scaffold CLI caller, complete observed provenance, physical/timeout/cleanup edge, and revision-bound WorkspaceModel source join. Add provenance selection/capture to the existing WorkModelSourceBundle and producer selection together; do not relax closed-path or coherent-capture controls. Quint retains sole lifecycle/semantic acceptance authority. First acceptance is one generic local package fixture, no ecosystem certification: exact archive/argv/cwd/tool observations, empty strings/spaces/metacharacters, no shell expansion, repeat determinism, full ownership/skill union, no-target-change negatives for missing required bindings, drift, bad policy/map, wrong tools, reserved writes, collisions, timeout/cancellation and cleanup. Pure emitted effects are not evidence that native execution occurred. Root separately admits real fixture operations. C2.2 cannot close on only a helper/test caller.

- [ ] **SDD928-C2.3 — SDD provenance reaches the selected Governance Verify host** — route: routine; conditional integration window.
  Depends on C2.2 and the existing GOV-423-C2 actual Verify caller. Freeze its narrow explicit incoming-context loader with the Governance owner; reuse the request, digests and independent policy defined here. Acceptance runs the real host through GateExecution, proves whole-request refusal before invocation and observes literal argv/deadlines/environment, then missing/stale/mismatched evidence refusal. Existing legacy verify/ship/route and semantic-only floors remain compatible. Completing C2.1 or scaffold creation alone does not complete this integration.

**C3 authoring SDK and harness outline — SDD928-C3.1.** After C2.2/C2.3 real acceptance, expose authoring helpers for canonical catalog sealing, fixture packaging, declared-tool/policy inputs and package-only qualification. Reuse the same production caller; no alternate executor or success-from-declaration shortcut. Qualify positive/failure controls, exact version/tool mismatches, awkward names, skill acceptance and cleanup. Templates#441 owns the four concrete language providers and actual product build/lint/test/entry-point/package/security evidence. Their outcome is not inferred from the generic fixture. This becomes executable when the qualified transport/runtime artifacts and actual Verify route are pinned; then extend only the next bounded harness window.

**C4 publication and adoption stays the original outline.** Root coordinates GOV-423-C3 with SDD928-C4: actual runtime distribution, the retained ReferenceGateSet 1.8.0 content artifact (with original custody), compatible Contracts/SDD versions and then Templates publication. Select release versions from the actual source/package delta; no future CLI version is reserved here. Pack once, verify source/tag/manifests and byte-identical archives on both required feeds, then independently install without sibling checkouts. No C1-only publication.

## Exact first reservation and later dependency boundary

C2.1 source reservation: new Artifacts `ProviderCatalogIntegrity.fsi/.fs`, `CatalogScaffoldProvenance.fsi/.fs`, `ScaffoldProvenanceDocument.fsi/.fs`; new Commands `CatalogScaffoldWorkflow.fsi/.fs` (preparation only); new CLI `Catalog.fsi/.fs` and its narrow dispatch in `Program.fs`; Artifacts/Commands/Cli `.fsproj` compile entries; existing Commands `CommandWorkflow/Foundation.fs` and `CommandWorkflow/HandlersRefresh.fs` document readers. Matching authored-signature baselines under `docs/api-surface`, existing package PublicSurface baselines, focused new tests in Artifacts/Commands/Cli test projects and their compile entries, existing `ScaffoldProvenanceTests.fs`/`ScaffoldParityTests.fs`, catalog fixtures, `specs/132-polyglot-provider-catalog/{spec.md,plan.md,tasks.md,contracts/catalog.md}` plus a provenance contract, and `docs/roadmaps/sdd-928-provider-catalog.md`. No Governance code/package reference, Contracts record changes, shared package props, release, registry or workflow edits belong to this reservation. Parent serializes overlap with another SDD owner. If CLI public option support needs an `Options` edit, reserve only its new catalog option helper without changing old command option sets.

Focused C2.1 checks are the affected semantic/parser/CLI tests, FSI API exercise, public surface/format checks and legacy scaffold/refresh/lifecycle parity. No provider install, browser/model/native-language matrix or package publication is needed. Run repository-required coherent checks at delivery using the existing exact-head route; do not describe unrun checks as passed.

For C2.2, root first acquires the actual Config artifact from the inspected source/coherent dependency closure, observes its nuspec/assembly resolver surface and dependency versions, hashes the once-packed bytes, and proves isolated package-only restore. An already published compatible artifact may be selected only after equivalent inspection. Source version 0.3.0 is a fact about the project, not a ready runtime reference. A locally packed artifact may support coherent source qualification in an isolated feed/cache; it must not be presented as publicly available or committed as a normal default restore pin while unavailable. Root then selects either an available stable package for a deliverable runtime reference or a clearly local candidate branch awaiting the protected release join. No copied resolver, sibling ProjectReference or guessed package pin is permitted. Until then, C2.1 remains fully deliverable and C2.2 dependency files/process effects are unreserved.

## Workspace impact, projection and accounting

C2.1 adds an explicit preview only; installed users change only after SDD publication. C2.2 first changes source behavior for explicit catalog creation into a new target. General fresh workspace contents change at the published SDD/Governance/Templates receiver boundary, followed by .github#3010 wizard/registry adoption and independent clean-creation evidence. Existing console/web/fable-game/fable-bindings routes stay compatible, omitted lifecycle stays `sdd`, and no V2 activation or typed-sdd default is selected. The same Quint WorkspaceModel remains semantic authority. Existing workspaces require a separate conflict preview, owner-byte preservation and Migrated/Ambiguous/Unsupported migration result; no automatic schema-1 conversion or old-client schema-2 support is claimed. After V2 adoption, repair forward under ADR-0091.

Proposed §9.8 link text, appended to the existing language-independent-workspaces row without replacing prior links: `SDD928 C2/C3: digest-checked preview ready; admitted creation depends on actual Config artifact qualification; SDK and public adoption follow` linking to the canonical SDD roadmap. Root updates §0 only after actual closure, in its immediate asynchronous projection. No planning-only PR or new issue is needed.

Invalidation requiring bounded replan: a transport cannot isolate exact template bytes or suppress implicit acquisition; no usable actual Config distribution can be selected; required environment limits cannot be enforced; no-replace commit cannot preserve the target; or a downstream consumer requires incompatible schema/API changes. These fence their affected runtime window, not independent C2.1 work. Detailed parser wire tests and routine naming are implementation decisions within this plan, not user permission questions.

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


## Cooperative local execution profile — 2026-10-09

This profile freezes the first delivery's supported failure model. It supersedes earlier
process-only sequencing and copied qualification recipes, including any requirement to finish
the native qualification chain before compiling the integrated implementation. The C2.2 product
acceptance criteria above remain authoritative.

**Guarantees.** Preserve existing and concurrently appearing targets; build the complete result
in owned staging and publish atomically with no replacement. Preserve literal arguments, exact
archive integrity, independently selected policy validation, bounded captured output and truthful
process/cleanup reporting. A successful report requires observed completion, provenance and
publication; declaration, elapsed time or an absent process is insufficient evidence.

**Supported failures.** Cooperative local tools may fail to start, return nonzero, produce invalid
or excessive output, encounter ordinary I/O errors, receive cancellation, or exceed an observed
monotonic deadline. Deadlines stop new work after expiry is observed and bound the permitted
cleanup interval; they do not promise hard real-time preemption of every syscall or managed
instruction. Retain the original failure and report unresolved acquisition, child, reader or commit
outcomes as unknown. Do not extend an expired cleanup budget or silently retry a consumed owner.

**Excluded conditions.** This first profile does not guarantee recovery from hostile descendants,
competing external reapers, adversarial pathname ABA or mount replacement, kernel/host failure,
unbounded resource exhaustion, or arbitrary asynchronous exceptions injected at every instruction.
It is not an OS sandbox or an OOM-proof execution contract. Findings inside the supported model
must be fixed; proposals to expand the model require a separate explicit profile rather than an
unbounded extension of this delivery. Ordinary ownership and cleanup defects remain in scope.

**Recovery.** Future runtime validation that requires whole-family teardown runs inside a fresh,
independently owned process-lifetime boundary. A PID namespace or equivalent backend must be
observed working with the actual runtime's environment requirements before that integration is
accepted. A process-group signal alone cannot cover children that create independent sessions.
Process teardown does not prove filesystem cleanup or undo a publication; inspect those results
separately. If the backend is unavailable or incompatible, report the blocked runtime check and
continue compilation and ordinary tests. Existing uncertain runs retain their original unknown
state; never infer their cleanup from a new boundary, elapsed time or process absence.

**One maintained harness.** Use `scripts/catalog-runtime-validation.py` for filesystem, child,
SDK and CLI validation, varying commands and fixtures. The harness owns literal execution,
output bounds, deadlines/cancellation and concise outcomes. Product code remains the execution
authority under test. Qualify a harness change once and test its affected integrations. Do not
copy launchers, guardians or review packets into new per-stage frameworks. Keep useful existing
fixtures and historical evidence without making the old orchestration a new prerequisite.

**Delivery sequence.** Compile the actual CLI early and after small changes; run ordinary affected
tests immediately. Then prove one complete generic package fixture through the actual CLI:
preparation, admission, install/create in staging, ownership/skill composition, strict provenance
readback, atomic publication and cleanup. Exercise existing/appearing target preservation, bad
archive or policy, missing bindings/wrong tools, reserved writes, nonzero execution, output limits,
explicit cancellation and timeout through the same path. Inspect target bytes and owned staging
as well as the exit status. Test counts alone do not close this acceptance window.

**Scheduling and evidence.** Run disjoint source edits, reviews and ordinary checks in parallel
without exclusive CPU leases. Bound concurrency for heavy native experiments when resource needs
justify it. Each result records the source revision plus dirty-source digest when applicable,
actual selected package hashes, command, log and observed outcome. Maintain one authoritative
current-state summary with owners, next actions and blockers; keep superseded states in history.
Report completed work, active parallel lanes and problems every 30 minutes during active work.
The [validation guide](../validation/catalog-runtime.md) gives the maintained harness interface.

## Coherent producer source preparation

The isolated source candidate selects shared SDD 2.3.0 for Artifacts, Commands, CLI and
Knowledge, with ordinary package references to published Contracts 7.6.0. The
Contracts producer retains its independent version and tests; its published archive
is reused rather than rebuilt for this SDD release. Config 0.3.0 remains the explicitly
local candidate awaiting distribution, so local qualification does not establish a
normal fresh-clone restore or public availability. The original Config and SDD 2.2.0
qualification archives remain immutable. C1+C2, exact new release qualification,
feed readback and the unknown original ReferenceGateSet 1.8.0 custody remain SDD
release requirements under the [release contract](../../specs/044-publish-cli-tool/contracts/release-workflow.md);
this source preparation does not admit SDD publication or close C2/C4.

A separate Config-only distribution step may unblock the normal dependency restore
after Governance records its narrow release-contract amendment and qualifies the
actual C1+C2/C2.3 journey, the selected Config archive with published Contracts 7.6.0,
both-feed collision checks and installed readback. That new Config operation does
not consume or clear the historical ReferenceGateSet 1.8.0 archive or operation.
After verified public distribution, regenerate and check SDD locks against the
actual published Config bytes before normal source delivery. Config availability
alone establishes neither SDD publication nor Governance runtime-carrier,
ReferenceGateSet or Templates adoption.

## C3.1 selected authoring example slice — 2026-10-09

Source-ready after accepted actual C2.2/C2.3 integration: one nonpackable typed package-only
[authoring example](../../examples/provider-authoring/README.md), under
[spec132](../../specs/132-polyglot-provider-catalog/spec.md#sdd928-c31--package-only-authoring-example).
It reuses public canonicalizers, policy/admission and strict provenance codec, independently supplied
inputs and the maintained CLI/Verify drivers. Retained qualified2.2 archives support immediate
compatibility compile; final2.3 archive qualification and actual receiver journey remain required.
No new SDK package, installed verb, schema, release default or publication is selected. C3.1 remains
open beyond this bounded example slice; C4 distribution stays separate.

Local authoring preparation now has actual cold2.2 package-only compilation (12 package assets,
no ProjectReference) and ten sample-command controls, including deterministic catalog/archive bytes,
input/output refusal and real strict schema2 reading. Final2.3 archive/CLI→Verify qualification
remains pending; this evidence does not mark C3.1 or distribution complete.
