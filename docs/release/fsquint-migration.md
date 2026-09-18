# Generic Quint replay ownership

SDD 2.0.2 delegates generic replay behavior to public FsQuint 0.1.0. Existing SDD public records/unions, CLR namespace and schema-v1
canonical bytes remain in place. The facade maps data; it contains no JSON parser,
canonical encoder, fingerprint implementation, validator or comparator.

FsQuint owns generic defects and releases. SDD retains its compiler, profiles,
lifecycle and action/source policy. Pin updates are reviewed and tested; Quint CLI
versions are independent. Restore requires only nuget.org. The temporary facade
is retired only by a separately reviewed major SDD API transition.

The extraction first shipped as a preview under FSQUINT-01. The update regression
fails with FsQuint preview 1 and passes with preview 2 and stable 0.1.0: malformed
UTF-16 is rejected before fingerprinting; valid Unicode vectors remain unchanged.

Validation includes the existing replay fixture, full artifact tests, and an
unchanged client compiled against public 2.0.1 then executed with the candidate
assembly. `scripts/quint-replay-binary-compat.sh` performs that binary check. Release
projection and golden changes are version/channel and their derived digest changes;
no fixture is refreshed to accept changed replay observations.

## Preview 2 update qualification

FsQuint PR4 fixes malformed UTF-16 fingerprint collisions. The SDD-only regression
uses the preserved SDD API and fails with served FsQuint preview 1, then passes with
served preview 2. Public restore, 635 artifact tests and 1,355 command tests pass.

A local qualification exercise then changes only the package pin back to preview 1,
restores successfully, and rejects the candidate with exactly the new Unicode test
failing (seven existing replay tests still pass). Restoring the immutable preview 2
pin and lock files, restoring packages and rerunning the eight replay tests passes.
This is a deliberately rejected downgrade, not a production incident. No model,
projection, expected observations or Quint tool pin changes. A failed package update
must retain the last qualified pin; no SDD algorithm fork is introduced.

## Stable adoption

The 2.0.2 release adopts publicly served FsQuint 0.1.0 with the same qualified
replay facade and Unicode regression. Upstream [release verification](https://github.com/FS-GG/FsQuint/actions/runs/35330871321)
confirms both-feed payload/source identity and anonymous independent use. Existing SDD CLR types and public signatures remain unchanged.

Renovate's existing organization preset discovers the central package pin. The
FsQuint rule uses anonymous public NuGet, exact pins and no automatic merge; Quint
compiler/tool identities remain separate. The obsolete GitHub-feed secret rule is
removed: it asserted that public packages were private and prevented a real local
Renovate scan from loading configuration. Renovate 44.99.0 now extracts the FsQuint
pin from `Directory.Packages.local.props` without credentials.

## Stable release qualification

SDD Artifacts and CLI **2.0.2** are published on GitHub Packages and nuget.org from
immutable tag `v2.0.2`, source `83790aedc228e2158c9da7a9ac8e30195bdf9fbe`.
The [candidate preparation](https://github.com/FS-GG/FS.GG.SDD/actions/runs/35333557301)
passed all release tests and retained the only publishable archives. The
[tagged release](https://github.com/FS-GG/FS.GG.SDD/actions/runs/35334648281)
pushed those archives to both feeds and passed payload readback, then hit a NuGet
indexing race during clean CLI installation. No archive or tag was replaced.

[Read-only recovery](https://github.com/FS-GG/FS.GG.SDD/actions/runs/35336067623)
passed source and dual-feed payload verification. A separate literal ZIP-entry
comparison against the retained candidate covered all 12 Artifacts and 45 CLI
non-signature entries, including `[Content_Types].xml`. This avoids the shell ZIP
reader treating brackets in that filename as a pattern.

After indexing, the unchanged client compiled against public Artifacts 2.0.1 ran
successfully against public 2.0.2. The installed-package Quint acceptance script
also passed against nuget.org 2.0.2 in a network-isolated user namespace: authoring,
inspection, .NET/Fable parity, exact migration rollback, retained profile 1 behavior
and all 17 exact-IR refusal controls. These checks complete the post-publication
acceptance that the initial workflow did not reach. Quint and Fable tools were
provisioned with the existing pinned versions and verified hashes.
