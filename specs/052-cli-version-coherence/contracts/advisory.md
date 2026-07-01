# Contract: CLI-staleness advisory & next-action (all three projections)

Serves FR-004/FR-005/FR-006/FR-007/FR-008. All facts are pure projections over one
`CommandReport`; JSON is the contract, text/rich add/drop no facts.

## Diagnostics

### `scaffold.cliBehindMinimum` — `DiagnosticInfo` (non-blocking)

- **Emitted iff** `Fsgg.Version.compare installed minimum = Some -1` (strictly behind), the
  provider declared a valid minimum, and the installed version parsed (D4/D5/D7).
- **Not emitted** when installed `>= min`, when the provider declares no minimum, when the
  minimum is malformed (D6), or when the installed version is unparseable (D7). → FR-006 /
  SC-003.
- **Message** (FR-004, "how far behind"): names installed version, required minimum, and the
  component gap, deterministically. Example:
  `"Installed fsgg-sdd 0.2.1 is behind the provider-declared minimum coherent version 0.3.0 (behind by 1 minor version). Seeded skills / early-stage guidance from newer CLIs may be missing."`
- **Correction** (remedy pointer, FR-008): names the re-seed path —
  `"Upgrade the fsgg-sdd CLI, then re-run `fsgg-sdd init` to re-seed the fs-gg-sdd-* skills and .fsgg/early-stage-guidance.md (idempotent, no-clobber). Note: fsgg-sdd refresh does not re-seed."`
- **Exactly one** such advisory per behind-run (SC-002).

### `scaffold.providerMinimumMalformed` — `DiagnosticWarning` (non-blocking)

- **Emitted iff** the provider declared a `minimumCliVersion` that fails `Fsgg.Version.tryParse`
  (D6). Names the malformed raw value. The comparison is skipped and
  `requiredMinimumCliVersion` is recorded as `null`.

Neither diagnostic sets `hasBlocking` (only `DiagnosticError` does), so the scaffold's outcome
classification and **exit code are unchanged** vs an up-to-date-CLI run (FR-005 / SC-004). An
incomplete scaffold is still never reported complete (unchanged).

## Next-action (only in the behind case)

New resolver branch keyed on `scaffold.cliBehindMinimum`, after the blocking branch:

| Field | Value |
|-------|-------|
| `ActionId` | `reseedSeededSkills` |
| `Command` | `Some Init` |
| `Reason` | upgrade `fsgg-sdd`, re-run `init` to re-seed; `refresh` does not re-seed (D8) |
| `RequiredArtifacts` | `.claude/skills/<seeded>/…`, `.codex/skills/<seeded>/…`, `.fsgg/early-stage-guidance.md` (sorted) |
| `BlockingDiagnosticIds` | `[]` (non-blocking pointer) |

## Projection surfacing (FR-007)

| Projection | Advisory | `requiredMinimumCliVersion` fact |
|-----------|----------|----------------------------------|
| **JSON** / default | in sorted `diagnostics[]`; `nextAction` object | new field in `scaffold` block (string-or-null) |
| **`--text`** | via `diagnostics: N` count + `nextAction: reseedSeededSkills` | new `scaffoldRequiredMinimumCliVersion: …` line |
| **`--rich`** | Diagnostics table row (auto) + next-action callout (auto) | derived from the text `key: value` line (auto) |

Rich is presentation-only, degrades to zero-ANSI when non-interactive, and is excluded from
golden/deterministic contracts.

## Tests (model: `EarlyStageProjectionTests.fs`)

- Behind → advisory present + `nextAction=reseedSeededSkills` in json/text/rich; installed &
  minimum & gap stated; exit code == equal-version run (SC-004).
- At/above, no-minimum, malformed-minimum → **no** `scaffold.cliBehindMinimum` (SC-003);
  malformed → `scaffold.providerMinimumMalformed` present.
- Cross-projection fact-parity (extend `ScaffoldParityTests.fs`).
