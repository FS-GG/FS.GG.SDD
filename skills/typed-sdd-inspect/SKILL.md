---
name: typed-sdd-inspect
description: Inspect Typed SDD authority, identity, and projection freshness.
---

# Typed SDD inspect

Run `fsgg-sdd typed-sdd inspect --work <id>`. Inspection dispatches only from the manifest version
and declared backend. V1 retains its F# compiler/projection checks. V2 proves exact package/profile/
toolchain identity plus closed Markdown, fence, generated Quint, typed-effect, source-map, contract,
binding, receipt, and rollback bytes. Missing, unreadable, aliased, edited, or semantically stale
artifacts fail with distinct diagnostics. Never infer a backend from file presence or bypass
`readiness/<id>/typed-authority.json`.
