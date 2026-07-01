# Contract: `Fsgg.Version` — shared version grammar & comparison

Module: `src/FS.GG.Contracts/Version.fs(.fsi)` (namespace `Fsgg`). Serves the spec Assumption
"uses the same version grammar the registry/provider contract already uses; does not introduce a
new version format."

## Public surface (`.fsi`)

```fsharp
namespace Fsgg

module Version =

    /// A parsed major.minor.patch version. Same grammar as the (formerly private)
    /// Registry SemVer engine, which is refactored to delegate here.
    type Version = { Major: int; Minor: int; Patch: int }

    /// Parse "major.minor.patch"; None when the text is not a valid triple.
    val tryParse: text: string -> Version option

    /// Total order over the string forms. Some -1 / Some 0 / Some 1 when BOTH parse;
    /// None when EITHER side is unparseable (callers degrade honestly — never assert a
    /// false ordering).
    val compare: left: string -> right: string -> int option
```

## Rules

- Grammar is byte-for-byte the existing `Registry.tryParseSemVer`/`compareSemVer`
  (`Registry.fs:73-89`); those private helpers are refactored to call `Fsgg.Version` so exactly
  one grammar exists in the repo.
- `compare a b` semantics: `Some -1` ⇒ `a < b`; `Some 0` ⇒ equal; `Some 1` ⇒ `a > b`;
  `None` ⇒ at least one side unparseable.
- Pure, total, exception-free, BCL-only (no third-party dependency).

## Usage by this feature

- Staleness advisory iff `Version.compare installed minimum = Some -1` (D4).
- Malformed minimum iff `minimum` present and `Version.tryParse minimum = None` (D6).
- Undeterminable installed version ⇒ `compare = None` ⇒ skip comparison (D7).

## Tests

- `tests/FS.GG.Contracts.Tests/` — parse valid/invalid triples; `compare` for `<`, `=`, `>`,
  and unparseable → `None`; regression that `Registry` range checks still behave identically
  after delegating.
- `tests/**/PublicSurface.baseline` for `FS.GG.Contracts` updated for the new module.
