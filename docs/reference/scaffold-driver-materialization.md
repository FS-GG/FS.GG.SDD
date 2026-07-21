# Scaffold-time driver-skill materialization

*Feature 108 · FS.GG.SDD#621 · ADR-0054 (driver skill class / byte-transport), ADR-0062/0063
(versioned package + on-disk materialize), ADR-0014 (one manifest, materialize-verify),
ADR-0061 (structural scope; semantic enforcement at the consumer).*

A **driver** skill is authored not by a producer repo but by `.github` itself, delivered as bytes
and materialized into a scaffolded product's skill roots. The first is `workRoadmap`. This page
describes how `fsgg-sdd scaffold` obtains and lays down those bytes. It embeds **no** `.github`- or
provider-specific package id, skill id, or path as behavior (`scaffold` FR-002 / SC-005): the package
identity is a pin, and the set of driver skills is read from the delivered manifest.

## The transport: pinned package → embedded bytes → offline materialize

1. **Pin.** `FS.GG.Drivers` is pinned in `Directory.Packages.local.props` (Renovate-managed, the same
   channel as `FS.GG.Kit`, ADR-0062). Bumping the driver is bumping this one version.
2. **Embed at build time (online).** `FS.GG.SDD.Commands` references the package; its auto-imported
   `build/FS.GG.Drivers.props` exposes `$(FsggDriversContentDir)`, from which the
   `driver-skill-manifest.json` and each `skills/<id>/SKILL.md` are linked as **embedded resources**
   (`Driver.manifest`, `Driver.skill/<id>/SKILL.md`).
3. **Materialize at scaffold time (offline).** A published `fsgg-sdd` runs as an installed `dotnet
   tool`; a package's *content* files are consumed at build time and are **not** carried into the
   installed tool nor guaranteed in an end user's NuGet cache. So the materializer reads the
   **compiled-in bytes** — never the NuGet cache, a `.github` clone, or the network (ADR-0054
   §Byte-transport). This is the same seam `SeededSkills` uses for the `fs-gg-sdd-*` skeleton.

## What the materializer does, per manifest row

For each row in the embedded `driver-skill-manifest.json`, in id order
(`DriverSkills.plan` → `HandlersScaffold` post-instantiation tick):

1. **Namespace guard (FR-007).** A row whose `id` collides with a seeded `fs-gg-sdd-*` skill is
   **rejected** (`scaffold.driverNamespaceCollision`) — a driver may never shadow the SDD skeleton.
2. **Predicate gate (FR-004).** The row is materialized **iff** its `materializes-when` predicate
   holds. The evaluator understands `always` (→ true), `false` (→ false), and `has <glob>` atoms
   joined by a single `and` **or** a single `or`, evaluated against the skill ids present in the
   workspace (seeded ∪ provider). A predicate it cannot evaluate yields a **skip** with a
   non-blocking `scaffold.driverPredicateUnevaluated` advisory — never a default materialize.
3. **Content-addressed verify (FR-003, ADR-0014).** The embedded body must hash (CRLF→LF-normalized
   SHA-256, lowercase hex) to the `sha256` its manifest row declares. A mismatch or missing body
   **fails closed** (`scaffold.driverVerifyFailed`): nothing is written for that row.
4. **Materialize (FR-001/FR-005).** A verified, predicate-true row is written into **all three**
   agent skill roots (`.claude`/`.codex`/`.agents` `/skills/<id>/SKILL.md`), byte-identically, with
   the no-clobber `AgentGuidanceTarget` write kind — an author-edited or pre-existing copy is
   preserved.

The delivered `FS.GG.Drivers 0.1.0` ships `workRoadmap` (`materializes-when: always`) and an inert
`drive-board` (`scope: operator`, `materializes-when: false`, bytes not shipped) — so every scaffold
materializes `workRoadmap` and skips `drive-board`.

## Provenance and refresh

Materialized driver paths are recorded in `.fsgg/scaffold-provenance.json` under the additive
`driverPaths` array (owner **`driver`**), each with the content `sha256` it was verified against. The
record schema stays **v1**. Driver paths are `.github`-owned external content: `refresh` never
regenerates them (it has no source for them), and its no-clobber union re-mirror preserves the
byte-identical copies — so a `refresh` neither rewrites nor removes a materialized driver.

The scaffold report projects the materialized set additively in all three projections
(`materializedDriverPaths` in json, `scaffoldMaterializedDriverPath` lines in text); an incomplete
materialization is surfaced by its diagnostic and never reported as complete (FR-009).

## Drift guard

The embedded manifest and bodies are pinned by a content-addressed drift guard
(`DriverSkillsTests`): the embedded manifest must parse, every shipped body must hash to its declared
`sha256`, and the delivered `workRoadmap` digest is pinned to a golden — so a stale pin or an
out-of-band edit is caught before release. The API surface is captured under
`docs/api-surface/**` and gated by `surface --check`.

## Not covered here

- Backfilling a **pre-existing** scaffold (before this feature) with a missing driver via
  `fsgg-sdd upgrade` / reporting it via `fsgg-sdd doctor` — the existing-scaffold transition path,
  carved to a follow-up item.
- Authoring or editing driver skill **content** — owned by `.github`; SDD lays the bytes down
  verbatim and verifies them.
