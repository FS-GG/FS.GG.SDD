# Research: Orchestrator skill fan-out

**Feature**: `056-orchestrator-skill-fanout` | Phase 0. Resolves every planning unknown, including
the spec's FR-007 `[NEEDS CLARIFICATION]`. No unresolved clarifications remain.

Grounding read of the current code:
- `SeededSkills.skillEffects` emits, per skill name, **two** `WriteFile(..., AgentGuidanceTarget)`
  — `.claude/skills/{name}/SKILL.md` and `.codex/skills/{name}/SKILL.md`, from one canonical
  embedded body (`SeededSkills.fs:64-68`). It is the single seam, reused by `init` and `scaffold`
  (`Foundation.initEffects` `@ SeededSkills.skillEffects`).
- `isSddTree` reserves `.fsgg/`·`work/`·`readiness/` and the **whole** `.claude/skills/` /
  `.codex/skills/` roots (`HandlersScaffold.fs:53-62`); `.agents/skills/` is **not** currently
  SDD-owned, so a provider write there is already permitted product.
- Scaffold's produced set is a path diff `afterSet − beforePaths − skeletonFiles − provenance`;
  post-instantiation staging (provenance write, `git init` probe, `chmod`) is a three-tick machine
  re-derived from the interpreted-effect log (`HandlersScaffold.fs:396-507`).
- Effects available: `EnumerateDirectory`, `ReadFile`, `WriteFile(path, text, kind)` — enough to
  read provider skill bodies and mirror them; **no new effect needed**.
- `ScaffoldProvenanceRecord` already carries two additive optional fields added at v1
  (`RequiredMinimumCliVersion`, `EffectiveParameters`) — the additive-at-v1 pattern is established.
- `Drift.expectedArtifactPaths` = for each `SeededSkills.skillNames`, the `.claude` and `.codex`
  SKILL.md, plus `.fsgg/early-stage-guidance.md` (`Drift.fs:18-21`).

---

## R1 — Introduce `.agents/skills/` as a third seeded root

**Decision.** Add a **third** root `.agents/skills/{name}/SKILL.md` to `SeededSkills.skillEffects`,
written from the same canonical embedded body with the same `AgentGuidanceTarget` no-clobber
write-kind. Because `init` and `scaffold` both consume `skillEffects` through the one seam, both
gain the third root with no other edit.

**Rationale.** The decision requires all three runtimes to be interchangeable; the seeded process
skills must exist in `.agents/skills/` too. One canonical body → three writes keeps the copies
byte-identical by construction (the same argument that makes Claude≡Codex today).

**Consequence.** `init` is **no longer byte-identical** to a pre-056 CLI (it writes a third root).
This is intentional and is exactly why the fan-out advances the orchestrator version axis (R8 /
ADR-0008). It is a *skeleton growth*, not a schema migration.

**Alternatives rejected.** (a) Mirror `.agents` only at scaffold, leaving `init` two-root — breaks
the invariant for `init`-seeded (non-scaffolded) repos and for `refresh`. (b) A separate
`.agents`-only seam — duplicates the single-seam guarantee and risks drift between seams.

## R2 — How scaffold mirrors provider skills into the other two roots

**Decision.** Add a new **post-instantiation MIRROR tick** to the existing staged machine (before
or alongside the `git init` probe). It:
1. `EnumerateDirectory ".agents/skills"` to find the provider's produced skill directories;
2. for each non-reserved provider skill `X`, `ReadFile ".agents/skills/X/SKILL.md"`;
3. `WriteFile` that exact body into `.claude/skills/X/SKILL.md` and `.codex/skills/X/SKILL.md`.
The seeded `fs-gg-sdd-*` skills already exist in all three roots (from `initEffects`), so the
**union** across roots = seeded (3 roots, from init) ∪ provider (`.agents`, mirrored to the other
two). Re-derive the tick from the interpreted-effect log like the existing ticks (no new model
field).

**Rationale.** Reuses existing effects and the proven staged-driver pattern. Reading the body (not
re-templating) guarantees byte-identity between the provider's `.agents` copy and the mirrored
`.claude`/`.codex` copies.

**Alternatives rejected.** (a) Have the provider write all three roots — that is precisely the
intrusion the strict guard forbids (and what #55 rejected). (b) Copy at the interpreter without a
planned effect — violates Principle V (I/O must be a planned, interpreted effect).

## R3 — Ownership / write-kind of the mirrored `.claude`/`.codex` copies

**Decision.** The mirrored copies are **SDD-generated mirror views**, not `generatedProduct`. Write
them with a no-clobber kind (`AgentGuidanceTarget`) so an author edit to a mirrored copy is
preserved, and record them in provenance under a **new** owner value `mirrored` (see R4). The
provider's canonical `.agents/skills/X/SKILL.md` stays `generatedProduct` (provider-owned source).
Seeded `fs-gg-sdd-*` in all three roots stay authored `AgentGuidanceTarget`.

**Rationale.** `refresh` must be able to **re-mirror** (FR-009). `refresh` *excludes*
`generatedProduct` from regeneration, so the mirror copies cannot be `generatedProduct` or refresh
would never touch them. Treating them as SDD-generated (orchestrator-owned) views is faithful to
"`fsgg-sdd` is the sole mirror authority" and lets refresh/upgrade own their currency. Byte-identity
is about content, not owner, so mixed ownership across roots is fine.

**Alternatives rejected.** (a) Mark mirror copies `generatedProduct` — breaks FR-009 (refresh
excludes them). (b) A brand-new `ArtifactWriteKind` — unnecessary; `AgentGuidanceTarget` already
gives no-clobber authored-SDD semantics. Only the provenance **owner** label needs a new value.

## R4 — Provenance shape (resolves spec FR-007 `[NEEDS CLARIFICATION]`)

**Decision.** **Additive, schema stays v1.** Add one optional field `MirroredPaths:
ScaffoldProducedPath list` to `ScaffoldProvenanceRecord`, serialized after `producedPaths`, sorted
by path, defaulting to `[]`; `tryParse` defaults absent/null to `[]` (same pattern as
`EffectiveParameters`). Each mirrored entry's `Owner` is the new `Mirrored` `ArtifactOwner` case
(serialized `"mirrored"`). `producedPaths`/`generatedProduct` keep their exact meaning (the
provider's `.agents` skills and other product remain there, unchanged).

**Rationale.** The repo's obligation and precedent (050/052/054) is additive-at-v1 via
`tryParse`-defaulting; readers that ignore unknown keys are unaffected. A separate `mirroredPaths`
array (rather than mixing `mirrored`-owned entries into `producedPaths`) keeps the "producedPaths =
provider product" invariant that downstream consumers/refresh-exclusion already rely on.

**Compatibility note.** Adding an `ArtifactOwner` case is additive to the enum, but any consumer
that pattern-matches owner strings exhaustively could see the new `"mirrored"` value. Because the
new value appears **only** in the new `mirroredPaths` array (never in the existing
`producedPaths`), existing `producedPaths` readers never encounter it → no observed break.

**Alternatives rejected.** (a) Schema **v2** — heavier; would demand a `<version>.md` migration note
and re-pin; unjustified for an additive array. (b) Overload `producedPaths` with `mirrored`-owned
rows — pollutes the provider-product invariant and refresh-exclusion.

## R5 — Extend the strict guard to reserve `.agents/skills/fs-gg-sdd-*` (FR-002)

**Decision.** `isSddTree` gains **one** clause: `p.StartsWith(".agents/skills/fs-gg-sdd-",
Ordinal)`. `.claude/skills/` and `.codex/skills/` stay **whole-root** reserved. Reservation is thus
deliberately **asymmetric**: providers may write *non-reserved* skills into `.agents/skills/` only.

**Rationale.** Providers must be able to write their `fs-gg-*` UI skills into the neutral root, but
must not be able to clobber SDD's process skills there. Whole-root reservation of `.agents` would
block the very thing the fan-out enables; namespace reservation protects SDD's names precisely.

**Alternatives rejected.** (a) Reserve whole `.agents/skills/` — blocks provider skills, defeats
the feature. (b) Reserve nothing in `.agents` — a provider could ship `.agents/skills/fs-gg-sdd-plan`
and the mirror/union would fight it; the guard should reject it up front (user choice confirmed).

## R6 — `refresh` re-mirror (FR-009)

**Decision.** `refresh` re-derives the union from on-disk state — the seeded `fs-gg-sdd-*` bodies
(canonical embedded) and the provider skills present under `.agents/skills/` (excluding the reserved
namespace) — and re-writes the `.claude`/`.codex` mirror copies (no-clobber) plus re-seeds any
missing seeded copy in any root. It reports currency in `summary.md` like the other regenerated
views. Refresh remains a generator (not a lifecycle stage).

**Rationale.** FR-009 requires the invariant to hold after authored sources change, not only at
scaffold. Deriving from on-disk `.agents` (the canonical provider source) keeps refresh independent
of a possibly-stale provenance.

**Alternatives rejected.** Re-mirror from provenance only — stale if the provider re-ran; on-disk
`.agents` is the live source of truth.

## R7 — `doctor` / `upgrade` three-root drift (FR-010)

**Decision.** Extend `Drift.expectedArtifactPaths` to include `.agents/skills/{name}/SKILL.md` for
every seeded name (three roots now). Extend the drift model with a **three-root union** check: for
each skill in the union (seeded ∪ provenance `mirroredPaths`/provider `.agents`), all three roots
must be present and byte-identical; a missing/divergent root is drift. `doctor` reports it
read-only; `upgrade` reconciles via no-clobber re-seed (seeded) + re-mirror (provider) — reusing
`init`'s effects and the R6 mirror.

**Rationale.** A product scaffolded by a pre-056 CLI has only two roots; the fan-out CLI must detect
and reconcile that (the ADR-0008 orchestrator-axis staleness story, now including the third root).

**Alternatives rejected.** Only check seeded skills — misses provider-skill divergence across roots.

## R8 — Orchestrator version axis (FR-011)

**Decision.** Treat the third-root fan-out as an orchestrator-axis surface growth (ADR-0008): the
CLI release carrying this feature advances the effective minimum for a coherent scaffold, and the
CLI must be **published before** a clean scaffold consumes it (publish-before-flip). No provider
package-id or version literal is embedded in generic SDD; the version-of-truth bump + release
sequencing is a release task, not a behavioral code path. `doctor`/`upgrade` already surface
CLI-behind-minimum; the third root simply extends the expected-artifact set they compare against.

**Rationale.** Consistent with ADR-0008's orchestrator axis; keeps SDD provider-agnostic.

**Alternatives rejected.** Silent surface growth with no version signal — reintroduces the ADR-0008
"old CLI, newest pin, missing skills" invisibility hole, now for `.agents`.

## R9 — Migration-note posture (docs)

**Decision.** **Additive → recording paragraph in `docs/release/migrations/README.md`**, no
`<version>.md` file. Provenance stays v1 (additive `mirroredPaths` + `mirrored` owner). The `init`
third-root growth is a version-gated skeleton change (ADR-0008), disclosed in the paragraph and via
`doctor`/`upgrade` drift, not a breaking public-contract migration.

**Rationale.** Matches the repo's explicit obligation (additive-only releases MUST NOT carry a
`<version>.md` note) and the 050/052/053/054 precedent. The one nuance to disclose: unlike those,
`init`'s *output* changes (third root) — but no existing contract/field is removed or reshaped, and
the growth is detectable+reconcilable, so it remains additive in contract terms.

**Alternatives rejected.** A `<version>.md` migration note — would violate the additive-only
obligation and imply a breaking change that isn't one.

---

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| FR-007 provenance shape | R4 — additive `mirroredPaths` array + `mirrored` owner, **schema v1** |
| Mirror mechanism | R2 — post-instantiation tick: enumerate `.agents`, read body, write 2 roots |
| Mirror copy ownership | R3 — SDD-generated `mirrored` (no-clobber), NOT `generatedProduct` (so refresh owns currency) |
| Third root introduction | R1 — extend the single `SeededSkills.skillEffects` seam |
| Guard extension | R5 — one clause: `.agents/skills/fs-gg-sdd-*`; `.claude`/`.codex` stay whole-root |
| refresh / doctor / upgrade | R6/R7 — re-mirror + three-root union drift |
| Version axis | R8 — orchestrator-axis growth, publish-before-flip (ADR-0008) |
| Docs | R9 — additive recording paragraph, no `<version>.md` |
