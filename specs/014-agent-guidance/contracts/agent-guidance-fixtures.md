# Contract: Agent Guidance Fixtures

Owner: `FS.GG.SDD` (command tests). Status: new in feature `014-agent-guidance`.
Fixture root: `tests/fixtures/lifecycle-commands/`.

Each fixture is an initialized SDD project tree (`.fsgg/`, `work/<id>/`,
`readiness/<id>/`) with the inputs needed to exercise one scenario. Valid
fixtures assert the generated guidance manifests, Markdown projections, report
JSON, and next action; blocked fixtures assert authored content is unchanged and
at least one actionable diagnostic is present.

## Valid fixtures (expect success / current / dry-run proposal)

| Fixture | Scenario |
|---|---|
| `agents-create` | Valid config + current work model, no prior guidance → generate manifests + Markdown for all targets; `generated-current`. |
| `agents-rerun-current` | Guidance already current → `NoChange`, no writes planned. |
| `agents-preserves-authored` | Authored lifecycle artifacts, `.fsgg/agents.yml`, `CLAUDE.md`, `AGENTS.md` all `Preserve`; only generated roots change. |
| `agents-refreshes-stale` | Work model changed since last generation → targets refreshed; report records the refresh. |
| `agents-claude-only` | Config declares only the `claude` target → one target generated. |
| `agents-codex-only` | Config declares only the `codex` target → one target generated. |
| `agents-claude-and-codex` | Both targets generated from the same shared model; equal `behaviorModelDigest`. |
| `dry-run` | `--dry-run` reports proposed generated changes; 0 files written. |
| `deterministic-report` | Three identical runs → byte-identical manifests + report JSON. |
| `text-projection` | `--text` projection contains the same facts as JSON, no extras. |
| `governance-boundary` | Optional Governance files absent or present-as-advisory; SDD-only generation succeeds without interpreting Governance. |

## Blocked fixtures (expect block + actionable diagnostic, authored content unchanged)

| Fixture | Scenario |
|---|---|
| `outside-project` | Run outside an initialized SDD project. |
| `missing-agents-config` | `.fsgg/agents.yml` absent. |
| `malformed-agents-config` | `.fsgg/agents.yml` malformed or unsupported schema. |
| `no-targets` | `.fsgg/agents.yml` declares an empty `agents:` list. |
| `missing-work-model` | `readiness/<id>/work-model.json` absent. |
| `stale-work-model` | Work model stale vs current sources. |
| `malformed-work-model` | Work model malformed / unparsable. |
| `malformed-work-id` | Selected work id violates the `WorkId` contract. |
| `duplicate-work-id` | Duplicate logical work id under `work/`. |
| `unknown-source-reference` | Work model references an unknown requirement/decision. |
| `stale-generated-guidance` | Existing target manifest stale vs current work model. |
| `malformed-generated-guidance` | Existing target manifest malformed. |
| `claude-codex-divergence` | `requireEquivalentClaudeAndCodexBehavior: true` and an existing target manifest's behavior model diverges from the shared model. |

## Cross-fixture assertions

- Generated manifests are deterministic and carry source digests, schema version,
  generator identity, and the generated marker.
- Markdown projections are derived from the manifest and add no facts.
- Every blocking fixture leaves authored lifecycle artifacts, `.fsgg/agents.yml`,
  `CLAUDE.md`, and `AGENTS.md` byte-unchanged.
- No fixture run requires Governance to be installed.
- At least 3 fixtures exercise real CLI smoke paths (JSON, dry-run, text) through
  the console host.
