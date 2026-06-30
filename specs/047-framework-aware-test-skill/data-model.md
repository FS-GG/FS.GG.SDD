# Phase 1 Data Model: Framework-aware required test skill

This change touches one config entity, one pure derivation, and one generated
task category. No new persisted entity is introduced.

## Entity: `ProjectLifecycleConfig` (modified)

Source: `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Config.fs` /
`Config.fsi`. Backing file: `.fsgg/project.yml` (`schemaVersion: 1`).

| Field | Type | Source key | Required | Notes |
|-------|------|------------|----------|-------|
| `SchemaVersion` | `SchemaVersion` | `schemaVersion` | yes | unchanged; stays `1` |
| `ProjectId` | `string` | `project.id` | yes | unchanged |
| `DefaultWorkRoot` | `string` | `project.defaultWorkRoot` | yes | unchanged |
| `SddConfigPath` | `string` | `sdd.config` | yes | unchanged |
| `AgentsConfigPath` | `string` | `sdd.agents` | yes | unchanged |
| `GovernancePolicyPath` | `string option` | `governance.policy` | no | unchanged |
| `GovernanceCapabilitiesPath` | `string option` | `governance.capabilities` | no | unchanged |
| `GovernanceToolingPath` | `string option` | `governance.tooling` | no | unchanged |
| **`TestFramework`** | **`string option`** | **`project.testFramework`** | **no** | **new, additive** |

**Validation rules**:
- Optional scalar. Absent ⇒ `None`. Present but blank/whitespace ⇒ treated as
  `None` (neutral skill; see resolver).
- No allow-list/validation of the value — any non-blank string is accepted
  (SDD keeps no closed framework list).
- Parser: `parseProjectConfig` reads the optional scalar via the existing
  `Internal.fs` optional-scalar helper (e.g. `tryScalarAt [ "project"; "testFramework" ]`).
  No new diagnostic codes; malformed-config diagnostics are unchanged.

## Derivation: `resolveTestSkill`

Pure, total function inside the internal `ParsingTasks` module (no public surface).

```text
neutralTestSkill = "automated-tests"

resolveTestSkill : string option -> string
  Some raw, raw non-blank  ->  normalize raw
  None | blank             ->  neutralTestSkill

normalize raw = raw.Trim() |> invariant-culture lowercase |> collapse internal whitespace runs to "-"
```

Examples:

| Declared `project.testFramework` | Resolved test skill |
|----------------------------------|---------------------|
| `expecto` | `expecto` |
| `Expecto` | `expecto` |
| `NUnit` | `nunit` |
| `My Custom Runner` | `my-custom-runner` |
| (absent) | `automated-tests` |
| `""` / `"   "` | `automated-tests` |

The result is never `xunit` unless the author explicitly declares `xunit`
(FR-004). Lowercasing uses the invariant culture (`String.ToLowerInvariant`),
not the ambient locale, so normalization is byte-stable across environments
(FR-006).

## Entity: Verification-obligation task (generated, modified)

Source seam: `obligationTasks` in `plannedTasks` (`ParsingTasks.fs`). Serialized
into `readiness/<id>/work-model.json` and the tasks artifact as `TaskEntry`
(`WorkModel.fs:50-62`).

- **Before**: `RequiredSkills = [ "xunit"; "readiness-evidence" ]` (sorted).
- **After**: `RequiredSkills = [ resolveTestSkill declared; "readiness-evidence" ]`,
  then `List.distinct |> List.sort` (existing `plannedTask` behavior).
  - declared `expecto` ⇒ `[ "expecto"; "readiness-evidence" ]`
  - undeclared ⇒ `[ "automated-tests"; "readiness-evidence" ]`

`readiness-evidence` is an SDD-process skill, **not** framework-specific, so it is
retained unchanged (FR-005).

## Invariants (unchanged task categories — FR-005 / SC-004)

The other six categories keep their exact skill lists:

| Category | `requiredSkills` |
|----------|------------------|
| Requirement (implementation) | `[ "fsharp"; "speckit-implement" ]` |
| Plan decision (implementation) | `[ "fsharp"; "speckit-implement" ]` |
| Contract | `[ "fsharp" ]` |
| Migration | `[ "schema-versioning" ]` |
| Generated-view | `[ "deterministic-json" ]` |
| Deferral | `[ "traceability" ]` |

## Verify obligation re-keying (FR-008)

`evidence.missingRequiredSkill` and `verifySkillViews` need **no code change**.
They group by each task's `RequiredSkills` string, so the obligation key becomes
the resolved token automatically. An author whose evidence covers the
verification-obligation task satisfies the obligation regardless of the token's
text; the token is now meaningful (matches the product's framework or is neutral)
instead of misleading.
