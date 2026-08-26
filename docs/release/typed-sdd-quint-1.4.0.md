---
title: Quint-backed Typed SDD 1.4.0
category: Release
categoryindex: 7
index: 14
description: Stable manifest-v2 Quint authority, offline lifecycle hosting, and authenticated v1 migration.
---

# Quint-backed Typed SDD 1.4.0

`FS.GG.SDD.Artifacts` and `FS.GG.SDD.Cli` 1.4.0 form one stable coherent set. The release preserves
manifest-v1 F# behavior and adds opt-in manifest-v2 `quint-specification-v1` authoring, inspection,
migration, rollback, and shared lifecycle dispatch. Standard SDD remains the omitted default.

## Install and author

Install both packages from the same feed/version, preseed the two qualified cache objects described in
[Typed SDD lifecycle](../typed-sdd-lifecycle.md), and run:

```console
fsgg-sdd typed-sdd author --work demo --title "Demo" \
  --agent agent-id --session session-id \
  --backend quint-specification-v1 --cache /preseeded/quint-cache
fsgg-sdd typed-sdd inspect --work demo
```

The command performs no acquisition. Missing, unreadable, mismatched, nondeterministic, or
semantically inconsistent evidence blocks without publishing a partial authority.

## Compatibility

Manifest v1 and its public record remain unchanged. Manifest v2 APIs are additive. Migration is
explicit and retains a byte-exact authenticated rollback inventory. Provider floors, registry rows,
consumer pins, workspace defaults, and coordinated adoption remain outside this release.

Migration never claims that arbitrary legacy requirements were compiled into the fixed Q1 executable
slice. It carries the complete canonical manifest-v1 model as a lossless semantic payload and binds it
independently in Markdown, the compiled contract, and rollback inventory. Transaction recovery is
hard-kill tested at every author move and during rollback; concurrent inspection waits for the same lock.

## Release proof

The stable release is built once from the independently accepted merge commit. Git tag, nuspec
repository metadata, and source revision must bind that commit. GitHub Packages is published first,
then nuget.org. Both packages are downloaded again; every ZIP entry except nuget.org's feed-added
`.signature.p7s` must be byte-identical. Clean public installs rerun the offline Q2 compiler/replay/
Fable proof and the Q3 author/inspect/migrate/rollback acceptance before completion.
