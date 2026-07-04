namespace FS.GG.SDD.Artifacts

open System.IO
open System.Text
open System.Text.Json
open Fsgg.Schemas

module SkillManifestJson =

    let private scopeValue (scope: SkillScope) =
        match scope with
        | Process -> "process"
        | Product -> "product"

    // SDD's producer manifest is process-only; every fs-gg-sdd-* skill is
    // unconditionally emitted, so `materializes-when` is the ADR-0017 canonical literal
    // `always` for every entry — a bare token in the grammar the union gate evaluates
    // (`==`/`!=`/`in [..]`/`and`/`or`/`always`), never a C-style parenthesized predicate.
    // SDD emits no product-scope skills; a Product predicate is a provider concern and
    // never reaches this serializer (the drift guard asserts every emitted entry is
    // Process).
    let private materializesWhen (_scope: SkillScope) = "always"

    let serialize (manifest: SkillManifest) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion)
        writer.WriteStartArray("skills")

        // Sorted by id so the emitted bytes are deterministic and reconcilable (FR-005).
        manifest.Skills
        |> List.sortBy (fun skill -> skill.Id)
        |> List.iter (fun skill ->
            writer.WriteStartObject()
            writer.WriteString("id", skill.Id)
            writer.WriteString("scope", scopeValue skill.Scope)
            writer.WriteString("sha256", skill.Sha256)

            match skill.ResolvablePath with
            | Some path -> writer.WriteString("resolvablePath", path)
            | None -> ()

            writer.WriteString("materializes-when", materializesWhen skill.Scope)
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        // Trailing LF so the committed artifact is POSIX-clean; Utf8JsonWriter emits `\n`
        // for indentation (not Environment.NewLine), so the bytes are platform-stable.
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"
