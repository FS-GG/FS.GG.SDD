# Contract: Markdown Report Card projection

`FS.GG.SDD.Validation.ValidationContracts.renderMarkdown : ValidationReport -> string`

A deterministic, ANSI-free Markdown projection of the `validation-report`. Fact-parity with the `--rich` projection (`renderValidationRichTo`): same verdict, same five counts, same per-matrix rollup, same non-passing cells; passing cells summarized, never enumerated.

## Document shape (normative)

```markdown
# Validation Report

**Verdict:** passed            # or "not passed" (mirrors Summary.OverallPassed and the exit rule)

## Summary

| passed | failed | skipped | coverageGaps | notValidated |
| --- | --- | --- | --- | --- |
| <p> | <f> | <s> | <cg> | <nv> |

## Matrices

| matrix | pass | fail | skipped | coverageGap | notValidated |
| --- | --- | --- | --- | --- | --- |
| <name> | <p> | <f> | <s> | <cg> | <nv> |
| …one row per matrix, sorted by name… |

## Non-passing cells

### <matrix name> (dimensions: <d1>, <d2>)

- (<dim>=<val>, <dim>=<val>) **fail**: <diagnostic message>
- (<dim>=<val>, …) **coverageGap**: <surface>
- (<dim>=<val>, …) **notValidated**: <reason>
- (<dim>=<val>, …) **skipped**: <reason>

### <matrix with no non-passing cells> (dimensions: …)

All evaluated cells pass.
```

## Rules

1. **Determinism**: matrices sorted by `Name`; within a matrix, non-passing cells sorted by their coordinate text (`dim=val, dim=val`, join order = declared coordinate order). No wall-clock, no `Sensed`, no width, no ANSI escape (`0x1B`) — byte-identical across runs.
2. **Status tokens**: `pass` / `fail` / `skipped` / `coverageGap` / `notValidated` — the exact tokens `cellStatusToken` uses, so the Markdown and rich projections name statuses identically.
3. **Detail text**: for `fail` the diagnostic `Message`; for `skipped`/`notValidated` the reason; for `coverageGap` the surface. Empty detail → omit the `: …` suffix.
4. **Passing cells**: never enumerated. A matrix whose cells all pass renders `All evaluated cells pass.` under its subheading.
5. **Optional fields**: `schemaVersion`/`generatorVersion` are not surfaced (parity with rich); their absence is not a completeness failure.
6. **Escaping**: any `|` in a value that appears inside a Markdown table cell (matrix name in the rollup) is escaped `\|` so the table cannot break. Cell detail lives in list items (not tables), reducing table fragility.
7. **Well-formedness**: even for an all-pass or empty-matrix report the output is a complete document (title + verdict + summary + matrices + a non-passing section stating all pass) — never an empty string.

## Example

Report: `baseline-conformance` all pass; `determinism` has one failing cell.

```markdown
# Validation Report

**Verdict:** not passed

## Summary

| passed | failed | skipped | coverageGaps | notValidated |
| --- | --- | --- | --- | --- |
| 3 | 1 | 0 | 0 | 0 |

## Matrices

| matrix | pass | fail | skipped | coverageGap | notValidated |
| --- | --- | --- | --- | --- | --- |
| baseline-conformance | 2 | 0 | 0 | 0 | 0 |
| determinism | 1 | 1 | 0 | 0 | 0 |

## Non-passing cells

### baseline-conformance (dimensions: command, artifact)

All evaluated cells pass.

### determinism (dimensions: command, run)

- (command=validate, run=second) **fail**: output differed between runs
```
