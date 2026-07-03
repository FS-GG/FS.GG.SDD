namespace FS.GG.Contracts.Tests

open System.IO
open Xunit

// Feature 068 / US6 / FR-... : the CLAUDE.md ↔ AGENTS.md drift guard. The two agent-context
// surfaces carry one canonical doctrine (CLAUDE.md is the authored source; AGENTS.md is a
// byte-identical mirror), so a change to one that is not mirrored to the other fails here. This
// is the constitution's "keep Claude and Codex behavior aligned" made enforceable (claude ≡ codex
// for the context docs).
module AgentSurfaceDriftTests =

    let private read name =
        File.ReadAllText(Path.Combine(TestSupport.repoRoot, name))

    [<Fact>]
    let ``CLAUDE.md and AGENTS.md are byte-identical`` () =
        let claude = read "CLAUDE.md"
        let agents = read "AGENTS.md"

        let message =
            "CLAUDE.md and AGENTS.md have drifted. They must carry byte-identical content "
            + "(CLAUDE.md is the authored source; mirror it into AGENTS.md). Re-sync AGENTS.md, then re-run."

        Assert.True((claude = agents), message)
