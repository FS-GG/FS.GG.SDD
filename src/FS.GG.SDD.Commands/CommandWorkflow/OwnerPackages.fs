namespace FS.GG.SDD.Commands.Internal

open System
open System.Reflection
open FS.GG.SDD.Artifacts.ArtifactRef

/// FS.GG.SDD#1155: which owner-package VERSION this CLI build embeds, per delivery channel.
///
/// The bytes of every owner-sourced skill are compiled into this assembly from a pinned package
/// (ADR-0054 / ADR-0063), but the package manifests carry no version, so the version is stamped at
/// build time from the restored package's own directory name as assembly metadata,
/// `OwnerPackage.<channel> = <packageId>|<version>`. The package identity therefore lives in the
/// build pin and this seam only reads it back by CHANNEL, so no package id enters generic SDD
/// behavior (the same rule the materializers follow, `scaffold` FR-002 / SC-005).
///
/// `upgrade` needs the version for one reason: never replace a skill with an OLDER pack than the one
/// already materialized (an old CLI run against a newer workspace).
module internal OwnerPackages =
    let private metadataPrefix = "OwnerPackage."

    /// The build-stamped metadata channel of an owner class; `None` for any class that is not an
    /// owner-sourced skill channel.
    let channelOf (owner: ArtifactOwner) =
        match owner with
        | ArtifactOwner.Driver -> Some "driver"
        | ArtifactOwner.GameSkill -> Some "gameSkill"
        | ArtifactOwner.RenderingSkill -> Some "renderingSkill"
        | ArtifactOwner.AudioSkill -> Some "audioSkill"
        | _ -> None

    /// `(package id, version)` for every channel this build stamped, keyed by channel.
    let embedded: Map<string, string * string> =
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
        |> Seq.choose (fun attribute ->
            match Some attribute.Key, Option.ofObj attribute.Value with
            | Some key, Some value when key.StartsWith(metadataPrefix, StringComparison.Ordinal) ->
                match value.Split('|') with
                | [| package; version |] when package <> "" && version <> "" ->
                    Some(key.Substring metadataPrefix.Length, (package, version))
                | _ -> None
            | _ -> None)
        |> Map.ofSeq

    /// The embedded `(package, version)` for an owner class, when this build stamped one.
    let embeddedFor (owner: ArtifactOwner) =
        channelOf owner |> Option.bind embedded.TryFind
