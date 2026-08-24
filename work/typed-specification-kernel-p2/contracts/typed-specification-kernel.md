# Typed specification kernel public contract

Status: planned public API sketch; `.fsi` remains the implementation authority.

## Namespace and package

- Package: `FS.GG.SDD.Artifacts`
- Namespace: `FS.GG.SDD.Artifacts.TypedSpecifications`
- Preview identity: `1.3.0-preview.1`

## Core shape

```fsharp
type SpecificationId
type SpecificationProvenance
type EvidenceObligation
type SpecificationDiagnostic
type SpecificationModel<'extension>
type ExtensionContract<'extension>
type CompiledSpecification<'extension>
type SemanticDiff = Equivalent | Changed of SemanticChange list

module SpecificationCompiler =
    val compile:
        ExtensionContract<'extension> ->
        SpecificationModel<'extension> ->
        Result<CompiledSpecification<'extension>, SpecificationDiagnostic list>

module SpecificationCodec =
    val serialize: ExtensionContract<'extension> -> SpecificationModel<'extension> -> Result<string, SpecificationDiagnostic list>
    val deserialize: ExtensionContract<'extension> -> string -> Result<SpecificationModel<'extension>, SpecificationDiagnostic list>
```

The concrete extension contract owns validation and canonical JSON/binary bytes
for one concrete extension type. The core never boxes it and never discovers it
through reflection.

## Requirements extension

`RequirementsExtension` carries stable typed IDs for scope, stories,
requirements, acceptance, ambiguity/decision state, and evidence bindings.
`RequirementsExtension.contract` is the first producer-owned extension contract.
Direct construction and its small builder must normalize identically.

## Migration

```fsharp
type MigrationOutcome<'a> =
    | Migrated of 'a
    | Ambiguous of MigrationFinding list
    | Unsupported of MigrationFinding list
```

Migration is analysis-only. Findings carry a stable code, message, and line /
column. No function in the migration module writes a canonical file.

## Projection and evidence

Projection generation returns deterministic Markdown and JSON plus fingerprints.
Validation consumes an observation (`Missing`, `Unreadable`, or `Content`) so an
unreadable view cannot be collapsed into absence. Evidence validation binds
receipts to obligation IDs and expected kinds without importing Governance.

## Compatibility

All additions are new API. Existing SDD artifacts, CLI reports, schemas, exit
codes, defaults, and generated views remain unchanged. S.I.R gameplay/rule types
are not part of this contract.
