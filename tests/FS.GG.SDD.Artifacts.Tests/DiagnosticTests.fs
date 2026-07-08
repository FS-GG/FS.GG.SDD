namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module DiagnosticTests =
    let artifact =
        ArtifactRef.create
            "work/001-sdd-artifact-model/tasks.yml"
            ArtifactRef.ArtifactKind.Tasks
            ArtifactRef.ArtifactOwner.Sdd
            true
        |> Result.defaultWith failwith

    [<Fact>]
    let ``Diagnostic factories create actionable findings`` () =
        let diagnostic =
            Diagnostics.unknownReference artifact "FR-999" "Declare FR-999 or remove the reference."

        Assert.Equal("unknownReference", diagnostic.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticError, diagnostic.Severity)
        Assert.Equal("Declare FR-999 or remove the reference.", diagnostic.Correction)
        Assert.Contains("FR-999", diagnostic.RelatedIds)

    [<Fact>]
    let ``Normalized work-model diagnostic factories use stable ids and severities`` () =
        let deprecated = Diagnostics.deprecatedSchemaVersion artifact "0"
        let future = Diagnostics.futureSchemaVersion artifact "3"

        let missing =
            Diagnostics.missingGeneratedWorkModel artifact "readiness/002-normalized-work-model/work-model.json"

        let untyped = Diagnostics.requirementNotTyped artifact "FR-999" "Declare FR-999."

        Assert.Equal("deprecatedSchemaVersion", deprecated.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticWarning, deprecated.Severity)
        Assert.Equal("futureSchemaVersion", future.Id)
        Assert.Equal(Diagnostics.DiagnosticSeverity.DiagnosticError, future.Severity)
        Assert.Equal("missingGeneratedWorkModel", missing.Id)
        Assert.Equal("requirementNotTyped", untyped.Id)

    [<Fact>]
    let ``create leaves a diagnostic un-marked and markToolDefect flips the typed bit`` () =
        let plain =
            Diagnostics.unknownReference artifact "FR-999" "Declare FR-999 or remove."

        Assert.False(plain.IsToolDefect)

        let marked = Diagnostics.markToolDefect plain
        Assert.True(marked.IsToolDefect)
        // markToolDefect changes only the bit — every other field is preserved.
        Assert.Equal(plain.Id, marked.Id)
        Assert.Equal(plain.Message, marked.Message)
        Assert.Equal<string list>(plain.RelatedIds, marked.RelatedIds)

    [<Fact>]
    let ``The defect-producing constructors carry IsToolDefect`` () =
        // The seven ids that escalated via the old providerDefectIds set must now carry the bit.
        Assert.True((Diagnostics.scaffoldProviderFailed "rendering" 1).IsToolDefect)
        Assert.True((Diagnostics.scaffoldProviderUnavailable "rendering").IsToolDefect)
        Assert.True((Diagnostics.scaffoldProviderWroteSddTree [ ".claude/skills/x" ]).IsToolDefect)
        Assert.True((Diagnostics.scaffoldMirrorFailed [ ".codex/skills/x" ]).IsToolDefect)
        Assert.True((Diagnostics.upgradeSelfUpdateFailed 1).IsToolDefect)
        Assert.True((Diagnostics.upgradeStepFailed "cli-self-update").IsToolDefect)
        // A representative user-input diagnostic stays un-marked (resolves at exit 1).
        Assert.False((Diagnostics.scaffoldProviderMissing ()).IsToolDefect)
        Assert.False((Diagnostics.unknownReference artifact "FR-1" "x").IsToolDefect)

    [<Fact>]
    let ``signalsStaleView is id-derived and independent of exact spelling`` () =
        // Matches any id containing "stale" (case-insensitive), regardless of spelling —
        // it operates on round-tripped diagnostics where only the id survives.
        Assert.True(Diagnostics.signalsStaleView (Diagnostics.staleGeneratedView artifact "Stale." "Regenerate."))

        Assert.True(
            Diagnostics.signalsStaleView
                { Diagnostics.unknownReference artifact "x" "y" with
                    Id = "refresh.STALEView" }
        )
        // A non-stale id is not misclassified.
        Assert.False(Diagnostics.signalsStaleView (Diagnostics.unknownReference artifact "FR-1" "x"))

    [<Fact>]
    let ``Diagnostics sort by severity id artifact and location`` () =
        let warning =
            Diagnostics.proseStructuredMismatch artifact "Mismatch." "Update prose."

        let error = Diagnostics.missingArtifact artifact "Create the task file."
        let sorted = Diagnostics.sort [ warning; error ]

        Assert.Equal("missingArtifact", sorted.Head.Id)
        Assert.True(Diagnostics.hasBlocking sorted)

    // Feature 094 (V22): the `surface.versionBumpRequired` id/severity contract. It is advisory —
    // a warning, never a tool defect — because SDD cannot see the previously *published* version
    // and so cannot prove the bump was not already applied in the change under review (FR-008/013).
    [<Fact>]
    let ``surfaceVersionBumpRequired is an advisory warning, never blocking`` () =
        let diagnostic =
            Diagnostics.surfaceVersionBumpRequired
                "breaking"
                "Directory.Build.props"
                "Version"
                "resolved"
                (Some "0.8.0")
                "major"
                (Some "1.0.0")

        Assert.Equal("surface.versionBumpRequired", diagnostic.Id)
        Assert.Equal(Diagnostics.DiagnosticWarning, diagnostic.Severity)
        Assert.False diagnostic.IsToolDefect
        Assert.False(Diagnostics.hasBlocking [ diagnostic ])

    // FR-010: with an unresolved axis the remediation names both `--param` overrides — the
    // diagnostic cannot tell a missing file from a missing property, so it offers both.
    [<Fact>]
    let ``surfaceVersionBumpRequired names both param overrides when the axis is unresolved`` () =
        let diagnostic =
            Diagnostics.surfaceVersionBumpRequired
                "additive"
                "Directory.Build.props"
                "Version"
                "undeterminable"
                None
                "minor"
                None

        Assert.Contains("--param versionAxisFile", diagnostic.Correction)
        Assert.Contains("--param versionAxisProperty", diagnostic.Correction)
        Assert.Contains("undeterminable", diagnostic.Message)

    // FS-GG/FS.GG.SDD#185: `surface.rootEscape` is blocking (exit 1), and a plain user-input error —
    // never a tool defect (exit 2). The operator supplied the param; SDD is not at fault.
    [<Fact>]
    let ``surfaceRootEscape is a blocking user-input error`` () =
        let diagnostic = Diagnostics.surfaceRootEscape "baselineRoot" "../OUTSIDE"

        Assert.Equal("surface.rootEscape", diagnostic.Id)
        Assert.Equal(Diagnostics.DiagnosticError, diagnostic.Severity)
        Assert.False diagnostic.IsToolDefect
        Assert.True(Diagnostics.hasBlocking [ diagnostic ])

    /// The message must quote the RAW param. `normalizeRelativePath` ends in `.TrimStart('/')`, so a
    /// diagnostic built from the normalized value would report `/etc/passwd` as `etc/passwd` — an
    /// innocuous-looking relative path — in the very sentence meant to name the escape.
    [<Fact>]
    let ``surfaceRootEscape names the offending param and quotes the raw value`` () =
        let absolute = Diagnostics.surfaceRootEscape "sourceRoot" "/etc/passwd"
        Assert.Contains("--param sourceRoot=/etc/passwd", absolute.Message)
        Assert.Contains("sourceRoot", absolute.Correction)

        let parent = Diagnostics.surfaceRootEscape "baselineRoot" "../OUTSIDE"
        Assert.Contains("--param baselineRoot=../OUTSIDE", parent.Message)
