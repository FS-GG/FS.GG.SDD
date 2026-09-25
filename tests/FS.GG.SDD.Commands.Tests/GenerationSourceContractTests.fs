namespace FS.GG.SDD.Commands.Tests

open System
open System.IO
open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.GenerationSourceContract
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open Xunit

module GenerationSourceContractTests =
    let private withTree action =
        let root = Path.Combine(Path.GetTempPath(), "sdd-source-contract-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(Path.Combine(root, "producer")) |> ignore
        try action root
        finally Directory.Delete(root, true)

    let private source path bytes =
        { Path = path; Digest = SchemaVersion.sha256Bytes bytes }

    let private contract files =
        { Version = 1; ClosedRoot = "producer"; Policy = ExactBytes; Sources = files }

    let private selected = { ClosedRoot = "producer"; Policy = ExactBytes }

    [<Fact>]
    let ``v1 binds the complete physical source set to immutable bytes`` () =
        withTree (fun root ->
            let bytes = [| 0xefuy; 0xbbuy; 0xbfuy; 65uy; 13uy; 10uy |]
            let file = Path.Combine(root, "producer", "a.txt")
            File.WriteAllBytes(file, bytes)
            match verify root selected (contract [ source "producer/a.txt" bytes ]) with
            | Error reason -> failwithf "valid contract refused: %A" reason
            | Ok captured ->
                Assert.Single captured |> ignore
                let returned = captured.Head.Bytes
                returned.[0] <- 0uy
                Assert.True(captured.Head.Bytes = bytes)
                Assert.Equal(SchemaVersion.sha256Bytes bytes, captured.Head.Digest))

    [<Fact>]
    let ``candidate cannot change producer root or digest policy`` () =
        withTree (fun root ->
            let bytes = Encoding.UTF8.GetBytes "value\r\n"
            File.WriteAllBytes(Path.Combine(root, "producer", "a.txt"), bytes)
            let valid = contract [ source "producer/a.txt" bytes ]
            Assert.Equal(Error WrongRoot, verify root selected { valid with ClosedRoot = "other" })
            Assert.Equal(Error WrongPolicy, verify root selected { valid with Policy = Utf8LfText })
            Assert.Equal(Error(UnsupportedVersion 2), verify root selected { valid with Version = 2 }))

    [<Fact>]
    let ``omitted physical source and stale digest refuse`` () =
        withTree (fun root ->
            let bytes = [| 1uy; 2uy |]
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), bytes)
            let original = contract [ source "producer/a.bin" bytes ]
            File.WriteAllBytes(Path.Combine(root, "producer", "b.bin"), [| 3uy |])
            Assert.Equal(Error(Physical(UnexpectedFile "producer/b.bin")), verify root selected original)
            File.Delete(Path.Combine(root, "producer", "b.bin"))
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), [| 1uy; 4uy |])
            Assert.Equal(Error(DigestDrift "producer/a.bin"), verify root selected original))

    [<Fact>]
    let ``malformed and duplicate candidate rows refuse`` () =
        withTree (fun root ->
            let bytes = [| 1uy |]
            File.WriteAllBytes(Path.Combine(root, "producer", "a.bin"), bytes)
            let valid = source "producer/a.bin" bytes
            let malformed = { valid with Digest = { Algorithm = "sha256"; Value = "ABC" } }
            Assert.Equal(Error(MalformedDigest "producer/a.bin"), verify root selected (contract [ malformed ]))
            Assert.Equal(Error(Physical(DuplicatePath "producer/a.bin")),
                verify root selected (contract [ valid; valid ])))

    [<Fact>]
    let ``contract propagates linked workspace ancestor refusal`` () =
        if OperatingSystem.IsLinux() then
            withTree (fun root ->
                let physical = Path.Combine(root, "physical", "workspace")
                Directory.CreateDirectory(Path.Combine(physical, "producer")) |> ignore
                let bytes = [| 1uy |]
                File.WriteAllBytes(Path.Combine(physical, "producer", "a.bin"), bytes)
                Directory.CreateSymbolicLink(Path.Combine(root, "alias"), Path.Combine(root, "physical")) |> ignore
                let workspace = Path.Combine(root, "alias", "workspace")
                Assert.Equal(Error(Physical(Symlink "..")),
                    verify workspace selected (contract [ source "producer/a.bin" bytes ])))
