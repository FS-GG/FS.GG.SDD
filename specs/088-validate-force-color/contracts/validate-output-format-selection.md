# Contract: `validate` Output-Format Selection (4-way)

**Applies to**: `fsgg-sdd validate` only. Other commands keep the 3-way `selectFormat` (`--rich > --text > --json > default`), unchanged.

## Selector

`Rendering.selectValidationFormat : string list -> ValidationFormat`

```
--rich      present → Standard Rich
--markdown  present → MarkdownCard
--text      present → Standard Text
--json      present → Standard Json
(none)              → Standard Json      // default
```

Precedence (highest → lowest): `--rich > --markdown > --text > --json > default(Json)`. Flags are mutually-exclusive intents; precedence decides the winner only when more than one is passed.

## Routing in `printValidate`

| Selection | stdout rendering | `--out` persisted projection |
|-----------|------------------|------------------------------|
| `Standard Json` | `serialize report` | `serialize report` |
| `Standard Text` | `renderText report` | `renderText report` |
| `Standard Rich` | `resolveValidation Rich caps report` (degrades to `renderText`) | `renderText report` (rich never persisted) |
| `MarkdownCard` | `renderMarkdown report` | `renderMarkdown report` |

## Invariants

- **Default unchanged**: no format flag → byte-identical to today's default JSON (`serialize`).
- **`--json` / `--text` unchanged**: byte-identical to `serialize` / `renderText`.
- **Exit code**: `0` iff `report.Summary.OverallPassed`, else `1` — independent of the selected projection and of force-color. A `--out` write failure is exit `1` with a stderr diagnostic (unchanged).
- **Stream routing**: the report renders to stdout for all four selections; `--out` is a side write. Force-color does not change routing.
- **Markdown determinism**: `renderMarkdown` output is byte-identical across runs and free of ANSI (see `markdown-report-card.md`).

## Acceptance mapping

- `--markdown --text --json` → `MarkdownCard` (spec US3 AS-1).
- `--rich --markdown` → `Standard Rich` (spec Edge Cases).
- `--markdown --out f` → persists Markdown; exit on verdict only (spec US2 AS-4 / FR-012).
