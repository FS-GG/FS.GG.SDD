# Retained extraction fixture preparation

TSDD-KNOWLEDGE-01.6 has a bounded source fixture candidate based on protected
SDD `9df47707ab22b2486400fb359f381e08c4de90df`. It exercises manually curated
public findings through the existing knowledge API. Native execution and
installed retained-project proof remain pending; .6 stays open.

The [fixture directory](../../../tests/FS.GG.SDD.Knowledge.Tests/Fixtures/RetainedExtraction/README.md)
contains six concise proposals: the Typed SDD authority boundary, a failing
non-saturating damage control, Q1's producer-only qualification, and a linked
liveness defect/cause/fix. Evidence references pin public source paths,
revision and digests. Q1's failed first repair, historical dates and finite-model
limits remain visible. Records use reported state and basis; extraction supplies
no verification or production acceptance.

The [focused tests](../../../tests/FS.GG.SDD.Knowledge.Tests/RetainedExtractionTests.fs)
cover explicit application without changing original documents, byte-idempotent
repetition without date refresh, owner-edit and stale-proposal conflicts, explicit
current-digest reconciliation, lossless current-record restore, and two real Git
versions recovered from an external bundle without cache. The six checked-in
proposal files total 8,924 UTF-8 bytes. The runtime assertions use `Store.check`
for complete canonical size, including schema bytes and the 10,485,760-byte cap;
that assertion has not yet run on this candidate.

The [current API](../../../src/FS.GG.SDD.Knowledge/Store.fsi) exposes capture with
an expected record digest, get/search/relations, export/restore and Git history.
It has no named extraction-proposal or diff/apply operation. This candidate
uses reviewable JSON and a checked-in owner/proposal diff plus explicit existing
API calls. It adds no public surface, CLI route, automatic extraction service,
raw-object store or migration cutover. Product-level proposal orchestration, if
required beyond curated review, needs a separate root scope decision before
implementation.

Before source delivery, allocate one CLR qualification slot for the focused
knowledge project's locked restore/build/tests and the new F# file's formatting
check. Required native gates remain independent. Pure-file checks cover JSON
shape, public source digests, relative links, changed-path reservation and
`git diff --check`; their result is recorded by the integration owner. Published
tooling qualification and any real old-store retirement are separate pending
operations. PR 1089 and private BAR storage are outside this candidate.
