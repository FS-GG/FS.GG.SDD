# Contract: `release.yml` four-SDD-package publish workflow

The release set contains four independently consumable packages:
`FS.GG.SDD.Artifacts`, `FS.GG.SDD.Commands`, `FS.GG.SDD.Cli` (`fsgg-sdd`),
and `FS.GG.SDD.Knowledge`. They share one source-evaluated version.
`FS.GG.Contracts` 7.6.0 is an already published dependency, never a member to
repack or push. This contract governs `.github/workflows/release.yml`.

## Read-only retained-candidate occupancy

Manual dispatch may select `occupancy_candidate_run` and
`occupancy_candidate_sha256` together, leaving `version` and `readback_version`
empty. This selects only `occupancy-preflight`; every resolver, test, pack,
candidate-location, publisher and published-version readback job is excluded,
including when only one occupancy input is supplied. Incomplete, malformed or
mixed inputs fail before artifact download or signature-verifier setup. The job
has only contents/actions/packages read permission and a six-minute timeout.

The maintained runner authenticates a successful same-repository manual release
run and its complete bounded artifact listing, selects exactly one unexpired
`coherent-sdd-packages-<original-source>` artifact, and requires its native digest
and downloaded ZIP to match the caller's expected SHA256. It accepts only the nine
regular retained v4 candidate files with bounded archive/expanded sizes. The
candidate manifest must bind the original run's source, coherent four-member
inventory and independent Contracts7.6.0. The current workflow checkout supplies
verification code; it does not become the old packages' source identity.

After the existing default custody verifier passes, the runner invokes the
existing strict occupancy checker with `--candidate`. It never uses the
candidate-only `--no-push-observation` exception. Denied, missing or incomplete
feed/namespace evidence remains a failed/Unknown preflight. Candidate freshness,
source/custody and original occupancy receipts are preserved. This mode neither
repacks nor publishes, promotes, changes a tag, retries a consumer, or authorizes
any such effect. A successful disposition applies to the selected original bytes
at its observation time; it is not a later publisher's fresh admission.

The original artifact and selected files remain runner-local data. A compact
result identifies the original run/artifact/source/hash, verifier source/file
hashes and outcome. Each original helper stage retains its exit/timeout and
sanitized diagnostic of at most16KiB, including precise refusal causes. Successful
strict output is projected into eight typed feed observations and the reused
Contracts disposition; missing/ambiguous output cannot qualify. No credentials or
HTTP response bodies are captured. The signature-aware checker may invoke
the pinned SDK verifier; there is no product build, package install or test run.
The existing `readback_version` mode still requires an already-published version
tag and cannot substitute for this unpromoted-candidate preflight. Normal
publication still requires its own same-head candidate and all existing gates.

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

## Candidate-only unresolved new namespace

The no-push candidate retains `packages/occupancy-observation.json` using the
checker's exclusive `--no-push-observation` output. For the newly introduced
standalone Commands package only, a missing first page of scoped versions may
remain `Unknown` while packing and package qualification proceed. This never
means `Absent` or publication authorization. All other access errors, malformed
or incomplete enumeration, known occupied versions, and reused dependency or
baseline authenticity failures still refuse.

The mode cannot be combined with publisher `--candidate` and is called only by
the no-push job. The publisher repeats its unchanged strict occupancy/collision
checks: promotion remains blocked until authoritative Commands visibility and
absence or exact retained-payload equality are established. Qualification of
these original bytes cannot substitute for that observation. The candidate
artifact retains the observation with the existing archive receipts.

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
A new four-package SDD release requires actual C1+C2/C2.3 acceptance, independently
verified Config distribution with published Contracts 7.6.0, the ordinary package/API
and installed gates, exact candidate custody, both-feed collision/readback checks,
and separate root release admission. It neither consumes nor clears the historical
ReferenceGateSet 1.8.0 candidate or any original unknown operation. First neutral-catalog
adoption, Governance runtime-carrier/ReferenceGateSet delivery, Templates adoption and
full C4 completion retain their applicable prerequisites. The generated optional
ReferenceGateSet 1.6.0 route stays unchanged. No C1-only publication. Historical
candidate schemas remain evidence, not qualification for v4.

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


## Authoritative Commands namespace fallback

The default scoped-version proof remains unchanged. When the new Commands package
returns404 on its first scoped-version page, an explicitly supplied
`FSGG_PACKAGES_READ_TOKEN` may establish absence using native owner authority.
`FSGG_PACKAGES_READ_ACTOR` binds the expected token login; the checker verifies it
against `/user`, never against a claimed workflow identity alone. The same classic
token needs `read:packages` and `read:org`, applicable organization/SSO authorization,
and native active/admin membership joined by exact user and organization numeric
IDs. This read capability grants no package write/delete or repository write access.
A separate membership credential and public-readable inventory are insufficient.
The implementation supports only the same-token route.

The checker enumerates the unfiltered organization NuGet inventory in sequential
100-entry pages, at most10pages and4MiB per response. Every package must have a
unique numeric ID and case-normalized name, expected organization owner/type and
valid visibility. API redirects, denied/malformed/partial responses, conflicting
pagination and incomplete populations refuse. A short terminal page completes the
population; a full tenth page remains Unknown. Commands absence means its name is
omitted from this authority-backed complete population. Listed-but-unreadable
Commands, other scoped404s and later-page404s remain Unknown. The no-push candidate
mode preserves Commands Unknown and never uses the fallback.

Optional workflow credential `secrets.FSGG_PACKAGES_READ_TOKEN` and matching
`vars.FSGG_PACKAGES_READ_ACTOR` reach only protected occupancy preflights. The
Actions token continues to authenticate original run/artifact custody; unused
credentials are stripped from its local custody-verifier child. Publishing still
uses the existing run-scoped writer and NuGet OIDC credential. Neither credential
provision nor secret mutation follows from this source change. Without the optional
read credential, the ordinary scoped path works and missing namespace still refuses.

The exclusive `--namespace-proof` output records typed method, actor/org IDs, owner
role, UTC observation time, pages/count, canonical population digest and selected
package/version. It contains no package inventory, credential, API body or SSO URL.
Retained results include that typed witness alongside original candidate identity,
current verifier identity/hashes and bounded sanitized helper diagnostics. Both
result schemas retain `publicationAuthorized=false`.

## Promote an original retained candidate with the current verifier

A new manual selection on protected `main` uses all three inputs
`promotion_candidate_run`, `promotion_candidate_sha256`,
`promotion_candidate_source` and the existing explicit stable `version` input.
The source is a full40-character commit and the digest is the original ZIP SHA256.
Missing or mixed occupancy/readback/promotion input refuses before publication.
Normal same-head publication still locates its unique retained no-push artifact.

Retained promotion evaluates source versions and runs the current Contracts,
Artifacts, Commands, CLI and Knowledge source-test job definitions against the
original candidate source. The current protected checkout separately authenticates
the successful original same-repository release run, complete artifact listing,
native ZIP digest, complete successful original qualification-job population,
exact v4 manifest/source/version and unchanged dependency/archive
custody using the maintained retained preflight. This repeats in the publisher with
a fresh download before effects. Current protected CLI self-containment, API/cold Commands consumption and Knowledge
package checks qualify the retained archives again. Packing is unreachable for any retained selection.
The publisher verifies against `candidate_source`, never relabels packages with the
current verifier source, and runs fresh strict occupancy immediately before the
existing GitHub-first and NuGet-second writes. Contracts7.6 remains reused. All eight
feed readbacks and existing public CLI/API/Knowledge/receiver qualifications remain.
The original successful candidate qualification is retained; no same-version rebuild,
archive substitution, tag rewrite or replay of a previous Unknown is permitted.

Current normal/tag and retained selections share a non-cancelling workflow concurrency
fence. Older workflow revisions keep their existing concurrency group; this change
does not cancel, supersede or prove noninterference with an older actor or reservation.
Root must reconcile those independently before dispatch.
Authority-backed occupancy is a fresh observation, not an atomic provider no-overwrite
contract. Root release admission and writer fencing remain necessary; source delivery
or a passing read-only observation alone authorizes no dispatch or publication.
Issue1113 remains open until its full supported disposition and consumer obligations
are verified or its precise held remainder is recorded by the accountable integrator.


## Keep installed-code qualification outside publication credentials

The publisher performs custody parsing, fresh occupancy, feed writes and all eight
archive readbacks without executing installed candidate/public binaries. It uploads
one immutable dual-feed receipt containing original archives, dependency identity,
manifest/hash files and all feed archives/maps. Native upload outputs supply its
artifact ID and ZIP digest to `qualify-public` through literal job dependencies.

`qualify-public` has only contents/actions/packages read permissions and no OIDC
permission. Its temporary Actions read token reaches only receipt authentication;
checkout credentials are not persisted. The maintained receipt verifier checks
native artifact ID/name/run/verifier-source/digest, byte equality, original
candidate source/version and exactly40 regular selected paths. It bounds ZIP bytes
to128MiB, expanded bytes to192MiB and each member to32MiB. Unexpected, duplicate,
missing or redirected member paths refuse before extraction. The job then verifies
original candidate custody and all eight payload joins again before installed-code
execution. No private package inventory or auth secret enters the receipt.

Current CLI, Knowledge, clean public-install and Q2/Q3 receiver checks run only in
this read-only job. The CLI and receiver qualifiers accept an explicit already
source-evaluated package version with a required package source, preserving it
through receiver recursion without permitting packing. Public Fable provisioning
uses the current SDK binding producer/verifier and its pure failure controls, matching
the main gate. Original-source test jobs depend on version/mode preflight, and mixed
promotion/readback selection executes a refusing validator rather than finishing
all-skipped.

Release success still depends on actual public qualification. If it fails after
publication, the writer's durable feed-effect receipt remains separate from the
qualification result; the failure does not erase completed writes, permit replay or
qualify consumer acceptance. Root retains recovery and final disposition admission.


## Read-only qualification of an already-published set — issue 1119

A published set can be left without its durable dual-feed receipt or public
qualification when the in-run readback expires first: SDD 2.4.0's push succeeded, but
nuget.org first served the four archives about 27 minutes later, past the original
10-minute readback, so the receipt upload and `qualify-public` were skipped. The
publisher readback now polls up to 99 times (15 seconds for 19 polls, then 30 seconds,
about 45 minutes). It is the same read-only loop and never retries a push.

The `qualify-published` job is a separate manual selection on protected `main` using
all four inputs `qualify_published_version`, `qualify_candidate_run`,
`qualify_candidate_sha256` and `qualify_candidate_source`; publish, readback,
promotion and occupancy inputs must be empty, and every other mode is excluded when it is
selected. It has only contents/actions/packages read permission, no OIDC, no publisher
or package-read secret and exactly one secret reference, the read-only workflow token.
It never packs, pushes, tags or changes a version. `scripts/qualify-published-set.py`
authenticates the original successful no-push run, ZIP digest, source, version and v4
manifest with the same checks as the retained promotion, then reads the eight published
archives (GitHub Packages with the workflow token, nuget.org anonymously) within the
bounded indexing wait, refusing denial immediately. The job rejoins original custody and
all eight payload joins, uploads a durable `published-dual-feed-receipt` (original
archives, dependency identity, both feed archives, entry/payload maps, readback record),
and then runs the same installed-code gate as `qualify-public`, kept byte-identical by
source controls and bound to the selected version. This result is a qualification of the
selected already-published version; it does not authorize publication and never replaces
an original publication receipt that exists.
