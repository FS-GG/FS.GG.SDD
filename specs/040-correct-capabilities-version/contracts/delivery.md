# Contract: Delivery — shared local folder feed + cross-repo registry pin

Covers FR-004, FR-005, FR-006, FR-008.

## Shared local folder feed (in-repo `nuget.config`)

**Config (committed):**

```xml
<!-- nuget.config (repo root) — adds the local feed; does NOT clear inherited sources -->
<configuration>
  <packageSources>
    <add key="fsgg-local" value="./.fsgg-local-feed" />
  </packageSources>
</configuration>
```

- `.fsgg-local-feed/.gitkeep` is committed so the configured source path exists and
  `dotnet restore` stays clean while the feed is empty.
- No `<clear/>`: nuget.org and any inherited sources keep resolving.

**Publish protocol (FR-004 / FR-005):**

```bash
# Pack the new immutable identity 1.0.1 (never re-pack 1.0.0)
dotnet pack src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release \
  -p:Version=1.0.1 -o "$TMP/packages"

# Push into the local folder feed (NuGet writes the hierarchical folder layout)
dotnet nuget push "$TMP/packages/FS.GG.Contracts.1.0.1.nupkg" --source fsgg-local
```

**Resolution contract (SC-002 / SC-005):** a consumer pointing a `nuget.config` at
the same conventional feed path resolves `FS.GG.Contracts 1.0.1`, and reading its
`Fsgg.Schemas` reports `capabilities = 2`, with **zero local literal**. `1.0.0`
remains present and unmutated.

## GitHub Packages — deferred (FR-008)

`.github/workflows/release.yml` is **not** modified. The fsproj `<Version>` is
`1.0.1`, so a future un-deferred release would publish `1.0.1` to
`nuget.pkg.github.com/FS-GG` — coherent and intended, but not triggered by this
feature. For this correction, `1.0.1` is delivered via the local folder feed only.

## Cross-repo registry pin follow-on (FR-006)

- **Where**: the `fsgg-contracts` entry in the `FS-GG/.github` org dependency
  registry (not this repo).
- **Who**: `owner: sdd`.
- **What**: advance the pin `1.0.0` → `1.0.1`.
- **When**: after `1.0.1` is published (so the registry points at a resolvable
  version).
- **How**: a coordination issue + PR via the `cross-repo-coordination` protocol,
  sequenced on the org Coordination board.
- **Acceptance (SC-003)**: registry reads `1.0.1`; contract-coherence workflow
  passes.

This step is bookkeeping outside this repo's tree; it is documented and tracked,
never silently dropped.
