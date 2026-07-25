# Plan — Cohesive command-contract families

## Design

Keep `CommandTypes` as the compatibility facade and source of every canonical definition. Add one
additive nested `CommandFamilies` module after the canonical types. Its nested modules contain only
F# type abbreviations:

- `Invocation`, `Artifacts`, `Lifecycle`, `Guidance`
- `Scaffold`, `Remediation`, `Surfaces`, `Lint`
- `Reporting`, `Runtime`

Because abbreviations erase to their underlying types, this improves source navigation without
creating replacement runtime types or changing existing fully qualified names.

## Contract posture

- Public API: additive source aliases only.
- Binary compatibility: unchanged canonical CLR types and members.
- Serialization: unchanged; the manual writers continue to receive the same underlying types.
- Persisted schemas/report version: unchanged.
- Migration: optional adoption. Existing imports remain supported.

## Verification

1. Compile one identity assertion for every alias.
2. Compare representative canonical/family runtime types.
3. Serialize one `CommandReport` through both annotations and assert byte equality.
4. Run Commands tests, public-surface reflection baseline, and `fsgg-sdd surface --check`.
5. Build the solution in Release.
