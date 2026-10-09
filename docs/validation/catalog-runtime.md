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
