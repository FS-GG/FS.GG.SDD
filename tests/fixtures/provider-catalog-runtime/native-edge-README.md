# First-host native fixture

`native-edge-fixture.c` tests the Linux-x64/glibc primitives used by the catalog
runtime. It is test code, not a shipped native helper or an alternate executor.
Use the [shared validation harness](../../../docs/validation/catalog-runtime.md)
and the [cooperative local profile](../../../docs/roadmaps/sdd-928-provider-catalog.md#cooperative-local-execution-profile--2026-10-09).
Ordinary CLI compilation and semantic tests do not depend on completing these
native checks.

`--abi-only` reports actual layout, offsets, constants and runtime exports. It
creates no child, mutates no workspace and invokes no SDK. Compare its output
with the managed interop assumptions; successful compilation alone does not
establish ABI compatibility.

`--primitives ROOT WORK_EPOCH CLEANUP_EPOCH WORK_MONOTONIC CLEANUP_MONOTONIC`
uses a fresh disposable root to exercise held-directory operations, no-replace
publication and native process ownership. Supply finite work and cleanup limits;
the fixture converts epoch limits once, clamps them to the supplied monotonic
limits and uses `CLOCK_MONOTONIC` thereafter. It does not renew cleanup authority.

The group control keeps its leader alive until it has reaped its writer. A
separate direct-exit control holds two direct children and their pidfds, reaps
the exited leader, observes that the writer still prevents EOF, then signals and
reaps the writer and observes EOF. Signal acceptance, EOF and actual reaping
are distinct observations. The fixture does not claim hostile-process containment
or protection against adversarial pathname ABA.

`--timeout-control` and `--output-control` deliberately fail. Preserve the
original failure and report cleanup separately; an expected refusal is not a
successful provider run. Future attempts that need whole-family retirement use
an independently owned teardown backend compatible with the fixture. Terminating
that boundary does not prove filesystem cleanup or successful publication.

Record the source/compiler identity, command, actual output and result for the
selected mode. Reuse unchanged evidence only within its tested scope, and rerun
changed primitives with their affected integrations. Use the same maintained
harness for managed filesystem, child, SDK and full CLI checks; do not copy
supervisors into new qualification recipes.

Historical attempts and their uncertainty remain in the programme evidence
archive. This guide neither upgrades old results nor releases old owners. Current
acceptance status belongs in the single programme summary. These low-level
fixtures cannot replace the real generic-package CLI creation and failure cases
required for C2.2 delivery.
