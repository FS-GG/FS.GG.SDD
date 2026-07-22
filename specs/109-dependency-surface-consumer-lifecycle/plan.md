# Feature 109 plan

## Design

Extend the existing staged MVU handler. The first wave enumerates `work/`, the capture root, and
reads the two CPM files. A second wave reads discovered `plan.md` files. Only then does the existing
package-surface/capture read gate run over the union of committed, explicit, and authored targets.
All filesystem and package access remains represented as effects; target discovery is pure over
interpreted snapshots.

Treat a readable target without a capture as drift during `--check`. Preserve the existing
fail-open unavailable verdict. `analyze` is unchanged and continues to consume committed JSON only.

Update the consumer-facing authoring contract, ADR, and gate wording, and prove the flow against a
real restored package in the command tests.

## Verification

- Focused `DependencySurfaceCommandTests` and `FrameworkReferenceCheckTests`.
- Full command-test project.
- Repository formatting/build gates relevant to the touched files.
