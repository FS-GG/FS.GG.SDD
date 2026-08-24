namespace FS.GG.SDD.Artifacts.Tests

open System
open System.IO
open FS.GG.SDD.Artifacts.TypedSpecifications
open Xunit

module TypedSpecificationKernelTests =
    let private id value =
        match SpecificationId.create value with
        | Ok identifier -> identifier
        | Error message -> failwith message

    let private scope statement =
        { Id = id "SB-001"
          Statement = statement }

    let private story =
        { Id = id "US-001"
          Priority = "P1"
          Statement = "An author can compile a typed requirement." }

    let private requirement acceptance evidence =
        { Id = id "FR-001"
          Statement = "The compiler MUST retain stable references."
          AcceptanceIds = acceptance
          EvidenceObligationIds = evidence }

    let private acceptance =
        { Id = id "AC-001"
          StoryIds = [ id "US-001" ]
          RequirementIds = [ id "FR-001" ]
          Statement = "Given a model, compilation succeeds." }

    let private ambiguity =
        { Id = id "AMB-001"
          Question = "Which package owns the model?"
          State = Resolved
          Decision = Some "FS.GG.SDD.Artifacts owns it." }

    let private directExtension () =
        { UserValue = "Authors share one typed specification."
          Scope = [ scope "Typed requirements only." ]
          NonGoals = []
          Stories = [ story ]
          Requirements = [ requirement [ id "AC-001" ] [ id "EV001" ] ]
          Acceptance = [ acceptance ]
          Ambiguities = [ ambiguity ]
          PublicImpact = [ "Adds a typed contract." ]
          LifecycleNotes = [ "Publish before adoption." ] }

    let private draftExtension () =
        RequirementsDraft.empty
        |> RequirementsDraft.addAmbiguity ambiguity
        |> RequirementsDraft.addLifecycleNote "Publish before adoption."
        |> RequirementsDraft.addPublicImpact "Adds a typed contract."
        |> RequirementsDraft.addAcceptance acceptance
        |> RequirementsDraft.addRequirement (requirement [ id "AC-001" ] [ id "EV001" ])
        |> RequirementsDraft.addStory story
        |> RequirementsDraft.addScope (scope "Typed requirements only.")
        |> RequirementsDraft.withUserValue "Authors share one typed specification."
        |> RequirementsDraft.build

    let private obligation idText kind description =
        { Id = id idText
          Kind = kind
          Description = description }

    let private model extension =
        { Identity = id "SPEC-001"
          SchemaVersion = 1
          Provenance =
            { Agent = "tern-91d9"
              Session = "session-1"
              SourcePath = "work/example/spec.md"
              SourceRevision = String.replicate 64 "a"
              AuthoredAtUtc = "2026-08-24T12:00:00Z" }
          Intent = "Prove the public contract."
          EvidenceObligations = [ obligation "EV001" "test" "Run the semantic suite." ]
          Extension = extension }

    let private diagnostics (result: Result<'value, SpecificationDiagnostic list>) =
        match result with
        | Ok _ -> failwith "expected diagnostics"
        | Error findings -> findings

    let private expectOk (result: Result<'value, SpecificationDiagnostic list>) =
        match result with
        | Ok value -> value
        | Error findings -> failwithf "expected success, got %A" findings

    let private replaceFirst (oldValue: string) (newValue: string) (text: string) =
        let index = text.IndexOf(oldValue, StringComparison.Ordinal)

        if index < 0 then
            failwithf "expected '%s' in test input" oldValue

        text.Remove(index, oldValue.Length).Insert(index, newValue)

    let private fixturePath name =
        Path.Combine(TestSupport.repoRoot, "tests", "fixtures", "typed-specifications", name)

    [<Fact>]
    let ``identity grammar is stable and rejects non-canonical text`` () =
        Assert.Equal("SPEC-001", id "SPEC-001" |> SpecificationId.value)
        Assert.True(SpecificationId.create "spec-001" |> Result.isError)
        Assert.True(SpecificationId.create "A--B" |> Result.isError)

    [<Fact>]
    let ``direct and functional authoring normalize to byte-identical models`` () =
        let direct = model (directExtension ())
        let drafted = model (draftExtension ())

        let directBytes =
            SpecificationCompiler.normalize RequirementsExtension.contract direct

        let draftedBytes =
            SpecificationCompiler.normalize RequirementsExtension.contract drafted

        Assert.Equal<byte array>(expectOk directBytes, expectOk draftedBytes)

        Assert.Equal(
            SpecificationCompiler.fingerprint RequirementsExtension.contract direct,
            SpecificationCompiler.fingerprint RequirementsExtension.contract drafted
        )

    [<Fact>]
    let ``validation accumulates duplicate unresolved and malformed findings deterministically`` () =
        let invalidExtension =
            { directExtension () with
                UserValue = ""
                Scope = [ scope "one"; scope "two" ]
                Requirements = [ requirement [ id "AC-999" ] [ id "EV999" ] ] }

        let invalidModel =
            { model invalidExtension with
                SchemaVersion = 2
                Provenance =
                    { (model invalidExtension).Provenance with
                        SourcePath = "" } }

        let findings =
            SpecificationCompiler.validate RequirementsExtension.contract invalidModel

        let codes = findings |> List.map _.Code

        Assert.Contains("SPEC-SCHEMA-UNSUPPORTED", codes)
        Assert.Contains("SPEC-PROVENANCE-SOURCE", codes)
        Assert.Contains("REQ-USER-VALUE-REQUIRED", codes)
        Assert.Contains("REQ-ID-DUPLICATE", codes)
        Assert.Contains("REQ-ACCEPTANCE-UNRESOLVED", codes)
        Assert.Contains("REQ-EVIDENCE-UNRESOLVED", codes)
        Assert.True((findings |> List.sortBy (fun item -> item.Path, item.Code, item.Message)) = findings)

    [<Fact>]
    let ``schema-v1 codec is deterministic and round-trips every authored field`` () =
        let expected = model (directExtension ())

        let json =
            SpecificationCodec.serialize RequirementsExtension.contract expected |> expectOk

        let actual =
            SpecificationCodec.deserialize RequirementsExtension.contract json |> expectOk

        Assert.Equal(expected, actual)
        Assert.Equal(json, SpecificationCodec.serialize RequirementsExtension.contract expected |> expectOk)
        Assert.EndsWith("\n", json)

    [<Fact>]
    let ``codec refuses unknown envelope fields and unsupported versions distinctly`` () =
        let json =
            SpecificationCodec.serialize RequirementsExtension.contract (model (directExtension ()))
            |> expectOk

        let unknown = json.Replace("\"identity\":", "\"unknown\":true,\n  \"identity\":")
        let unsupported = json |> replaceFirst "\"schemaVersion\": 1" "\"schemaVersion\": 2"

        Assert.Contains(
            diagnostics (SpecificationCodec.deserialize RequirementsExtension.contract unknown),
            fun item -> item.Code = "SPEC-CODEC-UNKNOWN-FIELD"
        )

        Assert.Contains(
            diagnostics (SpecificationCodec.deserialize RequirementsExtension.contract unsupported),
            fun item -> item.Code = "SPEC-SCHEMA-UNSUPPORTED"
        )

    [<Fact>]
    let ``semantic diff ignores authoring metadata and reports extension changes`` () =
        let before = model (directExtension ())

        let authoringOnly =
            { before with
                Intent = "A revised explanation."
                Provenance =
                    { before.Provenance with
                        Agent = "another-agent"
                        Session = "session-2"
                        AuthoredAtUtc = "2026-08-24T13:00:00Z" } }

        let changed =
            model
                { directExtension () with
                    Scope = [ scope "A changed semantic scope." ] }

        Assert.Equal(
            Ok Equivalent,
            SpecificationCompiler.semanticDiff RequirementsExtension.contract before authoringOnly
        )

        match SpecificationCompiler.semanticDiff RequirementsExtension.contract before changed with
        | Ok(Changed changes) -> Assert.Contains(changes, fun change -> change.Path = "/extension")
        | other -> Assert.Fail $"expected a semantic extension change, got {other}"

    [<Fact>]
    let ``projections are deterministic current and detect stale direct edit missing and unreadable observations`` () =
        let source = model (directExtension ())

        let projection =
            SpecificationProjection.generate RequirementsExtension.contract source
            |> expectOk

        Assert.Equal(
            projection,
            SpecificationProjection.generate RequirementsExtension.contract source
            |> expectOk
        )

        Assert.Empty(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source (Content projection.Markdown)
        )

        Assert.Empty(
            SpecificationProjection.validateJson RequirementsExtension.contract source (Content projection.Json)
        )

        Assert.Contains(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source Missing,
            fun item -> item.Code = "SPEC-PROJECTION-MISSING"
        )

        Assert.Contains(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source (Unreadable "denied"),
            fun item -> item.Code = "SPEC-PROJECTION-UNREADABLE"
        )

        let stale =
            projection.Markdown.Replace(projection.SourceFingerprint, String.replicate 64 "0")

        let edited =
            projection.Markdown.Replace("Authors share one typed specification.", "Edited projection text.")

        let appended = projection.Markdown + "\n\n"

        let editedJson =
            projection.Json
            |> replaceFirst "\"agent\": \"tern-91d9\"" "\"agent\": \"edited-agent\""
            |> replaceFirst "\"intent\": \"Prove the public contract.\"" "\"intent\": \"Edited intent.\""

        Assert.Contains(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source (Content stale),
            fun item -> item.Code = "SPEC-PROJECTION-STALE"
        )

        Assert.Contains(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source (Content edited),
            fun item -> item.Code = "SPEC-PROJECTION-DIRECT-EDIT"
        )

        Assert.Contains(
            SpecificationProjection.validateMarkdown RequirementsExtension.contract source (Content appended),
            fun item -> item.Code = "SPEC-PROJECTION-DIRECT-EDIT" && item.Path = "/projection/markdown"
        )

        Assert.Contains(
            SpecificationProjection.validateJson RequirementsExtension.contract source (Content editedJson),
            fun item -> item.Code = "SPEC-PROJECTION-DIRECT-EDIT" && item.Path = "/projection/json"
        )

    [<Fact>]
    let ``evidence validation distinguishes missing duplicate unknown and kind mismatch`` () =
        let obligations =
            [ obligation "EV001" "test" "Semantic tests"
              obligation "EV002" "review" "Independent review"
              obligation "EV003" "package" "Package consumption" ]

        let receipts =
            [ { ObligationId = id "EV001"
                Kind = "test"
                EvidenceRef = "run:1" }
              { ObligationId = id "EV001"
                Kind = "test"
                EvidenceRef = "run:2" }
              { ObligationId = id "EV002"
                Kind = "test"
                EvidenceRef = "run:3" }
              { ObligationId = id "EV999"
                Kind = "test"
                EvidenceRef = "run:4" } ]

        let result = SpecificationEvidence.validate obligations receipts
        let codes = result.Diagnostics |> List.map _.Code
        Assert.Contains("SPEC-EVIDENCE-DUPLICATE", codes)
        Assert.Contains("SPEC-EVIDENCE-KIND", codes)
        Assert.Contains("SPEC-EVIDENCE-UNKNOWN", codes)
        Assert.Contains("SPEC-EVIDENCE-MISSING", codes)

    [<Fact>]
    let ``current Standard SDD Markdown migrates losslessly without a write surface`` () =
        let fixture = fixturePath "supported-spec.md"

        match File.ReadAllText fixture |> RequirementsMigration.analyzeMarkdown with
        | Migrated extension ->
            Assert.Equal("Authors can share a typed model.", extension.UserValue)
            Assert.Single extension.Requirements |> ignore
            Assert.Empty(RequirementsExtension.validate extension)
        | other -> Assert.Fail $"expected migration, got {other}"

        let currentSpec =
            Path.Combine(TestSupport.repoRoot, "work", "typed-specification-kernel-p2", "spec.md")
            |> File.ReadAllText

        match RequirementsMigration.analyzeMarkdown currentSpec with
        | Migrated extension ->
            Assert.Equal(6, extension.Scope.Length + extension.NonGoals.Length)
            Assert.Equal(4, extension.Stories.Length)
            Assert.Equal(17, extension.Requirements.Length)
            Assert.Empty(RequirementsExtension.validate extension)
        | other -> Assert.Fail $"expected wrapped current SDD specification to migrate, got {other}"

    [<Fact>]
    let ``migration preserves resolved decisions and never returns an invalid migrated model`` () =
        let supported = fixturePath "supported-spec.md" |> File.ReadAllText

        let resolved =
            supported.Replace(
                "No material ambiguities recorded.",
                "- AMB-001 resolved: Which package owns the model? — FS.GG.SDD.Artifacts owns it."
            )

        match RequirementsMigration.analyzeMarkdown resolved with
        | Migrated extension ->
            let ambiguity = Assert.Single extension.Ambiguities
            Assert.Equal(Resolved, ambiguity.State)
            Assert.Equal(Some "FS.GG.SDD.Artifacts owns it.", ambiguity.Decision)
            Assert.Empty(RequirementsExtension.validate extension)
        | other -> Assert.Fail $"expected resolved decision migration, got {other}"

        let missingDecision =
            supported.Replace("No material ambiguities recorded.", "- AMB-001 resolved: Which package owns the model?")

        match RequirementsMigration.analyzeMarkdown missingDecision with
        | Unsupported findings -> Assert.Contains(findings, fun item -> item.Code = "REQ-MIGRATION-AMBIGUITY-DECISION")
        | other -> Assert.Fail $"expected invalid resolved ambiguity to be unsupported, got {other}"

    [<Fact>]
    let ``migration keeps unresolved references ambiguous and unknown semantic headings unsupported`` () =
        let fixture name =
            fixturePath name |> File.ReadAllText |> RequirementsMigration.analyzeMarkdown

        match fixture "ambiguous-spec.md" with
        | Ambiguous findings ->
            Assert.Contains(findings, fun item -> item.Reason = UnresolvedReference && item.Location.Line > 0)
        | other -> Assert.Fail $"expected ambiguity, got {other}"

        match fixture "unsupported-spec.md" with
        | Unsupported findings ->
            Assert.Contains(findings, fun item -> item.Reason = UnknownSemanticHeading && item.Location.Line > 0)
        | other -> Assert.Fail $"expected unsupported content, got {other}"

    [<Fact>]
    let ``public kernel surface does not expose SIR rule or coordination semantics`` () =
        let names =
            typeof<SpecificationModel<RequirementsExtension>>.Assembly.GetExportedTypes()
            |> Array.choose (fun value -> value.FullName |> Option.ofObj)
            |> String.concat "\n"

        Assert.DoesNotContain("SIR", names, StringComparison.OrdinalIgnoreCase)
        Assert.DoesNotContain("RuleDefinition", names, StringComparison.Ordinal)
        Assert.DoesNotContain("RuleSpecification", names, StringComparison.Ordinal)
        Assert.DoesNotContain("FS.GG.Coord", names, StringComparison.Ordinal)
