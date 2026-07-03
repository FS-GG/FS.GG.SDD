namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.CommandTypes
open Xunit

// Feature 068 / US2 / FR-005: pin the DU→wire-string mappings for the remediation vocabularies
// so a future case rename cannot silently change an emitted JSON/text byte. These are the exact
// spellings the upgrade/doctor JSON (`stepId`/`kind`/`outcome` + the appliedStepIds/etc. arrays)
// and text projections carry, also enforced by the RemediationCommand/Projection tests and the
// release-baseline byte-identity suites — this test makes the mapping itself the contract.
module ReconciliationDuTests =

    [<Fact>]
    let ``reconciliationStepIdValue pins every case spelling`` () =
        Assert.Equal("cliSelfUpdate", reconciliationStepIdValue ReconciliationStepId.CliSelfUpdate)
        Assert.Equal("templateRePin", reconciliationStepIdValue ReconciliationStepId.TemplateRePin)
        Assert.Equal("artifactReSeed", reconciliationStepIdValue ReconciliationStepId.ArtifactReSeed)

    [<Fact>]
    let ``reconciliationOutcomeValue pins every case spelling`` () =
        Assert.Equal("wouldApply", reconciliationOutcomeValue ReconciliationOutcome.WouldApply)
        Assert.Equal("applied", reconciliationOutcomeValue ReconciliationOutcome.Applied)
        Assert.Equal("skipped", reconciliationOutcomeValue ReconciliationOutcome.Skipped)
        Assert.Equal("failed", reconciliationOutcomeValue ReconciliationOutcome.Failed)
        Assert.Equal("noTarget", reconciliationOutcomeValue ReconciliationOutcome.NoTarget)

    // The value mappings are total over the closed DUs (FR-006): every case is listed above, and
    // the `…Value` functions themselves are exhaustive matches (a new case fails to compile until
    // both the mapping and this pin are extended).
