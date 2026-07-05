# Contract: Remediation pointer suffix

The stable, deterministic format of the pointer appended to a covered diagnostic's `Correction`.
This is the contract the guard test and the golden fixtures pin.

## Rendering rules

Let the covered id's registry entry be `{ Example = e; Anchor = a }`.

- **Both present** (`e = Some ex`, `a = Some an`):
  `See the shipped example <ex> and the grammar at <an>.`
- **Example only** (`a = None`):
  `See the shipped example <ex>.`
- **Anchor only** (`e = None`):
  `See the grammar at <an>.`
- **Neither**: not permitted (guard fails; every covered id has ≥1 target).

Where `<ex>` is the repo-relative POSIX example path
(`docs/examples/lifecycle-artifacts/<file>`) and `<an>` is the full
`docs/reference/authoring-contracts.md#<slug>` reference.

## Composition with the existing correction

The suffix is appended to the existing correction with a single separating space; the existing
correction is otherwise byte-unchanged. Example (`missingClarificationAnswer`):

- **Before**: `Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity.`
- **After**: `Provide an answer, accepted deferral, or explicit still-open note for each blocking ambiguity. See the shipped example docs/examples/lifecycle-artifacts/clarifications.md and the grammar at docs/reference/authoring-contracts.md#clarify-decision-tag-resolution.`

## Invariants (guard-enforced — FR-006 / US3)

1. **Coverage**: every id in `RemediationPointers.registry` renders a non-empty suffix.
2. **Example resolves**: every cited `<ex>` exists as a file on disk under the repo root.
3. **Anchor resolves**: every cited `<slug>` is in the set of GitHub slugs computed from the live
   `##`/`###` headings of `docs/reference/authoring-contracts.md`.
4. **Determinism (FR-007)**: the suffix contains no timestamp, absolute path, machine name, or
   other environment-dependent content; identical inputs render byte-identical output on every OS.
5. **Containment for covered diagnostics**: for each covered id, the constructed diagnostic's
   `Correction` ends with the rendered suffix.
6. **Non-interference (FR-008)**: for every diagnostic id **not** in the registry, the constructed
   `Correction` is byte-identical to its pre-feature text (no accidental suffix).

## Projection invariants (FR-003 / FR-009 / SC-005)

- The pointer is carried only in `Correction`; the diagnostic JSON has no new keys.
- `--text` and `--rich` render the same `Correction` string (rich adds styling only, no new facts).
- `fsgg-sdd lint` / `--explain` surface the identical correction (no special-casing).
- The only JSON byte deltas versus pre-feature output are the `correction` values of covered
  diagnostics; non-covered diagnostics' JSON is unchanged.

## Non-goals

- No new `Diagnostic` field, output stream, exit code, or persisted-schema version.
- Corrections outside the covered set are not required to carry a pointer and are not modified.
