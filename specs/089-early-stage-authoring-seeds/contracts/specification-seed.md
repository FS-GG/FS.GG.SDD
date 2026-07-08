# Contract: Seeded specification story and acceptance scenario

**Feature**: 089 · **Artifact**: `work/<id>/spec.md` (authored surface) · **Stage**: `specify`

Applies **only** when the `specify` invocation supplies no `story:` / no `acceptance:` intent fact.
When the author supplies them, their text is used verbatim and this contract does not apply (FR-005).

## Line grammar (unchanged from today — only the prose after `:` moves)

```
## User Stories
- US-001 (P1): <story-sentence>

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: <given-when-then-sentence>

## Functional Requirements
- FR-001: <author requirement text> (Stories: US-001; Acceptance: AC-001)
```

The ids, the `(P1)` marker, the `[US-001] [FR-001]` references, and the
`(Stories: US-001; Acceptance: AC-001)` trailer are **byte-for-byte** as before (FR-004). Downstream
`checklist` FR→AC coverage and `plan`/`tasks` back-references resolve unchanged.

## Seed derivation

Given `userValue` (required intent fact) and `title` (this invocation's `--title`, else the
humanized work id):

```
cap   = neutralizeIds (decapitalizeFirst (trimTrailingPeriod userValue))
shown = neutralizeIds title
```

**Seeded story**

```
As a user, I can {cap}.
```

**Seeded acceptance scenario**

```
Given {shown} is available, when the user exercises it, then they can {cap}.
```

## Worked example

Invocation:

```sh
fsgg-sdd specify --work demo --input "value: Let a player keep a highlight of their match
scope: Encode the captured frame buffer to a shareable file
requirement: The exported file plays back in a standard media player"
```

Before (meta seed — the §WD7 defect):

```markdown
- US-001 (P1): As a maintainer, I can specify Demo after chartering the work item.
- AC-001 [US-001] [FR-001]: Given a chartered work item, when specify runs with intent, then spec.md is created with stable ids.
```

After (feature-shaped seed):

```markdown
- US-001 (P1): As a user, I can let a player keep a highlight of their match.
- AC-001 [US-001] [FR-001]: Given Demo is available, when the user exercises it, then they can let a player keep a highlight of their match.
```

## Rules

| # | Rule |
|---|---|
| S1 | The story matches `As a <user>, I can <capability>` and ends with a single `.` (FR-001). |
| S2 | Neither seeded sentence contains `charter`, `specify`, `spec.md`, or `stable ids`, case-insensitively (FR-002/FR-003, SC-007). |
| S3 | `decapitalizeFirst s` lowercases `s[0]` iff `s.Length > 1 && isUpper s[0] && isLower s[1]`. `"MP4 export"` is left alone. |
| S4 | `trimTrailingPeriod` removes at most one trailing `.` so the seed never ends `..`. |
| S5 | `neutralizeIds` rewrites every `[A-Z]{2,3}-\d{3,}` token by replacing its hyphen with a space, so author text cannot manufacture a cross-reference inside a seeded id line (FR-017). Applied to the seeded lines only. |
| S6 | Each seeded sentence is exactly one line — never wrapped, never truncated (spec Edge Cases). |
| S7 | Rendering is total and deterministic: the same inputs always yield byte-identical output (FR-015). |
| S8 | `specify` blocks before this contract is reached when `userValue` is absent or blank, so `cap` is never empty. |

## Non-goals

- The scope and requirement fallbacks are **dead code** (`specify` blocks first) and are not touched.
- The non-goal fallback is a genuine default, not process boilerplate, and is not touched.
- No attempt is made to read the charter's title; `specify` has no access to it (research D1).
