# Contract: Report projections for the post-instantiation steps

The new step outcomes appear in all three projections of the one `CommandReport`, selected by
flag precedence `--rich` > `--text` > `--json` > default (CLAUDE.md). FR-011 / SC-006.

## JSON (default / `--json`) — the automation contract

`CommandSerialization.writeScaffold` (`CommandSerialization.fs:291-311`) gains, inside the
`scaffold` object, additive keys:

```jsonc
"scaffold": {
  "providerName": "...",
  "outcome": "providerSucceeded",
  "skeletonCreated": true,
  "providerInvoked": true,
  "producedPathCount": 3,
  "producedPaths": ["...", "..."],
  "repoInitOutcome": "initialized",          // NEW — closed vocabulary (data-model §3)
  "executableScriptCount": 1,                 // NEW
  "executableScriptsSkipped": 0,              // NEW
  "nextActionHint": "..."
}
```

- Additive only: existing keys/bytes unchanged (FR-012). No `SchemaVersion` bump (additive
  optional facts within `ReportVersion` policy; confirmed against the report contract).
- Deterministic within a fixed environment; `repoInitOutcome` is **sensed metadata**
  (research Decision 5) — goldens fix the environment.

## `--text`

`CommandRendering.renderText` (`CommandRendering.fs:196-209`) gains lines:

```text
scaffoldRepoInit: initialized
scaffoldExecutableScripts: 1
scaffoldExecutableScriptsSkipped: 0
```

(Emitted for every scaffold run; values `notApplicable` / `0` on the not-run/failed/dry-run
paths.)

## `--rich`

The rich view reuses the plain-text projection verbatim (`Cli/Rendering.fs:74` →
`CommandRendering.renderText`, split into lines). The new facts therefore appear in rich
**automatically**, adding/dropping no fact and changing **no** JSON byte. Rich is excluded
from deterministic/golden contracts.

## Parity guarantee (SC-006)

For every run, a reader of any single projection can determine which post-instantiation steps
ran, which were skipped, and why — without inspecting the filesystem. `ScaffoldParityTests.fs`
asserts the same repo-init/exec facts are present and equal across JSON, text, and rich.
</content>
