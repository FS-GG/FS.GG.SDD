namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// Pins the stable handler façades to the extracted pure policy services. The broader
/// command golden/property suites continue to pin the emitted artifacts byte-for-byte.
module CommandDomainServiceTests =
    [<Fact>]
    let ``task graph facade delegates skill normalization to domain policy`` () =
        let declared = Some "  Browser   Tests  "
        Assert.Equal("browser-tests", TaskGraphDomain.resolveTestSkill declared)
        Assert.Equal(TaskGraphDomain.resolveTestSkill declared, TaskGraphAuthoring.resolveTestSkill declared)

    [<Fact>]
    let ``scaffold facade delegates ownership and manifest mutation`` () =
        let listing = "src/App.fs\n.fsgg/project.yml\nAGENTS.md\n"

        Assert.Equal<string list>([ "src/App.fs" ], ScaffoldMutation.collisionPaths listing)
        Assert.True(ScaffoldMutation.isSddOwned ".agents/skills/fs-gg-sdd-plan/SKILL.md")
        Assert.Equal(ScaffoldMutation.toolManifestText "1.2.3", HandlersScaffold.toolManifestText "1.2.3")

    [<Fact>]
    let ``evidence source currency is owned by evidence domain`` () =
        let recorded = [ EvidenceDomain.sourceSnapshot "spec" "work/x/spec.md" "before" ]
        let same = [ EvidenceDomain.sourceSnapshot "spec" "work/x/spec.md" "before" ]
        let changed = [ EvidenceDomain.sourceSnapshot "spec" "work/x/spec.md" "after" ]

        Assert.False(EvidenceDomain.sourceSnapshotStale same recorded)
        Assert.True(EvidenceDomain.sourceSnapshotStale changed recorded)

        Assert.Equal(
            EvidenceDomain.sourceSnapshotStale changed recorded,
            HandlersEvidence.evidenceSourceSnapshotStale changed recorded
        )

    [<Fact>]
    let ``diagnostic facade delegates correction routing`` () =
        let diagnostics =
            [ DiagnosticConstructors.missingSpecificationPrerequisite "work/x/spec.md" "Specification is required." ]

        Assert.Equal(Some Specify, DiagnosticRouting.planCorrection diagnostics)

        Assert.Equal(
            DiagnosticRouting.planCorrection diagnostics,
            DiagnosticConstructors.planCorrectionCommand diagnostics
        )
