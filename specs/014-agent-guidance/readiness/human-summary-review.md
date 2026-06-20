# Human-Readable Summary Review — `fsgg-sdd agents --text`

The text projection (`CommandRendering.renderText`) renders only facts present in
the authoritative JSON report (`FR-018`). Captured output is in
`cli-text-smoke.txt`. Reviewed facts surfaced:

- `workId`, `agentsReadiness`, `agentsDisposition` (e.g. `generated-current`)
- one `agentsTarget:` line per generated target (claude, codex)
- one `agentsGeneratedRoot:` line per generated root
- `agentsEquivalenceRequired`, plus `agentsDivergentTarget:` lines when divergent
- `agentsReadyFindings` / `agentsAdvisory` / `agentsWarnings` / `agentsBlocking`
- `agentsGeneratedViewState`
- `nextAction`

No fact appears in the text projection that is absent from the JSON report; the
counts and disposition match the `agentGuidance` object byte-for-byte in meaning.
