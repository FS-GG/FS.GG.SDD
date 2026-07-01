# Phase 0 Research: Scaffold co-tenant skills under the shared skill roots

**Feature**: `055-scaffold-cotenant-skills` | **Date**: 2026-07-01

All spec-open planning choices are resolved below (R1–R8). This feature **narrows a
single intrusion discriminator** so a compliant template provider may co-populate the
shared skill roots (`.claude/skills/`, `.codex/skills/`) with its own skills, while SDD
keeps sole ownership of the `fs-gg-sdd-*` namespace. It adds no new command, effect,
outcome, exit code, persisted field, or public signature. Every decision preserves the
outcome taxonomy, the exit-code mapping (0/2/1), and the schema-v1 provenance record
(FR-008 / FR-010 / FR-011).

## Anchoring facts (existing code)

- The load-bearing discriminator is `isSddTree`,
  `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:53-62`. It currently claims
  the **entire** `.claude/skills/` and `.codex/skills/` prefixes as SDD-owned (lines
  61-62), alongside `.fsgg/`, `work/`, `readiness/`.
- `isSddTree` is the *only* place these prefixes are matched for classification, and it is
  used at exactly two sites (grep-confirmed, all in `HandlersScaffold.fs`):
  1. **Intrusion partition**, `finalizeScaffold:376-377` — the produced-path diff is split
     `intrusions = produced |> Set.filter isSddTree` and
     `producedPaths = produced |> Set.filter (isSddTree >> not)`. A non-empty `intrusions`
     ⇒ `scaffold.providerWroteSddTree`, exit 2 (`:379-384`).
  2. **Collision exclusion**, via `isSddOwned` (`:64-66`, `isSddTree p || p = "AGENTS.md" ||
     p = "CLAUDE.md"`) → `collisionPaths` (`:77-81`), which excludes SDD-owned *pre-existing*
     content from the target-collision check (`:164`).
- `producedPaths` is the sole input to provenance product: `provenanceWriteEffect:268-282`
  maps every produced path to `{ Path = path; Owner = GeneratedProduct }` (`:277`) and stamps
  `SchemaVersion = 1` (`:270`). So a path's product-vs-intrusion classification and its
  provenance ownership are decided by the *same* `isSddTree` result — they cannot diverge.
- Seeded `fs-gg-sdd-*` skills are excluded from product **before** the `isSddTree` filter, by
  the diff itself: `produced = (afterSet − beforePaths) − skeletonFiles − provenancePath`
  (`:372-374`). `skeletonFiles` (`:83-88`) is exactly `initEffects request`'s WriteFile paths,
  which include the 30 seeded skill files (`SeededSkills.skillEffects`,
  `SeededSkills.fs:64-68`, reused by `Foundation.initEffects:390`).
- The provider is a real `dotnet new` at the single MVU `RunProcess` edge
  (`CommandEffects.runProcess`), and the report projects three ways: json contract
  (`CommandSerialization.writeScaffold`, `producedPaths` at `:312-313`), text
  (`CommandRendering` scaffold block `:196-237`), rich (`Cli/Rendering.renderRichTo`, derived
  generically from the plain-text `key: value` lines — **no scaffold-specific rich code**).
- Fixture in scope: `tests/fixtures/scaffold-provider/skills-intrusion/` writes
  `.claude/skills/leak/SKILL.md` and `.codex/skills/leak/SKILL.md`; registry
  `registries/skills-intrusion.providers.yml`; asserted by
  `ScaffoldCommandTests.fs` `scaffold provider writing into the seeded skill trees is a
  provider defect` (~`:626-638`).

---

## R1 — The change is one clause narrowing in `isSddTree` (not a second predicate)

**Decision**: Replace the two whole-root skills clauses of `isSddTree` with reserved-namespace
clauses that match only the `fs-gg-sdd-*` skill subtrees:

```fsharp
// A skill subtree under either shared skill root whose top-level skill name is in the
// SDD-reserved `fs-gg-sdd-*` namespace (FR-002/FR-007). Factored out for the symmetry.
let private isReservedSkillSubtree (p: string) =
    p.StartsWith(".claude/skills/fs-gg-sdd-", StringComparison.Ordinal)
    || p.StartsWith(".codex/skills/fs-gg-sdd-", StringComparison.Ordinal)

let isSddTree (path: string) =
    let p = normalizeRelativePath path
    p.StartsWith(".fsgg/", StringComparison.Ordinal)
    || p.StartsWith("work/", StringComparison.Ordinal)
    || p.StartsWith("readiness/", StringComparison.Ordinal)
    || isReservedSkillSubtree p
```

**Rationale**: `isSddTree` is the single discriminator that drives *both* the intrusion
partition and provenance ownership (anchoring facts). Narrowing it in one place makes the two
coherent by construction: a co-tenant skill (`.claude/skills/fs-gg-elmish/…`) becomes
simultaneously "not an intrusion" (FR-001) **and** "recorded as `GeneratedProduct`" (FR-004),
because both flow from the same `isSddTree = false`. Introducing a *separate* intrusion-only
predicate while leaving the wide clause in `isSddTree` was **rejected**: it would keep
co-tenant skills matching `isSddTree` at `:377`, filtering them **out** of `producedPaths` and
therefore out of provenance — directly violating FR-004. So single-predicate narrowing is not
just cleanest, it is *required* for the product-recording requirement.

## R2 — Namespace reservation is by `fs-gg-sdd-` name-prefix, not exact-seeded-set

**Decision**: Reserve the whole `fs-gg-sdd-*` name-prefix under each root, not just the 15
currently-seeded names. `.claude/skills/fs-gg-sdd-custom/…` (a name SDD does not seed today) is
therefore rejected as a reserved-namespace intrusion.

**Rationale**: This is the crux the coordination issue flagged (spec Assumptions). Prefix
reservation is forward-compatible (protects `fs-gg-sdd-*` names SDD may seed later), matches the
guard's stated ownership scope, and needs no reference to `SeededSkills.skillNames` in the guard
(so adding a seeded skill never silently reopens a hole). The exact-seeded-set alternative was
rejected as brittle and narrower than the ownership SDD actually claims. Either way the outcome
for **non-**`fs-gg-sdd-*` skills is identical (permitted product), so the co-tenancy this feature
enables is unaffected by the choice. **No `/speckit-clarify` required** — the spec pre-commits to
the prefix and the assumption is explicit; clarify would only revisit it if a reviewer prefers
exact-collision semantics, in which case only R2 and FR-001/FR-002 wording change.

## R3 — Re-point the `skills-intrusion` fixture to a *new* reserved-namespace path (FR-009)

**Decision**: Re-point the fixture from `.claude/skills/leak` / `.codex/skills/leak` to
`.claude/skills/fs-gg-sdd-custom/SKILL.md` / `.codex/skills/fs-gg-sdd-custom/SKILL.md`, and
update the asserting test to expect those paths rejected.

**Rationale + the trap this avoids**: Under the narrowed guard, `leak` is outside the reserved
namespace and becomes legitimate product, so the fixture no longer exercises the guard (FR-009).
The re-point target **must be a path SDD does not already seed**. The produced-path set is a
*diff*: `produced = (afterSet − beforePaths) − skeletonFiles`. A provider write to an **existing
seeded** path such as `.claude/skills/fs-gg-sdd-plan/SKILL.md` is subtracted (it is in both
`beforePaths` and `skeletonFiles`) and would **never** reach the intrusion filter — the test
would go green while detecting nothing. `fs-gg-sdd-custom` is a *new* directory in the reserved
namespace, guaranteed to appear in the diff and be flagged. This is exactly the edge case the
spec enumerates (`.claude/skills/fs-gg-sdd-custom/` → reserved-namespace intrusion). The
same-path *rewrite* case ("re-writes a seeded skill with differing content") is out of scope
here: the path-diff mechanism cannot see an in-place content overwrite of an existing path, and
that is **pre-existing** behavior unchanged by this feature (the seeded skills' no-clobber
protection is `AgentGuidanceTarget`, not this guard) — noted in the plan, not addressed by it.

## R4 — Add a co-tenant *success* fixture (US1 has no existing positive analog)

**Decision**: Add a new fixture `skills-cotenant` (registry + `dotnet new` template) that writes
a non-reserved skill, e.g. `.claude/skills/fs-gg-elmish/SKILL.md` (and, to exercise FR-007
asymmetry-tolerance, *not* a `.codex/skills/` mirror), plus an ordinary product file. A new test
drives it and asserts exit 0, `providerSucceeded`, the co-tenant skill present in
`ProducedPaths` (and as `GeneratedProduct` in provenance), and the seeded `fs-gg-sdd-*` skills
byte-identical to `init`.

**Rationale**: The current suite has fixtures that *leak into* SDD trees but none that legitimately
co-populates a skill root, because today every such write blocks — that is the bug. US1/SC-001
need a real provider that succeeds while writing a skill sibling. Real `dotnet new` fixture, no
mocks (Principle VI). The asymmetric single-root write also nails the FR-007 edge case ("only one
root receives a co-tenant write → permitted").

## R5 — Collision-exclusion coupling is benign and, in the edge, more correct

**Decision**: Accept that narrowing `isSddTree` also narrows `isSddOwned`/`collisionPaths`
(`:64-81`), and cover it with an assertion rather than decouple it.

**Rationale**: `collisionPaths` filters SDD-owned **pre-existing** content out of the
target-collision check, computed from `beforePaths` at resolution time (`:164`), *before* the
skeleton is seeded. The normal scaffold contract is an empty/near-empty directory, so no
`.claude/skills/*` pre-exists and the change is **inert**. In the only affected edge — scaffolding
into a directory that already contains a *non-*`fs-gg-sdd-*` skill — that skill is now treated as
a real collision (surfaced, blocking unless `--force`) instead of silently absorbed as "SDD's",
which is *more* correct: it is not SDD-owned. A pre-existing `fs-gg-sdd-*` skill stays excluded
(still SDD-owned under the narrowed prefix). No separate predicate is warranted; a guard-level
unit test pins the intended `isSddTree`/`isSddOwned` truth table.

## R6 — No persisted-schema, signature, or projection-shape change (FR-008/FR-010)

**Decision**: `isSddTree` is a module-`internal let`; the change adds no type, field, or public
signature — so no `.fsi` edit and no `PublicSurface.baseline` refresh. `scaffold-provenance.json`
stays `SchemaVersion = 1` (`:270`); the json/text/rich projections gain **no new key** — a
co-tenant skill appears only as an additional entry in the already-existing `producedPaths` list
and the already-existing `GeneratedProduct` provenance array. Rich derives from text, so no rich
code changes.

**Rationale**: FR-008 requires the JSON automation contract to be additive with no removed,
renamed, or reshaped fields, and FR-010 requires `init` byte-identical. Because the only observable
delta is *which paths* populate existing arrays, US3's "diff differs only by additive produced-path
entries" holds by construction. Determinism is preserved (produced paths already sorted at
`:377`).

## R7 — `init` byte-identical; guard is the only behavioral change (FR-010)

**Decision**: Touch nothing in the seeding path (`SeededSkills.fs`, `Foundation.initEffects`,
`skeletonFiles`). Scaffold continues to reuse `initEffects` unchanged.

**Rationale**: FR-010 mandates byte-identical `init`. The seeded set, order, bodies, and
write-kind are untouched; the `SeededSkillsTests` drift guard and the `init` no-clobber tests
remain green with no change. Only the *classification of provider-produced paths* moves.

## R8 — Safety property retained: real intrusions still exit 2, incomplete never "complete"

**Decision**: Preserve every non-skill intrusion arm (`.fsgg/`, `work/`, `readiness/`) verbatim,
keep the `providerWroteSddTree`/`providerFailed`/`providerUnavailable` diagnostics, outcome
strings, and the `exitCodeForReport` mapping (provider-defect id + `Blocked` → 2) unchanged, and
keep provenance recording the incomplete-scaffold's produced paths as `GeneratedProduct` on the
defect terminal (`:383`) so nothing is laundered.

**Rationale**: FR-002/FR-003/FR-011 and SC-003 require the guard to stay closed on genuine
intrusions while it opens for co-tenants. The `lifecycle-intrusion` fixture (writes into
`work/`/`readiness/`) and the re-pointed `skills-intrusion` fixture (writes into
`fs-gg-sdd-custom`) together keep both intrusion classes exercised after the narrowing.

---

## Cross-repo

No versioned contract surface changes here (behavioral coherence only). Recording the co-tenant
`.claude/skills/` ownership model in the cross-repo coherence set — and an ADR if it is judged a
durable cross-repo choice — is an expected follow-up tracked by **FS-GG/FS.GG.Templates#47** and
is out of scope for this code fix (spec Assumptions). Close the loop on **FS-GG/FS.GG.SDD#55** at
merge via `cross-repo-coordination`; the unblocked consumer is **FS.GG.Rendering Feature 219**.
