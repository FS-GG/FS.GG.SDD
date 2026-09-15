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
