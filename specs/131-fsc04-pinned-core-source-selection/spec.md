# Pinned Core Work-Model Source Selection

**Status:** read-only FSC-04 prerequisite, stacked on draft #1016. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Discover the recognized core source set from physical paths, independently of producer-selected snapshots and candidate rows. `.fsgg/{project,sdd,agents}.yml` and `work/<id>/spec.md` are required. `work/<id>/{clarifications,checklist,plan,tasks,evidence}` are optional and selected when physically present. `charter.md` and unrelated siblings are not work-model sources.
- FR-002: Capture each recognized source through the Linux descriptor-pinned, no-follow selected-file reader. A missing optional file is omitted; a linked, aliased, unreadable, nonregular, or unstable selected path refuses. Missing required sources refuse.
- FR-003: Pass those fresh core captures to the #1016 pinned performance join, which derives performance paths from captured evidence bytes and then delegates to the v2 source bundle verifier. A source present physically but omitted from producer selection or candidate rows refuses. Changed bytes refuse.
- FR-004: This is read-only and cannot write a generated view or authorize publication or receiver adoption.

## Evidence and boundary

Two disposable red-before controls showed #1016 accepting stale supplied core captures after `spec.md` changed, and accepting a newly present `tasks.yml` omitted from both selection and supplied captures. The new entry point refuses both. Independent controls cover a changed required config file, an omitted config row, optional linked and case-aliased files, and a valid source set with unrelated `.fsgg` and `work/<id>` siblings.

The recognized-file profile comes from current `ViewGeneration.workModelSnapshots`; this is not a complete tree snapshot. Each file is captured at a different instant. New files, removals, ABA, timestamp-hidden writes, and changes after checks can escape the observed windows. A producer-owned coordinator must join this physical set to the actual generation plan before any output; cross-root atomicity, Windows concurrency, installed parity, output rollback, publication, receiver pinning, and GS2-10 freeze remain acceptance holds.
