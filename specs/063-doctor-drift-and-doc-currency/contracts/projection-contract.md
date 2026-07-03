# Contract: projection changes (what changes, what must not)

## A. Changed (deliberately — each a reviewed golden update, FR-012)

| # | Surface | Change |
|---|---------|--------|
| A1 | `doctor` **text** projection | gains `doctorSkillDrifts: N` + `doctorSkillDrift: <path>` (sorted; count always emitted, paths only when non-empty) |
| A2 | `upgrade` **text** projection | gains `upgradeSkillDrifts: N` + `upgradeSkillDrift: <path>` |
| A3 | `doctor`/`upgrade` **rich** projection | inherits A1/A2 via the text→details-table derivation (excluded from byte-golden) |
| A4 | `unknownCommand` **correction** string | enumerates all 18 accepted commands |
| A5 | reseed **NextAction** affected paths | gains `.agents/skills` |

## B. Held invariant (must NOT change)

| # | Invariant | Why |
|---|-----------|-----|
| B1 | `doctor`/`upgrade` **JSON** bytes | serializer untouched; drift already serialized (FR-004/SC-002) |
| B2 | `projectRoot` value (`"."`) | intentional determinism (FR-007) |
| B3 | Public API / `.fsi` surfaces (Artifacts & Commands baselines) | no signature changes |
| B4 | Any output not enumerated in A1–A5 | no unrelated golden may shift (FR-012) |
| B5 | Rich degrade-to-zero-ANSI behaviour | presentation only, unchanged |
| B6 | Governance-handoff compatibility + release catalog | no schema change |

## C. Doc corrections (Tier 2, FR-008..011)

- README + `docs/quickstart.md`: describe `doctor`/`upgrade`.
- `docs/index.md`: link `reference/doctor-upgrade.md`; drop "empty Spec Kit product
  scaffold" claim.
- `DEVELOPING.md`: five projects incl. `FS.GG.Contracts`; correct warning-ratchet
  props file.
- `.github/workflows/release.yml`: header comment carries no stale hardcoded
  versions.

## D. Pin (SC-003)

A test asserts the `unknownCommand` correction contains each of the 18 command
tokens, so a newly-added command that is not added to the correction fails CI.
