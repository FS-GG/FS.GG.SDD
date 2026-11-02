---
name: fs-gg-sdd-typed-author
description: Author explicit F# or exact-cache Quint Typed SDD authority through FS.GG.SDD.
---

# Typed SDD author

Run `fsgg-sdd typed-sdd author --work <id> --title <title> --agent <agent-id>
--session <session-id> --cache <cache-root>`. The omitted backend is `quint-specification-v1`; the cache contains the
Q1-qualified `objects/<sha256>` and is never acquired online. Two isolated tool runs must agree before
the complete typed-effect-bound authority commits. Use explicit `--backend fsharp-specification-v1`
only for compatibility inspection or migration during the 2.x window. Never replace v1
with `author --accept`; use migration to retain authenticated rollback.

For `--profile fsgg-quint-profile/2`, author the model in `--source` and select its
declarations with `--bindings`. Supply `--java-home` and `--apalache-dist` with the
provisioned `--cache`: authoring checks the proposed model before acceptance,
commits its revision link with the authority, then runs bounded Quint
typecheck, named scenarios, simulation, Apalache verification, and the SDD reducer
adapter. A blocked sync after commit leaves the accepted manifest visible for repair;
it does not grant verify or ship. Run `fsgg-sdd typed-sdd sync` with the same paths to
inspect a fresh report. Normal `verify` and `ship` require `--sync-cache`,
`--sync-java-home`, and `--sync-apalache-dist` and repeat the checks against current
source and implementation bytes. A prior JSON report never supplies success.
For an exact pre-sync profile-2 2.5.4 authority, run `typed-sdd sync
--adopt-legacy` with the same paths to adopt in place. Earlier revision history
remains unknown; missing current revision links block.
