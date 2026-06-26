# Phase 1 Data Model: R7 site taxonomy

This row has no runtime data model (no schema, type, or artifact changes). The "model"
here is the **taxonomy of edit sites and their per-site decision record** — the unit the
task graph and the verification gate operate over.

## Entities

### `PrivateSite`
A top-level `let private` / `type private` / `module private` binding in an `.fs` file.

| Field | Meaning |
|-------|---------|
| `file` | source `.fs` path |
| `fsiBacked` | whether the enclosing module has a sibling `.fsi` |
| `disposition` | `Removed` (redundant, gate-proven) \| `Retained` (load-bearing, gate-proven) |
| `retentionReason` | required iff `Retained`: `Collision` \| `Shadow` \| `AutoOpenCrossFile` \| `BuildError` |

- **Validation rule (FR-001/FR-002/FR-003)**: `disposition = Removed` is valid only if the
  D3 gate (build + suite + empty `.fsi`/baseline diff + empty output diff) passes with it
  removed. `disposition = Retained` requires a non-empty `retentionReason` provable by a
  failing build/test when removed — never preference.
- **Invariant**: every `.fsi` and every `PublicSurface.baseline` is byte-identical to the
  merge base regardless of disposition (SC-004).

### `FailwithEscape`
A call site converting `Result.Error`/`None` into a thrown exception inside total code.

| Field | Meaning |
|-------|---------|
| `file:line` | source location |
| `form` | `failwith` \| `Result.defaultWith failwith` \| `Option.defaultWith (fun () -> failwith …)` |
| `reachability` | `UnreachableByConstruction` (all 9 today) \| `ReachableOnBadInput` |
| `treatment` | `ContextBearingThrow` (FR-004a) \| `ThreadedDiagnostic` (FR-004b) \| `DeferredOutOfScope` (FR-005) |
| `contextNamed` | the identifier/path/value the new message must surface |

- **Validation rule (FR-004/FR-005/SC-002)**: every site ends as `ContextBearingThrow` whose
  message names `contextNamed` + the underlying error, **or** `ThreadedDiagnostic` that leaves
  tool-visible output byte-identical for currently-passing fixtures. Zero bare
  inner-error-string throws remain. A conversion that would change output is recorded
  `DeferredOutOfScope` and the site stays a context-bearing throw.
- **Invariant (FR-006)**: happy-path output is byte-identical — the rewrite only changes the
  message on an unreachable branch.

### `SurfaceBaseline` (read-only invariant)
The four `tests/*/PublicSurface.baseline` snapshots plus all `.fsi` files. Not edited by this
row; their empty diff vs merge base is the binding gate (SC-004).

## Site disposition record (to be completed during implementation)

| File | Sites | `fsiBacked` | Planned disposition |
|------|------:|:-----------:|---------------------|
| `ValidationRunner.fs` | 33 | yes | `Removed` (gate-proven) |
| `ReleaseContract.fs` | 20 | yes | `Removed` |
| `Cli/Rendering.fs` | 8 | yes | `Removed` |
| `WorkModel.fs` | 7 | yes | `Removed` |
| `GovernanceHandoff.fs` | 5 | yes | `Removed` |
| `ValidationHarness.fs` | 3 | yes | `Removed` |
| `LifecycleArtifacts/Verify.fs` | 3 | yes | `Removed` |
| `SchemaVersion.fs` | 1 | yes | `Removed` |
| `HandlersShip.fs` | 1 | **no** | `Removed` (gate-proven 2026-06-26 — `parseShipReadinessFacts` is the only binding of that name in the assembly; full Release build + 438-test suite green with `private` removed, so `[<AutoOpen>]` re-export causes no collision/shadow) |

**Final dispositions (gate-proven 2026-06-26):** all 81 sites `Removed`. Zero
`Retained` — no `retentionReason` needed. Full Release build (0 warnings, FS3261/FS0025
ratchet at 0), 438-test suite green, empty `.fsi`/`PublicSurface.baseline` diff, empty
`charter`/`analyze`/`refresh` `--json`/`--text` diff.

`FailwithEscape` records — all `UnreachableByConstruction` / `ContextBearingThrow`:

| Site | `contextNamed` |
|------|----------------|
| `ParsingTasks.fs:91` | constructed `EV%03d` evidence id |
| `ParsingTasks.fs:96` | tasks artifact path |
| `ParsingTasks.fs:101` | constructed `T%03d` task id |
| `HandlersEvidence.fs:220` | evidence artifact path |
| `HandlersEvidence.fs:259` | offending `workId` |
| `ReleaseContract.fs:266` | `CommandSerialization.fs` artifact path |
| `ReleaseContract.fs:451` | parsed-back artifact path |
| `SchemaVersion.fs:166` | generator-version component |
| `ValidationRunner.fs:642` | validation stage (`report not built after BuildReport`) |

**Final treatments (2026-06-26):** all 9 `ContextBearingThrow` /
`UnreachableByConstruction`. Zero `ThreadedDiagnostic`, zero `DeferredOutOfScope` —
no site is reachable on malformed external input, so FR-004b did not fire and no
new test was required (happy-path output byte-identical; only the impossible
branch's message changed).

## State transitions

None. No lifecycle state, no schema version bump. `SchemaVersion`/`GeneratorVersion` values
are untouched — only the throw message on `SchemaVersion.fs:166`'s impossible branch changes.
