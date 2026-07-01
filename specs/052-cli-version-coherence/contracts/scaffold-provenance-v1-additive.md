# Contract: `scaffold-provenance` schema v1 — additive `requiredMinimumCliVersion`

Artifact: `.fsgg/scaffold-provenance.json` · Owner: `sdd` · Stability: `additiveOptional` ·
Schema version: **stays 1** (additive) · Serves FR-001/FR-002/FR-003.

## Change

Add one optional field, `requiredMinimumCliVersion`, emitted **immediately after** the
`generator` object, always present as **string or `null`**.

## JSON shape (unchanged fields elided)

```jsonc
{
  "schemaVersion": 1,
  "generator": { "id": "FS.GG.SDD.Artifacts", "version": "0.2.1" },  // = CLI version used (FR-001)
  "requiredMinimumCliVersion": "0.3.0",   // NEW: provider-declared min; null when none/malformed (FR-002)
  "providerName": "…",
  "providerContractVersion": "…",
  "templateRef": "…",
  "outcome": "providerSucceeded",
  "producedPaths": [ /* sorted, unchanged */ ],
  "effectiveParameters": [ /* sorted, unchanged */ ]
}
```

## Rules

- **Present & valid minimum** → the parsed provider `minimumCliVersion` string, verbatim.
- **Provider declares no minimum** → `null` (never fabricated). Acceptance US1 scenario 2.
- **Provider declares a malformed minimum** → `null` (the value is not persisted), *and* a
  `scaffold.providerMinimumMalformed` warning is emitted (see `advisory.md`). Not silently
  "no minimum". Edge Case.
- **Determinism**: byte-identical across identical runs (US1 scenario 3); no clock, no absolute
  path; other fields' ordering/sorting unchanged.
- **Back-compat (parse)**: absent key or `null` → `None`. Records written before this field
  still parse (existing readers ignore the unknown key). Edge "Pre-existing provenance
  consumers".
- **Versioning**: additive-optional ⇒ schema stays v1, **minor** package bump
  (`versioning-policy.md`). No cross-repo handoff `contractVersion` (scaffold-provenance has
  `ContractVersion = None`), so only the package/registry coherence checklist applies.

## Fixtures that must be updated (golden/byte-stable)

- `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` — JSON golden (`:279-294`),
  provenance-content asserts (`:245-254`, `:442-467`), determinism (`:498-511`).
- `tests/FS.GG.SDD.Artifacts.Tests/ScaffoldProvenanceTests.fs` — serialize/`tryParse` round-trip,
  new back-compat case (old record without the field → `None`).
