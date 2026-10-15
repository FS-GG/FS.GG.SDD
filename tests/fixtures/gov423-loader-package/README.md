# Selected package loader qualification

This branch-only harness consumes the original SDD 2.3.0 candidate from run
37951954780, artifact11626413573. `inspection.json` binds its source and ZIP hash;
`expected-archives.json` selects all twelve package archive hashes. Qualification
copies only this standalone consumer into runner temporary storage, verifies all
archives, restores from a local feed containing only the selected twelve archives,
checks actual package assets and lock/cache content hashes, builds and runs the
public policy/provenance/admission API controls, and rechecks the archive bytes.

Synthetic provenance and controls establish API structure, correspondence and
complete-request refusal. They do not establish provider execution, authentic
scaffold output, Verify integration, physical capture, publication or adoption.
The preexisting general Commands smoke receipt remains separate evidence.

Run static preparation with `python3 tests/fixtures/gov423-loader-package/preflight.py`.
The Python orchestrator accepts one absent output directory and requires the exact
.NET SDK10.0.401, GitHub artifact read access and public NuGet read access.
Root owns admission of exactly one hosted workflow dispatch on this branch. No
local CLR execution is admitted. All evidence remains in the repository's existing
Actions artifact custody, including archives, assets, lock, consumer, logs,
synthetic fixtures, result and evidence manifest. Workflow retention is30 days;
root must move any accepted long-lived receipt through its normal custody route.

Preflight choice: static inspection and contract checks. The workflow is one
linear job, without reuse, publication, cancellation or conditional qualification
branches. A custom model would add little to this one bounded run. Static effort
cap is10 minutes; hosted job15 minutes, orchestration600 seconds. Qualification
collects independent API control failures within that attempt. Failed acquisition,
identity, closure, restore or compile prerequisites block dependent API execution.
This harness changes no production source and supplies no merged gate claim.
