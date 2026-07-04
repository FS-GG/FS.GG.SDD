namespace FS.GG.SDD.Cli

open System
open System.IO
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands

module RegistrySkillManifest =

    let manifestPath = ".agents/skills/skill-manifest.json"

    // The deterministic canonical JSON for the currently-seeded process skill set
    // (sorted by id, trailing LF). One source of truth: ProcessSkillManifest.build.
    let private generate () =
        SkillManifestJson.serialize (ProcessSkillManifest.build ())

    let private rootOf (args: string list) =
        let rec find =
            function
            | "--root" :: value :: _ when not (value.StartsWith "--") -> Some value
            | _ :: rest -> find rest
            | [] -> None

        find args |> Option.defaultValue "."

    let private targetPath (root: string) =
        Path.Combine(root, manifestPath.Replace('/', Path.DirectorySeparatorChar))

    let private usage =
        "Usage: fsgg-sdd registry skill-manifest [--check|--write] [--root <dir>]"

    let run (args: string list) : int =
        let target = targetPath (rootOf args)
        let json = generate ()

        if List.contains "--check" args then
            // Drift-guard mode: the committed artifact must be byte-identical to a fresh
            // generation. Missing or stale ⇒ non-zero + an actionable hint on stderr, so
            // stdout stays clean and CI fails loud (SC/AC-006).
            if File.Exists target && File.ReadAllText target = json then
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
        elif args |> List.exists (fun a -> a = "--help" || a = "-h") then
            Console.Out.WriteLine usage
            0
        else
            // Bare: the manifest JSON is itself the automation contract — to stdout.
            Console.Out.Write json
            0
