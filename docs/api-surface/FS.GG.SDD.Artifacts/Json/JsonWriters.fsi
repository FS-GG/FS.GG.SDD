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

    val writeStringList: writer: Utf8JsonWriter -> order: StringListOrder -> name: string -> values: string list -> unit

    val writeSourceDigest: writer: Utf8JsonWriter -> name: string -> digest: SourceDigest option -> unit

    val writeOutputDigest: writer: Utf8JsonWriter -> name: string -> digest: OutputDigest option -> unit

    val writeLocation: writer: Utf8JsonWriter -> name: string -> location: SourceLocation option -> unit

    val writeDiagnostic: writer: Utf8JsonWriter -> relatedIdsOrder: StringListOrder -> diagnostic: Diagnostic -> unit
