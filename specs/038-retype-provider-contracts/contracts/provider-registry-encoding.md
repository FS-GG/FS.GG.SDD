# Contract: `.fsgg/providers.yml` Encoding (v1, extended fields)

This contract defines how `parseProviderRegistry` reads a provider registry into
`Fsgg.Provider.ProviderDescriptor`. It is the registry encoding of the canonical
`FS.GG.Contracts` 1.0.0 provider surface. **No new schema version**: `schemaVersion`
remains `1`; the extended keys are optional and additive.

## Top-level shape (unchanged)

```yaml
schemaVersion: 1          # required; same version gate as before
providers:                # sequence of provider entries
  - <entry>
```

The schema-version gate is unchanged: a missing/empty registry or an out-of-range
version produces the same version diagnostics and the same `Error` result as before
(FR-007). `schemaVersion: 1` with a well-formed body produces `Ok`.

## Provider entry

### Required scalars (entry dropped if any is missing)

| Key | Type | Maps to |
|-----|------|---------|
| `name` | scalar | `Name` |
| `contractVersion` | scalar | `ContractVersion` |
| `templateId` | scalar | `TemplateId` |
| `source` | scalar | `Source` |

An entry missing any required scalar is **dropped** (a `--provider` naming it then
resolves to `scaffold.providerUnknown`). Declared/extended fields never rescue an
incomplete entry.

### `parameters` (optional, unchanged)

```yaml
parameters:
  - key: productName      # required within a parameter entry
    required: true        # optional, default false
    default: "Demo"       # optional
```

### Declared commands (optional, NEW) — `build` / `test` / `run` / `verify`

Each is a nested mapping mapping to a `DeclaredCommand option`:

```yaml
build:
  executable: dotnet            # required within the mapping
  arguments: [build, -c, Release]   # optional, default []
```

- Key absent ⇒ field is `None`.
- `executable` blank/empty/whitespace ⇒ field is `None` (treated as "not declared",
  never a launchable empty command — FR-005, via `isMalformed`).
- `arguments` absent ⇒ `[]`. `arguments` is a sequence of scalars.

| Key | Maps to | Default |
|-----|---------|---------|
| `build` | `Build` | `None` |
| `test`  | `Test`  | `None` |
| `run`   | `Run`   | `None` |
| `verify`| `Verify`| `None` |

### `nameParameter` (optional, NEW)

```yaml
nameParameter: projectName   # default "name" when absent or blank
```

| Key | Maps to | Default |
|-----|---------|---------|
| `nameParameter` | `NameParameter` | `"name"` (`defaultNameParameter`) |

## Backward compatibility (FR-006)

A registry written to today's shape (no declared commands, no `nameParameter`)
parses to a descriptor with all four command fields `None` and
`NameParameter = "name"`, and the five preserved fields identical to the prior local
result. Every scaffold output byte is preserved.

## Full example (extended)

```yaml
schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: __FIXTURE__/ok
    parameters:
      - key: productName
        required: true
    build:
      executable: dotnet
      arguments: [build, -c, Release]
    run:
      executable: dotnet
      arguments: [run, --no-build]
    nameParameter: projectName
```
