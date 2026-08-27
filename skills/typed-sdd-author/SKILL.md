---
name: typed-sdd-author
description: Author explicit F# or exact-cache Quint Typed SDD authority through the installed FS.GG.SDD tool.
---

# Typed SDD author

For manifest-v2 Quint authority, run `fsgg-sdd typed-sdd author --work <id> --title <title>
--agent <agent-id> --session <session-id> --backend quint-specification-v1 --cache <cache-root>`.
The cache must contain `objects/<sha256>` for the Q1-qualified Quint 0.32.0 and `lmt` objects; the
command never downloads tools. It runs both tools twice in isolated roots and atomically records
Markdown, fences, generated Quint, typed-effect evidence, source map, contract, bindings, receipt,
and manifest. Never use `author --accept` to replace v1; use migration so rollback remains exact.

For a consumer-defined model, add `--profile fsgg-quint-profile/2 --source <project-relative.md>
--bindings <project-relative.json>`. The binding document uses
`fsgg.quint.general-bindings/v1`; it selects constant value declarations and action declarations with
source ranges but must not duplicate semantic values from Quint. Keep facts, catalogue rows,
verification declarations, finite bounds, and external-algorithm registrations in the literate Quint
source. The host accepts only the closed profile-2 value vocabulary and bounded resource envelope.

Omitting `--backend` preserves manifest-v1 F# authoring. Never hand-edit generated authority files.
