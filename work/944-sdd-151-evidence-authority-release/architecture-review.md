# Qualification state-boundary review

Window: 2026-08-30 07:41–08:35 UTC (54 minutes; bounded inside one 48-hour
cluster window).

## Findings and classification

Two qualification findings occurred in the window:

1. Protected-main run 33299790063 attempt 1 observed a single empty seeding
   result in one Commands test. The identical attempt 2, 20 independent focused
   Debug processes, and both complete candidate suites passed. The source and
   assertion remained byte-identical.
2. Running `scripts/apicompat-check.sh` before a release-style
   `dotnet pack --no-restore` left `src/FS.GG.Contracts/obj/project.assets.json`
   bound to ApiCompat's deleted temporary `NUGET_PACKAGES`. The later pack
   emitted MSB3106 for the missing FSharp.Core path.

They share a broad order/isolation risk class, but no common causal state was
established. GitHub attempts run in fresh hosted jobs, so attempt 1 cannot read
local ApiCompat/MSBuild state. The seeding result is bounded as non-reproducing
process/runtime scheduling. The ApiCompat issue is a deterministic persistent
MSBuild-state leak and is repaired here.

Metrics: 2 related findings in 54 minutes; 1 reproducible state leak repaired;
1 non-reproducing anomaly bounded; 0 assertions weakened; 0 packages published.

## State and cache boundary map

| Stage | Reads | Writes | Lifetime / isolation |
| --- | --- | --- | --- |
| locked restore | lock files, configured feeds | global packages, HTTP cache, each project's `obj/project.assets.json` | workflow action uses a fresh job cache; local runs use caller state |
| build | project sources and assets | `bin/<configuration>`, `obj/<configuration>` | checkout-owned; intentionally consumed by `--no-build` tests |
| test | built assemblies and assets | test results plus test byproducts | per process; full runner serializes projects; exact seeding repetition used separate processes |
| ApiCompat | source project, feed baseline | package/HTTP cache, restore graph, binaries, validation package/log | now entirely under one `mktemp` root and removed together |
| release pack | exact HEAD sources, clean locked restore/build outputs | exactly two nupkgs and pre-push hashes | release job runner; source revision bound to exact HEAD |
| clean tool check | exact CLI nupkg | isolated NuGet config and tool path | one temporary directory; no repo source on PATH |

## Sibling inventory

- `.github/actions/locked-restore/action.yml` deliberately exports a fresh
  `NUGET_PACKAGES` to the rest of one job. Its `obj/project.assets.json` remains
  valid because the cache survives for that job and the runner is discarded.
- `.github/workflows/release.yml` separates resolve, test, pack, and read-back
  jobs onto fresh runners. No ApiCompat job state crosses into release pack.
- `.github/workflows/gate.yml` consumes the locked-restore job state with
  `--no-restore` build/tests, and runs ApiCompat in a separate job.
- `scripts/verify-cli-tool.sh` packs only when no pre-packed directory is
  supplied; the release path supplies exact bytes and installs with a private
  NuGet config/tool directory.
- `scripts/tests/apicompat-check.test.sh` is the only sibling test that changes
  `NUGET_PACKAGES` while invoking the production ApiCompat script. It now proves
  both the reachable contamination inversion and the hermetic positive case.
- Other script/workflow restore/build invocations found in the inventory either
  operate on synthetic temporary projects or intentionally share state inside a
  single job; none deletes a package cache while preserving a checkout-owned
  assets file for a later no-restore consumer.

## Repair and discrimination

ApiCompat now redirects `BaseIntermediateOutputPath`,
`MSBuildProjectExtensionsPath`, and `BaseOutputPath` beneath its own temporary
root, alongside its private package and HTTP caches. Cleanup therefore removes
the restore graph and the cache atomically and leaves candidate `obj`/`bin`
unchanged.

The functional test contains two discriminating legs:

- inversion: a pack with only `NUGET_PACKAGES` isolated writes a checkout-owned
  assets file containing that disposable path, and deleting the cache makes the
  poisoned state observable;
- positive: the production ApiCompat command succeeds while producing no
  candidate `obj` or `bin`, so no state survives for a later no-restore pack.

The real-repository sequence additionally hashes the Contracts `obj` tree before
and after ApiCompat and requires equality, then packs Artifacts and CLI with
`--no-restore` and no missing-cache warning. Final release bytes are generated
only after a fresh locked restore and Release build at exact HEAD.

## Second-order review: proof transport, not just proof production

Independent review round 0 completed at 09:03 UTC, 28 minutes after the first
cluster review was recorded. It found two material defects in the review's own
proof routes. The repair round therefore adds another review/qualification cycle
and two complete suites; that latency and compute cost is attributed to the first
review's incomplete trust-boundary model, not to the original evidence-authority
feature.

The first review missed Bash conditional-errexit vacuity because it read
`set -e` inside `check_ambient_cache_isolation` as a function-local invariant.
The caller executed that function as `if check_ambient_cache_isolation ...`; Bash
suppresses errexit for commands in a function whose invocation is a conditional
test. A failed inner `test` could therefore fall through to a later successful
assertion and return zero. The positive route and the three-property removal
mutation were observationally equivalent. The repaired harness executes every
subject as an ordinary command under explicit `set +e`/status capture, restores
`set -e`, and aggregates the result outside any conditional invocation.

The first review also conflated payload equality with nupkg archive identity.
Two back-to-back packs at one exact commit and without a rebuild produced
different whole-file SHA-256 values while extracted payload trees matched. ZIP
container metadata is therefore not a reproducible identity in this toolchain.
More importantly, the old tag/release workflow always packed again: even a
reproducible local pair would not constitute custody of the bytes authorized by
an earlier run.

## Revised trust, identity, and custody chain

| Boundary | Trusted identity | Custody rule | Failure rule |
| --- | --- | --- | --- |
| PR build/review | source head/tree, workflow and contract tests | no publishable archive claim; review qualifies the mechanism | changed head invalidates review |
| post-merge dry-run | exact main commit plus evaluated 1.5.1 identities | pack once; emit two nupkgs and a manifest containing commit, names, versions, sizes, SHA-256; upload one retained Actions artifact | any test, inventory, source binding, install, or manifest mismatch blocks artifact creation |
| artifact retention | Actions run id + artifact name + manifest | artifact is immutable for the run and retained long enough for release authorization | expired/missing/duplicate candidate runs fail closed |
| tag/release workflow | tag commit must equal manifest/source commit | discover the unique successful no-push release run for that commit; download its artifact; never invoke `dotnet pack` | zero/multiple candidates, head mismatch, or hash mismatch blocks before feed credentials are used |
| GitHub Packages | exact downloaded nupkgs | first push consumes retained files | non-duplicate failure stops before nuget.org |
| nuget.org | same local paths and hashes | second push consumes the same retained files without repack | push failure leaves obligation incomplete |
| public read-back | package payload plus served package identity/signature exception | compare each served non-signature entry to retained local artifact and record receipt | missing version or payload mismatch fails release |

Whole-file nupkg SHA-256 is authoritative only inside this custody chain. It is
not expected to match an independent pack. The back-to-back pack inversion proves
that distinction; the substituted-artifact/head/hash inversions prove the tag
path cannot silently switch archives.
