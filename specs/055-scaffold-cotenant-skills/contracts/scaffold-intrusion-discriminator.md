# Contract: Scaffold intrusion discriminator (narrowed for shared skill roots)

**Feature**: `055-scaffold-cotenant-skills` | **Kind**: behavioral contract (no schema/signature change)

This is the one behavioral contract this feature changes: how `fsgg-sdd scaffold` partitions
the paths a template provider produces into **intrusions** (rejected, exit 2) vs **product**
(recorded as `GeneratedProduct`). Realized by `isSddTree`,
`src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:53-62`.

## Reservation rule (after this feature)

A provider-produced path is an **SDD-tree intrusion** iff, after `normalizeRelativePath`, it
starts with any of:

- `.fsgg/`
- `work/`
- `readiness/`
- `.claude/skills/fs-gg-sdd-`   ← **narrowed** (was `.claude/skills/`)
- `.codex/skills/fs-gg-sdd-`    ← **narrowed** (was `.codex/skills/`)

Otherwise it is **provider product**. In particular, a skill directory under either shared skill
root whose top-level skill name is **not** in the `fs-gg-sdd-*` namespace is product.

### Properties

| ID | Property |
|----|----------|
| P1 | **Co-tenancy (FR-001).** `.claude/skills/<non-sdd>/…` and `.codex/skills/<non-sdd>/…` are product, never intrusions. |
| P2 | **Reservation (FR-002).** Any provider write matching `.{claude,codex}/skills/fs-gg-sdd-*` is an intrusion → `scaffold.providerWroteSddTree`, exit 2, outcome `providerFailed`. The reservation is by name-prefix, so `fs-gg-sdd-custom` (not currently seeded) is reserved too (research R2). |
| P3 | **Other trees (FR-003).** `.fsgg/`, `work/`, `readiness/` intrusion arms are byte-for-byte unchanged. |
| P4 | **Symmetry (FR-007).** The rule is identical for `.claude/skills/` and `.codex/skills/`; a provider may write a co-tenant skill to one root and not the other. |
| P5 | **Product recording (FR-004).** A permitted co-tenant path lands in `producedPaths` (`:377`) and therefore in provenance as `{ Owner = GeneratedProduct }` (`:277`) and in all three report projections' produced-path list. Classification and provenance ownership derive from the *same* `isSddTree` result — they cannot disagree. |
| P6 | **Seeded exclusion (FR-004/FR-010).** Seeded `fs-gg-sdd-*` skill paths never appear in `producedPaths` or provenance: they are removed by the `skeletonFiles` subtraction (`:373`) *before* the `isSddTree` filter, independent of this change. |
| P7 | **No schema/contract reshape (FR-008).** No new key in json/text/rich; provenance stays `SchemaVersion = 1`. Only additional entries in the existing produced-path arrays. |
| P8 | **Safety retained (FR-011/SC-003).** On any intrusion the run is `Blocked`, exits 2, and is never reported complete; produced paths on the defect terminal are still recorded (not laundered). |

## Determinism

`producedPaths` and `intrusions` remain `List.sort`-ordered (`:376-377`). For an equivalent
scaffold, the json report and provenance differ from the pre-change output **only** by additive
produced-path entries; the provenance schema version is unchanged (SC-004).

## Out of scope (documented, not addressed)

- **In-place rewrite of an existing seeded skill at the same path.** The produced set is a path
  diff (`afterSet − beforePaths`), so a provider overwriting `.claude/skills/fs-gg-sdd-plan/SKILL.md`
  *in place* is not visible as a produced path. This is **pre-existing** behavior, unchanged here;
  the seeded skills' protection against re-seeding clobber is the `AgentGuidanceTarget` no-clobber
  write-kind, not this guard. The negative fixture therefore targets a **new** reserved-namespace
  path (`fs-gg-sdd-custom`, research R3), which the diff does surface.
