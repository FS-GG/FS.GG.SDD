# SDD / Governance Boundary Review — `fsgg-sdd refresh`

`refresh` is a cross-cutting SDD-owned generated-view command
(`nextLifecycleCommand Refresh = None`). It authors no source artifact; its only
writes are generated views under their configured generated roots
(`work-model.json`, `agent-commands/<target>/`, and the new `summary.md`).

Boundary findings (verified by `GovernanceBoundaryCommandTests` and the refresh
tests):

- **Refreshed views and `summary.md` are not a second source of truth.** The
  authored lifecycle artifacts and the normalized work model remain
  authoritative. `summary.md` is rendered strictly from the structured readiness
  data and carries a generated marker with source digests (Constitution VII,
  FR-006, FR-013).
- **No Governance behavior.** Refresh computes no effective-evidence freshness,
  route selection, profile adjustment, gate selection, protected-boundary
  enforcement (including no stale-view blocking at a protected boundary), audit
  verdict, or release decision. The report's `governanceCompatibility[]` facts
  are advisory and carry `state = "notEvaluated"` only
  (test: `refresh succeeds without governance installed`).
- **Works with Governance absent.** No `.fsgg/policy.yml` is required or read as a
  derivation input; refresh succeeds on a project with no Governance installed.
- **Authored sources preserved.** Authored lifecycle artifacts, `.fsgg/*.yml`, and
  the hand-owned `CLAUDE.md`/`AGENTS.md` are never created, updated, reordered,
  normalized, or removed (test: `refresh preserves authored sources and
  hand-owned guidance files`). They appear as `Preserve`/`NoChange`.

## Scope note (honest deviation)

The structured downstream views `analysis.json`, `verify.json`, and `ship.json`
are **currency-reported** (current/stale/missing/malformed/blocked) rather than
destructively re-run by refresh. Re-running those generators out of lifecycle
order invalidates the cryptographic evidence-freshness binding the existing
`verify`/`ship` generators enforce against the prior work model (a pre-existing
property of the lifecycle generators, independent of this feature — reproduced by
re-running `analyze`/`verify` standalone on a completed project). Refresh
therefore regenerates the views whose generators converge (work model, agent
guidance, summary) and reports the currency of the rest, pointing stale/blocked
downstream views back at the responsible lifecycle command. See the feature
plan's Implementation Notes for detail.
