# Implementation plan

1. Add an additive typed join/planner/results API beside existing Workspace lifecycle contracts. Define the sync declaration and deterministic rejection identities. Verify with two-module, unknown-impact, missing-binding and empty-selection tests.
2. Add one `typed-sdd sync` operation using the existing Quint process and command effect edges. Check exact cached binaries, bounded typecheck/test/run/verify and the built SDD reducer adapter; invoke existing replay and correspondence on actual observations. Verify positive, mutant and repair journeys through the CLI.
3. Run the same loop before and after profile-2 author acceptance, and at verify/ship and CI. Existing migration accepts only profile 1; a future profile-2 migration must use this gate before acceptance. Regenerate seeded guidance and both tracked agent surfaces through their owner generator. Verify current and retained roots and code-only drift.
4. Qualify exact source with the #1164 acceptance matrix, required gates and independent review on one PR. Keep the PR draft until the gates pass. Source merge declares delivery continuation.
5. Coordinate the next coherent release after #1163; publish byte-identical packages and run package-only fresh and retained workspace journeys before native closure.

Initial build envelope: one CPU, 2 GiB, zero swap, five minute wall limit. Symbolic model checks require a separate exact-tool/resource preflight. Release props, lockfiles and release goldens remain under #1163 ownership until integration.
