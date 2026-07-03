# Feature Specification: Architecture longer-term cleanups

**Feature Branch**: `068-architecture-longterm`

**Created**: 2026-07-03

**Status**: Draft

**Input**: FS.GG.SDD roadmap issue #76 (repo-local, not cross-repo) — the final "longer-term" architecture bundle (item #12) from the 2026-07-02 code-quality & architecture review, drawing on §1.4 (module organization), §1.5 (purity soft spots), §3.4/§3.5 (duplication & complexity hotspots), and §6 item 12. Items #1–#11 of that plan shipped as features 059–067; this feature closes the batch. Source: `docs/reports/2026-07-02-140616-code-quality-architecture-review.md` @ 8881620.

**Change Tier**: Tier 2 (internal change). This is a pure refactor / maintainability-hardening feature over `src/FS.GG.SDD.Commands` (chiefly `CommandWorkflow/`) and a small no-clobber-class docs drift guard. It introduces **no** change to any `fsgg-sdd` CLI output, JSON automation contract byte, exit code, stream routing, persisted schema, generated view, artifact layout, agent-skill contract, public `.fsi` surface, or committed baseline. The three readiness JSON views (`analysis.json` / `verify.json` / `ship.json`) MUST serialize byte-for-byte identically before and after. All edits are confined to `.fs`-internal functions that appear in no `.fsi`, plus internal module renames/reorganization and DU introductions that are not observable in any emitted contract. Per the constitution, signatures and baselines remain unchanged.

## Overview

The 2026-07-02 review confirmed FS.GG.SDD's core architecture is sound — a pure
MVU planner core behind a single effect interpreter, strictly acyclic layering,
determinism engineered end-to-end — and that the June god-module finding was
already resolved. Items #1–#11 of the remediation plan (the correctness fixes,
CI-gate hardening, dead-surface removal, shared-seam extractions, and
test-infra work) have shipped as features 059–067.

What remains is item #12: a batch of **longer-term, structural maintainability**
cleanups that are individually low-risk but collectively remove latent traps.
None of them changes what the product does; each makes the code that produces
that behavior harder to break:

- The three readiness JSON views (`analysis.json`, `verify.json`, `ship.json`)
  are emitted by ~450 lines of near-identical hand-written `Utf8JsonWriter`
  code across three handlers, so a field added to one view can silently be
  omitted from the others — the views can **drift structurally** with nothing to
  catch it.
- Every `CommandWorkflow/` file is one flat `[<AutoOpen>] module internal` in a
  single namespace, so hundreds of functions share one scope, call-site
  provenance is invisible, and inter-file dependencies exist only as implicit
  `.fsproj` compile order.
- `ParsingEarly` / `ParsingMid` / `ParsingTasks` are named for their compile-order
  position, not their responsibility, so the file names tell a reader nothing.
- Working state that already has (or deserves) a discriminated union is threaded
  as **raw strings** — view-currency words (`"refreshed"`/`"blocked"`/…), upgrade
  step outcomes (`"wouldApply"`/`"applied"`/…), and step ids — compared with
  string equality across files, so a typo compiles and misfires at runtime.
- A handful of §1.5 **purity soft spots** (filesystem IO inside the Artifacts
  library, a `failwithf` at static init, an ambient-cwd resolve inside a pure
  planner) blur the otherwise-clean pure-core / effect-edge boundary.
- `CLAUDE.md` and `AGENTS.md` are two hand-maintained near-copies with **no
  drift guard**, unlike the skill trees which are guard-pinned — and the repo's
  own doctrine says keep the two agent surfaces aligned.

This feature pays that debt down. The product's observable behavior — every JSON
byte, exit code, and stream — is unchanged; what changes is that the code
becomes **structurally drift-proof, self-documenting, type-safe in its working
state, cleanly pure at its edges, and guarded against agent-surface divergence**.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the developers who read and change FS.GG.SDD's
command layer, plus the CI gates and drift guards that protect its contracts.

### User Story 1 - Readiness views cannot drift structurally (Priority: P1)

A developer adds or changes a field in one readiness view (`analysis` / `verify`
/ `ship`) and the shared envelope forces the same structural change across all
three, so the three JSON contracts stay in lockstep instead of silently
diverging.

**Why this priority**: The three views are a public JSON automation contract
consumed downstream (including the Governance handoff). ~450 lines of
copy-pasted `Utf8JsonWriter` code is the highest-leverage maintainability risk
in the batch: it is the one item where a future edit can silently break a
contract. Unifying the writer is also the load-bearing piece the review calls
out first (§3.4).

**Independent Test**: Regenerate all three readiness views before and after the
extraction for a representative set of work items; every emitted JSON file is
byte-identical. Then confirm the three views are produced through one shared
envelope writer rather than three independent copies.

**Acceptance Scenarios**:

1. **Given** a work item that produces `analysis.json`, `verify.json`, and `ship.json`, **When** those views are regenerated after the envelope extraction, **Then** each file is byte-identical to the pre-feature output.
2. **Given** the three readiness views, **When** the code that emits them is inspected, **Then** they share a single envelope writer that owns the common structure (schema-version, ids, ordering, envelope framing) rather than three independent hand-written writers.
3. **Given** the shared envelope writer, **When** a common structural element is changed in it, **Then** the change is reflected in all three views by construction (no per-view copy to keep in sync).

---

### User Story 2 - Working state is type-safe, not stringly-typed (Priority: P2)

A developer working in the refresh / upgrade / drift code manipulates
view-currency states, upgrade step outcomes, and step ids as typed discriminated
unions, so an invalid or mistyped state is a compile error rather than a silent
runtime mismatch.

**Why this priority**: String-typed working state compared with `=` across
`HandlersRefresh.fs`, `HandlersUpgrade.fs`, and `Drift.fs` is a live correctness
hazard — a typo (`"applied"` vs `"aplied"`) compiles and misfires, exactly the
class of bug that produced the phantom-id and substring-matching defects fixed
in earlier items. DU-ifying makes the compiler enforce exhaustiveness. Higher
maintainability value than the cosmetic renames, lower blast radius than the
envelope.

**Independent Test**: Locate the view-currency words, upgrade step outcomes, and
step ids; each is represented by a discriminated union with exhaustive matches,
and no raw-string comparison of those concepts remains. All existing tests pass
and all emitted contracts stay byte-identical (the DU serializes to the same
tokens).

**Acceptance Scenarios**:

1. **Given** view-currency state (`refreshed` / `blocked` / …), **When** it is produced and compared, **Then** it is a discriminated union and the comparisons are DU matches, not string equality.
2. **Given** upgrade step outcomes (`wouldApply` / `applied` / …) and step ids, **When** they flow through `HandlersUpgrade` / `Drift`, **Then** they are typed values, and any serialization to their existing string tokens happens at one projection point.
3. **Given** the DU-ified states, **When** the CLI emits any report that includes those tokens, **Then** the emitted strings are byte-identical to the pre-feature output.

---

### User Story 3 - Call-site provenance is visible (Priority: P2)

A developer reading a call in the command layer can tell from the code which
module a helper comes from, because `CommandWorkflow/` internals are accessed by
qualified module reference instead of one flat auto-opened scope.

**Why this priority**: One flat `[<AutoOpen>]` namespace across ~16 files hides
where every function is defined and lets inter-file dependencies masquerade as
implicit compile order. Dropping AutoOpen in favor of qualified access makes the
dependency structure legible and prevents accidental cross-file coupling. It is
higher-churn but low-risk (internal-only, `.fs`-confined).

**Independent Test**: Inspect the `CommandWorkflow/` modules; the flat
`[<AutoOpen>] module internal` scope is gone in favor of qualified module access
(or an explicitly justified minimal remainder), the projects build, and the full
suite passes.

**Acceptance Scenarios**:

1. **Given** the `CommandWorkflow/` modules, **When** they are inspected, **Then** the blanket `[<AutoOpen>]` on the internal modules is removed and call sites reference their helpers by qualified module path.
2. **Given** the de-AutoOpened modules, **When** the solution is built, **Then** it compiles with no new warnings and the full test suite passes unchanged.
3. **Given** the removal of the flat scope, **When** any emitted contract is regenerated, **Then** it is byte-identical (this is a purely internal reorganization).

---

### User Story 4 - Parsing modules are named by responsibility (Priority: P3)

A developer looking for the parser of a given lifecycle artifact finds it by a
responsibility-named module, not by guessing which compile-order slab
(`Early`/`Mid`/`Tasks`) happens to contain it.

**Why this priority**: `ParsingEarly` / `ParsingMid` / `ParsingTasks` are named
for their position in compile order, so the names carry no information about what
they parse. Renaming by responsibility improves navigability. Lowest structural
value and pure churn, so it is P3.

**Independent Test**: Inspect the parsing modules; each is named for the
artifacts/responsibility it owns rather than a compile-order position, and all
references are updated so the solution builds.

**Acceptance Scenarios**:

1. **Given** the three `Parsing{Early,Mid,Tasks}` modules, **When** they are renamed, **Then** each new name reflects the artifacts or stage-parsing responsibility it owns, and all `.fsproj` ordering and references are updated consistently.
2. **Given** the renamed modules, **When** the solution is built and the suite is run, **Then** everything compiles and passes with no contract change.

---

### User Story 5 - The pure core is clean at its edges (Priority: P3)

A developer relying on the "pure planner core behind one interpreter" invariant
finds the §1.5 soft spots resolved: no filesystem IO buried in the Artifacts
library planner path, no `failwithf` at static initialization, no ambient-cwd
resolution inside a pure planner.

**Why this priority**: These are localized purity leaks that don't currently
cause bugs but erode the strongest architectural property the codebase has
(mechanically-verifiable purity). Fixing them protects that invariant. Low blast
radius, contained to a few sites; P3 because nothing is broken today.

**Independent Test**: Inspect the named §1.5 sites; the ambient-cwd resolve and
static-init `failwithf` are replaced with edge-appropriate handling, and any
remaining filesystem edge in Artifacts is documented as an intentional,
justified exception. Behavior is unchanged (a missing resource surfaces as a
diagnostic, not an opaque `TypeInitializationException`).

**Acceptance Scenarios**:

1. **Given** `SeededSkills.seededSkills`, **When** an embedded resource is missing, **Then** the failure surfaces as an actionable diagnostic rather than an opaque `TypeInitializationException` at static init.
2. **Given** `Foundation.projectIdFromRoot`, **When** it resolves a project id, **Then** it does not depend on ambient process working directory inside the pure planner.
3. **Given** the `RegistryDocument.load` filesystem edge inside Artifacts, **When** the §1.5 items are addressed, **Then** that edge is either relocated to the host or retained with an explicit, documented justification (not silently left as an undocumented purity leak).

---

### User Story 6 - Agent surfaces cannot silently diverge (Priority: P2)

A maintainer editing `CLAUDE.md` or `AGENTS.md` is protected by a drift guard
that fails whenever the two agent-surface documents are not byte-identical, the
same way the seeded skill trees are pinned `claude ≡ codex ≡ agents`.

**Why this priority**: The repo's own doctrine mandates keeping Claude and Codex
surfaces aligned, yet the two documents are hand-maintained copies that have
already drifted — reworded facts and AGENTS.md missing ~26 lines, so a Codex
agent receives strictly less guidance than a Claude agent. The content is
agent-agnostic repo doctrine (the SDD/Governance boundary, the authored-vs-generated
model, CLI output rules) with no Claude- or Codex-specific instruction, so the
correct target state is a single canonical document mirrored into both files and
guarded, exactly the model the skills already use. P2 because it prevents ongoing
divergence and closes an existing content gap.

**Independent Test**: With `CLAUDE.md` and `AGENTS.md` reconciled to one canonical
content, the drift guard passes; change either file so they differ by a single
byte and the guard fails deterministically.

**Acceptance Scenarios**:

1. **Given** `CLAUDE.md` and `AGENTS.md` reconciled to identical content, **When** the drift guard runs, **Then** it passes.
2. **Given** an edit to either file that makes the two documents differ by any byte, **When** the drift guard runs, **Then** it fails and points the maintainer at the divergence.
3. **Given** the reconciliation, **When** the two files are compared, **Then** AGENTS.md carries the full canonical doctrine (no content the Claude surface has is missing from the Codex surface).

### Edge Cases

- **A readiness view legitimately has a view-specific field**: the shared envelope MUST still allow per-view specialization (the envelope owns the *common* structure; view-specific bodies remain), so unification does not force the three views to become identical.
- **A DU-ified state must serialize to an existing string token**: the mapping from DU case to its wire token MUST be exact and centralized, so no emitted byte changes.
- **De-AutoOpen creates an ambiguity or ordering conflict**: resolving it MUST NOT change behavior — only qualification/ordering, never semantics.
- **A Parsing-module rename touches `.fsproj` compile order**: compile order MUST be preserved (F# is order-sensitive); the rename is name-only, not a reordering, unless a reorder is separately justified and verified.
- **`CLAUDE.md` / `AGENTS.md` need an agent-specific note in the future**: this feature commits to fully-identical files; if a genuinely Claude- or Codex-specific instruction is ever required, it is a future decision to introduce a shared-canonical-block split — out of scope here, and the guard as delivered treats any byte difference as drift.
- **A §1.5 purity fix would require an architectural (Tier-1) change**: such a change is out of scope and MUST be deferred rather than forced within this Tier-2 feature (e.g. relocating `RegistryDocument.load` may be documented-and-deferred if it crosses the layering contract).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The `analysis.json`, `verify.json`, and `ship.json` readiness views MUST be emitted through a single shared envelope writer that owns their common structure (envelope framing, schema-version, ids, ordering), replacing the three near-identical hand-written `Utf8JsonWriter` copies.
- **FR-002**: After the envelope extraction, each of the three readiness views MUST serialize byte-for-byte identically to its pre-feature output for every representative work-item state (verified by a pre/post regeneration diff being empty).
- **FR-003**: The shared envelope MUST still permit view-specific bodies, so a field unique to one view is expressed without forcing the other two views to change.
- **FR-004**: View-currency states, upgrade step outcomes, and step ids that are today compared as raw strings across `HandlersRefresh.fs`, `HandlersUpgrade.fs`, and `Drift.fs` MUST be represented as discriminated unions and compared by DU match; no raw-string comparison of those concepts may remain.
- **FR-005**: Each DU-ified state that serializes to a wire token MUST map to its existing string token exactly, at a single centralized projection point, so no emitted contract byte changes.
- **FR-006**: The blanket `[<AutoOpen>] module internal` scope across the `CommandWorkflow/` files MUST be removed in favor of qualified module access, except where a specific remaining `AutoOpen` is explicitly justified; the reorganization MUST be internal-only and observable in no emitted contract.
- **FR-007**: The `ParsingEarly` / `ParsingMid` / `ParsingTasks` modules MUST be renamed to reflect their parsing responsibility rather than compile-order position, with all references and `.fsproj` ordering updated so the solution builds; compile order MUST be preserved unless a reorder is separately justified and verified.
- **FR-008**: The §1.5 purity soft spots MUST be addressed: `SeededSkills.seededSkills` MUST surface a missing embedded resource as an actionable diagnostic rather than a static-init `failwithf`/`TypeInitializationException`; `Foundation.projectIdFromRoot` MUST NOT depend on ambient process cwd inside the pure planner; and the `RegistryDocument.load` filesystem edge MUST be either relocated to the host or retained with an explicit documented justification.
- **FR-009**: `CLAUDE.md` and `AGENTS.md` MUST be reconciled to identical canonical content (AGENTS.md brought up to the full doctrine, losing no facts the Claude surface carries), and a drift guard MUST exist that fails when the two files are not byte-identical, mirroring the seeded-skill `claude ≡ codex ≡ agents` drift-guard discipline.
- **FR-010**: This feature MUST NOT change any `fsgg-sdd` CLI output, JSON automation contract, exit code, stream routing, persisted schema, generated view, artifact layout, agent-skill contract, public `.fsi` surface, or committed baseline — verified by the surface-area baselines and golden/deterministic contracts remaining byte-identical (a pre/post `git diff` over `**/*.baseline`, `src/**/*.fsi`, the readiness golden fixtures, and the JSON automation golden fixtures is empty).
- **FR-011**: The full existing test suite MUST pass unchanged after the feature, and the clean-rebuild warning count MUST NOT increase.

### Key Entities

- **Readiness envelope**: the shared JSON structure common to `analysis.json`, `verify.json`, and `ship.json` (framing, schema-version, ids, ordering), owned by one writer; each view supplies its view-specific body.
- **View-currency state**: the per-view generated-view freshness state (`refreshed` / `blocked` / …) currently carried as a string; to become a discriminated union.
- **Upgrade step outcome**: the result of an `upgrade` remediation step (`wouldApply` / `applied` / …) and its step id, currently string-typed; to become discriminated unions.
- **CommandWorkflow module**: one `.fs` file under `src/FS.GG.SDD.Commands/CommandWorkflow/`, currently `[<AutoOpen>] module internal`; to be accessed by qualified reference.
- **Parsing module**: one of the three compile-order-named parser slabs, to be renamed by responsibility.
- **Agent-surface drift guard**: a test that pins `CLAUDE.md` and `AGENTS.md` to their aligned shared content, permitting intentional per-agent sections.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The `analysis.json`, `verify.json`, and `ship.json` views are produced through one shared envelope writer; a pre/post regeneration diff of all three views across the representative work-item states is empty (0 differing bytes).
- **SC-002**: The count of independent hand-written readiness-view JSON writers drops from 3 to 1 shared envelope (with view-specific bodies), removing the ~450 lines of duplication the review inventoried.
- **SC-003**: Zero raw-string comparisons of view-currency states, upgrade step outcomes, or step ids remain in `HandlersRefresh.fs` / `HandlersUpgrade.fs` / `Drift.fs`; each concept is a discriminated union matched exhaustively.
- **SC-004**: The blanket `[<AutoOpen>]` on `CommandWorkflow/` internal modules is removed (any surviving `AutoOpen` is individually justified), and the solution builds with no new warnings.
- **SC-005**: The three Parsing modules are renamed by responsibility; grep for the old compile-order names returns zero references outside history.
- **SC-006**: The three §1.5 purity sites are resolved or explicitly documented: a missing seeded-skill resource yields a diagnostic (not a `TypeInitializationException`), `projectIdFromRoot` has no ambient-cwd dependency, and the `RegistryDocument.load` edge is relocated or documented.
- **SC-007**: `CLAUDE.md` and `AGENTS.md` are byte-identical after reconciliation; a drift guard exists, passes in that state, and fails deterministically on any single-byte divergence.
- **SC-008**: The `fsgg-sdd` surface-area baselines, `.fsi` public surfaces, JSON automation contracts, and golden/deterministic fixtures are unchanged — a diff of those artifacts before and after the feature is empty — and the full test suite passes with no increase in warning count.

## Assumptions

- The shared readiness envelope is an **internal writer unification**, not a new persisted schema or contract artifact: it exists to guarantee byte-identical output across the three views, so it introduces no new on-disk schema and changes no emitted byte. (The review's "declared shared readiness-envelope schema" language is realized as a shared *writer* + shared internal structure, not a versioned external schema, because a new external schema would violate the no-contract-change boundary.)
- The DU wire-token mappings are exact reproductions of today's strings; DU-ification changes representation and comparison, never serialized output.
- De-AutoOpen may retain a small, individually-justified set of `AutoOpen`s where qualification would be gratuitous (e.g. a genuinely ubiquitous foundation module); the requirement is removal of the *blanket* flat scope, not zero `AutoOpen` at any cost.
- Parsing-module renames preserve F# compile order; the rename is name-only. Any actual reordering is out of scope for this feature unless separately justified and verified.
- `CLAUDE.md` and `AGENTS.md` are reconciled to one canonical content and the guard asserts byte-identity (the "fully identical files" decision). CLAUDE.md is the authored source; AGENTS.md is the mirrored copy. The content is agent-agnostic repo doctrine, so no per-agent customization is lost by this choice.
- Any §1.5 fix that would require a Tier-1 architectural change (e.g. moving `RegistryDocument.load` across the layering contract) is documented-and-deferred rather than forced into this Tier-2 feature.
- No new external dependency, tool, or CI service is required; the work is contained to `src/FS.GG.SDD.Commands` internals, a few Artifacts/Foundation sites, the two agent-surface docs, and the test tree.
- The 2026-07-02 review report (@ 8881620) is the authoritative inventory; any additional structural defect discovered during implementation may be folded in only if it preserves the Tier-2, no-contract-change boundary.
