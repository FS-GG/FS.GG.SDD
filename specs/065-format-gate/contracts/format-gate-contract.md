# Contract: Format gate (feature 065)

Refines feature 064 contract **C3** into this feature's standalone deliverable.
This feature introduces **no** CLI/JSON/schema contract; the "contract" here is
the repo-config + CI behavioural contract the gate must satisfy.

## FG-1 — `.editorconfig` is the Fantomas config (FR-001)

- A repo-root `.editorconfig` MUST exist with `root = true`, general whitespace
  keys, and a `[*.fs]`/`[*.fsi]` section carrying the `fsharp_*` house-style
  keys.
- There MUST be **no** separate Fantomas configuration file (no `fantomas.json`).

## FG-2 — Pinned, out-of-manifest Fantomas check (FR-002 / FR-003)

- CI MUST run `fantomas --check` over the tracked F# tree using Fantomas pinned
  to `7.0.5`.
- The pinned Fantomas MUST be installed **without** modifying
  `.config/dotnet-tools.json`, which MUST remain byte-identical to the
  `FS-GG/.github` org source. The `build-config-drift` gate MUST stay green.

## FG-3 — Reject a non-clean tree, name the fix (FR-004)

- A tree that is not fantomas-clean MUST make the check exit non-zero.
- The failure output MUST name the reformat command (`fantomas <paths>`).
- A fantomas-clean tree MUST make the check exit `0`.

## FG-4 — Non-required job (FR-005)

- The `format` job MUST NOT be in the branch-protection required-checks set; a
  red `format` job MUST NOT block merge.

## FG-5 — One-time reformat is layout-only (FR-006)

- After the one-time reformat, the full test suite MUST be green.
- Every deterministic/golden JSON baseline MUST be byte-identical.
- Every `.fsi` public-surface baseline MUST be byte-identical.
- `fsgg-sdd validate` MUST stay `overallPassed`.

## FG-6 — Documented, reproducible locally (FR-007)

- The pinned-Fantomas install/run/fix commands used by CI MUST be documented for
  contributors, verbatim, so a local verdict matches CI.

## Verification map

| Contract | Verified by (quickstart) | Success criterion |
|---|---|---|
| FG-1 | inspect `.editorconfig` | — |
| FG-2 | run gate; run `build-config-drift` | SC-003 |
| FG-3 | negative check: mangle a file, run `--check` | SC-001 |
| FG-4 | branch-protection config / job not required | FR-005 |
| FG-5 | full suite + baseline diff + `validate` | SC-002 |
| FG-6 | follow documented command locally | SC-004 / SC-005 |
