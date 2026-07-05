# Contract: `.fsgg/providers.yml` provider registry (schema v1)

**schemaVersion**: `1` · **Owner**: author / provider (NOT SDD) · authoritative selection
contract for `--provider <name>`.

Author-/provider-supplied registry mapping a provider **name** to the data SDD needs to
resolve and invoke it. SDD ships **no** default entries and embeds **none** of these
values in code (FR-002 / SC-005). Optional file: absent ⇒ only the `--provider` option's
inline form (if any) is available, and `scaffold` without a resolvable provider blocks
with `scaffold.providerUnknown`/`providerMissing`.

## Shape

```yaml
schemaVersion: 1
providers:
  - name: <reference used by --provider>
    contractVersion: "1.0.0"        # SDD provider-contract version this provider implements
    templateId: <dotnet new short name / identity>   # opaque to SDD
    source: <nuget package id | local path for `dotnet new install`>  # opaque to SDD
    nameParameter: <param key carrying the raw product name>   # optional; default "name"
    identifierParameter: <param key receiving the derived F# identifier>  # optional (feature 080)
    parameters:
      - key: <param name>
        required: true|false
        default: <optional default value>
```

> Example values above are placeholders. A real entry for the reference provider lives in
> the **FS.GG.Rendering** repo (provider-owned), e.g. naming its `fs-gg-ui` template — that
> entry must never be copied into SDD source or generic-contract tests.

## Field rules

| Field | Type | Required | Rule |
|---|---|---|---|
| `schemaVersion` | int | yes | `1` |
| `providers[].name` | string | yes | unique within the file; matched by `--provider` |
| `providers[].contractVersion` | string | yes | semver; validated against SDD supported range before invocation |
| `providers[].templateId` | string | yes | passed to `dotnet new`; SDD does not interpret it |
| `providers[].source` | string | yes | NuGet id or local path; how SDD acquires the template (`dotnet new install`) |
| `providers[].parameters` | list | no | each `{ key, required, default? }`; required-without-value blocks invocation |
| `providers[].nameParameter` | string | no | the param key carrying the **raw product name** (the derivation *source*); default `name` |
| `providers[].identifierParameter` | string | no | the param key that receives the SDD-**derived valid-F# identifier** (the derivation *sink*, feature 080); absent ⇒ no derivation |

## Resolution & precedence

1. `--provider <name>` selects `providers[].name == <name>`. No match → `providerUnknown`.
2. Effective parameters = descriptor defaults, overlaid by repeated `--param key=value`.
3. When `identifierParameter` is declared and the author did **not** set that key via
   `--param`, SDD derives a valid F# namespace from the `nameParameter` value (a generic,
   language-level transform — hyphens/spaces dropped, leading digit and keyword guarded) and
   forwards it under the `identifierParameter` key. The raw name stays verbatim under
   `nameParameter`. An author `--param <identifierParameter>=…` override wins. A name with no
   identifier character at all → `scaffold.nameUnrepresentable` (exit 1, no invocation).
4. A declared `required` parameter with no resulting value → `providerParamMissing`
   (names the missing keys; SDD never guesses).
5. `contractVersion` outside SDD's supported range → `providerVersionUnsupported`
   (no invocation).

## Parsing

Parsed in `Artifacts/LifecycleArtifacts/Config.fs(i)` alongside the existing `.fsgg/*.yml`
parsers (`project.yml`, `sdd.yml`, `agents.yml`), using the same YAML reader. Malformed
YAML or unsupported `schemaVersion` → a standard schema diagnostic
(`malformedSchemaVersion` / `unsupportedSchemaVersion`), consistent with other configs.
