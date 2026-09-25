# Declared Performance Source Pre-Output Gate

**Status:** source-only FSC-04 prerequisite, stacked on draft #1014. **Owner:** FS.GG.SDD.

## Contract

- FR-001: When a parsed evidence declaration names a performance artifact, the work-model planner must inspect that path's interpreted read state before planning `CreateDirectory` or `WriteFile` for the generated work model.
- FR-002: An absent read, including no interpreted read, yields `missingPerformanceSource`. An unreadable or truncated read yields `unreadablePerformanceSource`. Both are blocking errors with the declared path and no generated-view output effects.
- FR-003: An observed `Bytes` read does not trigger this gate. The existing work-model consistency checks still decide whether the view can be written.
- FR-004: The gate uses the same evidence text as source selection, including the snapshot fallback used by post-evidence `analyze`. Malformed evidence stays under the existing parser and work-model diagnostics; this gate does not treat an unparsed path as authority.

## Evidence and limits

A disposable pure planner control reproduced the prior false green: a complete authored work item declared `tests/performance.txt`, the interpreted read was `Absent`, and `generatedViewPlan` still returned a `WriteFile` effect. Independent absent and unreadable controls now require distinct blocking diagnostics and no output effects; an observed control checks that the read-state gate does not falsely refuse `Bytes`. The full Commands test project exercises the shared planner across lifecycle stages.

This gate does not bind the read bytes to the later physical capture or prove that separate `.fsgg`, `work/<id>`, and performance reads form one atomic snapshot. The provisional v2 bundle and Linux selected-file adapter remain separate source-closure prerequisites. ABA, timestamp-hidden writes, changes after the final check, Windows concurrency, installed package parity, publication, output rollback, receiver pinning, and the GS2-10 candidate freeze remain outside this slice.
