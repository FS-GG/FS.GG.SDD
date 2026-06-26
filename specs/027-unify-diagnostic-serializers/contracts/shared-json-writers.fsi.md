# Internal Contract: Shared JSON Writer Module

This is the **internal-style plumbing** signature for Story 2. It is a *new*
module with its own `.fsi`; it does not touch the three named entry-point `.fsi`
files (`Serialization.fsi`, `CommandSerialization.fsi`, `CommandReports.fsi`),
which stay byte-identical. The sub-namespace keeps it out of the reflection-based
`FS.GG.SDD.Artifacts` surface baseline (research D1).

## Proposed `.fsi` (sketch — finalize in Story-1-stable state)

```fsharp
namespace FS.GG.SDD.Artifacts.Json

open System.Text.Json
open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Artifacts.SchemaVersion

module JsonWriters =

    /// Whether a string array is emitted in source order or sorted.
    /// Commands serializers pass Sorted; Artifacts serializers pass SourceOrder.
    type StringListOrder =
        | SourceOrder
        | Sorted

    val writeStringList:
        writer: Utf8JsonWriter -> order: StringListOrder -> name: string -> values: string list -> unit

    val writeSourceDigest:
        writer: Utf8JsonWriter -> name: string -> digest: SourceDigest option -> unit

    val writeOutputDigest:
        writer: Utf8JsonWriter -> name: string -> digest: OutputDigest option -> unit

    val writeLocation:
        writer: Utf8JsonWriter -> name: string -> location: SourceLocation option -> unit

    val writeDiagnostic:
        writer: Utf8JsonWriter -> relatedIdsOrder: StringListOrder -> diagnostic: Diagnostic -> unit
```

## Behavioral contract (byte-fixed against pre-change output)

- **`writeStringList`**: `Sorted` ⇒ `values |> List.sort` then write;
  `SourceOrder` ⇒ write in given order. Array is `WriteStartArray name` … values …
  `WriteEndArray`. (Matches `CommandSerialization.fs:13-16` and
  `Serialization.fs:19-22`.)
- **`writeSourceDigest` / `writeOutputDigest`**: `Some d` ⇒
  `WriteStartObject name; WriteString("algorithm", d.Algorithm);
  WriteString("value", d.Value); WriteEndObject`. `None` ⇒ `WriteNull name`.
  (Superset of the bare Artifacts `writeDigest`/`writeOutputDigest`, called with
  `Some`, and the option-wrapped Commands forms.)
- **`writeLocation`**: `Some l` ⇒ `WriteStartObject name`, then `line` =
  number-or-null, `column` = number-or-null, `WriteEndObject`. `None` ⇒
  `WriteNull name`. The `name` parameter unifies Commands' fixed `"location"`
  with Artifacts' `"location"`/`"sourceLocation"`.
- **`writeDiagnostic`**: field order **exactly** `id`, `severity`
  (`Diagnostics.severityValue`), `artifact` (path-or-null), `location`
  (via `writeLocation` with name `"location"`), `message`, `correction`,
  `relatedIds` (via `writeStringList` with `relatedIdsOrder`). (Matches
  `CommandSerialization.fs:49-62` and `Serialization.fs:169-182`.)

## Call-site bindings

| Caller | string lists | `writeDiagnostic` relatedIds | location name(s) |
|--------|--------------|------------------------------|------------------|
| `CommandSerialization` (Commands) | `Sorted` | `Sorted` | `"location"` |
| `Serialization` (Artifacts) | `SourceOrder` | `SourceOrder` | `"location"`, `"sourceLocation"` |

## Acceptance (maps to spec)

- SC-002: each unified writer body exists in exactly one place; both serializers
  consume it (no duplicate `writeDiagnostic`/`writeOutputDigest`/string-list/
  digest/location bodies remain across the two assemblies).
- FR-005: ordering and option/bare-digest divergences are parameters, not forks.
- FR-008 / SC-005: no change to the three named `.fsi` files or the per-assembly
  `PublicSurface.baseline`; one-way `Artifacts → Commands` layering preserved.
