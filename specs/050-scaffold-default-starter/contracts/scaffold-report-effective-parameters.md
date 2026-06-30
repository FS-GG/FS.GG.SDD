# Contract: Scaffold report — `effectiveParameters` (json / text / rich)

**Surface**: `CommandReport.Scaffold` (`ScaffoldSummary`) · **Owner**: SDD · **Change**: additive.

## JSON (automation contract)

`writeScaffold` (`CommandSerialization.fs:291-314`) emits, **after** `producedPaths`:

```jsonc
"scaffold": {
  "providerName": "...",
  "providerContractVersion": "...",
  "outcome": "...",
  "skeletonCreated": true,
  "providerInvoked": true,
  "producedPathCount": 2,
  "producedPaths": [ "..." ],
  "effectiveParameters": [                 // NEW — sorted by key, always present
    { "key": "productName", "value": "Demo" },
    { "key": "variant",     "value": "alpha" }
  ],
  "repoInitOutcome": "...",
  "executableScriptCount": 0,
  "executableScriptsSkipped": 0,
  "nextActionHint": "..."
}
```

- No other key, key order, stream, or exit code changes. No non-`scaffold` command output changes
  (FR-008 scoped guarantee).
- Empty map ⇒ `[]`.

## Text (`--text`) projection

`CommandRendering.fs:196-213` appends, after the `scaffoldProducedPath:` lines:

```
scaffoldEffectiveParam: productName=Demo
scaffoldEffectiveParam: variant=alpha
```

one line per entry, sorted by key. Omitted entirely when the map is empty (no header line).

## Rich (`--rich`) projection

Reuses the plain key/value lines (presentation only). Excluded from deterministic/golden
contracts; degrades to zero-ANSI when non-interactive / `NO_COLOR` / `TERM=dumb`.

## Verification

- JSON golden updated for a scaffold run with a declared default applied (Story 1) and for an
  explicit override (Story 2).
- Text projection golden updated correspondingly.
- Non-scaffold command goldens asserted byte-identical (FR-008).
