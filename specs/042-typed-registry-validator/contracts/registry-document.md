# Contract: `Fsgg.Registry` document model + pure validator

**Package**: `FS.GG.Contracts` (BCL-only; FSharp.Core only). **Module**: `Fsgg.Registry`.
**Posture**: additive (see plan §Version & migration). Declared in `Registry.fsi`.

## Added public surface (sketch — finalize in `.fsi` before `.fs`)

```fsharp
namespace Fsgg

module Registry =

    // --- existing legacy surface retained unchanged ---
    // RegistryComponent / DependencyEdge / RegistryModel / validate
    // RegistryDiagnostic / ValidationResult reused as-is.

    /// A repo participating in the registry (the `repos:` map).
    type RegistryRepo = { Id: string; Name: string; Role: string }

    /// A versioned cross-repo contract (`contracts[]`).
    type ContractEntry =
        { Id: string
          Version: string
          Owner: string
          Surface: string
          Consumers: string list
          PackageVersion: string option
          Range: string option }

    /// A hard dependency edge over repos (`dependencies[]`). `Via` is free-text.
    type DependencyEdge2 = { From: string; To: string; Via: string }

    /// A coherence state entry (`coherence[]`).
    type CoherenceEntry = { Id: string; Coherent: bool }

    /// The typed model of the real `registry/dependencies.yml`.
    type RegistryDocument =
        { SchemaVersion: int
          Repos: RegistryRepo list
          Contracts: ContractEntry list
          Dependencies: DependencyEdge2 list
          Coherence: CoherenceEntry list }

    // RegistryRule extended additively with these cases:
    //   | DuplicateComponent
    //   | MalformedDocument

    /// Pure validator over the real-schema document. Mirrors the rule *kinds* of
    /// scripts/validate-registry.py so the two cannot disagree on the canonical file.
    /// Deterministic: diagnostics in document order. No I/O.
    val validateDocument: document: RegistryDocument -> ValidationResult
```

## Rule contract (parity with `scripts/validate-registry.py`)

Given a `RegistryDocument`, `validateDocument` returns `Valid` iff **all** hold; otherwise
`Invalid` with one diagnostic per violation, in document order:

1. `SchemaVersion` is present and an integer (non-integer → `MalformedVersion`).
2. `Repos` is non-empty; each repo has non-blank `Name` and `Role` (→ `MissingField`).
3. `Contracts` is non-empty; for each contract:
   - `Id` non-blank (→ `MissingField`) and unique (→ `DuplicateComponent`).
   - `Version` present and non-blank (absent/blank → `MissingField`); when present,
     valid per the grammar (→ `MalformedVersion`).
   - `Owner`/`Surface`/`Consumers` non-blank (→ `MissingField`).
   - `Owner` ∈ repo ids ∪ {`github`} (→ `UnknownComponent`).
   - each `Consumers` entry ∈ repo ids (→ `UnknownComponent`).
   - `PackageVersion`, when present, valid (→ `MalformedVersion`).
   - `Range`, when present, well-formed (→ `MalformedVersion`).
4. For each dependency edge: `From`/`To` non-blank (→ `MissingField`) and ∈ repo ids
   (→ `UnknownComponent`). `Via` is **not** validated.
5. For each coherence entry: `Id` non-blank (→ `MissingField`); `Coherent` is a boolean.
6. A node that is not the expected shape → `MalformedDocument`.

**Version grammar**: `version`/`package-version` = `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$`
**or** `^\d+$`; `range` = `^[\d.xX*\s<>=~^|.-]+$`.

**Determinism (SC-004)**: identical input → byte-identical diagnostic list.

**Authority parity (SC-005)**: on the canonical `registry/dependencies.yml`,
`validateDocument` MUST return `Valid` (zero diagnostics) — agreeing with the Python
stand-in — so FS-GG/.github#18 can swap one for the other.
