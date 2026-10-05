# Retained extraction fixture preparation

TSDD-KNOWLEDGE-01.6 has a bounded source fixture candidate based on protected
SDD `9df47707ab22b2486400fb359f381e08c4de90df`. It exercises manually curated
public findings through the existing knowledge API. Focused source qualification
passed all 14 knowledge tests at `6cb29e12f8df05fffc7e2c147ba27d419f104b3a`.
Installed retained-project proof remains pending; .6 stays open.

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
for complete canonical size, including schema bytes and the 10,485,760-byte cap.
All four new retained-extraction cases and the ten existing store cases passed.

The [current API](../../../src/FS.GG.SDD.Knowledge/Store.fsi) exposes capture with
an expected record digest, get/search/relations, export/restore and Git history.
It has no named extraction-proposal or diff/apply operation. This candidate
uses reviewable JSON and a checked-in owner/proposal diff plus explicit existing
API calls. It adds no public surface, CLI route, automatic extraction service,
raw-object store or migration cutover. Product-level proposal orchestration, if
required beyond curated review, needs a separate root scope decision before
implementation.

Qualification used SDK 10.0.401 and runtime 10.0.12, locked restore, the focused
Debug build, and direct VSTest execution of the compiled test assembly. Fantomas
8.0.5 formatted and checked the new test file. Pure-file checks passed for JSON,
public source digests, relations, relative links, project XML and diff whitespace.
The first build exposed an ambiguous assertion overload; the following test run
exposed missing fixture copying. Both were repaired within the focused test scope,
and their failed attempts remain retained.

The accepted test run's restore, build and test steps all exited naturally with
zero status. Its supervisor's final observation refused an unrecorded adopted
child that the kernel reported as an exited-zero zombie. A subsequent read-only
verification independently joined all 14 named results, assembly/report digests,
source and package-cache bytes, and natural-zero receipts, then established
current absence of every recorded and adopted task generation. Root accepted
source qualification using that distinct later observation. The original failed
terminal result remains preserved; no earlier reaping or executable cause is
claimed. Peak observed capacity was two task CLR processes and four total.

Required hosted gates remain independent. Published tooling qualification and any
real old-store retirement are separate pending operations. PR 1089 and private BAR
storage are outside this candidate.
