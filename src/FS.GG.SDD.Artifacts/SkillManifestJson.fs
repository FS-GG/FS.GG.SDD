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
    let private processMaterializesWhen = "always"

    let serialize (manifest: SkillManifestV2) : string =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = true))

        writer.WriteStartObject()
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion)
        writer.WriteStartArray("skills")

        // Sorted by id so the emitted bytes are deterministic and reconcilable (FR-005).
        manifest.Skills
        |> List.sortBy (fun fileSet -> fileSet.Skill.Id)
        |> List.iter (fun fileSet ->
            let skill = fileSet.Skill

            writer.WriteStartObject()
            writer.WriteString("id", skill.Id)
            writer.WriteString("scope", scopeValue skill.Scope)

            // v1's `sha256` is RETAINED at v2, with its v1 meaning (the `SKILL.md` digest), so the
            // document stays a superset of v1 rather than a replacement of it — a v1 reader that
            // ignores unknown properties keeps working, and nothing it already relied on moved.
            writer.WriteString("sha256", skill.Sha256)

            match skill.ResolvablePath with
            | Some path -> writer.WriteString("resolvablePath", path)
            | None -> ()

            writer.WriteString("materializes-when", processMaterializesWhen)

            // ADR-0017 v2 (FS.GG.SDD#727): the COMPLETE file set, one row per file, sorted by path
            // so the emitted bytes are deterministic for the same reason the skills are. `SKILL.md`
            // is a row like any other; its digest is the same value `sha256` above carries, taken
            // with the same `Fsgg.SkillMirror.sha256` (CRLF-normalized) — one digest rule per
            // document, so no reader has to know which field used which.
            //
            // EMITTED LAST, DELIBERATELY. Every v1 property keeps its v1 position, so v2 is a pure
            // APPEND to each row rather than an insertion into it. Readers that scan these rows
            // positionally or by regex — and there are such readers in the org, some of which
            // FAIL OPEN when a row stops matching — see the v1 document they already parse, with
            // trailing content they ignore. An insertion between `id` and the later keys is the
            // one edit that could silently drop rows out of another repo's guard.
            writer.WriteStartArray("files")

            fileSet.Files
            |> List.sortBy (fun file -> file.RelativePath)
            |> List.iter (fun file ->
                writer.WriteStartObject()
                writer.WriteString("path", file.RelativePath)
                writer.WriteString("sha256", file.Sha256)
                writer.WriteEndObject())

            writer.WriteEndArray()
            writer.WriteEndObject())

        writer.WriteEndArray()
        writer.WriteEndObject()
        writer.Flush()

        // Trailing LF so the committed artifact is POSIX-clean; Utf8JsonWriter emits `\n`
        // for indentation (not Environment.NewLine), so the bytes are platform-stable.
        Encoding.UTF8.GetString(stream.ToArray()) + "\n"
