# Implementation Plan: Scaffold-Time Materializer for the Delivered `workRoadmap` Driver Skill

**Spec**: `specs/108-driver-scaffold-materializer/spec.md` · **Item**: FS.GG.SDD#621 · **Contracts**:
`scaffold-provider` (v1, additive), `skill-registry` (ADR-0017) · **ADRs**: ADR-0054, ADR-0062/0063,
ADR-0014, ADR-0037/0015 §3, ADR-0061.

## Architecture

The delivered `FS.GG.Drivers` package is consumed exactly as `SeededSkills` consumes the `fs-gg-sdd-*`
skeleton: **its bytes are embedded resources in the CLI's assembly**, restored online at build time
from the pinned package's `$(FsggDriversContentDir)`, and read from compiled-in bytes at scaffold time.
The decisive consequence: **reading the driver manifest and bodies is a pure compiled-in read — not an
MVU effect** — so the whole materializer plans ordinary no-clobber `WriteFile(AgentGuidanceTarget)`
effects, and **no `CommandEffect` case, `effectKey`, or interpreter arm changes.** (An earlier
read-from-NuGet-cache design would have needed one; embedding removes it and is what ADR-0054
§Byte-transport requires for offline scaffold time anyway.)

| Layer | Project / file | Adds | Why here |
|---|---|---|---|
| Pin | `Directory.Packages.local.props` | `PackageVersion FS.GG.Drivers 0.1.0` (Renovate-managed) | The one place package identity/version lives (ADR-0062; no literal in behavior). |
| Build embed | `src/FS.GG.SDD.Commands/…fsproj` | `PackageReference FS.GG.Drivers`; `EmbeddedResource` from `$(FsggDriversContentDir)` (`Driver.manifest`, `Driver.skill/<id>/SKILL.md`) | Restore online at build, read compiled-in at scaffold (offline). Same seam as `SeededSkills`. |
| Manifest model | `src/FS.GG.SDD.Artifacts/DriverManifest.fs`(+`.fsi`) | `DriverManifestEntry`/`DriverManifest`, `tryParse`; `DriverPredicate.evaluate` | BCL/STJ leaf, pure. The *shape* of a driver manifest, never this one's contents. |
| Provenance | `src/FS.GG.SDD.Artifacts/ArtifactRef.fs`, `ScaffoldProvenance.fs`(+`.fsi`) | `ArtifactOwner.Driver`; additive `DriverPaths` array (path+`sha256`), owner `driver` | Additive, schema stays **v1**; authoritative for `refresh` exclusion. |
| Materializer | `src/FS.GG.SDD.Commands/CommandWorkflow/DriverSkills.fs` | Read embedded manifest+bodies; content-addressed verify; predicate gate; plan `WriteFile(AgentGuidanceTarget)` per root | Reads compiled-in bytes (must live in the assembly that embeds them). Sibling of `SeededSkills`. |
| Scaffold wiring | `src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs` | Post-instantiation driver tick: plan driver writes, record `DriverPaths`, report ids/roots or defect | Where the mirror tick + provenance already live. |
| Remediation *(carved to follow-up)* | `HandlersDoctor.fs` / `HandlersUpgrade.fs` / `Drift.fs` | Driver in the expected-seeded set: `doctor` reports a missing driver (read-only); `upgrade` `artifactReSeed` backfills it no-clobber | The existing-scaffold transition path; a strict addition over new-scaffold materialization, sequenced after this PR. |
| Drift guard | `tests/` (+ optional `registry` sub-verb) | Assert embedded manifest+bodies parse and each body's digest equals its declared `sha256`; golden on `workRoadmap` sha256 | Catches an out-of-band embed edit or a stale pin (FR-008). |

### The content-addressed verify (ADR-0014), reused not reinvented

`Fsgg.SkillMirror.sha256` already computes the CRLF→LF-normalized, lowercase-hex canonical-body digest
that the manifest's `sha256` values are in (verified: the delivered `workRoadmap` body hashes to the
manifest's `2b9313bf…b06812`). `DriverSkills` computes `SkillMirror.sha256 body` and compares it to the
row's `sha256`; unequal ⇒ no write + `scaffold.driverVerifyFailed`. The `verify` three-axis model
(`missing` / `divergent-across-roots` / `hash-mismatch`) is reused for the doctor/drift read.

### Predicate evaluation (FR-004)

The delivered manifest uses `always` (→ materialize) and `false` (→ skip). `DriverPredicate.evaluate
predicate presentIds` returns `bool option`: `always`→`Some true`, `false`→`Some false`, and the
`has <glob>` / `and` / `or` forms of the ADR-0017 grammar evaluated against the present skill-id set
(seeded ∪ provider) — covering the documented composed driver shape. Any predicate it cannot parse
returns `None` ⇒ the row is **skipped with a non-blocking `scaffold.driverPredicateUnevaluated`
advisory**, never materialized (fail closed). This honors publish-before-flip: SDD evaluates the shapes
delivered artifacts actually carry and refuses to guess at ones they don't.

### Ownership & no-clobber (FR-005/FR-007)

Driver writes use the `AgentGuidanceTarget` write kind, which the interpreter edge already treats as
no-clobber (the seeded-skeleton and constitution class). The reserved `fs-gg-sdd-*` namespace is
protected by rejecting any driver row whose `id` matches a seeded skill name before planning writes.
Driver paths are recorded under the new `DriverPaths` provenance array (owner `driver`), disjoint from
`ProducedPaths` (provider, `generatedProduct`), `MirroredPaths` (`mirrored`), and `SddOwnedPaths`.

### Refresh / upgrade boundaries (FR-006/FR-010)

`refresh` reads `scaffold-provenance.json` for its exclusion set; `DriverPaths` join the excluded,
externally-owned set (like `generatedProduct` and the seeded skeleton) — `refresh` never regenerates or
removes them. `upgrade`'s `artifactReSeed` (missing-only, consumer-writes-only, no-clobber) gains the
driver in its expected-artifact set so a pre-existing scaffold can backfill a missing driver; `doctor`
reports the same missing driver as read-only drift.

## Verification plan

- **Unit** (`tests/FS.GG.SDD.Artifacts.Tests`, `tests/FS.GG.Contracts.Tests`): `DriverManifest.tryParse`
  round-trips the delivered manifest; `DriverPredicate.evaluate` truth-table (`always`/`false`/`has`/
  `and`/`or`/garbage→None); `ArtifactOwner.Driver` and `ScaffoldProvenance` `DriverPaths` serialize/parse
  round-trip with schema still `1`.
- **Materializer** (`tests/FS.GG.SDD.Commands.Tests`): `DriverSkills` plans one `WriteFile` per root for
  `workRoadmap` (materializes-when `always`), none for `drive-board` (`false`); a tampered body digest ⇒
  no writes + defect; an `fs-gg-sdd-*`-id row ⇒ rejected. Driver writes are `AgentGuidanceTarget`.
- **Scaffold acceptance** (`tests/FS.GG.SDD.Commands.Tests` scaffold path, offline synthetic provider):
  after scaffold, `workRoadmap/SKILL.md` exists byte-identically in all three roots; provenance lists it
  owner `driver` + sha256, schema `1`; a `refresh` leaves it untouched and unreported. **Driven, not just
  asserted** — record the scaffold report JSON (driver block) + a `refresh` verdict in the PR body.
- **Offline witness** (AC-004): the materialize path reads only embedded bytes — a test with no NuGet
  cache/network still materializes (embedded-resource read has no I/O dependency).
- **Drift guard** (FR-008/AC-007): green against the pinned package; red when the embedded manifest or a
  body is altered. Golden constant pins the delivered `workRoadmap` sha256.
- **Gates**: `dotnet test` green · `fantomas` clean · reflection `PublicSurface.baseline` updated for the
  additive `Artifacts` surface · `surface --update` refreshes `docs/api-surface/**` (regenerated, not
  declared) · `surface --check` green.

## Sequencing (publish-before-flip)

This is the **SDD-side, second** PR of the ADR-0054 sequence; the `.github`-side artifact (`FS.GG.Drivers`
`0.1.0`) is already published, so no ordering constraint remains open. The `.github` `skills.yml` /
predicate-widening work (`.github#1247`) is a separate, independently-sequenced item and is **not** a
dependency of this materializer, which reads the *delivered* manifest as it stands.
