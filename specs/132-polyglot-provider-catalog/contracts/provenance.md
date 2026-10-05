# Catalog scaffold provenance (schema 2)

C2.1 defines a pure codec and an ownership projection. It does not execute a provider,
produce runtime observations or write production schema-2 provenance. Synthetic test
observations establish codec behavior only. C2.2 owns the real writer after admitted execution.

The closed JSON root contains exactly `schemaVersion` (the canonical integer `2`),
`generator`, `declaration`, `observation` and `ownership`. Duplicate and unknown properties,
missing required fields, unsupported schema versions and malformed primitive values refuse
with a located `provenance.malformed` diagnostic. No schema-2 failure falls back to legacy
provenance, an absent document or the default lifecycle.

`generator` contains `id` and `version`. `declaration` contains:

- `rawCatalogDigest`, `catalogId`, `catalogRevision`, `catalogDigest` and the complete selected
  `descriptor`, including its verified semantic digest;
- `effectiveParameters` as unique `{key,value}` entries, and independent `rawProductName`,
  `packageIdentity` and `codeIdentifier` values which must correspond to catalog resolution;
- `archive`, `policy` and `evidenceMap`, each containing exact `id`, `version` and `digest`;
- `admission`, the full normalized neutral request: `contractVersion`, selected `platform`,
  `requiredCapabilityIds`, `semanticOnlyCapabilityIds`, independently supplied
  `supportedPlatforms`, `supportedEnvironmentIds`, `supportedFormats` (`id`, `version`),
  `evidenceMappings` (`format`, `id`, `version`) and the same complete `descriptor`;
- `budgets`, containing positive `requestedPreflightSeconds`, `requestedScaffoldSeconds`,
  `admittedPreflightSeconds` and `admittedScaffoldSeconds`. Requested budgets fit their ceilings.

`observation` contains the selected `platform`, exact observed `tools` (`id`, `version`,
`executable`), `consumedArchiveDigest`, `transport` (`executable`, `version`), ordered
`invocations`, `result` and complete `producedPaths`. Every invocation records its literal
`executable` and ordered `arguments`, `workingRoot`, contained `workingDirectory`, unique
`environmentRoots` (`key`, `role`, `path`), positive `timeoutSeconds` and canonical integer
`exitCode`. Root roles are `operation` or `staging`; paths are relative to that role rather
than machine-specific temporary roots. Empty argument values and spaces are preserved.
Success requires nonempty invocations, zero exit codes, `result: succeeded`, matching archive
bytes, platform and tool versions, and invocation budgets within the declared scaffold budget.
The codec checks correspondence; it does not authenticate observations or substitute for
Governance admission, capability semantics or production process enforcement.

`ownership` is the complete schema-1 ownership record: `schemaVersion`, `generator`,
`requiredMinimumCliVersion`, `providerName`, `providerContractVersion`, `templateRef`,
`outcome`, `effectiveParameters`, and all six path arrays: `producedPaths`, `mirroredPaths`,
`sddOwnedPaths`, `driverPaths`, `gameSkillPaths` and `renderingSkillPaths`. All success path
entries require `path`, the matching category `owner`, and a lowercase prefixed SHA-256 in
`sha256`. Paths must be contained relative paths with no duplicates or file/directory prefix
collisions. The observation's path/owner/hash map covers exactly this ownership union. Provider,
contract, template, parameters, generator and `providerSucceeded` outcome correspond to the
outer declaration. Schema-1 serialization remains unchanged.

`ScaffoldProvenanceDocument.parse` selects schema 1 or 2 explicitly. Its ownership projection
feeds existing refresh and typed lifecycle readers. Invalid documents block mutation. Schema 1
retains its existing compatibility behavior; an old client is not claimed to understand schema 2.
