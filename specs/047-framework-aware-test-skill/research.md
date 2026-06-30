# Phase 0 Research: Framework-aware required test skill

All Technical Context unknowns are resolved below. Two values were
user-confirmed during planning (config field location, neutral token); the
remainder are grounded in the existing codebase.

## Current state (where the defect lives)

- `src/FS.GG.SDD.Commands/CommandWorkflow/ParsingTasks.fs:244` — `obligationTasks`
  passes `[ "xunit"; "readiness-evidence" ]` as the required skills for every
  generated verification-obligation task. This is the **only** source occurrence
  of the hard-coded framework token.
- All seven task-category skill lists are co-located in `plannedTasks`
  (`ParsingTasks.fs` lines 204, 222, 233, 244, 255, 266, 281). Skills land in the
  record via `plannedTask` (`RequiredSkills = skills |> List.distinct |> List.sort`).
- `RequiredSkills` is `string list` on `WorkTask` (`Task.fs:58` / `Task.fsi:54`)
  and on the serialized `TaskEntry` (`WorkModel.fs:59` / `WorkModel.fsi:55`), JSON
  field `requiredSkills` (camelCase), sorted on projection and read-back.
- Verify: `evidence.missingRequiredSkill` (`CommandReports.fs:607`) is emitted by
  `verifySkillViews` (`HandlersVerify.fs:181-211`), which groups tasks by their
  `RequiredSkills` string and marks a skill "missing" when any requiring task has
  blocking evidence state. **Satisfaction is mediated through task-linked evidence
  dispositions, not by name-matching an author-supplied skill string.**

## Decision 1 — Where the test framework is declared

**Decision**: optional scalar `project.testFramework` in `.fsgg/project.yml`.

**Rationale**: `.fsgg/` is the SDD-owned config slot (ADR-0005); `project.yml`
already carries product identity (`project.id`, `project.defaultWorkRoot`) parsed
onto `ProjectLifecycleConfig` (`Config.fs:17-25`). The test framework is a fact
*about the product*, so it belongs in the `project:` block. The field is additive
and optional, so existing files remain valid and `schemaVersion` stays `1`.
**User-confirmed.**

**Alternatives considered**:
- `sdd.testFramework` (under the SDD config block) — rejected: that block holds
  SDD path config, not product metadata.
- `.fsgg/sdd.yml` policy file — rejected: heavier, and the value is product
  identity, not lifecycle policy.
- Auto-detecting the framework by scanning the product's `.fsproj`/test
  package references — rejected: brittle, non-deterministic across environments,
  and pulls framework-detection heuristics into generic SDD. An explicit declared
  signal is deterministic and provider-agnostic.

## Decision 2 — Framework-neutral token (no declaration)

**Decision**: `automated-tests`.

**Rationale**: Matches the spec's own example ("a generic automated tests
capability tag") and the existing kebab-case skill vocabulary
(`readiness-evidence`, `schema-versioning`, `deterministic-json`). Names no
framework, so it can never reintroduce the #42 defect. **User-confirmed.**

**Alternatives considered**: `tests` (less descriptive), `test-evidence`
(overlaps with the co-emitted `readiness-evidence` skill and ties to evidence
rather than the test capability).

## Decision 3 — Deriving the skill from a declared framework

**Decision**: `resolveTestSkill : string option -> string`:
- `Some raw` where `raw` is non-blank → `normalize raw` =
  `raw.Trim().ToLowerInvariant()` with internal whitespace runs collapsed to a
  single `-` (slugify). E.g. `Expecto` → `expecto`, `NUnit` → `nunit`,
  `My Custom Runner` → `my-custom-runner`.
- `None` or blank/whitespace → `automated-tests`.

**Rationale**: FR-002/FR-003 require a token corresponding to the declared
framework, or the neutral token when absent. Normalization gives stable,
deterministic tokens that match the lowercase skill convention. SDD trusts
unrecognized/custom values (edge case in spec) — it keeps **no closed list** of
approved frameworks, so no validation/allow-list is introduced (FR-007: no
framework-specific knowledge baked into generic SDD).

**Alternatives considered**:
- A prefixed token like `test-<framework>` — rejected: changes the shape of the
  current `xunit` token (bare framework name) and adds needless ceremony.
- Validating against a known-framework set — rejected: violates the generic
  requirement and the spec's explicit "no closed list" edge case.

## Decision 4 — Threading the signal into generation

**Decision**: extract `TestFramework` in `computeTasksPlan` (`HandlersEarly.fs:185`,
which already parses `project.yml` via `projectDiagnostics`), resolve the skill
with `resolveTestSkill`, and pass that single resolved string down through
`tasksDiagnosticsTextAndSummary` (`ParsingTasks.fs:546`) into `plannedTasks`, where
`obligationTasks` uses `[ resolvedTestSkill; "readiness-evidence" ]`.

**Rationale**: The `.fsgg/project.yml` read effect already exists in
`tasksReadEffects` (`Foundation.fs:275-287`); no new I/O edge is needed, so the
MVU boundary (constitution V) is preserved. Passing the resolved string (rather
than the raw option) keeps `plannedTasks` pure and free of config types.

**Alternatives considered**: routing through `ParsedWorkItem.Project`
(`WorkItem.fs:196`) — rejected for this seam because task generation reads the
project snapshot directly in `computeTasksPlan`; reusing that parse is the
smaller change.

## Decision 5 — `init` authored template unchanged

**Decision**: do **not** add `testFramework` to `projectConfigText`
(`Foundation.fs:34-42`). `init`/`scaffold` keep emitting a `project.yml` with no
framework declaration.

**Rationale**: Hard-coding a framework in the generic skeleton would either
reintroduce the #42 defect (assuming a framework) or embed provider/rendering
knowledge in generic SDD (FR-007). Default = undeclared ⇒ neutral skill, which is
the safe behavior. Declaring the framework is the author's (or the external
provider's, e.g. FS.GG.Rendering's scaffold template) responsibility. Keeping the
template unchanged also leaves `init` output byte-identical (no init-golden churn).

## Decision 6 — Schema version & migration posture

**Decision**: stay at `schemaVersion: 1`; no migration. Absent field ⇒ neutral
skill on the next generation run. Pre-existing generated work-models are not
rewritten outside a generation/refresh run (spec edge case).

**Rationale**: The field is purely additive and optional — older `project.yml`
files (no `testFramework`) parse unchanged and resolve to the neutral skill,
which is exactly the desired safe default. No backward-incompatible change → no
version bump.

## Decision 7 — Governance compatibility

**Decision**: no cross-repo contract change. `requiredSkills` remains an opaque
`string list`; only a data value changes. No version bump to a shared contract is
required.

**Rationale**: Governance consumes `requiredSkills` as opaque tags; changing the
emitted value is data, not a contract surface change. (If Governance later keys
gates on a specific `xunit` literal, that is a Governance-side concern surfaced
via the cross-repo coordination protocol — out of scope here.)

## Determinism note (FR-006 / SC-005)

`resolveTestSkill` is a pure, total function of the declared value; `plannedTask`
already sorts and dedups `requiredSkills`. For fixed inputs and a fixed declared
framework the emitted token and ordering are stable, so re-running generation
remains byte-identical. Golden fixtures are updated to the new expected values as
intended churn, not a regression.
