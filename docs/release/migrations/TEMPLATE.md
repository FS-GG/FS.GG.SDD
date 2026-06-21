---
title: Migration Note Template
category: SDD
categoryindex: 6
index: 21
description: Per-release template for a breaking-change migration note. Copy to <version>.md and fill in for any release with a Breaking change.
---

# Migration Note Template

Copy this file to `docs/release/migrations/<version>.md` for any release that
makes a Breaking change to a public contract, then replace the placeholders.
Delete this guidance paragraph in the copy. See the
[migration-note obligation](README.md) and the
[versioning policy](../versioning-policy.md).

---

```yaml
version: <x.y.z>
date: <YYYY-MM-DD>
```

# Migration to `<x.y.z>`

## Summary

One or two sentences describing the scope of the breaking changes in this release
and who is affected.

## Breaking changes and adaptation steps

Enumerate **every** breaking public-contract change in this release and the exact
step a consumer must take to adapt.

| Breaking change | Affected contract | Consumer adaptation step |
|---|---|---|
| `<what changed>` | `<contract, e.g. ship.json>` | `<what the consumer must do>` |
| … | … | … |

## References

- [Schema Reference](../schema-reference.md) — the versioned shape of each
  affected contract.
- [Versioning Policy](../versioning-policy.md) — the change-class to bump rules.
- [`release-readiness.json`](../release-readiness.json) — the authoritative
  machine contract this note projects.
