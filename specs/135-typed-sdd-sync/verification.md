# Source qualification record

Scope: FS.GG.SDD#1164 source window, 2026-11-02. Publication and package-only
fresh/retained workspace checks remain milestone 5. The bounded checks below
support only their declared inputs and model bounds.

## Executable journey

The exact Quint 0.32.0 and lmt objects were provisioned through `typed-sdd
provision`. The accepted profile-2 fixture declares two workspace modules,
seven scenarios, six invariants, and bounds of 100 samples, six steps and seed
927. A pinned Temurin 21.0.9+10 JRE and Apalache 0.56.1 distribution ran in
an isolated loopback namespace. Each execution stayed inside the admitted one
CPU/four GiB cgroup. The first `tests/typed-sdd-sync-acceptance.sh` passed in
108.9 seconds. After independent review exposed missing revision continuity,
content comparison, and proposed-model checks, the repaired journey passed in
about 194 seconds under the same cap:

- Proposed authoring ran the model and reducer checks before authority
  replacement. A false but type-valid invariant and an unreachable scenario
  left the accepted manifest, revision link and satisfied receipt unchanged.
  Repaired accepted edits advanced the transaction-bound revision from zero
  through four. A damaged link blocked and restoring it recovered revision zero.
  Independent review then found that deleting the link reset revision to zero;
  the repaired source fails closed on a missing link. A revision-four deletion,
  refusal, exact-link restoration and fresh revision-four recovery are in the
  acceptance journey.
- Accepted authoring wrote a satisfied fresh sync receipt. A prior forged
  `outcome: satisfied` JSON plus changed source could not pass normal verify.
- A missing verifier blocked; restoring it recovered fresh success.
- A new nonnegative-revision property passed against unchanged production code.
  Conflicting assumptions in the two accepted modules blocked.
- An accepted source edit during an in-flight model run blocked at the final
  byte recheck. Restoring the source passed.

The production adapter receives the accepted model derived from the compiled
contract. It checks the complete accepted module delta and revision from the
real reducer, then compares abstract observations with a model-produced
four-state witness. It exercises three observed actions and an independent
stale-base refusal. The full relevant obligation set is selected when impact
coverage is incomplete; this window makes no narrower dependency claim.

## Code-only defect and repair

A disposable copy of this source changed the exact-base guard in
`WorkspaceLifecycle.validateProposal` from
`proposal.BaseFingerprint <> expected` to
`false && proposal.BaseFingerprint <> expected`. The same accepted manifest
SHA-256 `47ee002e65d3cada694e015f637d4c1bc935e0d643e5b53384993480cce58fd3`
and model/tool inputs then produced `SYNC-STALE-ACCEPTED` through the real
reducer adapter. The mutant Artifacts assembly SHA-256 was
`f4d91ec652ec254da0bbe9fb4dc588c6bec7eede35201b5aa1b58900bfbd2ee3`.
Restoring only that production guard and rebuilding passed with seven scenarios
and three replay actions; the repaired assembly SHA-256 was
`d37e56af57fc1d0b12ab1174e537948a73686809027531892d9e13f8095b570e`.
The disposable copy and raw run output stay in private scratch, outside this
repository. This control demonstrates one concrete code-only deviation, not
general implementation equivalence.

An independent-review repair added a second disposable production mutant:
`WorkspaceLifecycle.applyChanges` ignored `Upsert` while `reduce` still
incremented revision. With the unchanged accepted manifest SHA-256 above,
the adapter returned `SYNC-REDUCER-CONTENT`. The mutant Artifacts assembly
SHA-256 was `1758ad1d4d600b613650cb21b839abe12faf7904ca337054afe76ce7cebee8ba`.
Restoring the production file and rebuilding passed the same seven scenarios
and three replay actions; the repaired assembly SHA-256 was
`9a2b32f0ba7f8e048ff93ec30ae816c4267592c50743d031d3c06f9fa734bc1b`.
The original review finding remains recorded because the first adapter could
pass this lost-content behavior.

## Build and test limits

Locked solution restore and Debug build completed with zero warnings/errors
under one CPU/two GiB. Focused CLI tests passed 62/62. The process skill
manifest tests passed 13/13 after regenerating the manifest from the edited
seeded skill. New WorkspaceSync tests passed 3/3. A full solution test attempt
hit its original five-minute cap while the Commands suite remained CPU active;
the first reported failures were three stale process skill manifest assertions,
which the regenerated manifest and focused rerun resolved. No full-suite green
claim is made from that timed-out attempt. API surface projection was checked
coherent with 98 mirrored declarations after adding the public `.fsi` mirrors.
The old pushed-head hosted gate completed in 17m58s with a synthetic public
`QuintSync` method and two skill-content digests in the command-report golden
as the only failures: CLI 245 passed/1 failed; Commands 1671 passed/1 failed/13
skipped. Replacing the task reader removed the synthetic method and its named
surface test passed 1/1. At this source version, only the two
skill-content-dependent digest operands were regenerated; #1163 independently
owns the same golden's version and release-dependent digest, which must be
regenerated after source and release assembly.
Exact-head CI and independent repair review remain required.

## Toolchain lesson

Quint 0.32.0 can exit zero while `quint test --main WorkspaceLifecycleTests`
discovers zero tests. The acceptance script and repaired installed journey
use an explicit seven-name selector, assert `7 passing` and each test name,
and include an empty-selector negative control. The Apalache endpoint and
Quint process must share a network namespace with loopback enabled; a loopback
server in another namespace led to a 60-second timeout. The sync launcher
sets up only isolated loopback and records the actual host utility identities.
