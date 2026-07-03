namespace FS.GG.Contracts.Tests

open FS.GG.SDD.TestShared

module TestSupport =
    // Delegates to the shared primitive (feature 067 / FR-010).
    let findRepoRoot = TestShared.findRepoRoot
    let repoRoot = TestShared.repoRoot
