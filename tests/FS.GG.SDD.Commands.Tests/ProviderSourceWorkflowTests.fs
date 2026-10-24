namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Artifacts
open FS.GG.SDD.Commands
open Xunit

module ProviderSourceWorkflowTests =
    // Synthetic events exercise pure control flow. They are not native acquisition receipts.
    let digest = "sha256:" + System.String('a', 64)
    let request: ProviderSourceWorkflow.Request =
        {
            Sources =
                {
                    Schema = "fsgg.provider-source-set/1"
                    Sources =
                        [
                            {
                                Id = "outside-author"
                                Origin = ProviderSource.Explicit
                                Locator = ProviderSource.LocalDirectory "/selected/source"
                                ManifestSha256 = digest
                            }
                        ]
                }
            SourceSetSha256 = digest
            Policy =
                {
                    Schema = "fsgg.provider-source-policy/1"
                    AllowedOrigins = []
                    MaximumRedirects = 0
                    AcquisitionSeconds = 60
                    MaximumTransferBytes = 1048576L
                    MaximumExpandedBytes = 2097152L
                }
            CacheRoot = "/selected/cache"
            Offline = false
        }

    [<Fact>]
    let ``offline cache miss never emits acquisition`` () =
        let initial, first = ProviderSourceWorkflow.init { request with Offline = true }
        Assert.Equal<ProviderSourceWorkflow.Effect list>([ ProviderSourceWorkflow.ReadExactCache ], first)
        let refused, effects = ProviderSourceWorkflow.update (ProviderSourceWorkflow.CacheObserved false) initial
        Assert.DoesNotContain(ProviderSourceWorkflow.AcquireSelectedData, effects)
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome refused)

    [<Fact>]
    let ``verified data is not prepared until original operation retirement succeeds`` () =
        let initial, _ = ProviderSourceWorkflow.init request
        let cached, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.CacheObserved true) initial
        let verified, _ = ProviderSourceWorkflow.update ProviderSourceWorkflow.ValidationCompleted cached
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome verified)
        let unknown, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.RetirementObserved false) verified
        Assert.Equal(ProviderSourceWorkflow.CleanupUnknown, ProviderSourceWorkflow.outcome unknown)

    [<Fact>]
    let ``later collection and success events cannot replace first failure`` () =
        let initial, _ = ProviderSourceWorkflow.init request
        let failure: Fsgg.ProviderCatalog.Diagnostic =
            { Code = "source.digestMismatch"; Path = "$.members[0]"; Message = "Original mismatch" }
        let failed, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.FailureObserved [ failure ]) initial
        let late, _ = ProviderSourceWorkflow.update ProviderSourceWorkflow.CachePublished failed
        let terminal, _ = ProviderSourceWorkflow.update (ProviderSourceWorkflow.RetirementObserved true) late
        Assert.NotEqual(ProviderSourceWorkflow.Prepared, ProviderSourceWorkflow.outcome terminal)
        Assert.Equal(failure, ProviderSourceWorkflow.diagnostics terminal |> List.head)

    [<Fact>]
    let ``invalid source declarations have no acquisition or cache effects`` () =
        let invalid = { request with CacheRoot = "relative/cache" }
        let refused, effects = ProviderSourceWorkflow.init invalid
        Assert.Empty effects
        Assert.Equal(ProviderSourceWorkflow.Refused, ProviderSourceWorkflow.outcome refused)

    [<Fact>]
    let ``effect preparation binds raw bytes and performs no path acquisition`` () =
        let unwrap = function
            | Ok value -> value
            | Error errors -> failwithf "%A" errors

        let encode value = System.Text.Encoding.UTF8.GetBytes(value: string)
        let sourceBytes = ProviderSource.serializeSet request.Sources |> unwrap |> encode
        let policyBytes = ProviderSource.serializePolicy request.Policy |> unwrap |> encode
        let actual: ProviderSourceEffects.Request =
            {
                SourceSetBytes = sourceBytes
                ExpectedSourceSetSha256 = ProviderSource.sha256 sourceBytes
                PolicyBytes = policyBytes
                ExpectedPolicySha256 = ProviderSource.sha256 policyBytes
                CacheRoot = "/this-path-does-not-exist/source-cache"
                Offline = true
            }

        let operation = ProviderSourceEffects.prepare actual System.Threading.CancellationToken.None |> unwrap
        let observation = ProviderSourceEffects.observe operation
        Assert.Equal(ProviderSourceWorkflow.Preparing, observation.Phase)
        Assert.Equal(ProviderSourceWorkflow.Pending, observation.Outcome)
        Assert.Equal<Result<unit, Fsgg.ProviderCatalog.Diagnostic list>>(Ok(), ProviderSourceEffects.release operation)

        match ProviderSourceEffects.prepare { actual with PolicyBytes = sourceBytes } System.Threading.CancellationToken.None with
        | Error errors -> Assert.Contains(errors, fun error -> error.Code = "source.rawDigestMismatch")
        | Ok _ -> failwith "Caller metadata did not bind the actual policy bytes."
