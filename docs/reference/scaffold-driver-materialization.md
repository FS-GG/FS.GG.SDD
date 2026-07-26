# Scaffold-time driver-skill materialization

*Feature 108 · FS.GG.SDD#621 · ADR-0054 (driver skill class / byte-transport), ADR-0062/0063
(versioned package + on-disk materialize), ADR-0014 (one manifest, materialize-verify),
ADR-0061 (structural scope; semantic enforcement at the consumer).*

A **driver** skill is authored not by a producer repo but by `.github` itself, delivered as bytes
and materialized into a scaffolded product's skill roots. The current always-on set is `padd-item`,
`work-board`, and `work-roadmap`. This page
describes how `fsgg-sdd scaffold` obtains and lays down those bytes. It embeds **no** `.github`- or
provider-specific package id, skill id, or path as behavior (`scaffold` FR-002 / SC-005): the package
identity is a pin, and the set of driver skills is read from the delivered manifest.

## The transport: pinned package → embedded bytes → offline materialize

1. **Pin.** `FS.GG.Drivers` is pinned in `Directory.Packages.local.props` (Renovate-managed, the same
   channel as `FS.GG.Kit`, ADR-0062). Bumping the driver is bumping this one version.
2. **Embed at build time (online).** `FS.GG.SDD.Commands` references the package; its auto-imported
   `build/FS.GG.Drivers.props` exposes `$(FsggDriversContentDir)`, from which the
   `driver-skill-manifest.json` and every member below each `skills/<id>/` directory are linked as
   **embedded resources** (`Driver.manifest`, `Driver.skill/<id>/<relative-path>`).
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
3. **Manifest-v2 verify (FR-003, ADR-0014).** The ordered `files` array is compact-JSON hashed
   against `tree-sha256`. Every row requires a unique, contained forward-slash relative path, a
   raw-byte SHA-256, and a Boolean executable flag. The embedded directory must be a closed match:
   missing, extra, unreadable, digest-mismatched, duplicate, or path-traversing members fail the
   entire skill row (`scaffold.driverVerifyFailed`), never producing a partial directory. The
   legacy row-level `sha256` still binds `SKILL.md`.
4. **Materialize (FR-001/FR-005).** Every file in a verified, predicate-true row is written into
   **all three** agent skill roots (`.claude`/`.codex`/`.agents` `/skills/<id>/<relative-path>`),
   byte-identically, with the no-clobber `AgentGuidanceTarget` write kind. Declared executable
   members receive their executable bit. A provider-owned same-id skill owns its complete
   directory, so SDD never creates a mixed provider/driver tree.

The delivered `FS.GG.Drivers 0.8.3` ships three `scope: driver`,
`materializes-when: always` rows: `padd-item`, `work-board`, and `work-roadmap`. It also carries
`drive-board`, `p-add`, and `cut-nuget-release` as `scope: operator`,
`materializes-when: false`; those operator commands are withheld from product workspaces.
`padd-item` files a described issue onto the product workspace's explicitly configured organization,
named-user, or viewer GitHub Projects v2 board. Its package-delivered guidance refuses missing wiring
without mutation and never falls back to the FS-GG organization board.

The 0.8.3 payload is closed under product materialization: its real Markdown links target packaged
members or guaranteed product siblings. Conditional coordination-kit dependencies remain named
requirements rather than filesystem links. The fresh-scaffold regression resolves every remaining
relative link, including parent-relative targets; it has no prefix-based escape hatch.

## Provenance and refresh

Every materialized driver file is recorded in `.fsgg/scaffold-provenance.json` under the additive
`driverPaths` array (owner **`driver`**), with its manifest file digest. The record schema stays
**v1**. Driver paths are `.github`-owned external content: `refresh` never
regenerates them (it has no source for them), and its no-clobber union re-mirror preserves the
byte-identical copies — so a `refresh` neither rewrites nor removes a materialized driver.

The scaffold report projects the materialized set additively in all three projections
(`materializedDriverPaths` in json, `scaffoldMaterializedDriverPath` lines in text); an incomplete
materialization is surfaced by its diagnostic and never reported as complete (FR-009).

## Drift guard

The embedded manifest and complete directories are pinned by a content-addressed drift guard
(`DriverSkillsTests`): the embedded manifest must parse, its tree digest must bind the file index,
and every shipped file must hash to its declared digest — so a stale pin or an out-of-band edit is
caught before release. The API surface is captured under
`docs/api-surface/**` and gated by `surface --check`.

## Backfilling an existing scaffold (`upgrade` / `doctor`)

*ADR-0063 (Consequences: "Existing scaffolds need a backfill") · FS.GG.SDD#624.*

A tree scaffolded **before** owner-sourced delivery is missing the owner-sourced skills — a shipped
`sdd`-lane tree with no `work-roadmap`, a `--profile game` tree with no `fs-gg-playtest`. The
decision, settled once for the driver and product classes alike (ADR-0063; FS.GG.SDD#620 asked the
same question):

- **`fsgg-sdd upgrade` backfills them** — no re-scaffold required. It is the additive, non-destructive
  path, and it is the **recommended** one.
- **Re-vendor (re-scaffold) also backfills** by construction, since `scaffold` materializes the same
  set; it is the heavier path, for a tree being regenerated anyway.

`upgrade` treats a missing owner-sourced skill as a **missing expected artifact** and folds it into
the same no-clobber `artifactReSeed` reconciliation step that already re-seeds the SDD skeleton. The
bytes come from the **same embedded, content-addressed materialize-and-verify plan** `scaffold` runs
(`DriverSkills.plan` / `GameSkills.plan`) — so a backfill is offline, reproducible, and can never lay
down an unverified body (ADR-0014 preserved; only the byte *source* changed — ADR-0063). The plan is
fed from the recorded provenance rather than a live scaffold: the driver `has …` grammar reads the
present-skill set (seeded ∪ the product ids recorded in provenance), and the product
`materializes-when` predicate reads the recorded `effectiveParameters` (`profile`, …). Writes are
no-clobber `AgentGuidanceTarget`, so an author-edited or already-present copy is preserved and only
the missing roots are filled.

`fsgg-sdd doctor` reports the same gap read-only, including an older directory that has `SKILL.md`
but lacks its declared `references/**`, `agents/**`, or `scripts/**` members. The missing files are
previewed under the `artifactReSeed` step (kept out of the seeded-skeleton
`missingArtifacts`/`expectedArtifactCount` axis). After a successful backfill, `upgrade` amends
`driverPaths` with every affected directory member and digest, including the pre-existing
`SKILL.md`; present files remain no-clobber.

## Not covered here

- Authoring or editing driver skill **content** — owned by `.github`; SDD lays the bytes down
  verbatim and verifies them.
- **Content** drift of an owner-sourced copy (a tampered `work-roadmap` body) — `upgrade`/`doctor`
  reconcile owner-sourced skills by **presence** (backfill), not against a recorded digest the way
  provider *product* skills are content-verified; the materialize-time content-addressed verify still
  guards what a backfill writes.
