# Contract: lint report JSON projection + grammar-pointer map

The lint result is a `CommandReport` carrying a `LintSummary`. The default/`--json` projection is
the automation contract (golden-tested, deterministic). `--text`/`--rich` add/drop no facts.

## JSON shape (illustrative — golden-locked in tests)

```json
{
  "command": "lint",
  "outcome": "Blocked",
  "lint": {
    "artifactPath": "work/x/clarifications.md",
    "kind": "clarification",
    "outcome": "DefectsFound",
    "defects": [
      {
        "class": "MissingDecisionTag",
        "id": "unresolvedBlockingAmbiguity",
        "severity": "Error",
        "location": { "line": 42, "column": 1 },
        "message": "...",
        "correction": "Resolve AMB-001 with a DEC-### decision line carrying its AMB id.",
        "grammarPointer": {
          "doc": "docs/reference/authoring-contracts.md",
          "anchor": "clarify-decision-tag-resolution",
          "exampleTag": "clarify-decision:resolved"
        }
      }
    ]
  },
  "diagnostics": [ /* same defects mirrored as CommandReport.Diagnostics */ ],
  "nextAction": { /* fix-guidance when defects; omitted/None when clean */ }
}
```

- `lint.outcome ∈ {Clean, DefectsFound, UnusableInput}` maps to exit `0/1/2`.
- `defects` is ordered `(location.line, location.column, id)` — stable (FR-012).
- `defects[].correction` is the parser's existing fix hint (FR-007a); never empty for the four
  grammar classes.
- `grammarPointer` is present for every `{CoverageLine, MissingDecisionTag, FrontMatter,
  DuplicateId}` defect (FR-007b / SC-003); absent for `Parse`/`Unresolvable`.
- On a clean artifact: `lint.outcome = "Clean"`, `defects = []`, top-level `outcome = "NoChange"`
  or `"Succeeded"`, exit 0.
- On unusable input: `lint.outcome = "UnusableInput"`, one `Unresolvable`/`Parse` defect, exit 2.

## Grammar-pointer map (FR-007 — new, drift-guarded)

Pure lookup `LintDefectClass -> GrammarPointer`, resolving into the feature-046 grammar-of-record:

| Class | anchor (heading slug in `authoring-contracts.md`) | exampleTag (tagged fence) |
|---|---|---|
| `CoverageLine` | `acceptance-coverage-line` | `coverage:accepted` |
| `MissingDecisionTag` | `clarify-decision-tag-resolution` | `clarify-decision:resolved` |
| `FrontMatter` | `per-stage-front-matter` | (none) |
| `DuplicateId` | `stable-id-declarations` | (none — id-declaration rule) |

**Drift guard** (`LintGrammarPointerTests`): every `anchor` MUST match an existing heading in
`docs/reference/authoring-contracts.md`, and every non-null `exampleTag` MUST match a tagged fenced
block there (mirroring `AuthoringDocsContractTests` block extraction). The build fails if a pointer
rots — the pointer map cannot silently diverge from the doc.

## Invariants (contract tests)

1. Clean canonical `docs/examples/lifecycle-artifacts/*` ⇒ `defects = []`, exit 0 (FR-013/SC-002).
2. The combined broken fixture ⇒ all four classes present, each with `correction` + `grammarPointer`
   (SC-001 4/4, SC-003).
3. Identical bytes ⇒ byte-identical JSON (SC-005).
4. Every `defect.severity = "Error"` (FR-017).
5. `--text`/`--rich` carry the same defect set as `--json` (no fact drift); `--rich` degrades to
   zero-ANSI when non-interactive.
