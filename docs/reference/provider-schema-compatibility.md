# Provider schema-v1 compatibility window

*Declared 2026-10-25 under FS-GG/FS.GG.SDD#1139 (SDD928). Parent: #928. This page is the
authoritative declaration; the machine-readable block below is checked in the required
gate by `scripts/check-provider-schema-window.py` against the source and the tests it pins.*

#928 asks SDD to keep "schema-v1 F# provider inspection and invocation" working while the
additive catalog/provider contract (catalog schema 2, contract 3.0.0) is introduced, "during
the compatibility window". This page declares that window.

## What the window covers

| Subject | Declaration |
|---|---|
| Registry | `.fsgg/providers.yml` with `schemaVersion: 1` (a missing, malformed or out-of-range registry schema is refused) |
| Descriptor contract | `contractVersion` major 1: accepted for any value whose first dotted component is `1` (`1.0.0`, `1.9.9`, even `1.x` or `1`) with the legacy reader semantics. Major 2: accepted for a stable `2.y.z` that declares `minimumFsggSdd.version` of at least `2.1.0` which the installed CLI meets. Supported range, in source: `>=1.0.0 <3.0.0` |
| Invocation | `fsgg-sdd scaffold --provider <name>` (inspection means the registry parse, the `--dry-run` plan and the read-only `doctor`) runs the descriptor's `dotnet new` template with the effective parameters forwarded literally, including a provider-declared identifier parameter, and produces no output outside the provider and SDD ownership rules |
| Provenance | the legacy route writes `.fsgg/scaffold-provenance.json` with `schemaVersion` 1; the legacy reader accepts that schema and refuses others |

Separate and additive, not covered by the window's guarantees: the explicit catalog route
(`scaffold --catalog ...`, contract 3.0.0), which writes provenance schema 2, and the provider-source
route, which writes schema 3. They never change what the schema-v1 route does, and the schema-v1 route
refuses contract 3.0.0 before any mutation. No automatic conversion of a schema-1 workspace to the catalog
route is provided or implied.

## The window

* **Opens** with SDD 1.0.0, the first stable release line. Pre-1.0 releases (0.4.0 to 0.32.0) already ship the registry route (with the narrower range `>=1.0.0 <2.0.0`) but are not covered by this window or the installed journey.
* **Stays open** through the entire SDD 2.x line.
* **May close no earlier than SDD 3.0.0.** Narrowing or removing any covered behaviour is a breaking public
  contract change: it needs a major version, the repository's ApiCompat and migration process, and an
  announcement at least one MINOR release earlier in the release notes plus a non-blocking diagnostic from the
  tool. Nothing is deprecated today; no announcement exists.
* A migration path is a preview with no-clobber and owner-byte preservation (the retained-upgrade contract),
  never an automatic rewrite.

## Guarantees during the window

Each guarantee is enforced by the pinned existing unit tests (checked by name) and, across published
releases, by the installed journey `scripts/provider-schema-window-journey.py`.

| Id | Guarantee |
|---|---|
| G1 | A registry provider whose contract is outside the supported range (major 0, major 3 or above, a non-numeric major, or a major-2 pre-release) is refused before any provider process or write, with `scaffold.providerVersionUnsupported`; contract 3.0.0 is refused by the legacy route before mutation |
| G2 | A major-2 provider is admitted only with a stable version and a declared minimum of at least 2.1.0 that the installed CLI meets; otherwise it is refused before writes and provider processes with `scaffold.providerCapabilityRefused`. An older release refuses a contract newer than it supports rather than misreading it |
| G3 | A schema-v1 registry provider keeps its legacy shape: the today-shape entry parses with the extended fields defaulted, and an out-of-range registry schema is refused |
| G4 | The legacy route's provenance stays schema 1 (also when an identifier parameter is derived and when the provider fails), is byte-stable and deterministic, and the legacy reader refuses any other schema |
| G5 | A repeated scaffold blocks on collision without clobbering the provenance, and refresh excludes provider-produced paths |
| G6 | Across the nine sampled published releases 1.0.0 to the current release (of about thirty stable releases from 1.0.0; the sample spans both majors, the 2.1.0 contract-2 boundary and every release of the catalog line), the same schema-v1 provider scaffolds successfully and the provider-owned bytes and the provenance core (schema, provider, contract, template, outcome, effective parameters, minimum, produced and mirrored paths with digests) are identical; the record gains additive keys across releases (driver, game and rendering skill paths) that are not part of the core; only SDD-owned seeded content and the generator version legitimately differ between releases |
| G7 | The current release reads a workspace scaffolded by each sampled release (older ones and its own) read-only (`doctor`): no provenance or registry diagnostic (a `doctor.driftDetected` warning that the workspace is behind the current coherent set is expected and allowed), and no write |
| G8 | Out-of-range and below-minimum contracts are refused by the installed current release before any provider process or write |

## Not guaranteed

* Old clients reading newer provenance: a release older than the schema refuses it; no claim is made.
* Equality of SDD-owned seeded skills, tool manifests and scripts between releases.
* The provider's own template output: the window preserves SDD's transport, not provider content.
* Releases that are not sampled by the journey: they are covered by the declaration and the pinned tests, not by an installed run.
* The catalog route: it is governed by the catalog contract, not by this window.

## Machine-readable declaration

```json provider-schema-window
{
  "schema": "fsgg.provider-schema-window/v1",
  "opensAtRelease": "1.0.0",
  "openThroughLine": "2.x",
  "closesNotBefore": "3.0.0",
  "announceMinorReleasesBeforeClose": 1,
  "registry": {
    "schemaVersion": 1,
    "supportedContractRange": ">=1.0.0 <3.0.0",
    "major2MinimumCliVersion": "2.1.0"
  },
  "provenance": {
    "legacyRouteSchema": 1,
    "catalogRouteSchemas": [2, 3]
  },
  "publishedReleases": ["1.0.0", "1.8.0", "2.0.3", "2.1.0", "2.2.0", "2.3.0", "2.4.0", "2.5.0", "2.5.1"],
  "guarantees": [
    {"id": "G1", "tests": [
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "scaffold unsupported contract version blocks before invocation"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "window legacy scaffold refuses a contract outside the supported range before invocation"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "window legacy scaffold admits a major one contract with the legacy reader semantics"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "catalog protocol three remains refused by legacy scaffold before mutation"}]},
    {"id": "G2", "tests": [
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "contract two capability failures refuse before writes and provider processes"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "contract two admits ordinary and typed lifecycles without changing selection"}]},
    {"id": "G3", "tests": [
      {"file": "tests/FS.GG.SDD.Artifacts.Tests/ProviderRegistryParseTests.fs", "name": "parseProviderRegistry defaults extended fields for a today-shape entry"},
      {"file": "tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs", "name": "parseProviderRegistry rejects an out-of-range schema version"}]},
    {"id": "G4", "tests": [
      {"file": "tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs", "name": "serialize is byte-stable and sorts produced paths"},
      {"file": "tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs", "name": "tryParse on an unsupported schema yields None"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "scaffold identifier derivation keeps provenance schema at 1"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "scaffold provider failure leaves provenance at schema v1 with no captured output"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "scaffold provenance and json are deterministic across identical runs"}]},
    {"id": "G5", "tests": [
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "repeat scaffold blocks on collision without clobbering provenance"},
      {"file": "tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs", "name": "refresh excludes provider-produced paths and flags malformed provenance"}]},
    {"id": "G6", "journey": true},
    {"id": "G7", "journey": true},
    {"id": "G8", "journey": true}
  ]
}
```

## Verifying the window

* Required gate: `python3 -I -B scripts/check-provider-schema-window.py` (declaration versus source constants,
  the SDD major line, the accepted provenance schemas, the published-release list and the pinned tests), with
  its controls in `scripts/tests/provider-schema-window.test.py`.
* Installed, across published releases, from an empty private cache and a credential-free environment:
  `python3 -I -B scripts/provider-schema-window-journey.py run --work <fresh-dir>`. Ordinary verification
  under ADR-0096 on the supported Linux-x64 / .NET 10.0.12 / glibc 2.44 host with at most six CPUs; a local
  cooperative profile, not a hosted or hostile one.
