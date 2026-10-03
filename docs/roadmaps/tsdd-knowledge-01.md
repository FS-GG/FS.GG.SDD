# TSDD-KNOWLEDGE-01 — Concise project knowledge from Typed SDD initialization

Owner: FS.GG.SDD. Provider composition: FS.GG.Templates. Stage: independent V2
workspace capability; implementation, publication and installed adoption remain open.

Authority: [Unified Roadmap §9.8 and §9.9.1 at protected 7b60b256](https://github.com/FS-GG/.github/blob/7b60b256/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md#991-project-knowledge-from-typed-sdd-initialization).
The 2026-10-02 clarification selects concise findings and a 10 MiB canonical store.
This corrected window supersedes the earlier unlanded original-byte/object-store
proposal under the same feature identity and cost lineage. That proposal read an
obsolete checkout. Its implementation was stopped uncommitted; none of its claimed
capabilities counts as delivered acceptance. Do not restart the cost ledger.

Every newly initialized Typed SDD project starts with reviewable, evidence-backed
findings that people, scripts and agents can capture and retrieve. Source code,
raw logs, transcripts, document dumps, build artifacts and generated indexes are
not knowledge records. Their canonical bytes remain in their existing custody;
knowledge records preserve concise conclusions and revision/digest/run references.

## Existing seams and bounded evidence

Inspected protected producer heads: SDD `388e4e0dbb216de8a37687818af042030d4e26c5`
(source version 2.0.3), Templates `908da309c9a52490cf914c3f2d5ce1eaf1188b2e`
(`FS.GG.Workspace.Template` source version 0.17.0). These are source identities,
not knowledge-capable installed releases.

SDD's `src/FS.GG.SDD.Commands/CommandWorkflow/Foundation.fs` owns `initEffects`
and `scaffoldInitEffects`; `HandlersScaffold.fs` owns effective provider parameters
and successful composition. Generic `init` does not select Typed SDD. The CLI's
`TypedSdd.fs` owns typed entry points. Reuse these seams and existing no-clobber skill
seeding; provider-specific literals do not belong in generic SDD. Scaffold currently
initializes Git but does not perform the caller's initial commit.

Templates' `providers/*.providers.yml` and packed
`templates/*/.template.config/template.json` own provider composition. Current
provider pins differ; inspect the receiver inventory before changing any pin.
Rendering is an external producer and the wizard/registry are `.github` boundaries.
Their active owners receive any necessary narrow integration, not overlapping edits.

Templates' independent preparation is recorded at local commit `a55716a` in
`docs/roadmaps/tsdd-knowledge-receivers.md` and
`docs/roadmaps/evidence/tsdd-knowledge-receiver-preparation-20261003.md`.
Its six-family inventory includes Rendering app/game and Fable player/complete/
legacy variants. All inspected owned raw templates currently produce app-only
workspaces without `.fsgg`; passing `--lifecycle typed-sdd` alone does not initialize
SDD. Fable legacy raw omission remains `sdd` although its provider omission selects
`typed-sdd`. The receiver worker reports ten preparation tests passing; this local
preparation is not protected source or .4 installed acceptance. The wizard's actual
normal initial-commit owner still needs verification in .4; it does not block .1.

Representative source material already inspected includes SDD's
`docs/typed-sdd-lifecycle.md`, `docs/initial-implementation-plan.md` and
`docs/experiments/quint-q1/slices/sir-damage-rule.md`, and Templates'
`docs/reports/2026-08-01-fable-full-stack-toolchain-compatibility-spike.md` and
`docs/reports/2026-09-28-svg-release-d5-public-receiver.md`. Extract findings with
their limits and evidence links; do not import these documents into the store.

The installed BAR skill's `references/project-knowledge.md` and synthetic tests
demonstrate useful evidence/status/conflict semantics. Its combined private
source/knowledge database and retention service are not a generic published
dependency or the size baseline. No BAR corpus or operational state is copied.

## Accepted v1 implementation packet

Canonical layout is `.fsgg/knowledge/schema.json` plus current
`records/<stable-id>.json`, encoded as portable UTF-8 structured text. Add configuration
only when needed and include every such byte in size accounting. There is no object
store, embedded record-version archive or in-store backup. Reject unknown payload
files and unsafe paths/links rather than excluding them from the measured population.

Record fields: `schemaVersion`, stable `id`, `kind`, `title`, concise `summary`,
`rationale`, `limits`, `state`, evidence `basis`, author attribution, creation/update/as-of dates,
optional event date, `scope`, `applicability`, `evidence[]` and typed `relations[]`.
Evidence has a safe locator and applicable repository/revision/path/digest/run
identities; relations connect bugs to causes/fixes and experiments to outcomes.
States distinguish proposals, open questions, reported claims, observations,
accepted decisions, superseded conclusions and retractions. Reported or inferred
claims never become verified observations merely through import or newer dates.
No arbitrary raw-body, attachment or original-file import API is admitted.
Structural checks enforce shape and exclusions, not usefulness or truth; substantive
review and evidence establish those. Avoid claiming a heuristic can detect every
log pasted into a summary.

The aggregate maximum is exactly **10,485,760 bytes**, covering all current canonical
records and schema/configuration bytes. Both common write path and CI check the same
complete population, report current/proposed bytes and growth, accept the exact
boundary and refuse above it. Serialize local writes and compare an expected current
record digest to avoid silent overwrites. Compute the digest from serialized bytes;
do not embed a self-referential digest. Interrupted or over-limit writes leave the
prior complete state. Independent record files support normal concurrent Git edits;
same-record divergence requires explicit reconciliation, never newest-wins selection.

Use Git commits for historical versions, with actual commit IDs returned by history
and version retrieval. Consolidation is an ordinary reviewed change: retain useful
conclusions and provenance in current records and previous versions in Git. Do not
raise the cap or silently delete findings. Git history is outside the size measure.

Search scans canonical records directly initially. Any optional cache lives under
ignored `.fsgg/cache/`, is rebuilt solely from these records, and is unnecessary for
clone retrieval. Views also stay outside the canonical directory. Use one F# API for
capture/update, validate/size, search/filter, get, related, history, selected export
and restore; the CLI, thin `.fsx` example and agent guidance consume it.

Selected-record export contains exact canonical record bytes and a digest inventory,
retains provenance/access restrictions, and explicitly states that it is a current
record export without Git history. Restore preserves owner edits unless an explicit
expected-revision update is supplied, and enforces the same size/schema boundary.
Full-history backup/recovery uses an explicit repository Git clone/bundle stored
outside the canonical directory. Keep that operation distinct from selected-record
export; do not silently export an entire repository. Test history recovery from the
bundle in a fresh clone, with limitations such as shallow history reported honestly.

The shared repository is the access boundary. Private knowledge stays in a separately
access-controlled repository/store; credentials and restricted raw evidence remain
in existing custody. Safe references may cross that boundary only without leaking
private titles, paths, snippets or payloads. Classification labels do not provide
access control. Search, projections and exports inherit the actual canonical boundary.

Typed initialization seeds initial canonical metadata and a concise initialization
record, capture guidance and ignore rules before reporting ready. Generic init can
seed no-clobber guidance without selecting typed lifecycle or enabling its store.
Typed scaffold and explicit typed initialization/authoring entry paths invoke the
same initializer. Preserve existing user content while ensuring broad ancestor,
repository and user ignore rules do not silently hide canonical knowledge. Validate
normal staging and the caller's normal initial commit; caches stay ignored. No
background commit or push is introduced. Later updates accompany their relevant work
through ordinary commits/PRs. A git-unavailable route cannot claim Git qualification.

## Executable milestones

- [x] **TSDD-KNOWLEDGE-01.1 — Bounded canonical findings and safe writes — route: routine.**
  Depends on: none. SDD owns the schema/library, write validation and fixtures.
  Prove representative concise architecture, diagnostic, failed-experiment and
  bug/cause/fix records with evidence references. Test exact-limit acceptance,
  one-byte-over refusal including schema/config bytes, growth reporting, concurrent
  expected-revision conflicts, interrupted-write recovery and prohibited payload
  files. Keep the same size checker available for CI. No original-byte store.

- [x] **TSDD-KNOWLEDGE-01.2 — Shared access, Git history and portable recovery — route: routine.**
  Depends on: .1. SDD owns CLI/F#/.fsx/agent adapters and local browse guidance.
  Prove equivalent record IDs/versions through each access path, text search,
  relations, semantic status and provenance. Make two ordinary Git commits, retrieve
  both versions, consolidate without losing useful conclusions/provenance, and
  recover history from an explicit external Git bundle. Round-trip selected records
  losslessly without overwriting owner edits. Fresh clone retrieval succeeds without
  cache or agent session; public queries/exports reveal no private fixtures.

- [x] **TSDD-KNOWLEDGE-01.3 — Typed initialization and CI enforce the contract — route: routine.**
  Depends on: .1/.2. SDD owns initialization seams, seeded guidance, ignore handling
  and a proportionate native CI entry for the common checker.
  Before first development work, typed scaffold and explicit typed entry routes have
  usable canonical records and capture guidance. Re-run preserves owner edits.
  Normal `git add .` and the caller's initial commit include canonical files even
  with representative broad ignore rules; caches remain ignored. Later ordinary
  commits include updates. Independently bypass the write API with an over-limit
  fixture and require CI refusal. Existing lifecycle selections remain unchanged.
  This is the first source milestone changing fresh initialization behavior.

- [ ] **TSDD-KNOWLEDGE-01.4 — All supported provider routes compose initialization — route: routine.**
  Depends on: .1 contract for fixture preparation; .3 for integration acceptance.
  Templates owns route inventory, descriptor requirements and provider composition.
  Exercise supported console, web, Fable bindings, Python, applicable fable-game
  variants and external Rendering routes from the actual packed definitions.
  Include raw-template, provider and wizard creation where supported; record who
  completes typed initialization and the normal initial commit. An app-only template
  or a pending warning is not a knowledge-ready project. Refuse insufficient producer
  versions and demonstrate canonical inclusion and retrieval for every admitted route.
  Independent inventory/fixtures may land before producer publication.

- [ ] **TSDD-KNOWLEDGE-01.5 — Published producers and clean installed adoption — route: routine.**
  Depends on: .3/.4; authorized release operation and existing release gates.
  Publish the coherent SDD capability first, then adopt it in Templates/provider
  releases and the `.github` wizard/registry if required. Record exact identities,
  digests, feed readbacks and receiver pins. Clean tool/package homes must use only
  published artifacts to capture/retrieve a decision, diagnostic lesson linked to
  raw evidence, failed-experiment finding and bug/cause/fix record. Prove normal
  initial/later Git inclusion, fresh clone retrieval, history, exact size boundary,
  lossless record export/restore and actual public/private separation. Existing
  source versions are not assigned the new capability retroactively.

- [ ] **TSDD-KNOWLEDGE-01.6 — Retained extraction preserves findings and ownership — route: routine.**
  Depends on: .1/.2 for source/fixture preparation; published tooling for installed proof.
  SDD owns preserving extraction/import with owner-visible proposal/diff and explicit
  application. Extract concise findings from representative existing material; retain
  original documents and private/raw evidence in place. Preserve project edits,
  dates, negative results, provenance and qualification limits. Repeated extraction
  is idempotent; changed conclusions require an explicit versioned update and never
  become current by import time. The migrated canonical store remains within 10 MiB.
  Demonstrate cache-free restored retrieval and history before any old combined store
  can be retired. Retiring a real old store is a separate authorized operation;
  this fixture is not authority to modify BAR's private database.

## Execution lanes and native checks

Current first window is .1, then .2 and .3 under one SDD integration owner; Templates
may prepare .4 inventory and isolated fixtures in parallel. .6 extraction fixtures
can proceed after the record contract without waiting for publication. Sol medium
workers implement this Astra-high corrected plan under the original feature lineage.

SDD owner touch-set: `src/FS.GG.SDD.Knowledge/**`, its focused test directory and API
baselines; then `src/FS.GG.SDD.Cli/Knowledge.fs`, CLI dispatch/project registration,
initialization/typed-entry seams, seeded capture guidance and size-check integration.
Serialize shared project/solution files, CLI dispatch, `Foundation.fs`,
`HandlersScaffold.fs`, `TypedSdd.fs` and skill registration within that owner.
Templates' independent preparation touch-set is `tests/knowledge-receiver/**` and
its dedicated route/evidence documentation. Actual provider pins, packed bootstrap
files and release metadata belong to its later adoption owner. Root owns the unified
index and `.github` integration; external Rendering edits require its current owner.

SDD uses locked restore/build, focused behavioral tests, current public API checks
and `scripts/test.sh` tiers. Use the full native tier for scaffold/CLI subprocess
changes, avoiding unrestricted parallel solution test hosts. Preserve existing
required native gates. Templates uses package restore/pack, its focused receiver
fixtures, `tests/composition/run.sh` and relevant existing product/lifecycle checks.
Publication and public-only receiver verification are distinct from source delivery.
All work remains routine under Unified §4; no new issue/claim or ceremony is implied.

## Later outline, impact and accounting

Measure corpus size/query cost before adding a disposable index. Hosted browsing,
MCP, automated extraction and cross-repository private search need demonstrated
requirements before executable expansion. A convenience integration cannot relax
conciseness, the aggregate limit, Git history or the actual access boundary.

Default-on for new `typed-sdd` initialization; other lifecycle defaults remain as
declared. .3 changes producer source, .5 proves public clean adoption, and .6 proves
retained extraction separately. Before: development findings depend on scattered
documents/session context. After: concise Git-tracked findings are available from
initialization through common human/script/agent access with explicit evidence links.

Native fixture results, exact commits, package identities and installed receiver
reports provide observations. Telemetry begin was again `not-configured`; coverage
and cost attribution remain unknown. Preserve earlier planning/abandoned-work costs
under this original item. Unified §7.4's 5% target/10% ceiling and separate broader
measure remain; useful test execution is excluded from narrow bureaucracy. Missing
attribution is not zero and this plan claims no efficiency qualification.
## Source window readback

The .1–.3 candidate implements the shared store, access routes and typed producer
seams. Its [source qualification receipt](evidence/tsdd-knowledge-source-20261003.md)
records the checks and receiver observations. Milestones .1–.3 merged in [SDD PR 1091](https://github.com/FS-GG/FS.GG.SDD/pull/1091)
at protected `5f07e02cb5064a3f3b9cf43a7d439e9c9a949d29`, independently verified
with whole-candidate tree `c5a14679d1583bd838296be41d8897cfb935a360`.
The hosted deterministic/API/format checks passed exact source head
`e7ca28737c03839f91791bfed8fd2d3f2f7ebeee`. Receiver replay
`7cb5bc8aaf83fbd89cfce99496d5b639137ee8ba` reports 18/18 source probes against
the frozen a46 closure: nine automatic typed provider initializations and nine
standalone raw-product knowledge routes, including three expected raw Fable
generic-init partial-refusal controls. These are source observations, not
installed/public .4–.6 completion. The SDD source CI gate uses the shared checker; generated product
CI enforcement is a Templates/bootstrap composition join requiring the new public
capability floor, and remains pending with .4/.5. No installed availability,
publication or retained-project adoption is claimed here.

The next source window prepares the coherent additive [2.1.0 release](../release/knowledge-2.1.0.md),
including the new standalone SDK, native artifact custody and installed qualifiers, and additive
provider-major-2 admission before effects. It does not close .4–.6 before their actual public and
retained evidence is available.
