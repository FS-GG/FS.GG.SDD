namespace FS.GG.SDD.Commands.Internal

open System

/// Internal SemVer adapter for command metadata. The shared contract parser owns
/// numeric triples; command/tool versions may additionally carry prerelease or
/// build metadata, whose numeric core still drives floor and bump comparisons.
module internal CommandVersion =
    let private core (value: string) =
        if String.IsNullOrWhiteSpace value then
            ""
        else
            let text = value.Trim()
            let separator = text.IndexOfAny([| '-'; '+' |])
            if separator < 0 then text else text.Substring(0, separator)

    let tryParse value = value |> core |> Fsgg.Version.tryParse

    let compare left right =
        match tryParse left, tryParse right with
        | Some a, Some b ->
            if a.Major <> b.Major then Some(compare a.Major b.Major)
            elif a.Minor <> b.Minor then Some(compare a.Minor b.Minor)
            else Some(compare a.Patch b.Patch)
        | _ -> None
