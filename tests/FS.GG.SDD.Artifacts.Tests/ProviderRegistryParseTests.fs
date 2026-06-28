namespace FS.GG.SDD.Artifacts.Tests

open Fsgg.Provider
open FS.GG.SDD.Artifacts
open FS.GG.SDD.Artifacts.Config
open Xunit

/// Feature 038 US2: the re-typed `parseProviderRegistry` reads the canonical contract's
/// extended optional fields — declared `build`/`test`/`run`/`verify` commands and
/// `nameParameter` — from `.fsgg/providers.yml`, with behavior-preserving defaults.
module ProviderRegistryParseTests =
    let private snapshot text : FileSnapshot = { Path = ".fsgg/providers.yml"; Text = text }

    let private one result =
        match result with
        | Ok [ descriptor ] -> descriptor
        | other -> failwith $"Expected exactly one descriptor, got {other}."

    // T010 (SC-003, FR-003/FR-004): a registry declaring `build` and `run` (executable +
    // arguments) and a `nameParameter` carries those values; the undeclared `test`/`verify`
    // stay `None`.
    [<Fact>]
    let ``parseProviderRegistry reads declared build, run, and nameParameter`` () =
        let registry =
            """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
    build:
      executable: dotnet
      arguments: [build, -c, Release]
    run:
      executable: dotnet
      arguments: [run, --no-build]
    nameParameter: projectName
"""

        let descriptor = one (parseProviderRegistry (snapshot registry))
        Assert.Equal(Some { Executable = "dotnet"; Arguments = [ "build"; "-c"; "Release" ] }, descriptor.Build)
        Assert.Equal(Some { Executable = "dotnet"; Arguments = [ "run"; "--no-build" ] }, descriptor.Run)
        Assert.Equal<DeclaredCommand option>(None, descriptor.Test)
        Assert.Equal<DeclaredCommand option>(None, descriptor.Verify)
        Assert.Equal("projectName", descriptor.NameParameter)

    // T011 (FR-006): a today-shape entry (no extended keys) parses to all command fields
    // `None` and the default `NameParameter`, so the scaffold path is byte-unchanged.
    [<Fact>]
    let ``parseProviderRegistry defaults extended fields for a today-shape entry`` () =
        let registry =
            """schemaVersion: 1
providers:
  - name: fixture
    contractVersion: "1.0.0"
    templateId: fsgg-fixture-app
    source: /abs/path/ok
"""

        let descriptor = one (parseProviderRegistry (snapshot registry))
        Assert.Equal<DeclaredCommand option>(None, descriptor.Build)
        Assert.Equal<DeclaredCommand option>(None, descriptor.Test)
        Assert.Equal<DeclaredCommand option>(None, descriptor.Run)
        Assert.Equal<DeclaredCommand option>(None, descriptor.Verify)
        Assert.Equal("name", descriptor.NameParameter)
        Assert.Equal("name", resolveNameParameter descriptor)

    // T012 (FR-005): a declared command whose `executable` is blank/whitespace is treated as
    // "not declared" (`None`), never a launchable empty executable; a blank `nameParameter`
    // falls back to the default.
    [<Fact>]
    let ``parseProviderRegistry treats a blank executable and blank nameParameter as defaults`` () =
        let registry =
            "schemaVersion: 1\n"
            + "providers:\n"
            + "  - name: fixture\n"
            + "    contractVersion: \"1.0.0\"\n"
            + "    templateId: fsgg-fixture-app\n"
            + "    source: /abs/path/ok\n"
            + "    build:\n"
            + "      executable: \"   \"\n"
            + "      arguments: [build]\n"
            + "    nameParameter: \"   \"\n"

        let descriptor = one (parseProviderRegistry (snapshot registry))
        Assert.Equal<DeclaredCommand option>(None, descriptor.Build)
        Assert.Equal("name", resolveNameParameter descriptor)
