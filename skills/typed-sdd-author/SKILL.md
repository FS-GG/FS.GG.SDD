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

For profile 2, also pass `--java-home` and `--apalache-dist`. Accepting the authority
runs bounded Quint typecheck, explicitly named tests, simulation, Apalache verification,
and the production SDD reducer adapter. If the post-commit check blocks, repair and
rerun `fsgg-sdd typed-sdd sync --root <root> --work <id> --cache <cache-root>
--java-home <jre> --apalache-dist <distribution>`. Normal `verify` and `ship` require
`--sync-cache`, `--sync-java-home`, and `--sync-apalache-dist`; both run a fresh check.
A prior sync JSON never supplies current evidence.

Omitting `--backend` selects Quint. Pass `--backend fsharp-specification-v1` for
manifest-v1 compatibility authoring. Never hand-edit generated authority files.
