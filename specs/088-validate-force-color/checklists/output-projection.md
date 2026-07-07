# Requirements Quality Checklist: Output-Projection Surface & Color/TTY Gate

**Purpose**: Validate that the spec unambiguously and consistently specifies the force-color override and the Markdown report card before planning.
**Created**: 2026-07-07
**Feature**: [spec.md](../spec.md)
**Focus**: Color/TTY gate precedence, FORCE_COLOR semantics, format-selection precedence, contract invariants, Markdown determinism & fact-parity.

## Color/TTY Gate Precedence

- [x] CHK001 - Is the precedence between `NO_COLOR`, force-color, and capability sensing stated as a single, total ordering? [Clarity, Spec §Clarifications, §FR-003]
- [x] CHK002 - Is it unambiguous that a force-color signal overrides BOTH the non-interactive/redirected gate AND `TERM=dumb`, but NOT `NO_COLOR`? [Completeness, Spec §FR-002, §FR-003]
- [x] CHK003 - Is the `NO_COLOR`-wins behavior when both signals are present specified as a testable invariant (zero ANSI)? [Measurability, Spec §SC-002, §Edge Cases]
- [x] CHK004 - Are the two force-color sources (`FORCE_COLOR` env var and `--force-color` flag) specified as equivalent in effect? [Consistency, Spec §FR-001]
- [x] CHK005 - Is it specified which output sink the gate applies to when a report routes to stderr (Blocked) vs stdout? [Coverage, Spec §Clarifications]

## FORCE_COLOR Semantics

- [x] CHK006 - Is the boolean-ish interpretation of `FORCE_COLOR` (unset/empty/`0` = off; anything else = on) explicitly defined? [Clarity, Spec §FR-004]
- [x] CHK007 - Is the distinction between `FORCE_COLOR` semantics and `NO_COLOR`'s "present-with-any-value" semantics called out to avoid conflation? [Consistency, Spec §FR-004, §Assumptions]
- [x] CHK008 - Are the non-forcing edge values (`FORCE_COLOR=0`, empty) covered by an acceptance scenario? [Edge Case, Spec §US1 AS-5]

## Format-Selection Precedence

- [x] CHK009 - Is the full four-way precedence `--rich > --markdown > --text > --json > default` stated exactly once as the canonical order? [Clarity, Spec §FR-011]
- [x] CHK010 - Are the format flags stated to be mutually-exclusive intents (so precedence only decides multi-flag invocations)? [Consistency, Spec §US3, §Clarifications]
- [x] CHK011 - Is the default projection (no flag) explicitly pinned to JSON and byte-identical to today? [Completeness, Spec §FR-013, §SC-005]
- [x] CHK012 - Are multi-flag combinations (`--rich --markdown`, `--markdown --text`) covered by acceptance scenarios with a defined winner? [Coverage, Spec §US3 AS-1, §Edge Cases]

## Contract Invariants

- [x] CHK013 - Is it specified that force-color and `--markdown` change no JSON bytes, exit code, or stream routing? [Completeness, Spec §FR-006, §FR-013, §FR-015]
- [x] CHK014 - Is the exit-code rule (0 iff overall verdict passes, else 1) stated as independent of projection and force-color? [Clarity, Spec §FR-015]
- [x] CHK015 - Is it specified that the `validation-report` JSON contract and persisted schema do not change, and that the report card is not added to the release catalog? [Consistency, Spec §FR-014]
- [x] CHK016 - Is force-color specified to have zero effect on non-rich projections (JSON/text/Markdown stay ANSI-free)? [Coverage, Spec §FR-006, §Edge Cases]

## Markdown Report Card — Determinism & Fact-Parity

- [x] CHK017 - Is the Markdown projection's determinism (byte-identical across runs; no wall-clock/sensed/width data) stated as a testable requirement? [Measurability, Spec §FR-009, §SC-003]
- [x] CHK018 - Is zero-ANSI specified for the Markdown projection in every environment (interactive or not)? [Clarity, Spec §FR-009, §SC-003]
- [x] CHK019 - Is fact-parity with the rich projection specified (verdict, five counts, matrix rollup, every non-passing cell; passing cells summarized not enumerated)? [Completeness, Spec §FR-008, §SC-004]
- [x] CHK020 - Is it specified that the Markdown projection invents no fact absent from the report and tolerates absent optional fields (`schemaVersion`/`generatorVersion`)? [Coverage, Spec §FR-010, §Edge Cases]
- [x] CHK021 - Is the `--markdown --out <file>` persistence behavior specified (persist the deterministic Markdown; exit code unaffected)? [Completeness, Spec §FR-012, §US2 AS-4]
- [x] CHK022 - Is the empty/all-pass Markdown output specified to still be a well-formed document (never an empty file)? [Edge Case, Spec §Edge Cases, §US2 AS-1]

## Scope & Consistency

- [x] CHK023 - Is the scope of force-color (all `--rich`-capable commands) vs the Markdown card (`validate`-only) stated consistently across FRs, assumptions, and success criteria? [Consistency, Spec §FR-005, §FR-007, §Assumptions]
- [x] CHK024 - Is the uniform-gate claim backed by a measurable outcome exercising a non-`validate` command? [Measurability, Spec §SC-006]

## Notes

- All items resolved against the spec as written (post-clarify revision). No open gaps; ready for `/speckit-plan`.
