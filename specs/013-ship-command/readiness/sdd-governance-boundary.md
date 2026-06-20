# SDD / Governance Boundary Review — Ship Command

The `fsgg-sdd ship` slice stays inside the SDD lifecycle boundary:

- The ship-ready next action is `ship.next.protectedBoundary` with a **null command pointer**;
  enforcing the protected boundary is explicitly left to Governance.
- Ship never computes or emits effective-evidence freshness, route selection, profile
  adjustment, gate selection, audit verdicts, or release gating.
- Optional Governance pointers (`.fsgg/policy.yml`, `.fsgg/capabilities.yml`,
  `.fsgg/tooling.yml`) are exposed only as advisory compatibility facts with
  `state: notEvaluated`.
- Ship works with no Governance files installed (test: `ship does not require Governance files`).

Evidence: `command-ship-tests.txt` (GovernanceBoundary assertions in ShipCommandTests),
`fsi-public-surface.txt`.
