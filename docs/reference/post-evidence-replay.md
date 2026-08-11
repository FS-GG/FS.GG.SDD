# Post-evidence lifecycle replay

`analyze` remains the pre-implementation gate: before `evidence.yml` exists it
derives its work model from the authored lifecycle artifacts alone. After evidence
exists, it derives the same evidence-enriched model used by `verify` and `ship`.

Evidence retains `sourceAnalysis` and its full `sourceSnapshots` on disk. Those
snapshots are checked by `evidence` to detect stale sources. The work-model digest
canonicalises just that tool-owned snapshot payload, because it can contain an
analysis view that cites the work model itself. This separates provenance checking
from evidence meaning and makes the post-ship replay converge:

```text
analyze -> evidence -> verify -> ship -> refresh -> agents
```

Run the sequence once to advance artifacts created by older versions, then run it
again. A current ship-ready package reports `noChange` for every command and leaves
the worktree clean. A changed authored source still makes the recorded evidence
snapshots stale; replay convergence is not a freshness bypass.
