# Work-model source policy characterization

## Producer inventory

`ViewGeneration.workModelSnapshots` selects three `.fsgg/*.yml` files, available authored `work/<id>/spec.md`, `clarifications.md`, `checklist.md`, `plan.md`, `tasks.yml`, and `evidence.yml`, plus optional performance-budget artifact paths parsed from evidence declarations. `charter.md` is intentionally excluded. The work-model source identity hashes selected text with `SchemaVersion.sha256Text`, which folds CRLF to LF. The `evidence.yml` text first replaces its `sourceSnapshots` section with `sourceSnapshots: []`; that rewrite is not represented by #1001's `ExactBytes` or `Utf8LfText` policy. Performance artifact paths may lie outside either `.fsgg` or `work/<id>`.

`Serialization.checkGeneratedWorkModelCurrency` reselects sources through `WorkItem.loadWorkItemFromSnapshots` and `Serialization.sourceStale`. The latter only checks that every recorded source matches a current path/digest/schema major. It does not require every current source to appear in the generated manifest. The loader's path map accepts an unselected case alias alongside a selected path without a diagnostic. An extra recorded row and a selected source digest change do mark the view stale. `GenerationManifest.isStale` in #1000 implements a stricter pure source-set check, but it is not currently used by this currency path.

## Producer decision required before multi-root wiring

The producer owner must choose an authoritative source-set boundary for a versioned work-model contract:

1. Preserve the explicit selected-file set, with independently enumerated physical roots and a rule for otherwise valid unselected files; or
2. Move/prepare a dedicated closed input bundle and bind its bytes before generation.

Either choice must define how the evidence `sourceSnapshots` projection and optional performance artifact bytes are represented, how case aliases and path duplicates refuse, and how existing generated views migrate. A generic single-root `#1001/#1002` contract cannot make this decision. No live producer or output owner is changed in this draft.

## Characterization evidence

The tests exercise omitted and extra manifest rows, a case-alias physical input, selected digest drift, and the evidence projection. In a temporary red-before control, changing the omitted-row and case-alias assertions to require a stale verdict failed 2/4 focused tests on the current implementation; the checked-in assertions document that current behavior. This is test-only evidence, not an acceptance claim for stricter future behavior.
