# Plan: Joint Work-Model Output and Capture Preview

1. Prepare exact proposed JSON from selected snapshots and generator version before any capture.
2. Obtain and verify two source sets using the read-only #1017 physical reader; compare exact raw bytes by path.
3. Return only the verified digest/path preview, with distinct first/second and proposal refusals.
4. Exercise independent stale-output, changed-source, BOM-only, and stable controls, then run the Commands suite and warning-clean build.

This plan adds no generation output, authored-source staging, package publication, receiver pin, or live effect.
