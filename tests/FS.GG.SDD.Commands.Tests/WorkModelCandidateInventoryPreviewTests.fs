namespace FS.GG.SDD.Commands.Tests

open System.Text
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.SchemaVersion
open FS.GG.SDD.Commands.GenerationSourceSnapshot
open FS.GG.SDD.Commands.WorkModelCandidateInventoryPreview
open Xunit

module WorkModelCandidateInventoryPreviewTests =
    let private workId = "sample"
    let private selected = "work/sample/spec.md"
    let private other = "work/other/spec.md"

    let private file (path: string) (text: string) =
        let bytes = Encoding.UTF8.GetBytes text
        CapturedFile(path, bytes, SchemaVersion.sha256Bytes bytes)

    let private selectedFile () = file selected (TestSupport.validSpec workId "Selected")

    [<Fact>]
    let ``red-before complete supplied candidate set refuses duplicate logical work id`` () =
        let files = [ selectedFile (); file other (TestSupport.validSpec workId "Duplicate") ]
        Assert.Equal(Error(DuplicateWorkId [ other ]), verify workId files)

    [<Fact>]
    let ``unrelated candidate and noncandidate work file are accepted`` () =
        let files =
            [ selectedFile (); file other (TestSupport.validSpec "other" "Unrelated")
              file "work/other/notes.txt" "unrelated" ]
        match verify workId files with
        | Error reason -> failwithf "unrelated candidate refused: %A" reason
        | Ok preview -> Assert.Equal<string list>([ other; selected ], preview.CandidatePaths)

    [<Fact>]
    let ``malformed candidate refuses instead of silently disappearing from inventory`` () =
        Assert.Equal(Error(MalformedCandidate other),
                     verify workId [ selectedFile (); file other "no front matter" ])

    [<Fact>]
    let ``candidate case aliases and missing selected spec refuse`` () =
        let alias = "work/other/Spec.md"
        Assert.Equal(Error(InvalidPath alias),
                     verify workId [ selectedFile (); file alias (TestSupport.validSpec "other" "Alias") ])
        Assert.Equal(Error(DuplicatePath other),
                     verify workId [ selectedFile (); file other (TestSupport.validSpec "other" "First")
                                     file other (TestSupport.validSpec "other" "Second") ])
        Assert.Equal(Error(MissingSelectedSpec selected),
                     verify workId [ file other (TestSupport.validSpec "other" "Only") ])
        Assert.Equal(Error(MissingSelectedSpec selected),
                     verify workId [ file "work/SAMPLE/spec.md" (TestSupport.validSpec workId "Alias") ])

    [<Fact>]
    let ``supplied raw digest must attest to candidate bytes`` () =
        let bytes = Encoding.UTF8.GetBytes(TestSupport.validSpec "other" "Changed")
        let forged = CapturedFile(other, bytes, SchemaVersion.sha256Bytes [| 0uy |])
        Assert.Equal(Error(RawDigestDrift other), verify workId [ selectedFile (); forged ])
