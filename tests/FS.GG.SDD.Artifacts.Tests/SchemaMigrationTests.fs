namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

module SchemaMigrationTests =
    [<Fact>]
    let ``SchemaMigration current schema is non blocking`` () =
        let model = TestSupport.normalizedModel "valid-work-item"

        Assert.All(model.Sources, fun source -> Assert.Equal("current", source.SchemaStatus))
        Assert.Empty(WorkModel.blockingDiagnostics model)

    [<Fact>]
    let ``SchemaMigration deprecated schema warns without blocking`` () =
        let model = TestSupport.normalizedModel "deprecated-schema-version"

        TestSupport.assertDiagnostic "deprecatedSchemaVersion" model
        Assert.Empty(WorkModel.blockingDiagnostics model)
        Assert.Contains(model.Sources, fun source -> source.SchemaStatus = "deprecated")

    [<Fact>]
    let ``SchemaMigration unsupported schema blocks`` () =
        let model = TestSupport.normalizedModel "unsupported-schema-version"

        TestSupport.assertDiagnostic "unsupportedSchemaVersion" model
        Assert.NotEmpty(WorkModel.blockingDiagnostics model)

    [<Fact>]
    let ``SchemaMigration future schema blocks`` () =
        let model = TestSupport.normalizedModel "future-schema-version"

        TestSupport.assertDiagnostic "futureSchemaVersion" model
        Assert.NotEmpty(WorkModel.blockingDiagnostics model)

    [<Fact>]
    let ``SchemaMigration malformed schema blocks`` () =
        let model = TestSupport.normalizedModel "malformed-schema-version"

        TestSupport.assertDiagnostic "malformedSchemaVersion" model
        Assert.NotEmpty(WorkModel.blockingDiagnostics model)
