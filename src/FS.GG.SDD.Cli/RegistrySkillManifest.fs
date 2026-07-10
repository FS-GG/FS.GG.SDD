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

    // FS-GG/FS.GG.SDD#237 (Gap C finding 4 / #203): confine `--root` so the manifest path
    // stays inside the workspace. `Path.Combine(root, …)` returns the manifest suffix verbatim
    // when `root` is rooted, and `GetFullPath` resolves `..`, so a bare `--root /etc --write`
    // would write out of tree. The single authoritative lexical guard lives in
    // `FS.GG.SDD.Artifacts.PathContainment.escapesRoot` (FS-GG/FS.GG.SDD#337); the durable
    // effect-edge containment primitive is #203/ADR-0002, not this predicate.

    let private targetPath (root: string) =
        Path.Combine(root, manifestPath.Replace('/', Path.DirectorySeparatorChar))

    let private usage =
        "Usage: fsgg-sdd registry skill-manifest [--check|--write] [--root <dir>]"

    // ADR-0002 Gap C finding 4 (#203, FS-GG/FS.GG.SDD#258): the registry subcommands parse with
    // their own scanners outside the lifecycle interpreter, so — like every lifecycle command since
    // #196 — an option they cannot honor must block (exit 1, nothing written) instead of being
    // silently swallowed by the `_ :: rest` catch-all. Recognized here: the three modes and the
    // value-taking `--root`. The bare `--` end-of-options separator (#246) is not an option, and the
    // token after `--root` is its value, not an option. (`RegistryValidate` keeps a sibling copy of
    // this reject pass over its own recognized set; kept small and comment-linked so they cannot drift.)
    let private recognizedOptions =
        set [ "--check"; "--write"; "--root"; "--help"; "-h" ]

    let private unknownOptions (args: string list) =
        let rec scan acc =
            function
            | [] -> List.rev acc
            | "--root" :: value :: rest when not (value.StartsWith("--", StringComparison.Ordinal)) -> scan acc rest // `--root <value>`: the value is not an option token
            | "--" :: rest -> scan acc rest // POSIX end-of-options separator (#246)
            | token :: rest when
                token.StartsWith("-", StringComparison.Ordinal)
                && not (recognizedOptions.Contains token)
                ->
                scan (token :: acc) rest
            | _ :: rest -> scan acc rest

        scan [] args

    let private formatOptions (options: string list) =
        options |> List.map (fun option -> $"'{option}'") |> String.concat ", "

    let run (args: string list) : int =
        // Reject an unrecognized option before anything acts; a token the command cannot honor is
        // not masked by a later `--help`/mode flag — stderr diagnostic + exit 1, stdout clean (parity
        // with the `--root` containment error below).
        let unknown = unknownOptions args

        if not (List.isEmpty unknown) then
            Console.Error.WriteLine($"registry skill-manifest: unrecognized option {formatOptions unknown} — {usage}")

            1
        // `--help` must not depend on manifest generation, so resolve it before any
        // embedded-resource read; `generate ()` runs only in the branches that emit.
        elif args |> List.exists (fun a -> a = "--help" || a = "-h") then
            Console.Out.WriteLine usage
            0
        else

            let root = rootOf args

            // Containment first: an escaping `--root` plans no read and no write (parity with
            // `surface` #185). User-input failure ⇒ exit 1, stderr diagnostic, stdout stays clean.
            if PathContainment.escapesRoot root then
                Console.Error.WriteLine(
                    $"registry skill-manifest: --root '{root}' escapes the workspace root — "
                    + "pass a path inside the workspace (no absolute path or '..')."
                )

                1
            else

                let target = targetPath root
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
