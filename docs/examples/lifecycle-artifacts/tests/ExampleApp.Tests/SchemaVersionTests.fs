namespace ExampleApp.Tests

open ExampleApp
open Xunit

/// Proves the schema-version posture the plan declares. Cited by evidence EV010.
module SchemaVersionTests =

    /// The version is pinned, not incidental: a report that silently changes shape breaks every
    /// downstream consumer that trusted it.
    [<Fact>]
    let ``report declares its schema version`` () = Assert.Equal(1, Report.schemaVersion)

    /// The failure leg: an unsupported version is refused rather than best-effort parsed.
    [<Fact>]
    let ``an unsupported schema version is refused`` () =
        match Report.parse "{\"schemaVersion\":99}" with
        | Error message -> Assert.Contains("schemaVersion", message)
        | Ok _ -> failwith "expected an unsupported schemaVersion to be refused"
