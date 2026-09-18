# Generic Quint replay ownership

The 2.0.2 extraction preview delegates generic replay behavior to public FsQuint
0.1.0-preview.2. Existing SDD public records/unions, CLR namespace and schema-v1
canonical bytes remain in place. The facade maps data; it contains no JSON parser,
canonical encoder, fingerprint implementation, validator or comparator.

FsQuint owns generic defects and releases. SDD retains its compiler, profiles,
lifecycle and action/source policy. Pin updates are reviewed and tested; Quint CLI
versions are independent. Restore requires only nuget.org. The temporary facade
is retired only by a separately reviewed major SDD API transition.

This preview is intentional under FSQUINT-01; it does not change the stable 2.0.1
channel for ordinary consumers. Stable adoption follows production-consumer and
upstream-update qualification. The update regression fails with public preview 1 and passes with preview 2: malformed
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

The 2.0.2 release is prepared against the same qualified replay facade and Unicode
regression. Its final FsQuint pin is advanced only after the stable upstream archives
are publicly served. Existing SDD CLR types and public signatures remain unchanged.

Renovate's existing organization preset discovers the central package pin. The
FsQuint rule uses anonymous public NuGet, exact pins and no automatic merge; Quint
compiler/tool identities remain separate. The obsolete GitHub-feed secret rule is
removed: it asserted that public packages were private and prevented a real local
Renovate scan from loading configuration. Renovate 44.99.0 now extracts the FsQuint
pin from `Directory.Packages.local.props` without credentials.
