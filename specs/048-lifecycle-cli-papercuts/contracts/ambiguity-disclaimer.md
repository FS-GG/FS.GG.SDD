# Contract: Ambiguity "no outstanding" disclaimer (§3.3 / FR-003, FR-004)

**Surface**: parsing of the `## Ambiguities` section of `spec.md`
(`Specification.fs` `missingIdDiagnostics` `:84-102` and id extraction `:176`),
consumed by `clarify`. No type-signature change.

## Rule

A line inside `## Ambiguities` is a **no-outstanding sentinel** iff, after
stripping an optional leading bullet marker (`- `), its trimmed text is empty or
matches the disclaimer convention already used by `parseNonEmptySectionLines`
(`Internal.fs:211-218`): case-insensitive `StartsWith "No "`, or an explicit
"none outstanding" phrasing (e.g. `None outstanding`, `None`, `No open questions`,
`No material ambiguities …`).

- A **sentinel** line is exempt from the "every bullet needs an `AMB-###` id" rule
  (`missingIdDiagnostics`) and yields **no** `AmbiguityId`.
- A line bearing an `AMB-###` token yields a real `AmbiguityId` (unchanged).
- A non-sentinel bullet without an `AMB-###` id still produces
  `missingSpecificationId` (unchanged).

## Guarantees

- `## Ambiguities` containing only sentinels ⇒ `AmbiguityIds = []` ⇒ `clarify`
  proceeds with no blocking ambiguity (FR-003, SC-003), whether the disclaimer is
  prose or a bullet.
- Genuine ambiguities still detected and blocked: an `AMB-###` bullet still parses,
  `clarify` synthesizes its blocking question, `BlockingAmbiguityCount > 0` still
  blocks (FR-004).
- Mixed content: a sentinel alongside a real `AMB-###` bullet blocks on the real
  one; only the sentinel is ignored (Edge Case).

## Test obligations

- Bullet disclaimer (`- None outstanding`) under `## Ambiguities` → `AmbiguityIds`
  empty (artifact test) and `clarify` succeeds (command test).
- Prose disclaimer (existing) → still non-blocking (regression).
- Genuine `- AMB-001 …` → still blocks.
- Mixed (disclaimer + `- AMB-001 …`) → blocks on AMB-001 only.
