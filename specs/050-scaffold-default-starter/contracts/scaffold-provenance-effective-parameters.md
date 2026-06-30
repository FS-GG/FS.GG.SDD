# Contract: `scaffold-provenance.json` — `effectiveParameters` (schema v1, additive)

**Artifact**: `.fsgg/scaffold-provenance.json` · **Schema**: `1` (unchanged) · **Owner**: SDD
(produced) · **Change**: additive optional field.

## Field

```jsonc
{
  "schemaVersion": 1,
  "generator": { "id": "...", "version": "..." },
  "providerName": "...",
  "providerContractVersion": "...",
  "templateRef": "...",
  "outcome": "...",
  "producedPaths": [ { "path": "...", "owner": "generatedProduct" } ],
  "effectiveParameters": [            // NEW — always present (empty array when none)
    { "key": "productName", "value": "Demo" },
    { "key": "variant",     "value": "alpha" }
  ]
}
```

## Rules

1. `effectiveParameters` is the resolved `key → value` map forwarded to the provider:
   provider-declared `default`s overlaid by author `--param` overrides (author wins).
2. Entries are **sorted ascending by `key`** (deterministic; same discipline as `producedPaths`).
3. The field is **always emitted**; an empty effective map serializes as `[]`.
4. Values are recorded **verbatim** — SDD never interprets, validates, or enumerates them.
5. A required parameter that is unsatisfied does **not** appear here; the scaffold fails with
   `scaffold.providerParamMissing` before provenance is written for that outcome.

## Compatibility (D3)

- Backward: `tryParse` defaults `effectiveParameters` to `[]` when the key is absent, so
  provenance files written before this field still parse. `schemaVersion` stays `1`.
- Forward: readers that ignore unknown keys are unaffected.
- A migration note is recorded under `docs/release/migrations/` (Tier 1).

## Verification

- Round-trip: `serialize >> tryParse` preserves `effectiveParameters` (order and content).
- Golden: a byte-exact `scaffold-provenance.json` fixture including the new field.
- Absent-field parse: a v1 document without `effectiveParameters` parses to `[]`.

> Satisfies FR-003 (auditable chosen starter, reproducible product), within FR-008's scoped
> additive guarantee.
