# Typed specification kernel P2 mutation controls

Environment: local .NET SDK 10.0.302, Release configuration, isolated worktree
`item/910-typed-specification-kernel`. Authority is the checked-out source and the
test/build outputs below; each mutation was reverted immediately after observation.

## Duplicate identifier validation

- Subject: `Requirements.validateWithEvidence` and
  `TypedSpecificationKernelTests.validation accumulates duplicate…`.
- Mutation: remove `yield! duplicates "/extension" allIds`.
- Census: the then-current 11-test typed-kernel class ran; 10 passed and 1 failed.
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

## JSON projection direct-edit rejection

- Subject: `SpecificationProjection.validateJson` over the deterministic generated
  JSON projection, including authoring/provenance fields intentionally excluded from
  semantic diff.
- Escaping mutation: edit the embedded `provenance.agent` and `intent` values while
  retaining the projection fingerprints. The pre-repair built-artifact harness
  observed `diagnostics=[]`, proving the semantic-diff-only check was bypassable.
- Repair control: the named projection test applies those same two edits; exact
  deterministic projection comparison now emits `SPEC-PROJECTION-DIRECT-EDIT` at
  `/projection/json`.
- Census: the repaired 12-test typed-kernel class passed, including the known-good
  unchanged JSON projection and the negative direct-edit control.
- Result: the original escape is caught; the unchanged projection remains admitted.

## Migration validity and production-shaped wrapping

- Subject: `RequirementsMigration.analyzeMarkdown` over resolved decisions and the
  repository's real wrapped Standard SDD specification.
- Escaping mutations: before repair, a resolved ambiguity without a retained decision
  returned `Migrated` even though extension validation emitted
  `REQ-AMBIGUITY-DECISION-REQUIRED`; the real P2 specification returned `Unsupported`
  with 46 malformed continuation-row findings.
- Repair controls: the test migrates an explicit resolved decision and asserts the
  decision bytes plus an empty validation result; its missing-decision counterpart
  must return `Unsupported`. It also migrates
  `work/typed-specification-kernel-p2/spec.md` and asserts 6 boundaries, 4 stories,
  17 requirements, and zero extension diagnostics.
- Census: all 12 typed-kernel tests pass after logical continuation folding and the
  fail-closed validation classification.
- Result: neither original escape survives, and the production-shaped positive
  control is admitted losslessly.
