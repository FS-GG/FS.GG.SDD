# Contract: `release.yml` four-SDD-package publish workflow

The release set contains four independently consumable packages:
`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, `FS.GG.SDD.Cli` (`fsgg-sdd`),
and `FS.GG.SDD.Knowledge`. They share one source-evaluated version.
`FS.GG.Contracts` 7.6.0 is an already published dependency, never a member to
repack or push. This contract governs `.github/workflows/release.yml`.

## Triggers and source versions

Manual dispatch without `version` runs all gates and packs a no-push candidate.
A supplied manual version must equal the coherent SDD source version; it cannot
override source properties. A version-bearing event tag must match that same
SDD version. Contracts is resolved from the exact central package pin `[7.6.0]`,
not the Contracts producer project. All four SDD project versions must agree.
Publishing events locate exactly one successful retained no-push candidate at
the exact event commit; they do not rebuild it.

## Jobs and gates

| Job | Responsibility |
|-----|----------------|
| `resolve-versions` | Canonical repository guard, exact published dependency pin, four coherent SDD versions and publish intent. |
| `contracts-tests` | Existing source Contracts tests; these do not authorize publishing its producer. |
| `artifacts-tests` | Locked Release tests and genuine clean package consumer. |
| `commands-tests` | Locked Release tests, authored public surface and existing reflection contracts. |
| `cli-tests` | Locked Release CLI tests. |
| `knowledge-tests` | Locked Release Knowledge tests. |
| `publish-artifacts` | Needs all five test jobs and resolver; packs four SDD members once, qualifies them, retains exact bytes without push. |
| `locate-artifacts` | Requires the same gates; selects one unexpired same-head no-push candidate, refusing ambiguous custody. |
| `publish-cli` | Verifies retained source, archive hashes and feed occupancy before credentials; publishes the same four archives to both feeds and reads them back. |

Forks cannot reach these jobs. Top-level permissions remain read-only. Only the
publisher has package-write and OIDC authority. No independent Contracts publisher exists.

## Candidate v4 and reused dependency custody

`scripts/sdd-release-packages.txt` is exactly the four SDD members above.
`candidate.env` uses `fsgg.sdd.release-candidate/v4`, selected SDD source head,
shared version, separate `contracts_version`, and that ordered inventory.
`pre-push.sha256` covers exactly the four once-packed release archives. Each
nuspec must bind its package/version to the selected SDD source commit.
Global `Version` or `PackageVersion` pack overrides are prohibited.

The dependency archive is retained separately under `packages/dependencies`,
with `contracts-reuse.json`. Its published original source is
`cf2f046a10497a336d6243c9a314c3f91fb15771`, archive SHA-256 is
`b1df3ebd6251f5b18aaece4dd0c5449a7825dc7056f925cf2516febc35f9dfc5`,
and DLL SHA-256 is
`91f484d28416c5d860a375a91ed70cdda1d3b6d85d504c15ea21e08a9af727ee`.
The verifier checks these original identities plus a signature-aware payload
hash; it must not rewrite dependency provenance to the new SDD commit.
Both feeds must already have matching normalized Contracts payloads. Exact raw
equality with the independently pinned public archive needs no exclusion. Any
different archive requires `dotnet nuget verify --all` for every signed input
before excluding `.signature.p7s`; failure, unavailable verifier or 30-second
timeout refuses. Verifier stdout/stderr are discarded, so retained output is
bounded to zero bytes. Unsigned archives have no signature to exclude. Dependency
metadata is created once and a repeated record attempt refuses overwrite.
Artifacts/Commands declare that selected dependency; the CLI embeds its DLL and
the exact retained standalone Commands DLL.

Archive SHA-256 is custody identity, not reproducible-build proof. Equal payload
entries in a different ZIP container cannot replace the retained archive.
The publisher has no pack command, downloads by retained run id, rechecks exact
identity and hashes before credentials, pushes GitHub first, rechecks hashes,
then pushes the same archives publicly. Eight push calls cover the four members;
Contracts is never pushed. Occupied versions permit only signature-aware exact
payload equality, never overwrite. GitHub archive404 needs complete scoped
version enumeration; inaccessible or incomplete observations refuse.

## Actual qualification and readback

Candidate API checks compare Artifacts, Knowledge and prior CLI-bundled public
surfaces against published 2.2.0. There is no fabricated standalone Commands
2.2 baseline: Commands instead requires authored source surface tests and a
cold candidate-package consumer using published Contracts7.6. Default dependency
restore must work, including Config; private caches are not release proof.
The actual tool install/smoke and NuGet Knowledge qualifier remain mandatory.
API comparison, cold restore and installed validation run against retained bytes
before handoff; custody verification runs again afterward.

Both feeds are read back for all four SDD members. Non-signature entry maps must
match the retained archives. The durable receipt includes archive/payload maps,
candidate manifest, original dependency archive and reuse metadata. Public clean
consumers include standalone Commands. Read-only historical recovery uses the
selected source tag's inventory without changing old schemas or inferring new
qualification from an old candidate.

## Conformance and source preflight

C1: no-push dispatch packs four SDD archives once and separately retains the
published dependency. C2: source-mismatched manual/tag versions refuse. C3:
occupied archives accept only exact non-signature payloads. C4: every package
source test gate precedes retention/promotion. C5: readback covers four SDD IDs.
C6/C7: actual installed CLI and clean package consumers pass. C8: two push calls
per member and none for Contracts. C9: source versions and exact dependency pin
survive packing without overrides. C10: ambiguous/expired/wrong-head/version/
inventory/hash candidates refuse before credentials. C11: equal-payload archive
substitution refuses. C12: wrong reused source/archive/payload/DLL or mismatched
CLI embedding refuses.

The existing static/synthetic checks are proportionate to this sequential
handoff: `python3 scripts/check-release-custody.py`,
`python3 scripts/tests/release-four-package-controls.py`,
`bash scripts/tests/release-artifact-custody.test.sh`,
`python3 scripts/tests/sdd-release-occupancy.test.py`, and
`python3 scripts/tests/package-payload-readback.test.py`. Actual resolver controls
execute the workflow shell with a stub evaluator, not a CLR or live feed. No new
model or framework is required. Initial source/control effort cap30 minutes;
warm checks target under60 seconds. Hosted savings remain unmeasured.

This source change selects no live pack, registry promotion or runtime authority.
The accepted C1+C2 fence, original Config/ReferenceGateSet custody, actual API,
installed candidate/public readback, and separate root release admission remain
mandatory. No C1-only publication. Historical candidate schemas remain evidence,
not qualification for v4.

## Historical SDD928-C4 retained Contracts extension (v3)

A new dry-run candidate uses `fsgg.sdd.release-candidate/v3`: the existing three
SDD members keep their shared evaluated version, while FS.GG.Contracts retains its
independent evaluated version in `contracts_version`. All four archives bind the
same selected source SHA and exact raw hashes. Contracts tests join the candidate
prerequisites. Contracts packs once in the no-push job and has no later pack path.
The existing publisher downloads and verifies the four-member artifact, observes
both-feed occupancy for both version lines, pushes the original four archives to
GitHub first, rechecks original hashes, and pushes those same archives publicly.
A GitHub archive404 establishes absence only with successful complete scoped
version enumeration; unreadable/listed-but-unreadable versions refuse.

Old v2 three-member candidates are historical artifacts; they cannot qualify this
four-member route. No version/default pin, registry promotion or runtime authority
is selected by this source change. The accepted GOV423-C3/SDD928-C4 C1+C2 fence,
original Config and ReferenceGateSet custody, genuine package API/installed gates,
and separate root release admission remain mandatory. No C1-only publication.

Preflight choice: static checks over the actual workflow plus synthetic archive and
feed-response mutations, using `python3 scripts/check-release-custody.py`,
`python3 scripts/tests/release-four-package-controls.py`, and
`bash scripts/tests/release-artifact-custody.test.sh`. The resolver runs the static
check before version evaluation; candidate/publisher dependencies prevent packing
or effects after refusal. Independently running test jobs retain their existing
semantics. A custom model is deferred for this sequential archive handoff; the
initial source/control effort cap is30 minutes and warm target under60 seconds.
Actual hosted savings and setup/queue costs remain unknown. Exact source
`5084433b6021b488dcd87610ef14b109f468ca86` passed the selected local seven-project
locked dependency/evaluation/build qualification and both focused
`ReleaseWorkflowContractTests` cases. The final twelve-command continuation
completed with clean owned custody and no resource failure, using the pinned
SDK10.0.401/runtime10.0.12 closure. Earlier failed attempts remain retained.
This is source-contract qualification only: actual release packs, feed collision
observations, installed candidate/public readback, provider runtime and coherent
publication are not accepted by these two tests and remain separately gated.
