namespace FS.GG.SDD.Commands.Internal

open System.Reflection

/// Audio-owned browser guidance delivered through the same closed schema-v2 product-skill
/// contract as Rendering. The package identity exists only in the build pin/resource wiring;
/// runtime selection remains driven by the producer manifest and effective provider parameters.
module internal AudioSkills =
    let manifestResourceName = "AudioSkill.manifest"
    let private skillResourcePrefix = "AudioSkill.skill/"

    // Provider descriptors/provenance retain the concrete dotnet template id, while owner
    // manifests use the registry's id without its package prefix. Derive that predicate input
    // from the durable template reference instead of trusting an arbitrary forwarded parameter.
    let templatePredicateValue (templateRef: string) =
        let prefix = "fs-gg-"
        if templateRef.StartsWith(prefix, System.StringComparison.Ordinal) then
            templateRef.Substring(prefix.Length)
        else
            templateRef

    let ownerPredicateParameters templateRef effective =
        effective |> Map.add "template" (templatePredicateValue templateRef)

    let private tryLoadBytes name =
        let assembly = Assembly.GetExecutingAssembly()
        match assembly.GetManifestResourceStream(name) with
        | null -> None
        | stream ->
            use stream = stream
            use buffer = new System.IO.MemoryStream()
            stream.CopyTo buffer
            Some(buffer.ToArray())

    let manifestText () =
        tryLoadBytes manifestResourceName
        |> Option.bind (fun bytes -> Fsgg.SkillMirror.decodeBody bytes |> Result.toOption)

    let embeddedFiles () =
        let assembly = Assembly.GetExecutingAssembly()
        assembly.GetManifestResourceNames()
        |> Array.choose (fun name ->
            let normalized = name.Replace('\\', '/')
            if normalized.StartsWith(skillResourcePrefix, System.StringComparison.Ordinal) then
                let rest = normalized.Substring(skillResourcePrefix.Length)
                let separator = rest.IndexOf('/')
                if separator <= 0 || separator = rest.Length - 1 then None
                else
                    let id = rest.Substring(0, separator)
                    let path = rest.Substring(separator + 1)
                    tryLoadBytes name |> Option.map (fun bytes -> (id, path), bytes)
            else None)
        |> Map.ofArray

    let plan parameters =
        RenderingSkills.planFilesFrom (manifestText ()) (embeddedFiles ()) parameters
