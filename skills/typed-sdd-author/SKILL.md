---
name: typed-sdd-author
description: Author explicit F# or exact-cache Quint Typed SDD authority through the installed FS.GG.SDD tool.
---

# Typed SDD author

For manifest-v2 Quint authority, run `fsgg-sdd typed-sdd author --work <id> --title <title>
--agent <agent-id> --session <session-id> --backend quint-specification-v1 --cache <cache-root>`.
Before the first author run, acquire the pinned objects using the producer's documented recipe and run
`fsgg-sdd typed-sdd provision --cache <cache-root> --quint <path> --lmt <path>`. Provisioning validates
the exact Linux/amd64 profile-2 hashes and stages the content-addressed set; author never downloads tools.
It runs both tools twice in isolated roots and atomically records
Markdown, fences, generated Quint, typed-effect evidence, source map, contract, bindings, receipt,
and manifest. Never use `author --accept` to replace v1; use migration so rollback remains exact.

For a consumer-defined model, add `--profile fsgg-quint-profile/2 --source <project-relative.md>
--bindings <project-relative.json>`. The binding document uses
`fsgg.quint.general-bindings/v1`; it selects constant value declarations and action declarations with
source ranges but must not duplicate semantic values from Quint. Keep facts, catalogue rows,
verification declarations, finite bounds, and external-algorithm registrations in the literate Quint
source. The host accepts only the closed profile-2 value vocabulary and bounded resource envelope.

Omitting `--backend` preserves manifest-v1 F# authoring. Never hand-edit generated authority files.
