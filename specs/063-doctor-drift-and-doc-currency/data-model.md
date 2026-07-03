# Data Model: Surface skillDriftPaths + correct stale report/doc surfaces

No new types, no schema changes. This feature renders and corrects existing data.

## Existing types (unchanged)

- **`DoctorSummary`** (`CommandTypes.fs:~355`) — already has
  `MissingArtifactPaths: string list` and `SkillDriftPaths: string list`.
- **`UpgradeSummary`** (`CommandTypes.fs:~370`) — likewise has `SkillDriftPaths:
  string list`.

Both are already serialized (`CommandSerialization.fs:394,415` write
`skillDriftPaths`). This feature adds their **text rendering** only.

## Render shape (text projection, mirrors `missingArtifacts`)

In `CommandRendering.renderText`, `doctor` block (after the missing-artifacts
lines) and `upgrade` block:

```
doctorSkillDrifts: <count>
doctorSkillDrift: <path>        (one per path, sorted; omitted when count = 0)
upgradeSkillDrifts: <count>
upgradeSkillDrift: <path>       (one per path, sorted; omitted when count = 0)
```

The rich projection inherits these automatically (`Cli/Rendering.fs:117` renders
`renderText` lines into its details table).

## Corrected static content

| Site | Before | After |
|------|--------|-------|
| `DiagnosticConstructors.unknownCommand` correction | 11 commands (init…ship) | 18 commands (…+ agents, refresh, scaffold, doctor, upgrade, validate, registry) |
| `NextActionRouting` reseed `NextAction` affected paths | `.claude/skills`, `.codex/skills`, `.fsgg/early-stage-guidance.md` | + `.agents/skills` (sorted) |
| `ReportAssembly` `projectRoot` | `"."` (no comment) | `"."` (+ rationale comment; no value change) |

## Invariants

- `doctor`/`upgrade` **JSON** byte-identical before/after (FR-004).
- `skillDrift` render mirrors `missingArtifacts` exactly (count line always
  emitted; per-path lines sorted; none when empty).
- `unknownCommand` correction contains every command token the CLI accepts;
  pinned by a test.
