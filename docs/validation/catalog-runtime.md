# Catalog runtime validation

`scripts/catalog-runtime-validation.py` is the maintained runner for future
catalog build, test, filesystem, child, SDK, and CLI validation commands. It
replaces future use of copied private phase guardians. It does not inspect,
retry, signal, or adopt any historical run.

Run an ordinary build or test command with a JSON config and a private output
directory outside the checkout:

```sh
python3 scripts/catalog-runtime-validation.py --config build.json --output /tmp/catalog-build
python3 scripts/tests/catalog-runtime-validation.test.py
```

A config selects literal commands; it performs no tool discovery or shell
expansion. Relative working directories resolve from the invoking directory.
Environment entries overlay the current environment. Commands run in order;
the first failed command stops the sequence.

```json
{
  "sourceRoot": ".",
  "packages": [],
  "commands": [{
    "name": "build",
    "mode": "ordinary",
    "argv": ["/usr/share/dotnet/dotnet", "build", "src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj", "-c", "Release", "--no-restore", "-m:1", "-p:UseSharedCompilation=false"],
    "cwd": ".",
    "environment": {"DOTNET_PROCESSOR_COUNT": "2"},
    "timeoutSeconds": 300,
    "cleanupSeconds": 15,
    "outputBytes": 8388608
  }]
}
```

Each run records the current Git revision and before/after tree hashes of
tracked and nonignored source bytes. `commandsPassed` records command outcomes;
`sourceIdentityStable` separately requires matching hashes. A changed tree fails
exact-source acceptance even when every command passed. This detects net changes
and does not claim an atomic snapshot or detect changes reverted during a run.
Optional `identityPaths` selects explicit repository-relative globs (recorded in
the result), useful for product builds while disjoint documentation changes
continue. For example: `src/**`, `*.props`, `*.targets`, `global.json`,
`NuGet.Config`, and `.claude/skills/**`. External embedded package bytes belong
in `packages`; omitted input paths are not covered by the selected tree hash. Optional `packages` records raw archive SHA256
hashes; these are byte identities, not authentication or package acceptance.
The result contains literal argv, working directory, actual direct-child PID
and exit code when observed, first failure, separate stdout/stderr paths and
EOF observations, charged versus retained output bytes, duration, and cleanup
state. Output is capped across both logs before retention. A nonzero exit,
launch error, cancellation, timeout, or output overflow fails validation.
No absent status or elapsed deadline becomes exit zero.

The supported ordinary model is cooperative local tools with normal startup
and I/O. SIGINT/SIGTERM cancellation and timeout request process-group TERM,
then KILL if the original direct child remains live. The cleanup interval is
bounded and never extends the original work-plus-cleanup end. This is not
hostile-process containment, a memory/CPU quota, or proof that descendants are
empty. A child can create another session. `direct-child-reaped-streams-eof`
means exactly those observations; `descendantTeardown` remains `not-proven`.
If the direct child is still unresolved at cleanup expiry, the failed report
remains incomplete and the CLI passively waits on the same child without new
signals. Library callers must retain `INCOMPLETE_OWNERS`; this is exceptional
failure handling, not an assertion that cleanup succeeded.

Native filesystem/child/SDK/real CLI commands must use `mode: native-runtime`
and explicitly select `backend: pid-namespace`. Omitting the backend refuses
before launch. The selected installed backend is:

```text
/usr/sbin/unshare --user --map-root-user --pid --fork --kill-child=KILL \
  /usr/sbin/timeout --signal=KILL <timeoutSeconds>s <literal argv...>
```

GNU timeout is PID 1 inside the new PID namespace. When it exits, Linux retires
that namespace's remaining processes, including children that create another
session. `--kill-child=KILL` ties the forked PID 1 to the outer unshare owner's
lifetime for cancellation. The same runner drains both streams and observes the
actual outer exit. Benign real tests cover timeout, explicit cancellation, and
normal command exit with a separate-session pipe holder. An unavailable
installed backend refuses; a denied namespace launch fails with its actual
nonzero exit. This is a process lifetime boundary, not filesystem/network
containment, resource quotas, or product runtime acceptance.

**The opaque-provider actual CLI positive case has passed.** The
private `/proc` mount is denied on this host, so the backend inherits the outer
namespace's `/proc` view. The Linux helper now separates outer-proc PID identity
from namespace-local wait results; an actual compiled child smoke passed ABI,
spawn, observation, session census, reap, and release inside this backend. This
clears that specific PID mismatch. The separate CLI acceptance driver then observed the actual SDK install/create, shared composition, no-replace publication, settled ownership, and strict schema-2 provenance readback with literal arguments. Principal refusal and cancellation cases remain separately reported.
Generic harness reports explicitly record `productEnvironmentQualified: false` and
`procView: inherited-outer-pid-namespace`. A successful harness command does not
upgrade those facts. No container, VM, or delegated systemd/cgroup backend is
currently available, and no alternative is silently selected.

This runner does not promise recovery from arbitrary asynchronous exceptions,
OOM, hostile escape, or competing child reapers. It does not change product
ownership or cleanup contracts and provides no strong provider-profile claim.

## Actual CLI acceptance

`scripts/catalog-cli-acceptance.py` invokes the compiled CLI, rather than an
Effects-only caller. Its inputs come from the compiled original
`CatalogScaffoldRuntimeTests.request` helper through
`scripts/fixtures/catalog-cli-fixture.fsx`; that helper owns genuine fixture
package construction and calls production catalog canonicalization. Build the
actual Commands.Tests assembly first, then run this ordinary input-generation
command through the shared runner:

```text
dotnet fsi --exec scripts/fixtures/catalog-cli-fixture.fsx generate \
  <Commands.Tests.dll> <fresh-private-input-root> <unused-target-path> <request.json>
```

Select the manifest in `scripts/fixtures/catalog-cli-acceptance.example.json`,
then run `python3 scripts/catalog-cli-acceptance.py --config <config.json>
--output <fresh-private-result-root> --case positive`. Only after this first
positive case passes, use `--case negatives`, `--case probes`, or `--case variants` in separate fresh result roots; `--case all` combines the configured cases. The driver
checks actual CLI JSON status/ownership, literal product bytes, strict schema-2
provenance via the production parser, observed tools/invocations, ownership and
skill paths. Negative cases check dry-run observation absence, catalog/policy
raw digest drift, archive tamper, unsupported platform, duplicate options, and
byte-for-byte preservation of an occupied target. An outer timeout is not a
successful product refusal. The data-only `catalog-cli-negative-probes.json` selects a trusted absolute Python tool with literal arguments for cancellation, nonzero exit, output limits, deadlines, stderr/version refusal, and a target appearing before publication. `sealPolicyCommand` invokes the actual production digest and policy parser after changing that independently selected tool; it does not replace the real dotnet transport. These controls are accepted only when the report contains their actual expected diagnostics, settled ownership, target preservation, and no new owned staging. Genuine `generate-missing-binding` and `generate-reserved-write` helper modes reuse the fixture and production catalog producers; configure their manifests under `fixtureVariants`. An unconfigured probe or variant is not coverage. These scoped controls do not close whole C2 acceptance or publish the local Config candidate.

The exercised opaque-provider set includes the positive publication case, seven baseline cases (including dry-run), eight real policy probes, and the two genuine missing-binding/reserved-write variants. Reports retain exact commands, raw archive identity, product source identity before/after, diagnostics, direct-child exit/EOF, target snapshots, and sibling staging snapshots. The aggregate-output case reports `catalog.diagnosticLimit`; the separate per-stream case reports `catalog.workStopped`. Product SIGINT is sent by the selected direct probe to its original CLI parent and observed through the CLI cancellation token; namespace termination does not count as that control. Earlier failed diagnostic attempts remain recorded separately. These observations apply to this local candidate and cooperative host profile, not distributed package availability, whole C2 completion, or stronger provider containment.

The repo-owned PID identity regression is
`CatalogScaffoldLinuxIdentityTests` in Commands.Tests. With a current compiled
assembly, run the following through this same runner in `native-runtime` mode
with `backend: pid-namespace`, `FSGG_SDD_NAMESPACE_REGRESSION=1`, and a 30-second
work / 15-second cleanup interval:

```text
/usr/share/dotnet/dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj \
  -c Release --no-build --no-restore --filter FullyQualifiedName~CatalogScaffoldLinuxIdentityTests
```

The default ordinary route runs one parser test and skips the native case;
the selected namespace route runs both. Both routes passed against the actual
compiled test assembly. This regression covers the child identity/lifecycle
join, separately from complete template/SDK/CLI acceptance.

## Package-only typed authoring input

The optional `examples/provider-authoring` sample can produce the same fixture
manifest without a test assembly or reflection. Build and qualify the sample
against explicitly selected package archives before selecting its output for an
actual CLI run. The example is nonpackable; `emit` and `check` are sample entry
points, not installed `fsgg-sdd` commands.

In the existing acceptance config, replace `fixtureManifest` with literal
`producerCommand` and `producerArguments`. Retain the existing runtime,
verification, source identity and CLI selections. For example:

```json
{
  "producerCommand": ["/usr/share/dotnet/dotnet", "/qualified/Authoring.dll", "emit"],
  "producerArguments": [
    "--template-root", "/inputs/copied-opaque-template",
    "--fixture-executable", "/qualified/TestHandoffFixture",
    "--test-input", "/inputs/test-input.txt",
    "--policy", "/inputs/independent-policy.json"
  ],
  "verifyCommand": ["/usr/share/dotnet/dotnet", "/qualified/Authoring.dll", "check"]
}
```

This fragment extends the existing config; it is not a standalone runner config.
`fixtureManifest` and `producerCommand` are mutually exclusive. The driver
appends `--out <fresh-result-root>/authoring-inputs` and reads its `request.json`.
Do not supply `--out` yourself. Arguments are passed literally, including empty
strings, spaces and metacharacters. The manifest keeps its existing catalog,
archive, policy, independent names and test-handoff fields;
`generatorAssemblySha256` identifies the actual Authoring assembly.

Input production runs through the unchanged maintained harness using the
existing `verification` settings, capped by the original `totalSeconds` and
verification timeout. Its result is recorded as `fixturePreparation`. A failed
command, incomplete cleanup or missing/malformed manifest stops before CLI
execution and cannot qualify the product environment. Strict readback invokes
`verifyCommand <provenance-path> <summary-path>`; the sample's production parser
writes the same summary consumed by the existing assertions. The default
compiled test-helper `fixtureManifest` route remains available unchanged.

Run the focused driver controls with
`python3 scripts/tests/catalog-cli-acceptance.test.py`. They mock selected
commands and exercise the actual driver's default/typed input selection,
literal arguments, original budget, refusal before launch, and failed-preparation
reporting. These controls do not compile the sample, qualify fresh archives,
invoke the SDK or establish the actual authoring-to-CLI-to-Verify journey. That
integration requires the separately selected qualified package/runtime inputs
and the existing SDD and Governance acceptance drivers.


## Selected-template data transport

The catalog edge selects exactly one descriptor template alias from the held multi-template
package and checks its installed identity/configuration/alias against the SDK cache. Unselected
legacy templates confer no execution authority. The selected profile supports literal text,
string and single-valued choice parameters, descriptions and boolean requiredness, plus one
fixed declared-parameter hyphen-to-underscore payload substitution. Generated symbols are not
forwarded as CLI parameters. Static source exclusions use only directory/extension forms;
`**` includes zero directories. Expected payload bytes exclude those paths and retain all
other declared substitutions. Conditions, source renames, arbitrary generators and callbacks
refuse. Synthetic parser/cache tests do not qualify actual SDK matching or a language provider;
the real Node vertical must use its owner's exact once-packed multi-template archive through
the maintained driver and namespace backend. Public contracts and custody remain unchanged.


For an explicitly selected real provider, `fixtureExpectations` replaces only the opaque
fixture's product-file and produced-skill assertions. It requires `files` (each with a
contained `path` and exactly one literal UTF8 `text` or lowercase raw `sha256`),
`absentPaths`, and `producedSkillPaths` (neutral `.agents/skills/.../SKILL.md` paths).
Positive files and skills are mandatory; each inventory is limited to16 entries, expected
text to1MiB total, and each actual file read to1MiB. Links, escaping paths, missing
files, changed bytes, pre-existing excluded outputs and missing produced skills refuse.
Omitting the field preserves the existing opaque defaults. The common strict provenance,
archive/tool/literal argv, target/staging, mirrored/SDD-owned paths and executable-mode
checks still run. These assertions configure the maintained driver; they confer no
provider execution authority or evidence-decoder qualification.

The supported selected-tool profile captures each independently selected probe executable
up to128MiB, retaining its exact bytes and digest for revalidation. This covers the selected
Node24.8.0 executable (129,318,464 bytes). Larger opened files refuse before byte allocation;
archive, produced-payload and command-output limits are unchanged. Nuspec XML is parsed
from its original bounded bytes, including standard UTF8 BOM handling; package identity,
dependency refusal and the raw archive digest remain mandatory.

## External provider source I/O coverage (#935)

Four `ProviderSourceWorkflowTests` facts use the shared
`CatalogNamespaceFactAttribute` and `kind=provider-source-native` trait. The
ordinary unfiltered suites keep 49 source and three new CLI declarations. Unless
explicitly opted in, the four I/O facts are reported as skipped/not executed,
never as passes. Theory expansion and pass counts require actual test results.
The source-census check runs before SDK setup in the gate and Commands release
job; removing a classification or adding an unclassified I/O consumer fails it.

A separately selected native-runtime plan uses the existing pid-namespace
backend, sets `FSGG_SDD_NAMESPACE_REGRESSION=1`, and supplies this literal argv
with the selected SDK executable and a fresh private results directory:

```text
<SDK401>/dotnet test tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj --no-build --no-restore -c Debug --filter kind=provider-source-native --logger trx;LogFileName=provider-source-native.trx --results-directory <private-results>
```

The plan stores this as an argv array; the semicolon is part of the logger
argument, not a shell separator. The flag merely selects discovery. Neither it
nor a PID namespace qualifies the carrier. Actual Linux-x64, .NET10.0.12, exact
glibc2.44 with every required export, complete selected input custody and a
finite external custodian are still prerequisites. A runner label or ordinary
SDK setup does not establish them. The namespace runner's existing limitations
and unknown-resource ownership rules remain unchanged.

Acceptance requires all four named facts executed and passed, zero failures or
skips, current source/binary/input joins, and actual EOF/retirement evidence:

- no-provider inspection lists every captured provider without resolution or reread;
- whole-set provider collision refuses before an inspectable handle exists;
- offline exact cache survives removal of original local sources without acquisition;
- selected source joins reject changed cache bytes and inspection only scaffold targets.

These four results ran and passed on 2026-10-24 on the exact Linux-x64/.NET 10.0.12/glibc 2.44
development host through this runner (`native-runtime`, `pid-namespace`,
`FSGG_SDD_NAMESPACE_REGRESSION=1`, filter `kind=provider-source-native`): 4 of 4 passed with the
source tree identity stable and direct-child exit/EOF observed. Ordinary CI still skips them, so it
cannot close #935 or #928 or grant publication on skipped cases; the installed third-party journeys
are run by `scripts/provider-source-journey.py` against the published tool.

## Governance Verify capture lifetime (SDD928 C3.1-03, #1131)

One Governance Verify provider invocation owns exactly one capture session
(Governance#465 decision, #468 host). The Verify host is the sole caller of
`release`; the OUTER admitted runtime supplies the invocation's original work
and cleanup ends and owns whole-worker teardown. For SDD's package-only
qualification that outer runtime is this runner. The join is:

| Duty | Owner | Mechanism |
|---|---|---|
| Capture session, `release`, settlement and exit mapping | Governance Verify host | `fsgg-governance verify` (provider mode) |
| Original work and cleanup ends | this runner | command `captureLifetime`; the runner is the only supplier of `FSGG_VERIFY_WORK_SECONDS` and `FSGG_VERIFY_CLEANUP_SECONDS` |
| Lifetime bound | this runner | `startupAllowanceSeconds + cleanupEndSeconds <= timeoutSeconds`, refused before launch otherwise |
| Whole-worker teardown | this runner | `native-runtime` + `pid-namespace`: PID 1 of the namespace is `timeout`; when the host exits, or at the runner's work end, the kernel retires every remaining process, including detached sessions |
| Witness that teardown happened | this runner | `descendantWitnessPath`: descendants append `<outer-pid> <start-time>`; afterwards none may still be that process |

```json
{"name": "verify", "mode": "native-runtime", "backend": "pid-namespace",
 "argv": ["/path/fsgg-governance", "verify", "--repo", "...", "--provider-provenance", "..."],
 "timeoutSeconds": 150, "cleanupSeconds": 30,
 "captureLifetime": {"workSeconds": 60, "cleanupEndSeconds": 75, "startupAllowanceSeconds": 5},
 "descendantWitnessPath": "/abs/fresh/witness"}
```

Both ends are whole seconds measured by the host from ITS start, and the
cleanup end is absolute (`cleanupEndSeconds >= workSeconds`), exactly as the
host reads them. The runner starts counting before the host does, so the whole
capture window plus a startup allowance must fit inside the runner's own work
window. Consequently the runner never signals a host that is still inside its
declared window: the host can settle (`release` Ok), refuse, or exit 4 on its
own. `captureLifetime` requires native-runtime mode with the pid-namespace
backend; a bare process group is not whole-worker teardown. `FSGG_VERIFY_*`
in `environment` is refused, so there is a single supplier.

The result's `captureLifetime.outcome` is derived only from what the runner
observed (the host's own exit mapping and reports, never a runner inference):

- `host-exited-after-settlement`: the host exited by itself with a pass (0) or
  blocked (1) result, none of its unsettled reports, inside the admitted
  window. This is the only outcome with `hostSettled: true`.
- `host-refused-input-unavailable`: exit 3 inside the window. The host returns
  it both for refusals before any capture session exists (digest, policy,
  platform, absent ends) and for a planning rejection after one, so it proves
  no settlement and none is claimed; the case's expected diagnostic
  distinguishes the refusal.
- `host-exited-with-retained-session` and `host-exited-with-unsettled-session`:
  exit 4 (the tool error the host also uses to replace an exit of a session that
  did not settle cleanly), or any exit accompanied by the host's "retained",
  "not released ... stays resident" or "did not settle cleanly" reports.
- `host-exited-without-settlement-proof`: any other exit (usage 2, a crash).
- `host-exited-after-its-window`: a 0, 1 or 3 exit after the admitted window.
- `worker-ended-by-outer-runtime`: the runner's work end (or cancellation, or
  the namespace backstop) ended the worker. If the host reported that it "stays
  resident while a read may be pending" this is the designed ending of a
  session whose read cannot be proven finished, and `hostReportedResident` is
  true. It is a non-passing result, never a settlement.

Teardown is checked two ways after the worker ended. `descendantWitnessPath` is
cooperative: descendants append `<outer-pid> <start-time>` and none may still be
that process (an empty witness proves nothing and is not upgraded).
`workerScan` is not: every capture command carries a unique
`FSGG_RUNNER_WORKER_ID` in its environment, and no live process may still carry
it (this finds the runtime host and detached sessions that kept their inherited
environment, not only volunteers). Either finding is a failure
(`descendant-survived-teardown`, `worker-process-survived-teardown`).

A Governance contract change is not required: the join uses the documented
environment interface and exit mapping. Not covered here, and still owned by
Governance#423: an authentic blocked regular-file read and an independently
observed uncertain physical close. Those are the only inputs that make the real
host stay resident; the resident branch of this join is exercised with a
stand-in host (`scripts/tests/capture-lifetime.test.py`), not claimed against
the real one.

### Package-only journey

`scripts/verify-capture-journey.py run --work <fresh-dir>` runs the whole
package-only path with a credential-free environment, an empty private NuGet
cache, at most six CPUs and nuget.org as the only package source:

1. download and install the published `FS.GG.SDD.Cli` and
   `FS.GG.Governance.Cli` bytes (SHA-256 recorded, optionally compared with
   `--expect-sha256`);
2. restore and build `examples/provider-authoring` against the published SDD
   version (every resolved library a package);
3. deterministic double emission, existing-output and malformed-policy refusals;
4. the installed SDD CLI through `catalog-cli-acceptance.py` (`positive` and
   `negatives`) with strict schema-2 readback;
5. commit the scaffolded workspace plus the Governance workspace catalog from
   `tests/fixtures/provider-verify-journey/fsgg`, then run the installed
   `fsgg-governance verify` in provider mode under the join: positive, missing,
   stale, mismatched and non-zero evidence, a detached descendant, a declared
   deadline, four whole-request refusals and the ends-not-supplied refusal.

Each Verify case must also end with the host having exited by itself with a
pass/blocked exit inside the admitted window (refusals: exit 3 with their intended diagnostic, no settlement claimed), every witnessed descendant
absent and the worker scan empty, and each refusal must carry its intended
diagnostic. The published tool package SHA-256 values are pinned in the script
(prefixes recorded on #935/#928, full values checked against nuget.org downloads). The policy, fixture and
Governance catalog are SDD-owner authored fixtures: independent of the sample,
but not supplied by a third party. This is ordinary verification on the
supported Linux-x64/.NET 10.0.12/glibc 2.44 profile, a local cooperative
profile, not hostile containment or provider certification.

## Provider qualification harness and certification report (SDD928 C3, #1134)

`scripts/provider-certification.py` is the reusable harness a provider author
(or a catalog admitter) runs against a PACKED provider artifact, using the
INSTALLED SDD CLI and the INSTALLED Governance CLI. It writes a closed,
versioned report, `fsgg.provider-certification/v1`, suitable for catalog
admission. The reference provider is the opaque-template fixture packed by the
typed authoring example's `emit`.

```sh
python3 -I -B scripts/provider-certification.py certify --request request.json --output FRESH_DIR
python3 -I -B scripts/provider-certification.py check-report FRESH_DIR/certification-report.json
```

`certify` exits 0 only when every required check passed. `check-report` is the
admission-side validator: it requires the closed schema, every required check
present exactly once, a passing check to carry evidence, a failed or skipped
check to say why, verdict counts that match the checks, `certified` true only
when every required check is present and passed, exact tool/artifact digests,
and no machine-specific absolute path. A committed example is
`tests/fixtures/provider-certification/report.example.json`.

The request (`fsgg.provider-certification-request/v1`, closed) names
`sourceRoot`/`identityPaths` (the harness source identity), `environment` (a
credential-free environment for every child), `sdd` (`cliCommand` prefix and
`transportExecutable`), `authoring.checkCommand` (the strict schema-2 readback
command), `artifact.request` (the fixture manifest `emit` wrote: catalog, policy,
archive, digests, overrides, capability and evidence declarations),
`governance` (`tool`, `workspaceCatalog` directory of `.fsgg/*.yml`,
`verifyPaths`, `captureLifetime`, `evidenceCases`) and `tools` (`version` and
`sha256:` package digest of each installed tool).
`evidenceCases` must include `positive` (exit 0) and `missing`, `stale`,
`mismatched`; extra cases are reported as `evidence.<name>`.

| Group | Check ids | What it proves from the packed artifact |
|---|---|---|
| lifecycle | `scaffold`, `dry-run`, `strict-readback`, `deterministic-instantiation` | settled, no owned staging; dry run observes and creates nothing; the strict production codec reads the provenance; two instantiations into different parents are byte-equal |
| provenance | `inputs-bound`, `tools-and-invocations`, `declared-bindings` | schema-2 digests and effective parameters equal the packed inputs; the declared tool at its exact version and the literal template argv; the declared command, arguments and evidence output |
| skill-union | `recorded-and-present`, `mirrors-equal` | recorded provider and SDD-owned skills exist and do not collide; the whole neutral skill set and its `.claude` mirror are equal byte for byte |
| no-partial-state | `occupied-target`, `catalog-digest`, `policy-digest`, `archive-tamper`, `malformed-catalog`, `unsupported-schema-version`, `wrong-tool-version`, `unsupported-platform` | each is refused by the product with its intended diagnostic (not an outer timeout), ownership settled, the target unchanged or never created, no owned staging |
| evidence | `positive`, `missing`, `stale`, `mismatched`, ..., `whole-request-refusal`, `capture-lifetime` | the declared build/test/package command runs through Governance Verify/GateExecution; the evidence is produced and accepted; missing, stale and mismatched evidence is refused; a bad provenance digest is refused before the command launches; every Verify host exits inside the admitted window with no surviving worker process |

Malformed-provider and wrong-version controls are derived from the packed
artifact: a digest-correct catalog with an unknown member (`catalog.malformed`),
the same with `schemaVersion` 99 (`catalog.unsupportedSchema`), and a policy
whose tool probe reports a different version (`catalog.toolVersionRefused`).

Nothing is skipped silently. If a prerequisite fails its dependants are
recorded as `skipped` with the reason and the verdict is not certified; a
changed harness source tree during the run adds a failing check.

`scripts/qualify-reference-provider.py run --work <fresh-dir>` is the
package-only qualification of the reference provider: published SDD CLI and
Governance CLI from nuget.org in an empty private cache, the typed example
compiled against published packages, the artifact packed, certified, and the
report validated. It also proves the harness is not vacuous: a deliberately bad
provider (its positive-case fixture writes evidence citing the wrong bytes) is
NOT certified and fails exactly `evidence.positive`, and each of six
tamperings with the certified report is detected by `check-report`.

The `tools` identities in a report are attested by the request's caller; the
qualification driver derives them from the nuget.org bytes it installed, and an
admitter must not accept them from an author without that provenance.

Scope: ordinary verification under ADR-0096 on the exact Linux-x64 / .NET
10.0.12 / glibc 2.44 profile; a local cooperative profile, not hostile
containment or a hosted-runner profile. Not covered: distribution (C4), the
schema-v1 window, concrete language providers (Templates#441), and the
authentic blocked-read/uncertain-close cases (Governance#423 prerequisite b).
The policy, fixture and Governance catalog are SDD-owner fixtures. A third
party must supply its own artifact, fixture and workspace catalog; the report
certifies that artifact only.
