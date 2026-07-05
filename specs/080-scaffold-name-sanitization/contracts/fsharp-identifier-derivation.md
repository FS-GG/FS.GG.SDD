# Contract: F# identifier derivation (`FS.GG.SDD.Artifacts/FsharpIdentifier`)

A pure, generic, language-level transform. No provider knowledge, no I/O, no MVU.

## Signature (`FsharpIdentifier.fsi`)

```fsharp
namespace FS.GG.SDD.Artifacts

module FsharpIdentifier =

    /// Why a name cannot be derived into any F# identifier.
    type DerivationError =
        /// The name contains no character valid in an F# identifier, so no
        /// identifier can be formed (drives scaffold.nameUnrepresentable).
        | Unrepresentable of name: string

    /// Derive a valid F# *namespace* from an arbitrary product name.
    ///
    /// - Dots delimit namespace segments and are preserved; each segment is derived
    ///   independently.
    /// - Per segment: drop characters invalid in an F# identifier (keep Unicode
    ///   letters, digits, `_`); if the result starts with a digit, prefix `_`; if it
    ///   equals an F# reserved keyword, suffix `_`.
    /// - Deterministic and culture-invariant (ordinal); a no-op on inputs already
    ///   valid as F# namespaces.
    /// - `Error (Unrepresentable name)` iff the name (or a segment) has no identifier
    ///   character at all.
    val deriveNamespace: name: string -> Result<string, DerivationError>
```

## Behavior table (golden — drives `FsharpIdentifierTests.fs`)

| Input | Output | Note |
|---|---|---|
| `Roquelike-DungeonCrawler` | `Ok "RoquelikeDungeonCrawler"` | hyphen dropped (the reported defect) |
| `Acme` | `Ok "Acme"` | no-op |
| `Acme.Foo` | `Ok "Acme.Foo"` | dots preserved as segment boundaries |
| `My App` | `Ok "MyApp"` | space dropped |
| `foo.bar-baz` | `Ok "foo.barbaz"` | per-segment; second segment sanitized |
| `3Crawler` | `Ok "_3Crawler"` | leading digit guarded |
| `type` | `Ok "type_"` | reserved-keyword segment guarded |
| `mod` / `const` | `Ok "mod_"` / `Ok "const_"` | hard reserved words guarded (unescaped ⇒ FS0010) |
| `Acme._` | `Ok "Acme"` | lone-underscore segment (F# wildcard) collapsed, never emitted |
| `_` | `Error (Unrepresentable "_")` | lone underscore is the wildcard, not an identifier |
| `Acme..Foo` | `Ok "Acme.Foo"` | empty middle segment — **collapsed** (resolved; pinned by the golden test) |
| `---` | `Error (Unrepresentable "---")` | no identifier character |
| `""` | `Error (Unrepresentable "")` | empty |

> **Resolved:** empty interior segments are **collapsed** (so `Acme..Foo` → `Acme.Foo`); an
> `Error (Unrepresentable)` is returned only when the **whole** name reduces to no segment at all.
> This choice is pinned by the golden test in `FsharpIdentifierTests.fs`.

## Invariants (property tests)

- **Idempotent**: `deriveNamespace x |> Result.map deriveNamespace = deriveNamespace x |> Result.map Ok` for `Ok` results (deriving a derived value changes nothing).
- **Valid output**: every `Ok` result is a legal F# namespace (each segment compiles as an identifier).
- **No-op on valid**: for `x` already a valid namespace, `deriveNamespace x = Ok x`.
- **Deterministic**: repeated calls and cross-platform calls agree (ordinal, no culture/clock).
