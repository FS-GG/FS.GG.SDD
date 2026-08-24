---
name: typed-sdd-inspect
description: Inspect Typed SDD authority, identity, and projection freshness.
---

# Typed SDD inspect

Run `fsgg-sdd typed-sdd inspect --work <id>`. A clean result proves the lifecycle/backend,
package identity, canonical F# digest, and projection digests recorded in
`readiness/<id>/typed-authority.json`. Resolve its diagnostic ID exactly; do not replace or bypass
the authority manifest.
