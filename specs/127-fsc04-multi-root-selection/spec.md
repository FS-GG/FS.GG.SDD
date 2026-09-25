# Work-model multi-root selection characterization

**Status:** provisional FSC-04 source evidence, stacked on the read-only descriptor-pinned draft. **Owner:** FS.GG.SDD.

## Outcome

An executable fixture records the producer's current selected work-model source paths when configuration, evidence, and a performance artifact are present. A second fixture records that a declared performance artifact disappears from the selected set both when no read was recorded and when a read explicitly failed. This evidence informs a later producer-owned multi-root or closed-bundle choice; it does not make that choice.

## Boundaries

The existing v1 generation-source contract binds one caller-selected closed root and digest policy. The work-model selector spans `.fsgg`, `work/<id>`, and optionally `readiness/<id>`. The evidence source-snapshot section is projected before hashing. A declared performance artifact with no selected snapshot is omitted whether the read was absent or explicitly unreadable. These facts cannot be represented by merely naming one of the existing directories as a closed root. The future producer must specify physical closure for each selected root or create a dedicated bundle, distinguish missing from unreadable optional-artifact behavior, and bind the evidence projection and digest policy before live wiring.

This characterization makes no producer or output change. It does not publish a package, pin a receiver, or claim a coherent point-in-time capture across roots.
