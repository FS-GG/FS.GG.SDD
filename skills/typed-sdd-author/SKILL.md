---
name: typed-sdd-author
description: Author canonical Typed SDD F# specifications through the installed FS.GG.SDD tool.
---

# Typed SDD author

Use `fsgg-sdd typed-sdd author --work <id> --title <title> --agent <agent-id> --session <session-id>`.
Treat `work/<id>/specification.fsx` as authority. Edit it only within an authoring session, then
regenerate its normalized JSON, Markdown projection, and authority receipt. Never ingest Markdown
as Typed SDD authority and never hand-edit generated projections.
