# Phase 0 Research: Prompt the Coherent-Set Version Bump

**Feature**: `094-surface-version-bump` | **Date**: 2026-07-08

Every finding below was verified against the tree at `d1c6e20`, not inferred. R4 and R8 were
additionally **executed** under `dotnet fsi` — their tables are observed output, not reasoning.

## R1 — The classification is already computed before the baseline write

`HandlersSurface.computeSummary` builds its `classified` list from the interpreted read snapshots,
derives `writes` (the `--update` baseline `WriteFile` effects) from that same list, and *then* folds
`classification` out of it. The classification therefore describes the drift **as observed at run
start**, in both modes — the `--update` writes are only *planned* at that point, never applied.

**Consequence**: AMB-002 ("prompt under `--update` too") needs no restructuring of the handler. The
prompt reads `summary.Classification` exactly as `--check` does. `computeSurfaceNext` already appends
`driftDiagnostics @ orphanDiagnostics` after `computeSummary`; the version prompt is a third list in
that position, differing only in that it is **not** gated on `not model.Request.SurfaceUpdate`.

## R2 — A missing file is a first-class, non-failing read result

`CommandEffects.snapshotIfExists` returns `None` when the file is absent, and the effect is still
recorded in `InterpretedEffects` with `Snapshot = None`. `Foundation.snapshot path model` therefore
yields `None` for "read, and it wasn't there", while `hasInterpreted` stays `true`.

`HandlersSurface.readGate`'s own comment states the invariant: *"a missing baseline stays absent after
its read, so the gate resolves on read interpretation, not presence."*

**Consequence**: `versionAxisState: undeterminable` for a missing axis file falls out of the existing
machinery. No new effect kind, no `Exists` probe, no exception path.

## R3 — `--param` is parsed generically; only `--help` text changes

`Cli/Program.fs:parseParams` collects every repeatable `--param key=value` into
`CommandRequest.Parameters` for **all** commands. `Foundation.surfaceParam key fallback request` then
picks a key out with a whitespace-guarded fallback.

**Consequence**: `versionAxisFile` / `versionAxisProperty` need **zero** flag-parsing changes.
`Program.fs` drops out of the touch-set. Only `CommandHelp.fs` gains the two documented keys (FR-016).

## R4 — `Fsgg.Version.tryParse` rejects pre-release and build metadata

`tryParse` splits on `'.'`, demands exactly three segments, and parses each with
`NumberStyles.None` + invariant culture (explicitly to reject signs and whitespace). So:

| Text | Result |
|---|---|
| `0.8.0` | `Some {0;8;0}` |
| `1.2.3-beta` | `None` — `tryInt "3-beta"` fails |
| `1.2.3+sha` | `None` — `tryInt "3+sha"` fails |
| `1.2` | `None` — two segments |
| ` 1.2.3` | `None` — `NumberStyles.None` rejects the leading space |

**Consequence**: FR-006's `unparseable` state is exactly `tryParse = None` on a present element. The
spec's decision *not* to widen the shared grammar is load-bearing: `Fsgg.Version` lives in
`FS.GG.Contracts`, whose assembly is ApiCompat-gated (`scripts/apicompat-check.sh` +
`CompatibilitySuppressions.xml`), so widening it is a cross-repo contract change, not a local edit.
Note the leading-space row: FR-002's *trim before parse* is therefore mandatory, not cosmetic —
`<Version>\n  0.8.0\n</Version>` is the common formatting and would otherwise be `unparseable`.

## R5 — `bumpFor` and `bumpRule` are NOT duplicates

| Function | Domain | `major` | `minor` | third case |
|---|---|---|---|---|
| `HandlersSurface.SurfaceClassify.bumpFor` | classification string | `"breaking"` | `"additive"` | `cosmetic`/`none` → `"none"` |
| `ReleaseContract.bumpRule` | `ChangeClass` DU | `Breaking` | `Additive` | `Clarifying` → `"patch"` |

They agree on two of three cases and diverge on the third, because the third case *means* different
things: a **cosmetic `.fsi` edit** has, by construction, no member-token delta (a comment, a blank
line, a reordering) and warrants no release; a **clarifying contract change** warrants a patch.

**Consequence**: unification is a behavior change, not a refactor. Rejected (AMB-005). This removes
`ReleaseContract.fs`/`.fsi` from the touch-set — and with it one of the two file collisions with
FS.GG.SDD#177, which is actively editing `ReleaseContract` for the ship-verdict catalog entry.

## R6 — Projection conventions (report JSON ≠ authored YAML)

- **JSON**: nested optional scalars are written as an explicit `null` under a **stable key set** —
  `writeScaffold`/`writeDoctor` both do `match … with Some v -> WriteString(k, v) | None -> WriteNull k`
  for `requiredMinimumCliVersion`. `writeSurface`'s own comment demands "a stable shape" for the
  automation contract.
- **Text**: `defaultArg x "(none)"`, and the line is *always* emitted (`scaffoldRequiredMinimumCliVersion`,
  `doctorRequiredMinimum`).
- **Rich**: `Cli/Rendering.fs` **auto-derives** its rows from the text projection's `key: value` lines.
  Feature 087 needed zero rich changes for exactly this reason (see the comment at
  `CommandRendering.fs:480`).

**Consequence**: FR-014 costs one serializer edit + one text-renderer edit and **no** `Cli/Rendering.fs`
change. Feature 091's key-omission rule is about the authored `evidence.yml` and does not reach the
`CommandReport`.

## R7 — There is no root-containment guard today

`Foundation.normalizeRelativePath` does `Trim() → Replace('\\','/') → TrimStart('/')`. It does not
reject a `..` segment, and `CommandEffects.fullPath projectRoot path` then combines it, so a
`--param baselineRoot=../elsewhere` escapes the workspace root today.

**Consequence**: FR-017's guard is genuinely new code, not a documentation of existing behavior.
Scoped to `versionAxisFile` alone; retrofitting `sourceRoot`/`baselineRoot` is behavior-changing for
any workspace relying on the hole and is deferred to its own item (spec § Deferred).

## R8 — No XML reader exists in `src/`

`grep -rln "XDocument\|XmlDocument" src/` returns nothing; `Directory.Build.local.props` holds this
repo's `<Version>0.8.0</Version>`. The version-axis read is the first XML consumption in SDD source.

**Consequence**: `System.Xml.Linq` is a BCL namespace on `net10.0`, needs no package reference, and a
single `XDocument.Parse` + `Descendants() |> Seq.tryFind (fun e -> e.Name.LocalName = property)` is
idiomatic F# under Principle IV (no reflection, no type provider, no MSBuild API). Matching on
`LocalName` rather than the full `XName` is deliberate: some repos' `Directory.Build.props` declares
the legacy `http://schemas.microsoft.com/developer/msbuild/2003` namespace, and a namespace-sensitive
lookup would silently return `undeterminable` there.

`XDocument.Parse` throws `System.Xml.XmlException` on malformed input — the one exception path, caught
and mapped to `undeterminable` (spec edge case; FR-006).

**Executed** (`dotnet fsi`, `XDocument.Parse` + `Descendants() |> tryFind (LocalName = "Version")` +
`.Value.Trim()`):

| Input | `.Value.Trim()` | `tryParse` | State |
|---|---|---|---|
| `<Version>0.8.0</Version>` | `0.8.0` | `Some` | `resolved` |
| `<Version>\n  0.8.0\n</Version>` | `0.8.0` | `Some` | `resolved` — trim is load-bearing |
| `<Version>0.8.0<!-- pinned --></Version>` | `0.8.0` | `Some` | `resolved` — comments ignored |
| `<Version>0.8.0<X>zz</X></Version>` | `0.8.0zz` | `None` | `unparseable` — child text concatenates |
| two `<Version>` elements | first | `Some` | `resolved` — document order |
| `xmlns="…/msbuild/2003"` | `3.1.4` | `Some` | `resolved` — **a namespace-sensitive `XName` lookup would have returned `undeterminable` here** |
| `<Other>1.0.0</Other>` | — | — | `undeterminable` |
| `<Project><Version>0.8.0</Ver` | — | — | `XmlException` ⇒ `undeterminable` |

The MSBuild-2003 row is why FR-002 specifies matching on `LocalName`. It is the one place a reasonable
implementation silently produces the wrong answer.

## R9 — Report version and release catalog

`CommandReports/ReportAssembly.fs` pins `ReportVersion = "1.3.0"` with a running changelog comment.
An additive field set bumps it one minor → `1.4.0`. `ReleaseContract.fs`'s `inventory` list names only
**top-level** report blocks; feature 087 added nested fields and did not touch it, nor
`docs/release/release-readiness.json` or its baseline twin.

**Consequence**: this feature nests inside the existing `surface` block, so the release catalog and its
two JSON baselines stay untouched. `docs/release/schema-reference.md` still gains the prose (that is
what 087 did).
