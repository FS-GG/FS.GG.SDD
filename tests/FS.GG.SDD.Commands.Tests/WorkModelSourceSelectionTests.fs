namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands.CommandTypes
open FS.GG.SDD.Commands.Internal
open Xunit

/// Source-selection characterization; it does not authorize a physical multi-root adapter.
module WorkModelSourceSelectionTests =
    let private workId = "fixture-work"
    let private evidencePath = $"work/{workId}/evidence.yml"

    let private emptyModel =
        {
            Request = TestSupport.request Analyze "."
            PendingEffects = []
            InterpretedEffects = []
            Diagnostics = []
            Specification = None
            Clarification = None
            Checklist = None
            Plan = None
            Tasks = None
            Analysis = None
            Evidence = None
            Verification = None
            Ship = None
            AgentGuidance = None
            Refresh = None
            Scaffold = None
            Doctor = None
            Upgrade = None
            Lint = None
            Surface = None
            DependencySurface = None
            GeneratedViews = []
            Report = None
        }

    let private selectedEvidence text =
        ViewGeneration.workModelSnapshots
            workId None None None None None None (Some text) emptyModel
        |> List.find (fun source -> source.Path = evidencePath)

    [<Fact>]
    let ``work-model source selection erases only the evidence source-snapshot section`` () =
        let first = "sourceSnapshots:\n  - path: first\nevidence:\n  - id: E-1\n"
        let second = "sourceSnapshots:\n  - path: other\nevidence:\n  - id: E-1\n"
        let changedEvidence = "sourceSnapshots:\n  - path: first\nevidence:\n  - id: E-2\n"
        let selected = selectedEvidence first

        Assert.Equal("sourceSnapshots: []\nevidence:\n  - id: E-1\n", selected.Text)
        Assert.Equal(selected.Text, (selectedEvidence second).Text)
        Assert.NotEqual(selected.Text, (selectedEvidence changedEvidence).Text)
        Assert.Equal(SchemaVersion.sha256Text selected.Text,
                     SchemaVersion.sha256Text (selectedEvidence second).Text)
