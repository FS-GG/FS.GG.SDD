# Phase 1 Data Model: Honor Provider-Declared Default Starter Selection

Entities are derived from the spec's Key Entities and the FR set. Only the **EffectiveParameters**
additions are new; all other shapes are pre-existing and shown for context.

## Entity: ProviderParameterSpec (UNCHANGED — cross-repo contract)

`src/FS.GG.Contracts/Provider.fs:9-12` (`.fsi:15`)

| Field    | Type            | Notes |
|----------|-----------------|-------|
| `Key`    | `string`        | Parameter name forwarded as `--<Key>`. |
| `Required` | `bool`        | A declared `Default` does **not** make a required param optional (Edge Case). |
| `Default` | `string option` | The declared default starter when present. Parsed at `Config.fs:189`. |

Validation/precedence rules (FR-001/FR-002, already implemented):
- When `request.Parameters` has no entry for `Key` and `Default = Some v` → effective value is `v`.
- When `request.Parameters` has `Key=u` → effective value is `u` (author override wins), even if `Default = Some v`.
- When `Required = true` and the effective map lacks `Key` → surface `scaffold.providerParamMissing`
  (`Diagnostics.fs:175-186`); never silently substitute (Edge Case).
- `Default = Some ""` / whitespace → treated as the existing contract treats a blank declaration:
  surfaced, not masked (Edge Case). No silently invented value.
- SDD forwards the effective value verbatim and never interprets/validates/enumerates the allowed set.

## Entity: Effective scaffold parameters (NEW recorded representation)

The resolved `key → value` map scaffold forwards to the provider — declared defaults overlaid by
author `--param` overrides. Computed by `effectiveParameters` (`HandlersScaffold.fs:85-92`).
**New**: persisted on both the provenance record and the report summary.

Canonical serialized form (deterministic): an array of `{ "key": <string>, "value": <string> }`
objects **sorted ascending by key** (matches the existing `producedPaths` sort discipline at
`ScaffoldProvenance.fs:50` and `writeStringList … Sorted`). Empty map ⇒ empty array `[]`.

## Entity: ScaffoldProvenanceRecord (schema v1 — ADD field)

`src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs:15-22` (`.fsi:10-21`)

| Field | Type | Change |
|-------|------|--------|
| `SchemaVersion` | `int` | unchanged (`1`) |
| `Generator` | `GeneratorVersion` | unchanged |
| `ProviderName` | `string` | unchanged |
| `ProviderContractVersion` | `string` | unchanged |
| `TemplateRef` | `string` | unchanged |
| `Outcome` | `string` | unchanged |
| `ProducedPaths` | `ScaffoldProducedPath list` | unchanged |
| **`EffectiveParameters`** | **`(string * string) list`** | **NEW** — sorted by key; the values forwarded to the provider |

JSON key: `effectiveParameters` (array of `{key,value}` objects). `serialize`
(`ScaffoldProvenance.fs:33-60`) writes it after `producedPaths`; `tryParse`
(`ScaffoldProvenance.fs:62-101`) reads it, **defaulting to `[]` when the key is absent**
(backward compatibility, D3). Field is always emitted (empty array when no params forwarded).

## Entity: ScaffoldSummary (report — ADD field)

`src/FS.GG.SDD.Commands/CommandTypes.fsi:328-339`

| Field | Type | Change |
|-------|------|--------|
| existing fields (`ProviderName … NextActionHint`) | — | unchanged |
| **`EffectiveParameters`** | **`(string * string) list`** | **NEW** — sorted by key |

Populated in both summary constructors in `HandlersScaffold.fs` (`notRunSummary` `:111-122` →
empty list; `terminalSummary` `:259-270` and the success path → the effective map). Projected by:
- **json** `writeScaffold` (`CommandSerialization.fs:291-314`): emit `effectiveParameters` array
  after `producedPaths` (the JSON automation contract).
- **text** scaffold block (`CommandRendering.fs:196-213`): one `scaffoldEffectiveParam: <key>=<value>`
  line per entry, sorted.
- **rich**: reuses the plain key/value lines (presentation only; excluded from golden contracts).

## Entity: Composition-acceptance verdict (UNCHANGED shape; default-starter input)

`tests/FS.GG.SDD.Acceptance.Tests/CompositionResult.fs` — `Verdict = Pass | Fail _ | SkipUnavailable`,
serialized as `pass | fail | skip-unavailable`. The default-starter run is expressed by **omitting**
the starter param from `scaffoldRequest` (`AcceptanceSupport.fs:125-128`); the result document's
`inputs.params` (`CompositionResult.fs:169-174`) and the byte-exact golden
(`CompositionAcceptanceTests.fs:407-441`) reflect exactly the params actually sent (still no starter
key when omitted — by reference to the registry default, never naming a value).

## FR-008 byte-level scope (explicit)

For any scaffold input, the change:
- **adds** exactly one field (`effectiveParameters`) to the scaffold JSON object and one line-group
  to the text projection;
- **changes no other field, key order, stream routing, or exit code**, and changes no JSON for any
  command other than `scaffold`.
This is the scoped reading of FR-008 reconciled in research.md (D2). Golden fixtures for scaffold
JSON, the scaffold text projection, and `scaffold-provenance.json` are updated to include the new
field; all non-scaffold command goldens remain byte-identical.

## Test data model (fixtures)

New generic, value-agnostic registry fixture
`tests/fixtures/scaffold-provider/registries/default-declaring.providers.yml`:
- one provider, `schemaVersion: 1`, `contractVersion: "1.0.0"`;
- a **required** `productName` (so required-with-default precedence is testable);
- a **non-required** parameter with a declared `default` (abstract key/value, e.g. `variant` /
  `default: alpha`) — **no** `game`/`app`/rendering identity.
Drives Story 1 (omit → default applied + recorded), Story 2 (`--param variant=beta` → override
recorded), and the FR-004 boundary (fixture is grep-clean).
