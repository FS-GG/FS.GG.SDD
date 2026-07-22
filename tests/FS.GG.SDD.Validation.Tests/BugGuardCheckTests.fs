namespace FS.GG.SDD.Validation.Tests

open FS.GG.SDD.Artifacts.Diagnostics
open FS.GG.SDD.Validation.BugGuardCheck
open Xunit

/// FS.GG.SDD#654 — the preventive lane check that stops an OPEN filed bug from
/// co-existing indefinitely with a GREEN test pinning its reported-wrong behavior.
/// The rule fires on a marker for a still-open issue and goes silent once it closes.
module BugGuardCheckTests =

    // ---- marker grammar (scanText) ----

    [<Fact>]
    let ``scan finds pins-bug and guards markers with their kind, issue, and line`` () =
        let text =
            "module MazeTests\n\
             // pins-bug #14 — asserts the straight-line route (reported-wrong)\n\
             let straightLineRoute () = ()\n\
             // guards #27 regression\n\
             let flowFieldRoute () = ()\n"

        let markers = scanText "tests/MazeTests.fs" text

        Assert.Equal<BugGuardMarker list>(
            [ { Kind = PinsBug
                Issue = 14
                Path = "tests/MazeTests.fs"
                Line = 2 }
              { Kind = Guards
                Issue = 27
                Path = "tests/MazeTests.fs"
                Line = 4 } ],
            markers
        )

    [<Fact>]
    let ``scan is case-insensitive and tolerates whitespace around the hash`` () =
        let markers = scanText "t.fs" "// PINS-BUG # 14 and Guards #27\n"
        Assert.Equal(2, List.length markers)
        Assert.Equal(PinsBug, markers.[0].Kind)
        Assert.Equal(14, markers.[0].Issue)
        Assert.Equal(Guards, markers.[1].Kind)
        Assert.Equal(27, markers.[1].Issue)
        // Both matches sit on line 1.
        Assert.All(markers, fun m -> Assert.Equal(1, m.Line))

    [<Fact>]
    let ``the bare word guards without a hash-number is not a marker`` () =
        // The mandatory `#<n>` is what distinguishes a deliberate marker from prose.
        let markers =
            scanText "t.fs" "// this test guards the invariant but references no issue\n"

        Assert.Empty(markers)

    // ---- the lane rule (check) ----

    // A resolver standing in for the CI edge that has GitHub issue access.
    let private resolver (states: (int * IssueState) list) =
        fun issue ->
            states
            |> List.tryFind (fun (n, _) -> n = issue)
            |> Option.map snd
            |> Option.defaultValue Unknown

    [<Fact>]
    let ``fires on a test guarding an OPEN issue`` () =
        let markers =
            [ { Kind = PinsBug
                Issue = 14
                Path = "tests/MazeTests.fs"
                Line = 2 } ]

        let diagnostics = check (resolver [ 14, Open ]) markers

        let diag = Assert.Single(diagnostics)
        Assert.Equal("bugGuard.openIssuePinned", diag.Id)
        Assert.Equal(DiagnosticWarning, diag.Severity)
        Assert.Contains("#14", diag.Message)
        Assert.Contains("OPEN", diag.Message)
        Assert.Equal(Some 2, diag.Location |> Option.bind (fun l -> l.Line))

    [<Fact>]
    let ``stays silent once the guarded issue is CLOSED`` () =
        let markers =
            [ { Kind = PinsBug
                Issue = 14
                Path = "tests/MazeTests.fs"
                Line = 2 } ]

        let diagnostics = check (resolver [ 14, Closed ]) markers

        Assert.Empty(diagnostics)

    [<Fact>]
    let ``a guards marker on an OPEN issue also fires`` () =
        let markers =
            [ { Kind = Guards
                Issue = 27
                Path = "tests/MazeTests.fs"
                Line = 4 } ]

        let diag = Assert.Single(check (resolver [ 27, Open ]) markers)
        Assert.Equal("bugGuard.openIssuePinned", diag.Id)
        Assert.Contains("guards", diag.Message)

    [<Fact>]
    let ``an unresolvable issue is flagged as a dangling link`` () =
        let markers =
            [ { Kind = PinsBug
                Issue = 9999
                Path = "t.fs"
                Line = 1 } ]

        let diag = Assert.Single(check (resolver []) markers)
        Assert.Equal("bugGuard.unresolvedIssue", diag.Id)
        Assert.Equal(DiagnosticWarning, diag.Severity)

    [<Fact>]
    let ``mixed corpus: only markers whose issue is OPEN produce warnings, deterministically ordered`` () =
        let markers =
            [ { Kind = PinsBug
                Issue = 14
                Path = "b.fs"
                Line = 5 }
              { Kind = Guards
                Issue = 27
                Path = "a.fs"
                Line = 3 }
              { Kind = PinsBug
                Issue = 31
                Path = "a.fs"
                Line = 1 } ]

        // #14 open, #27 closed (fixed → silent), #31 open.
        let diagnostics = check (resolver [ 14, Open; 27, Closed; 31, Open ]) markers

        Assert.Equal(2, List.length diagnostics)
        // Ordered by (path, line, issue): a.fs:1 (#31) before b.fs:5 (#14).
        Assert.Contains("#31", diagnostics.[0].Message)
        Assert.Contains("#14", diagnostics.[1].Message)

    [<Fact>]
    let ``the TD1 14 scenario: scan-then-check fires while open, silent after the fix lands`` () =
        // The M2 test that pinned the maze straight-line route (the reported-wrong behavior).
        let source =
            "tests/MazeRoutingTests.fs",
            "// pins-bug #14: maze ground enemies follow the straight line, not the flow field\n\
             [<Fact>]\n\
             let ``ground enemy route on a maze equals the lab waypoints`` () = ()\n"

        // While #14 is OPEN across M2..M9 the lane check would have surfaced the contradiction.
        let whileOpen = checkSources (resolver [ 14, Open ]) [ source ]
        Assert.Single(whileOpen) |> ignore

        // Once #14 is fixed and CLOSED, the check is silent (the pinning test is expected to
        // have been updated to red / corrected; the marker no longer nags).
        let afterFix = checkSources (resolver [ 14, Closed ]) [ source ]
        Assert.Empty(afterFix)
