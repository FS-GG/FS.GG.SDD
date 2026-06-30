# Contract: Help report (§3.5 / FR-008–011)

**Surface**: `fsgg-sdd --help` / `-h` / `help`, and `fsgg-sdd <command> --help` /
`-h`, projected through the existing `CommandReport` three ways.

## Types (additive)

- `HelpFlag = { Name; Argument: string option; Description }`
- `HelpCommandEntry = { Name; Description }`
- `HelpScope = TopLevel | Command of string`
- `HelpSummary = { Scope; Usage; Commands: HelpCommandEntry list; GlobalFlags: HelpFlag list; CommandFlags: HelpFlag list }`
- `CommandReport.Help: HelpSummary option` — additive jsonField `help`, serialized
  object-when-`Some` / `null`-when-`None` (the `writeScaffold` convention), always
  present. `schemaVersion` stays `1`; `stability: additiveOptional`.

New module `CommandHelp` (`FS.GG.SDD.Commands`) provides the static flag/description
table and `topLevelHelp` / `commandHelp` builders.

## Behavior

| Invocation | Output | Exit |
|---|---|---|
| `fsgg-sdd --help` / `-h` / `help` | top-level usage: command list + global flags (`HelpScope = TopLevel`) | 0 |
| `fsgg-sdd <known> --help` / `-h` | that command's flag listing (`HelpScope = Command name`) | 0 |
| `fsgg-sdd --help --json` (or `--text`/`--rich`) | top-level help in the selected projection | 0 |
| `fsgg-sdd <unknown>` (± `--help`) | `unknownCommand` diagnostic | 1 |

- Help reports route to **stdout** (outcome is not `Blocked`); they carry no error
  diagnostics and no changed artifacts → `outcome = NoChange` → exit 0.
- The command list MUST include the 14 lifecycle/cross-cutting `SddCommand` names
  **and** the CLI-level peers `version`, `validate`, `registry`.
- `--help` is never reported as `unknownCommand` (FR-008/FR-011); a genuinely
  unknown command is never masked by `--help` (FR-011).

## Projection guarantees (FR-010)

- Default/`--json`: deterministic canonical JSON carrying the `help` object;
  byte-identical across runs (FR-012).
- `--text`: portable plain-text help (usage, commands, flags) projected from the
  same report.
- `--rich`: Spectre projection derived from the text projection (adds/drops no
  facts); degrades to zero-ANSI plain text when non-interactive, redirected,
  `NO_COLOR`, or `TERM=dumb`.

## Catalog / docs

- `docs/release/release-readiness.json`: add `help` to the `command-report (--json)`
  inventory (`jsonField`, `additiveOptional`).
- `docs/release/schema-reference.md`: document the `help` field so the conformance
  test agrees.

## Test obligations

- Top-level `--help`, `-h`, `help` → usage + command list + global flags, exit 0,
  zero `unknownCommand`.
- `<command> --help` for every command → that command's flags, exit 0.
- `--help --json` / `--text` / `--rich` → correct projection; rich degrades to text
  under non-interactive/`NO_COLOR`.
- `frobnicate` and `frobnicate --help` → `unknownCommand`, exit 1.
- Determinism: byte-identical JSON/text across runs.
