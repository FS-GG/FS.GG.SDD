# Phase 1 Data Model: name → valid F# identifier

## Entities

### ProviderDescriptor (extended — `Fsgg.Provider`)

Additive change to the existing org-shared descriptor. Existing fields unchanged.

| Field | Type | New? | Meaning |
|---|---|---|---|
| `NameParameter` | `string` | activated | The forwarded param key carrying the **raw product name** (the derivation **source**). Defaults to `"name"` via `resolveNameParameter`. Previously parsed but unused. |
| `IdentifierParameter` | `string option` | **new** | The forwarded param key that receives the **derived F# identifier** (the derivation **sink**). `None` ⇒ no derivation (backward compatible). |

**Validation / rules**:
- `IdentifierParameter` parsed from optional `identifierParameter:` scalar in `providers.yml`
  (missing ⇒ `None`). A blank/whitespace value is treated as `None`.
- A descriptor may declare `IdentifierParameter` equal to `NameParameter` — discouraged, but if
  it does, derivation would overwrite the raw name; the parse layer records it verbatim and the
  handler applies precedence (D4). (No cross-field rejection in v1; documented as a provider
  authoring caveat.)

### DerivationError (new — `FS.GG.SDD.Artifacts/FsharpIdentifier`)

| Case | When |
|---|---|
| `Unrepresentable of name: string` | The name (or any segment) contains **no** valid F# identifier character, so no identifier can be formed. Drives `scaffold.nameUnrepresentable`. |

### Derived identifier (value)

Not persisted as its own field — it is injected into the **forwarded/effective parameters** map
under the `IdentifierParameter` key and recorded via the existing `EffectiveParameters` surface.

| Property | Rule (FR / D) |
|---|---|
| Valid F# namespace | dot-separated, each segment a legal F# identifier (FR-001, D2). |
| Deterministic | pure, ordinal, culture-invariant; same input → same output (FR-003, D3). |
| No-op on valid input | `derive x = Ok x` when `x` is already a valid namespace (FR-003, D3, SC-005). |
| Raw name untouched | the `NameParameter` value is never modified (FR-006, US2). |

## Function contract (derivation)

```
// FS.GG.SDD.Artifacts/FsharpIdentifier.fsi  (sketch — final in contracts/)
module FsharpIdentifier =
    type DerivationError = Unrepresentable of name: string
    /// Derive a valid F# namespace from an arbitrary product name (per-segment,
    /// language-level, deterministic, no-op on already-valid input). Error only when
    /// the name contains no identifier character at all.
    val deriveNamespace: name: string -> Result<string, DerivationError>
```

## Parameter-resolution flow (scaffold `resolveScaffold`, pure)

```
effective            = effectiveParameters descriptor request        // existing (defaults ⊕ author --param)
sinkKey              = descriptor.IdentifierParameter                 // None ⇒ skip all below
sourceKey            = resolveNameParameter descriptor
authorSetSink        = request.Parameters |> List.exists (fst >> (=) sinkKey)   // D4 precedence
rawName              = Map.tryFind sourceKey effective
=>
  match sinkKey, authorSetSink, rawName with
  | None, _, _                    -> effective                                  // no derivation (today's behavior)
  | Some _, true, _               -> effective                                  // author override wins (D4)
  | Some _, false, None           -> effective                                  // nothing to derive from (no-op)
  | Some k, false, Some name ->
      match FsharpIdentifier.deriveNamespace name with
      | Ok ident   -> Map.add k ident effective                                 // inject derived sink (FR-005)
      | Error err  -> BLOCK scaffold.nameUnrepresentable (exit 1)               // FR-009 / D5
```

The resulting `effective` map forwards to `dotnet new` verbatim (existing edge) and is recorded
in provenance/report (existing `EffectiveParameters`, schema v1 — D6).

## State / transitions

No durable state machine. The only new decision is the pure branch above inside the scaffold MVU
`update`; the block path yields the existing `ScaffoldBlocked` resolution with a not-run summary.
