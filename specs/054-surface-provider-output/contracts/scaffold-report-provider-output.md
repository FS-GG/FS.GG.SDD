# Contract: scaffold report `providerInvocation` block

**Feature**: `054-surface-provider-output` | Additive to the `scaffold` `CommandReport`
block. JSON is the single source of truth; text and rich add/drop no facts (FR-004).

## Presence gate (FR-006)

`providerInvocation` is present (non-null) **iff** the provider was invoked and the
outcome is a provider defect:

| Outcome | `providerInvocation` |
|---------|----------------------|
| `providerFailed` (non-zero exit) | present |
| `providerFailed` (SDD-tree intrusion, `providerWroteSddTree` diagnostic) | present |
| `providerUnavailable` (never launched) | present |
| `providerSucceeded` / `providerSucceededEmpty` | `null` / omitted |
| dry-run (`providerNotRun`) | `null` / omitted |
| user-input block (`providerMissing`/`providerUnknown`/`providerVersionUnsupported`/`providerParamMissing`/`targetCollision`) | `null` / omitted |

## JSON shape (contract)

Appended inside the existing `scaffold` object, fixed key order:

```json
"scaffold": {
  "...": "existing fields unchanged, through nextActionHint",
  "providerInvocation": {
    "commandLine": "dotnet new fsgg-fixture-app -o . --productName Acme",
    "processStarted": true,
    "exitCode": 127,
    "standardOutput": "",
    "standardOutputTruncated": false,
    "standardError": "'--productName' is not a valid option",
    "standardErrorTruncated": false
  }
}
```

- On success / non-defect paths: `"providerInvocation": null`.
- `exitCode` is an integer when `processStarted` is `true`, else `null` (FR-003 — a real
  `0` is never confused with "not launched").
- On `providerUnavailable`: `"processStarted": false, "exitCode": null`, `commandLine` is the
  attempted command line, `standardError` holds the launch error (R4).
- Embedded newlines in `standardOutput`/`standardError` are JSON-escaped as `\n` (faithful).

## Text projection (single-line, R6)

Emitted only when `providerInvocation` is present, after the existing scaffold lines, one
`key: value` pair each (so the rich renderer's k/v derivation stays intact):

```
scaffoldProviderCommandLine: dotnet new fsgg-fixture-app -o . --productName Acme
scaffoldProviderExitCode: 127
scaffoldProviderStdout:
scaffoldProviderStdoutTruncated: false
scaffoldProviderStderr: '--productName' is not a valid option
scaffoldProviderStderrTruncated: false
```

- `scaffoldProviderExitCode: (not launched)` when `exitCode` is `null`.
- Captured streams are single-line-encoded (embedded newline → literal `\n`).

## Rich projection

Derived automatically from the text k/v pairs (`Cli/Rendering.fs`); presentation-only;
degrades to zero-ANSI when non-interactive or `NO_COLOR`/`TERM=dumb`. Excluded from
deterministic/golden contracts (FR-009).

## Invariants

- **INV-1** (FR-007): the block adds diagnostic visibility only — outcome string and exit
  code (2/1/0) are byte-identical to today for every path.
- **INV-2** (FR-005/SC-005): neither `standardOutput` nor `standardError` exceeds
  `providerOutputCapChars` (65 536); truncation is always flagged.
- **INV-3** (SC-004): on success the JSON differs from today only by the additive
  `"providerInvocation": null`.
- **INV-4** (FR-010): nothing here reaches `.fsgg/scaffold-provenance.json` (schema v1).
- **INV-5** (SC-003): json ≡ text ≡ rich facts; rich-redirected ≡ text.
