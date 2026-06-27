# Phase 1 Data Model: Scaffold lifecycle-parameter pass-through & app-only provenance

This feature introduces **no new schema or artifact type**. The entities below are the
existing structures *under verification*, plus the test-only fixture that exercises them.
Each entry states the invariant this feature pins and where the authoritative definition
lives (unchanged).

## Entities

### 1. Lifecycle parameter (opaque key=value)

- **What**: the `(key, value) = ("lifecycle", <value>)` pair, one of many in the scaffold
  request's `Parameters: (string * string) list` (`CommandTypes.fs:70-73`), overlaid onto
  provider defaults into `effective: Map<string,string>` (`HandlersScaffold.fs:84-96`) and
  serialized as `-p:lifecycle=<value>` (`HandlersScaffold.fs:175-178`).
- **SDD's relationship**: **opaque**. SDD attaches no meaning to the key `lifecycle` or any
  of its values. It is carried, never interpreted/renamed/defaulted/special-cased/dropped.
- **Invariants pinned**:
  - Forwarded verbatim end-to-end (US1, FR-002): the recording fixture's echoed file equals
    the supplied value.
  - The forwarded *set* equals `defaults ⊕ author --param` exactly — no add/drop/rename
    (FR-003, SC-001).
  - Forwarding is **value-agnostic**: an arbitrary value forwards identically (Decision 4
    value-agnosticism test; FR-007 / US3.2).
- **Owner**: provider/template (FS.GG.Rendering in production). **Not SDD.**

### 2. App-only product tree

- **What**: the set of files the provider's template materializes into the target —
  e.g. `App.fsproj`, `Program.fs`, `scaffold-manifest.txt` for the recording fixture.
- **Definition**: computed by `finalizeScaffold` diffing the post-create target against the
  SDD skeleton, filtering out SDD-owned paths (`HandlersScaffold.fs:62-75, 217-285`).
- **Invariants pinned**:
  - Disjoint from the SDD skeleton (no overlap, FR-005 / SC-002).
  - Exactly the set recorded in provenance (precision + recall, FR-004 / SC-003).
  - Each marked `generatedProduct` (FR-004 / SC-002) and excluded by `refresh` (FR-007 of
    030, re-asserted here).
- **Owner**: external (`generatedProduct`).

### 3. SDD skeleton

- **What**: the `init`-equivalent files SDD establishes: `.fsgg/`, `work/`, `readiness/`,
  and the agent context files (`AGENTS.md`, `CLAUDE.md`).
- **Definition**: produced by `initEffects` (`Foundation.fs`), reused **unchanged** by
  scaffold; classified by `isSddOwned` (`HandlersScaffold.fs:52-60`).
- **Invariants pinned**:
  - Established **byte-identical** to a standalone `init` run (FR-005): each skeleton file a
    `lifecycle=sdd` scaffold writes equals the corresponding `init`-written file byte-for-byte.
  - Never recorded as app-only; never modified by the provider (intrusion → `providerWroteSddTree`,
    exit 2; FR-008).
- **Owner**: SDD / author.

### 4. Scaffold provenance record (`.fsgg/scaffold-provenance.json`, schema v1 — unchanged)

- **What**: the boundary record written after a real provider run (`HandlersScaffold.fs:205-215`),
  serialized deterministically (`ScaffoldProvenance.fs:33-60`).
- **Shape** (unchanged — reproduced for reference only; **not** modified by this feature):
  ```json
  {
    "schemaVersion": 1,
    "generator": { "id": "...", "version": "..." },
    "providerName": "fixture",
    "providerContractVersion": "1.0.0",
    "templateRef": "fsgg-fixture-lifecycle",
    "outcome": "providerSucceeded",
    "producedPaths": [ { "path": "App.fsproj", "owner": "generatedProduct" }, … ]
  }
  ```
- **Invariants pinned**: `producedPaths` == app-only tree (entity 2); all `owner =
  generatedProduct`; sorted by `path`; byte-identical across two identical runs (FR-006 /
  SC-004); no skeleton path present (FR-005 / SC-002).
- **Owner**: SDD (written by SDD as the ownership boundary record).

### 5. Composition fixture provider — *recording* variant (test-only, NEW)

- **What**: `tests/fixtures/scaffold-provider/lifecycle/`, a local `dotnet new` template +
  `registries/lifecycle*.providers.yml`. Rendering-agnostic.
- **`template.json` symbols**:
  | Symbol | datatype | replaces | required | purpose |
  |---|---|---|---|---|
  | `productName` | string | `PRODUCT_NAME` | yes | existing app-name substitution |
  | `lifecycle` | string | `LIFECYCLE_VALUE` | no (req. variant: yes) | echo channel + opaque-forward proof |
- **Produced files**: `App.fsproj`, `Program.fs` (app stubs), and `scaffold-manifest.txt`
  containing at least `lifecycle=LIFECYCLE_VALUE` (and `productName=PRODUCT_NAME`) — the
  recording channel for the verbatim-arrival assertion.
- **Registry variants**:
  | File | `lifecycle` required? | template → fixture dir | exercises |
  |---|---|---|---|
  | `lifecycle.providers.yml` | no | `fsgg-fixture-lifecycle` → `lifecycle/` | US1, US2, determinism, value-agnosticism |
  | `lifecycle-required.providers.yml` | yes | same template | required-but-missing edge (FR-008) |
  | `lifecycle-empty.providers.yml` | no | `fsgg-fixture-lifecycle-empty` → `lifecycle-empty/` | empty-product edge (FR-008) |
  | `lifecycle-intrusion.providers.yml` | no | `fsgg-fixture-lifecycle-intrusion` → `lifecycle-intrusion/` | SDD-tree-intrusion edge (FR-008) |
- **Constraint**: no real FS.GG.Rendering package id / template id / docs URL anywhere in
  the fixture (FR-001, SC-005). Uses the `__FIXTURE__` absolute-path token convention
  (`ScaffoldCommandTests.fs:21-24`).
- **Owner**: this repo (test data).

### 6. Leak-invariant scan (test-only, EXTENDED)

- **What**: the build-enforced guard in `ScaffoldGuardTests.fs`.
- **Components** (Decision 4):
  1. **Identifier deny-list** over `src/**/*.{fs,fsi}` and the generic-contract tests
     (existing `forbiddenTokens`, possibly extended) — fails with `"{path}: {token}"`.
  2. **Scoped lifecycle-literal scan**: the literal token `lifecycle` must not occur in the
     curated scaffold-source union (`HandlersScaffold.fs` + scaffold branches of
     `CommandSerialization.fs` / `CommandRendering.fs` / `CommandReports.fs` /
     `Cli/Rendering.fs`). Scope is the curated list of scaffold-owning source files, **not**
     the whole repo.
  3. **Planted-violation unit test**: the offender-detection function returns a non-empty,
     located list for a synthetic source string carrying (a) a rendering identifier and
     (b) a `lifecycle` literal.
- **Companion behavioral guard** (in `ScaffoldCommandTests.fs`, not the scan): value-agnosticism
  — an arbitrary `lifecycle` value forwards identically.
- **Owner**: this repo (test/guard surface — the one place allowed to name the deny-list
  tokens, per `ScaffoldGuardTests.fs:7-12`).

## Verification state map (requirement → entity → assertion site)

| Req | Entity | Assertion (test module) |
|---|---|---|
| FR-002 / US1.1 | 1 | recording-fixture manifest contains `lifecycle=sdd` (`ScaffoldCommandTests`) |
| FR-003 / SC-001 / US1.3 | 1 | dry-run create-arg `-p:` vector == overlay set (`ScaffoldCommandTests`) |
| FR-008 (order) | 1 | reversed `--param` order → identical create-arg vector (`ScaffoldCommandTests`) |
| US1.2 | 1,2 | outcome `providerSucceeded`, `ProviderInvoked = true` (`ScaffoldCommandTests`) |
| FR-004 / SC-002,003 / US2.1,2.3 | 2,4 | provenance producedPaths == app files, all `generatedProduct` (`ScaffoldCommandTests`) |
| FR-005 / SC-002 / US2.2 | 3,4 | no skeleton path in provenance; skeleton byte-identical to `init` (`ScaffoldCommandTests`) |
| FR-006 / SC-004 / US2.4 | 4 | JSON+provenance byte-identical across two runs; 3-projection produced-path parity (`ScaffoldCommandTests`, `ScaffoldParityTests`) |
| FR-007 / US3.1 | 6 | identifier deny-list scan clean (`ScaffoldGuardTests`) |
| FR-007 / US3.2 | 1,6 | scoped lifecycle-literal scan clean **and** value-agnosticism behavior (`ScaffoldGuardTests`, `ScaffoldCommandTests`) |
| FR-007 / SC-005 / US3.3 | 6 | planted-violation unit test catches + locates (`ScaffoldGuardTests`) |
| FR-008 (req-missing) | 1,5 | `lifecycle` required + omitted → `scaffold.providerParamMissing`, exit 1, no invocation (`ScaffoldCommandTests`) |
| FR-008 (empty) | 2,5 | empty fixture + `lifecycle=sdd` → `providerEmpty`/`ProviderSucceededEmpty`, exit 0 (`ScaffoldCommandTests`) |
| FR-008 (intrusion) | 3,5 | intrusion fixture + `lifecycle=sdd` → `providerWroteSddTree`, exit 2, no laundering (`ScaffoldCommandTests`) |
| SC-007 | (all) | 4× `PublicSurface.baseline` unchanged; no golden diff except new fixtures/tests |

## Non-changes (explicit)

- No new diagnostic id; no change to the 10 existing `scaffold.*` codes (`Diagnostics.fs:145-251`).
- No change to provenance schema (v1), the descriptor schema (v1), the report projections,
  or the CLI surface (FR-010 / SC-007).
- No agent-surface change (no workflow change).
