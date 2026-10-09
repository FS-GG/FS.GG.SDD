# Typed provider-authoring example

This nonpackable example authors one generic test-handoff provider with public SDD package APIs.
It seals a catalog, packages explicitly supplied template data and validates an independently
selected policy. Preparation does not execute a provider or certify its evidence. No test assembly,
reflection, source ProjectReference or sibling checkout is needed by the copied consumer.

Copy this directory and the data under `tests/fixtures/provider-catalog-runtime/opaque-template`
into a disposable working directory. Select an exact qualified SDD package version and feed
configuration independently; `SddPackageVersion` is required and has no default.

```sh
dotnet restore Authoring.fsproj --configfile /absolute/path/to/selected-nuget.config \
  --packages /absolute/path/to/empty-private-cache -p:SddPackageVersion=2.2.0
dotnet build Authoring.fsproj -c Release --no-restore -m:1 \
  -p:UseSharedCompilation=false -p:SddPackageVersion=2.2.0
dotnet bin/Release/net10.0/Authoring.dll emit \
  --template-root /absolute/path/to/copied-template \
  --fixture-executable /absolute/path/to/pinned-fixture-apphost \
  --test-input /absolute/path/to/tests-input.json \
  --policy /absolute/path/to/independently-selected-policy.json \
  --out /absolute/path/to/absent-output
```

The `2.2.0` recipe selects retained local compatibility inputs, including the qualified Config
0.3 candidate and published Contracts7.6. It establishes early compilation only. Select qualified
final2.3 archives and repeat cold package-only compilation before claiming that acceptance;
source versions or a local version string do not prove package availability. This example creates
no release artifact and changes no repository feed or installed default.

`emit` accepts five unique explicit options, preserves raw policy/test bytes and writes the existing
`request.json` fixture manifest with catalog, policy and archive paths/digests. The declaration
requires Linux-x64, `test:test`, real fixture version1.0.0 and opaque evidence format
`fsgg.governance-handoff@2.0.0`. The policy must independently support these exact declarations,
the selected fixture's `--version` executable, the template package and budgets. It is validated
with the production parser and complete capability resolver; the sample does not synthesize policy.

The declared literal command is the pinned apphost followed by `run --input inputs/tests.json
--output out/governance-handoff.json` and three arguments: an empty string, `with spaces`, and
`$(literal);&`. Its cwd is `.` and admitted environment is `fixture-local`. The independently selected
Governance caller must supply `FSGG_FIXTURE_VALUE=admitted` and remove `FSGG_FIXTURE_AMBIENT` for
the selected genuine fixture. `emit` never probes or launches the executable. Its whole adjacent
runtime closure must be selected and qualified independently; an apphost hash alone is insufficient.

The supplied template contributes its existing nuspec and content bytes. The sample adds unchanged
`inputs/tests.json` and an empty `out/.gitkeep`; handoff output must be absent before execution.
Archive entries sort ordinally with fixed1980 timestamp, zero attributes and no compression.
Creation order and file mtimes do not affect archive identity. Duplicate paths and links refuse.
Outputs require an absent directory with an existing parent; a failure may leave sample-owned
partial output for inspection, which must not be reused as a successful manifest. This is a
cooperative authoring example, not hostile pathname isolation.

The maintained CLI driver can select `producerCommand: [dotnet, Authoring.dll, emit]` and literal
`producerArguments` containing the four input option/value pairs. It appends `--out` to its fresh
input directory. Do not also supply `fixtureManifest`. Set `verifyCommand` to
`[dotnet, Authoring.dll, check]`; the driver appends provenance and summary paths. `check` uses
the strict production schema2 codec and emits the existing summary. It establishes structural
correspondence, not authentic execution or Governance acceptance.

Use the existing maintained harness and CLI→Verify journey described in
[the validation guide](../../docs/validation/catalog-runtime.md). Actual command/evidence outcomes,
source identity and cleanup are independent of authoring preparation. C3.1 remains open beyond
this bounded example; distribution, installed adoption and the Governance runtime carrier are
separate.

## Repeating the deterministic check

Run `emit` twice against two explicit copies of the same template bytes, using the same executable,
test input and policy. Change input creation order and mtimes before the second run, choose separate
absent output directories, and compare the actual outputs:

```python
from pathlib import Path
first = Path("/absolute/path/to/first-output")
second = Path("/absolute/path/to/second-output")
for name in ("catalog.json", "opaque-fixture.nupkg", "policy.json"):
    assert (first / name).read_bytes() == (second / name).read_bytes()
```

The local2.2 compatibility run passed ten actual sample commands: two emissions; existing-output,
malformed-policy, relative-executable, linked-input, duplicate-input and duplicate-option refusals;
and real schema2 check plus malformed-provenance refusal. First compile's summary byte-array/span
overload failure was retained and repaired. These are authoring/helper results, not a final2.3
package or actual new CLI→Verify acceptance claim.
