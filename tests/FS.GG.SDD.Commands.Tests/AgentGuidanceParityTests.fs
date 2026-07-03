namespace FS.GG.SDD.Commands.Tests

open FS.GG.SDD.Commands.Internal
open Xunit

// Feature 068 / US5 / FR-012: the CLAUDE.md ↔ AGENTS.md drift guard.
//
// Reinterpreted from the spec after checking the artifacts. This repo's *root* CLAUDE.md and
// AGENTS.md are hand-authored parallel context docs with no mechanical relationship (they share
// only the title line), so a byte/structural guard on them is infeasible. The invariant the
// constitution actually cares about — "keep Claude and Codex behavior aligned", encoded as the
// `requireEquivalentClaudeAndCodexBehavior: true` policy init seeds into `.fsgg/agents.yml` — is
// the *seeded* guidance `init` writes into a scaffolded product: `WriteFile("CLAUDE.md",
// agentGuidance "Claude", …)` and `WriteFile("AGENTS.md", agentGuidance "Codex", …)`
// (Foundation.fs). Those two are produced by the single `Foundation.agentGuidance` function and
// MUST differ only in the agent-name token. This guard pins that so a future refactor splitting
// the two surfaces cannot let them silently diverge.
module AgentGuidanceParityTests =

    [<Fact>]
    let ``seeded Claude and Codex guidance differ only in the agent-name token`` () =
        let claude = Foundation.agentGuidance "Claude"
        let codex = Foundation.agentGuidance "Codex"

        // Substitute each surface's own agent name with a common placeholder; the remainder MUST be
        // byte-identical. If a future edit adds Claude- or Codex-specific body prose, this fails.
        let normalize (name: string) (text: string) = text.Replace(name, "«AGENT»")
        Assert.Equal(normalize "Claude" claude, normalize "Codex" codex)

    [<Fact>]
    let ``the guard is meaningful — the two surfaces are not already identical`` () =
        let claude = Foundation.agentGuidance "Claude"
        let codex = Foundation.agentGuidance "Codex"

        Assert.NotEqual<string>(claude, codex)
        Assert.Contains("# Claude SDD guidance", claude)
        Assert.Contains("# Codex SDD guidance", codex)
