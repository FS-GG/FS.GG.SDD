namespace FS.GG.SDD.Cli

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands

module RegistrySkillManifest =

    // Derive the committed path from the single source of truth for the neutral skills
    // root (`Fsgg.SkillMirror.providerSourceRoot` = ".agents"), so it moves with the
    // mirror root rather than re-spelling it. = ".agents/skills/skill-manifest.json".
    let manifestPath =
        Fsgg.SkillMirror.providerSourceRoot + "/skills/skill-manifest.json"

    // The deterministic canonical JSON for the currently-seeded process skill set
    // (sorted by id, trailing LF). One source of truth: ProcessSkillManifest.build.
    let private generate () =
        SkillManifestJson.serialize (ProcessSkillManifest.build ())

    // Compare tolerant of a CRLF checkout of the LF-authored artifact (Git's Windows
    // `core.autocrlf=true` rewrites the committed file to CRLF on disk). Matches the
    // CRLF-normalization `Fsgg.SkillMirror.sha256` already applies to skill bodies
    // (feature 070), so `--check` and the drift guard don't spuriously flag drift.
    let private normalizeNewlines (text: string) = text.Replace("\r\n", "\n")

    let private rootOf (args: string list) =
        let rec find =
            function
            | "--root" :: value :: _ when not (value.StartsWith("--", StringComparison.Ordinal)) -> Some value
            | _ :: rest -> find rest
            | [] -> None

        find args |> Option.defaultValue "."

    let private targetPath (root: string) =
        Path.Combine(root, manifestPath.Replace('/', Path.DirectorySeparatorChar))

    let private usage =
        "Usage: fsgg-sdd registry skill-manifest [--check|--write] [--root <dir>]"

    let run (args: string list) : int =
        // `--help` must not depend on manifest generation, so resolve it before any
        // embedded-resource read; `generate ()` runs only in the branches that emit.
        if args |> List.exists (fun a -> a = "--help" || a = "-h") then
            Console.Out.WriteLine usage
            0
        else

            let target = targetPath (rootOf args)
            let json = generate ()

            if List.contains "--check" args then
                // Drift-guard mode: the committed artifact must match a fresh generation
                // (modulo checkout line endings). Missing or stale ⇒ non-zero + an actionable
                // hint on stderr, so stdout stays clean and CI fails loud (SC/AC-006).
                if File.Exists target && normalizeNewlines (File.ReadAllText target) = json then
                    Console.Out.WriteLine($"registry skill-manifest: {manifestPath} up to date")
                    0
                else
                    let reason = if File.Exists target then "is STALE" else "is MISSING"

                    Console.Error.WriteLine(
                        $"registry skill-manifest: {manifestPath} {reason} — "
                        + "run `fsgg-sdd registry skill-manifest --write`"
                    )

                    1
            elif List.contains "--write" args then
                match Path.GetDirectoryName target with
                | null
                | "" -> ()
                | dir -> Directory.CreateDirectory dir |> ignore

                File.WriteAllText(target, json)
                Console.Out.WriteLine($"registry skill-manifest: wrote {manifestPath}")
                0
            else
                // Bare: the manifest JSON is itself the automation contract — to stdout.
                Console.Out.Write json
                0
