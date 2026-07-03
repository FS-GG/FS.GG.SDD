namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.Internal
open Xunit

// Feature 068 / US3b: pin projectIdFromRoot's contract and make its cwd coupling explicit and
// tested (the review's concern was that it was *hidden*). For an ABSOLUTE root the id is derived
// purely from the argument — deterministic, no filesystem or cwd read — which is how every
// generator/test drives it. The relative-root case intentionally resolves the leaf against the
// process working directory (documented on the function); full edge-purification is deferred.
module FoundationTests =

    [<Fact>]
    let ``projectId is the lowercased leaf of an absolute root, derived purely from the argument`` () =
        // A path that does not exist on disk: proves the id comes from the string, not the
        // filesystem or the process working directory.
        Assert.Equal("fancy-proj", Foundation.projectIdFromRoot "/nonexistent/parent/Fancy-Proj")
        Assert.Equal("myproject", Foundation.projectIdFromRoot "/tmp/some/MyProject")

    [<Fact>]
    let ``projectId lowercases and is stable for a given absolute root`` () =
        let root = "/nonexistent/UPPER-Case-Name"
        Assert.Equal(Foundation.projectIdFromRoot root, Foundation.projectIdFromRoot root)
        Assert.Equal("upper-case-name", Foundation.projectIdFromRoot root)

    [<Fact>]
    let ``an empty root yields a non-empty, lowercased id (fallback or cwd leaf), never a crash`` () =
        // Empty/whitespace normalizes to "." → the leaf resolves against cwd (the documented
        // relative-root coupling); we pin only that it degrades to a legible non-empty lowercase id.
        let id = Foundation.projectIdFromRoot ""
        Assert.False(System.String.IsNullOrWhiteSpace id)
        Assert.Equal(id.ToLowerInvariant(), id)
