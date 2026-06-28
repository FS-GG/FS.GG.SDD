# Phase 1 Data Model: Canonical Provider Descriptor & Registry Mapping

This feature adds **no new types**. It adopts the canonical `Fsgg.Provider` types
(shipped in `FS.GG.Contracts` 1.0.0, feature 036) and removes the local
re-encoding. This document records the authoritative shapes and the
registry → descriptor mapping the re-typed `parseProviderRegistry` performs.

## Canonical types (authoritative — `FS.GG.Contracts`, `src/FS.GG.Contracts/Provider.fsi`)

### `DeclaredCommand`
| Field | Type | Notes |
|-------|------|-------|
| `Executable` | `string` | A launchable command; blank ⇒ "not declared" (`isMalformed`). |
| `Arguments` | `string list` | Ordered args; may be empty. |

### `ProviderParameterSpec`
| Field | Type | Notes |
|-------|------|-------|
| `Key` | `string` | Parameter name. |
| `Required` | `bool` | Default `false` when absent in YAML. |
| `Default` | `string option` | Optional default value. |

### `ProviderDescriptor`
| Field | Type | Source key | Default when absent |
|-------|------|-----------|---------------------|
| `Name` | `string` | `name` | — (required; entry dropped if missing) |
| `ContractVersion` | `string` | `contractVersion` | — (required) |
| `TemplateId` | `string` | `templateId` | — (required) |
| `Source` | `string` | `source` | — (required) |
| `Parameters` | `ProviderParameterSpec list` | `parameters` | `[]` |
| `Build` | `DeclaredCommand option` | `build` | `None` |
| `Test` | `DeclaredCommand option` | `test` | `None` |
| `Run` | `DeclaredCommand option` | `run` | `None` |
| `Verify` | `DeclaredCommand option` | `verify` | `None` |
| `NameParameter` | `string` | `nameParameter` | `"name"` (`defaultNameParameter`) |

### Helpers (reused, not re-implemented)
- `defaultNameParameter : string` = `"name"`.
- `resolveNameParameter : ProviderDescriptor -> string` — returns
  `defaultNameParameter` when the stored value is null/whitespace.
- `isMalformed : DeclaredCommand -> bool` — `true` when `Executable` is
  null/empty/whitespace.

## Type being removed

- `FS.GG.SDD.Artifacts.Config.ProviderParameterSpec` (local copy — identical shape).
- `FS.GG.SDD.Artifacts.Config.ProviderDescriptor` (local 5-field subset:
  `Name/ContractVersion/TemplateId/Source/Parameters`).
- The acceptance harness's local `AcceptanceSupport.DeclaredCommand` (1:1 copy).

After this feature, **exactly one** `ProviderDescriptor`, `ProviderParameterSpec`,
and `DeclaredCommand` exist — all in `FS.GG.Contracts` (SC-001).

## Registry → descriptor mapping (re-typed `parseProviderRegistry`)

Per provider entry under `providers:`:

1. **Required fields** — `name`, `contractVersion`, `templateId`, `source` must
   all be present scalars; otherwise the entry is **dropped** (unchanged
   drop-incomplete behavior, FR-007). The extended fields never rescue an
   incomplete entry.
2. **Parameters** — parsed exactly as today: each needs a `key`; `required`
   defaults `false`; `default` is optional.
3. **Declared commands** — for each of `build`/`test`/`run`/`verify`: if the key
   is present, read `executable` (scalar) and `arguments` (sequence of scalars,
   default `[]`) into a candidate `DeclaredCommand`; if `isMalformed` (blank
   executable) the field becomes `None`, else `Some candidate`. Absent key ⇒
   `None`.
4. **Name parameter** — read optional `nameParameter` scalar; the stored
   `NameParameter` resolves to `"name"` when absent or blank.

### Behavior-preservation invariant (FR-006)

A registry entry that declares **none** of `build/test/run/verify/nameParameter`
yields a descriptor with:

```
Build = None; Test = None; Run = None; Verify = None
NameParameter = "name"
Name/ContractVersion/TemplateId/Source/Parameters  // identical to the prior local result
```

…and the scaffold command produces byte-identical `CommandReport`, scaffold
summary, provenance, and diagnostics.

## State / lifecycle

No state machine. `parseProviderRegistry` is a pure function:
`FileSnapshot -> Result<ProviderDescriptor list, Diagnostic list>`. The schema
version gate is evaluated before success:

- `Some version, []` (valid version, no diagnostics) ⇒ `Ok descriptors`.
- otherwise ⇒ `Error versionDiagnostics` (unchanged).

## Probe command flow (acceptance harness)

| Probe | Declared source | Default (declared `None` or blank) |
|-------|-----------------|-------------------------------------|
| `buildProbe` | `descriptor.Build : DeclaredCommand option` | `dotnet build` at product root (300 s timeout) |
| `runProbe` | `descriptor.Run : DeclaredCommand option` | `dotnet run --project <discovered>` (10 s grace / 60 s overall) |

`Test` and `Verify` are read into the descriptor for completeness/contract parity
but are **not** wired to probes by this feature (no acceptance probe consumes them
today); they default to `None` and are exercised only by parsing tests.
