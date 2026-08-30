---
title: Evidence authority 1.5.1
category: SDD
categoryindex: 6
index: 24
description: Stable patch release carrying exact-candidate local evidence authority.
---

# Evidence authority 1.5.1

`FS.GG.SDD.Artifacts` and `FS.GG.SDD.Cli` 1.5.1 form one stable release set.
The CLI continues to embed the matching Commands and Validation assemblies. The
independently versioned `FS.GG.Contracts` package remains 7.5.2.

This patch makes local evidence authority candidate-owned: ignored, untracked,
staged-only, missing, or Git-unverifiable local evidence cannot authorize verify
or ship readiness. Tracked candidate artifacts remain supported, and explicit
durable external URI receipts remain the non-local authority form.

The release changes no persisted schema and removes no public API. Consumers
update their exact `FS.GG.SDD.Cli` pin from 1.5.0 to 1.5.1.

Release qualification preserves the seeding non-vacuity assertion. Protected-main
run 33299790063 failed it once on attempt 1, then passed the complete gate on the
identical commit in attempt 2; repeated isolated-process qualification is recorded
in `work/944-sdd-151-evidence-authority-release/evidence.yml`.

Publication, tags, and feed verification occur only after the exact candidate is
independently accepted. Both feeds receive the same pre-qualified `.nupkg` bytes;
the release workflow does not repack between pushes.
