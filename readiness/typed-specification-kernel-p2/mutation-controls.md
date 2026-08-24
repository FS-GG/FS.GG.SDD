# Typed specification kernel P2 mutation controls

Environment: local .NET SDK 10.0.302, Release configuration, isolated worktree
`item/910-typed-specification-kernel`. Authority is the checked-out source and the
test/build outputs below; each mutation was reverted immediately after observation.

## Duplicate identifier validation

- Subject: `Requirements.validateWithEvidence` and
  `TypedSpecificationKernelTests.validation accumulates duplicate…`.
- Mutation: remove `yield! duplicates "/extension" allIds`.
- Census: the full 11-test typed-kernel class ran; 10 passed and 1 failed.
- Positive control: the unchanged test also observed the known-present unresolved
  acceptance, unresolved evidence, user-value, provenance, and schema diagnostics.
- Result: exit 1; `REQ-ID-DUPLICATE` was absent from the observed code collection.

## API-surface drift

- Subject: the authored `SpecificationKernel.fsi` and committed
  `docs/api-surface` mirror.
- Mutation: remove `SpecificationId.value` from the committed mirror only.
- Census: `surface --check` enumerated 66 authored signatures, with 0 missing and
  0 orphan baselines.
- Positive control: it identified the known changed signature path exactly.
- Result: exit 1; `surfaceDrifted: 1` for
  `src/FS.GG.SDD.Artifacts/TypedSpecifications/SpecificationKernel.fsi`.

## Release-readiness projection

- Subject: `docs/release/release-readiness.json` against
  `ReleaseContract.currentRelease()`.
- Mutation: change the generator version to `1.3.0-preview.mutated`.
- Census: the full 19-test `ReleaseContractTests` class ran; 18 passed and 1 failed.
- Positive control: the failure reported both canonical `1.3.0-preview.1` and the
  mutated value at the exact differing position.
- Result: exit 1 in `T017 the published docs artifact matches the contract`.

## Process-skill manifest

- Subject: the embedded Claude authoring-contract skill and committed process
  `skill-manifest.json`.
- Mutation: append `MUTATION` to the skill title, rebuild the CLI so the embedded
  resource changed, then run `registry skill-manifest --check --root .`.
- Census: the command regenerated the complete 16-skill manifest model.
- Positive control: the unchanged rebuilt command subsequently wrote and checked
  the canonical manifest; Claude and Codex skill bytes compare equal.
- Result: exit 1 with `skill-manifest.json is STALE`.

## Clean package consumer

- Subject: packed `FS.GG.SDD.Artifacts.1.3.0-preview.1.nupkg` consumed by the
  isolated fixture under a temporary directory and local NuGet feed.
- Mutation: add a `ProjectReference` shortcut marker to the consumer project.
- Census: the fixture packed both producer packages, restored the preview from the
  isolated feed, ran the public compiler/codec/projection path, then scanned every
  project/props/targets file in the copied consumer tree.
- Positive control: the production route completed with
  `typed-specification-consumer: ok` before the independence scan evaluated the
  known mutation.
- Result: exit 1 with `clean consumer contains a forbidden source or dependency shortcut`.

## Preview version-axis support

- Subject: `surface` version-axis interpretation for SemVer prerelease values.
- Mutation: bypass `parseCoreVersion` and send `1.3.0-preview.1` directly to the
  numeric-triple parser.
- Census: the full 67-test `SurfaceCommandTests` class ran; 64 passed and 3 failed.
- Positive control: ordinary stable triples continued to resolve in the same run.
- Result: exit 1; V1b, V1c, and V13 each observed `unparseable` where `resolved`
  was required.
