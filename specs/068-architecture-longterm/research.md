# Phase 0 Research: Architecture longer-term cleanups (068)

Feature: roadmap #76 / review §6 item 12. Tier 2, contract-neutral. Every
decision below is constrained by the same invariant: **no emitted contract byte,
exit code, stream, `.fsi` surface, or committed baseline may change.** The
grounding line references are from the 2026-07-02 review and were re-verified
against `main` on 2026-07-03.

---

## Decision 1 — Shared readiness envelope (US1 / FR-001..003)

**Finding (grounded).** The three readiness views are emitted by
`HandlersVerify.verifyJson` (`HandlersVerify.fs:299-394`),
`HandlersShip.shipJson` (`HandlersShip.fs:235-…`), and
`ViewGeneration.analysisJson` (`ViewGeneration.fs:537-…`). They **already share**
a set of sub-writers defined in `ViewGeneration.fs`: `writeViewPreamble` (:402),
`writeSourcesArray` (:417), `writeLifecycleReadiness` (:440),
`writeGeneratedViewsArray` (:460), `writeBoundaryFacts` (:475),
`writeViewDiagnostics` (:490), `writeReadinessFindings` (:500), `writeNextAction`
(:517), `writeStringArray` (:254). So the *field-level* writers are not the
duplication — the duplication is the **envelope frame** each view hand-repeats:
`new MemoryStream()` + `new Utf8JsonWriter(…, Indented = true)`, the opening
object, `writeViewPreamble`, `writeSourcesArray`, … the terminal
`WriteEndObject()`, flush, and UTF-8 decode to string.

**Critical constraint discovered.** The three views' *tail ordering differs* and
must be preserved byte-for-byte:

- `analysisJson` emits `findings` **before** `generatedViews`, and uses the
  boundary key `"optionalBoundaryFacts"`.
- `verifyJson` / `shipJson` emit `generatedViews` **before** `findings`, use the
  boundary key `"governanceCompatibility"`, and share an identical
  generatedViews→findings→boundary→diagnostics tail.

Therefore the envelope may only own what is **provably identical** across all
three, or byte output will change.

**Decision.** Introduce one `writeReadinessEnvelope` in `ViewGeneration.fs` that
owns exactly the invariant frame:

1. the `MemoryStream` + `Utf8JsonWriter(Indented = true)` lifecycle,
2. the opening `WriteStartObject()` + `writeViewPreamble workId viewKind readiness generator` + `writeSourcesArray sourceKind sources`,
3. a `writeBody: Utf8JsonWriter -> unit` callback (each view supplies its ordered middle+tail verbatim),
4. the terminal `WriteEndObject()`, `Flush()`, and `Encoding.UTF8.GetString`.

Verify and ship additionally share their *identical* tail, so a second helper
`writeGovernanceReadinessTail writer generatedViews findings diagnostics` (the
generatedViews→findings→`governanceCompatibility`→diagnostics sequence) is
factored out and called from both view bodies; analysis keeps its own body
because its order differs. This removes the ~450 lines of frame duplication the
review inventoried while making byte-identity structurally guaranteed by
construction (one frame) and provable by the existing golden fixtures.

**Rationale.** Owning only the truly-common frame is the maximal safe extraction:
each view's body callback still contains its exact current byte sequence, so the
regeneration diff (FR-002) is empty by construction. Pushing the *tail* into the
envelope was rejected because the analysis tail ordering differs — doing so would
require the envelope to branch on view kind, which is more fragile than a
callback and risks a byte change.

**Alternatives considered.**
- *A versioned external "readiness-envelope schema" artifact* (the review's
  literal phrasing) — **rejected**: a new persisted schema changes the on-disk
  contract, violating Tier 2. Realized instead as a shared *writer* + shared
  internal structure (spec Assumption 1).
- *A fully data-driven envelope* (build an intermediate record, serialize
  generically) — **rejected for now**: correct long-term, but re-deriving the
  exact current byte layout (indentation, key order, empty-string vs omitted
  fields) from a generic serializer is high-risk against the byte-identity
  constraint. The frame+callback extraction captures the duplication win at a
  fraction of the risk.

---

## Decision 2 — DU-ify string-typed working state (US2 / FR-004..005)

**Finding (grounded).**
- **View-currency** in `HandlersRefresh.fs` is threaded as raw strings
  `"blocked" | "refreshed" | "stale" | "already-current" | "current" | "na"`,
  compared with `=`/pattern-match on strings at ~30 sites (`:204,209,324,328,330,
  336,340,353,363-365,375,384-385,390,404-405,429-430,441,455,484-485,539,545,
  549,562-563,573,577,600,622,680-682,710-711`). These ultimately map to the
  **already-existing** `GeneratedViewCurrency` DU (`GeneratedViewCurrency.Stale`
  used at `:682`) — so a typed representation already exists for the destination;
  the intermediate stringly layer is the defect.
- **Upgrade step outcomes** are the raw strings `"wouldApply" | "applied" |
  "failed" | "skipped"` in `Drift.Step.Outcome` (a `string` field,
  `Drift.fs:199,216,227`) and `HandlersUpgrade.fs:88,105,120,165-166,266`.

**Decision.** Introduce two internal DUs in the `FS.GG.SDD.Commands.Internal`
namespace (no `.fsi`, no public surface):

- `RefreshViewState = Blocked | Refreshed | Stale | AlreadyCurrent | Current | NotApplicable`
- `UpgradeStepOutcome = WouldApply | Applied | Failed | Skipped`

Replace the raw-string comparisons with DU matches; type `Drift.Step.Outcome` as
`UpgradeStepOutcome`. Serialization to the existing wire tokens happens at **one**
projection point per DU (`toToken : RefreshViewState -> string`,
`toToken : UpgradeStepOutcome -> string`), producing the exact current strings, so
no emitted byte changes (FR-005). Where a refresh state already flows into
`GeneratedViewCurrency`, prefer converging on that existing DU rather than adding
a parallel one, to avoid two DUs for one concept.

**Rationale.** The compiler enforces exhaustiveness (kills the typo/`Contains`
class the earlier features fixed elsewhere), and a single `toToken` keeps the wire
contract centralized and byte-exact.

**Alternatives considered.** Reuse `GeneratedViewCurrency` for *all* refresh
states — **partially adopted**: it doesn't cover `"na"`/`"already-current"`
cleanly, so a Commands-internal `RefreshViewState` that maps *into*
`GeneratedViewCurrency` at the boundary is cleaner than overloading the artifact
DU. Confirmed during implementation by reading each of the ~30 sites.

---

## Decision 3 — Drop the flat `[<AutoOpen>]` scope (US3 / FR-006)

**Finding (grounded).** Of the 17 `CommandWorkflow/*.fs` files, **15** are
`[<AutoOpen>] module internal` in `FS.GG.SDD.Commands.Internal`; **2 already are
not** — `Drift` (`Drift.fs:14`) and `SeededSkills` (`SeededSkills.fs:19`) — and
they are referenced by qualified name today. So the target pattern is already
established and proven to compile; this decision extends it to the other 15.

**Decision.** Remove `[<AutoOpen>]` from the 15 modules. Since all modules live in
one namespace, call sites resolve helpers either by qualified path
(`Foundation.snapshot`) or by an explicit `open <Module>` at the top of each
consuming file — both make provenance visible in a way AutoOpen does not. Prefer
an explicit `open` for genuinely ubiquitous modules (e.g. `Foundation`) and
qualified access elsewhere; the requirement is removal of the *blanket* implicit
scope, not zero `open`. No behavior changes; this is internal reorganization
(FR-006, verified by empty contract diff).

**Rationale.** Explicit opens/qualification restore call-site provenance and make
the inter-file dependency graph legible instead of implicit in `.fsproj` order.
It is compiler-checked and mechanical.

**Alternatives considered.** Leave a few high-traffic modules AutoOpen — allowed
by FR-006 ("except where a specific remaining `AutoOpen` is explicitly
justified") but the default is removal; any survivor must be individually
justified in the PR.

**Risk / sequencing.** This is the highest-churn cluster (touches most files).
Sequence it **after** the envelope (Decision 1) and DU (Decision 2) work so those
land against stable module boundaries, and do it as a mechanical pass with a green
build+suite gate.

---

## Decision 4 — Rename Parsing slabs by responsibility (US4 / FR-007)

**Finding (grounded).** `ParsingEarly.fs` (1,327 LOC), `ParsingMid.fs` (1,407),
`ParsingTasks.fs` (981) are named for compile-order position. Their `.fsproj`
order is Early → Mid → Tasks (between `Foundation`/`SeededSkills`/`Drift` and the
`ViewGeneration`/`Prerequisites`/`Handlers*`).

**Decision.** Rename each module + file to reflect the artifacts/stage-parsing it
owns, preserving compile order (F# is order-sensitive; the rename is **name-only**,
not a reorder). Exact target names are chosen during implementation by reading
each module's actual responsibilities (charter/spec/clarify/checklist vs
plan/analyze/evidence vs task-graph), but the naming *scheme* is
responsibility-based (e.g. `SpecStageParsing` / `PlanStageParsing` /
`TaskGraphParsing`, subject to matching the real contents). Update the `.fsproj`
`<Compile Include>` entries and every reference.

**Rationale.** Lowest structural value, pure navigability improvement; kept P3.
Name-only preserves order and guarantees no behavior change.

**Alternatives considered.** Splitting the slabs by responsibility (they are
large) — **out of scope**: FR-007 is a rename, not a decomposition; splitting
would enlarge the blast radius for marginal gain and is a candidate follow-up.

---

## Decision 5 — Purity soft spots (US5 / FR-008)

**Finding (grounded).**
- `SeededSkills.seededSkills` (`SeededSkills.fs:57-58`) is an **eager module-level
  `let`** that calls `loadBody` per skill; a missing embedded resource throws
  inside static init → opaque `TypeInitializationException`.
- `Foundation.projectIdFromRoot` (`Foundation.fs:34-40`) derives the id from
  `DirectoryInfo(normalizeRoot root).Name`; for a bare `"."` the meaningful name
  depends on ambient process cwd.
- `RegistryDocument.load` (`RegistryDocument.fs:92-…`) does `File.Exists` +
  `File.ReadAllText` **inside the Artifacts library** — the one artifact-load edge
  outside the host. (Note: its integer parse already uses `Int32.TryParse` at
  `:111`; the overflow hazard was a different site fixed in 059.)

**Decisions.**
- **SeededSkills**: convert `seededSkills` from an eager value to a `lazy`
  (or a `loadSeededSkills ()` function) evaluated at a defined call site, and make
  the missing-resource case raise a dedicated, actionable message naming the
  resource — so a broken build fails with a diagnostic, not an opaque static-init
  crash. The `SeededSkillsTests` drift guard (which pins the on-disk set) must be
  re-pointed at the new accessor; because every real build embeds all resources,
  observable behavior is unchanged.
- **projectIdFromRoot**: ensure the root is resolved to an absolute path at the
  edge before it reaches the planner, so `projectIdFromRoot` never resolves `"."`
  against ambient cwd inside pure code. Confirmed non-observable (the id output is
  unchanged for every real invocation, which always passes a concrete root).
- **RegistryDocument.load**: **document, do not relocate.** Relocating the file
  read to the host crosses the layering/host boundary and would ripple into CLI
  call sites and the registry-validate command — a Tier-1 architectural change,
  out of scope for this Tier-2 feature. Add an explicit code comment marking it an
  intentional, justified edge (co-located with the registry model it loads),
  matching how `ValidationRunner`'s deliberate `TZ` mutation is documented. Record
  it in the purity ledger (data-model) as accepted-and-documented.

**Rationale.** Two of three are cheap, non-observable hardening; the third is
honestly out of Tier-2 reach and is documented-and-deferred (spec Assumption 6)
rather than forced.

**Alternatives considered.** Relocating `RegistryDocument.load` to the host now —
**rejected** as Tier-1 scope creep; filed conceptually as a follow-up.

---

## Decision 6 — CLAUDE.md ↔ AGENTS.md guard (US6 / FR-009)

**Finding (grounded).** The two files are **not** structurally aligned near-copies:
they diverge from line 3 (reworded identical facts) and `AGENTS.md` (150 lines) is
**26 lines shorter** than `CLAUDE.md` (176) — a Codex agent receives strictly less
guidance. The content is agent-agnostic repo doctrine (SDD/Governance boundary,
authored-vs-generated model, CLI output rules); nothing in it is Claude- or
Codex-specific.

**Decision (per user, "fully identical files").** Reconcile the two onto **one
canonical content** — `CLAUDE.md` is the authored source; `AGENTS.md` becomes a
byte-identical mirror (bringing AGENTS.md up to the full doctrine, losing no
facts). Add a drift guard test asserting `File.ReadAllText "CLAUDE.md" =
File.ReadAllText "AGENTS.md"` (byte-identity), placed alongside the existing
doc-contract guards (`EarlyStageGuidanceContractTests` /
`AuthoringDocsContractTests` in `FS.GG.Contracts.Tests`, or the Commands.Tests
counterpart), failing with an actionable message pointing at the divergence. This
mirrors the seeded-skill `claude ≡ codex ≡ agents` discipline for the context
docs.

**Rationale.** The content is agent-agnostic, so the skills' one-source-mirrored
model applies directly; byte-identity is the simplest guard and the strongest
guarantee, and it closes the existing content gap in AGENTS.md.

**Scope note.** This targets the **repo-root working docs** for developing
FS.GG.SDD. The *scaffold-seeded* CLAUDE.md/AGENTS.md pair emitted into consumer
products by `init` is a separate `AgentGuidanceTarget` artifact set and is **out
of scope** here; if it should follow the same principle, that is a separate item.

**Alternatives considered.** *Shared-canonical-block + thin per-agent header* and
*soft fact-guard* — both presented to the user; **not chosen**. If a genuinely
agent-specific instruction is ever needed, introducing a canonical-block split is
a future decision (spec Edge Cases).

---

## Cross-cutting: verification strategy

The whole feature's safety net is **byte-identity of every contract** (FR-010/011):

- A pre-feature snapshot of the three readiness golden fixtures, the JSON
  automation golden fixtures, all `**/*.baseline` files, and `src/**/*.fsi` is
  captured; a post-feature `git diff` over those paths MUST be empty.
- The full existing suite (835 facts) MUST stay green with no new warnings.
- New tests added by this feature: the CLAUDE↔AGENTS byte-identity guard (US6),
  and DU-exhaustiveness is enforced by the compiler (no runtime test needed for
  FR-004 beyond the unchanged existing refresh/upgrade tests, which already cover
  the state transitions and will fail if a `toToken` mapping is wrong).

Sequencing (de-risked): Decision 1 (envelope) → Decision 2 (DUs) →
Decision 5 (purity) → Decision 4 (renames) → Decision 3 (de-AutoOpen, highest
churn, last) → Decision 6 (docs guard, independent, any time). `/speckit-tasks`
encodes the true dependency graph.
