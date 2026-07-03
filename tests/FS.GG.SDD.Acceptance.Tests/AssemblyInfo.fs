namespace FS.GG.SDD.Acceptance.Tests

open Xunit

// Feature 067 / FR-002: `CompositionAcceptanceTests` nulls `FSGG_SDD_ACCEPTANCE_REGISTRY` and
// empties process-global `PATH` to force the provider-unavailable path. Those mutations are
// process-global, so running acceptance collections in parallel — as happens on the scheduled
// run where the registry is set — lets one class's mutation bleed into another's process spawn.
// Serialize the assembly (mirrors FS.GG.SDD.Validation.Tests). The offline inner loop is
// unaffected: the mutating facts self-skip when the registry is unset.
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()
