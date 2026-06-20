# SDD / Governance Boundary Review — `fsgg-sdd agents`

## Generated guidance is not a second source of truth (Constitution VII)

- The per-target `guidance.json` manifest is derived **only** from
  `readiness/<id>/work-model.json` via `WorkModel.deriveGuidanceModel`. The
  hand-owned `CLAUDE.md` / `AGENTS.md` files are never read as derivation inputs.
- Every manifest carries `generated: true`, the work-model source path + digest,
  the generator identity, and a `behaviorModelDigest`. A reviewer can confirm the
  guidance was derived from the lifecycle model (`SC-008`).
- `commands.md` / `skills.md` are pure projections of the manifest and add no
  facts; each states it is generated and points back to `guidance.json`.
- Authored lifecycle artifacts, `.fsgg/agents.yml`, `CLAUDE.md`, and `AGENTS.md`
  are never created, updated, reordered, normalized, or removed by `agents`
  (verified byte-identical in
  `AgentsCommandTests.agents preserves authored sources...`). Generated guidance
  writes use the `GeneratedView` overwrite policy so refreshes are safe, while
  the hand-owned guidance-target files remain protected by the
  `AgentGuidanceTarget` no-overwrite rule used by `init`.

## No Governance behavior is implemented (FR-022..FR-024)

- `agents` runs fully without `.fsgg/policy.yml`, `.fsgg/capabilities.yml`, or
  `.fsgg/tooling.yml` present (verified in
  `agents succeeds without governance installed`).
- The report exposes Governance pointers only as advisory
  `governanceCompatibility` facts with `state: notEvaluated`. No
  effective-evidence freshness, route, profile, gate, audit, protected-boundary
  enforcement, or release verdict is computed.
- `nextLifecycleCommand Agents = None`: `agents` is cross-cutting and does not
  alter the `charter -> ship` chain.
