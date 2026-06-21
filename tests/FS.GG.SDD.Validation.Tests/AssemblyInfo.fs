namespace FS.GG.SDD.Validation.Tests

open Xunit

// The validation harness builds real projects in temp directories and perturbs
// process-global host facts (locale / time zone) to prove host-variance determinism.
// Running these in-process tests in parallel would let one test's perturbation bleed
// into another's build, so collections run serially within this assembly. (The real
// `fsgg-sdd validate` runs as its own isolated process, where this does not arise.)
[<assembly: CollectionBehavior(DisableTestParallelization = true)>]
do ()
