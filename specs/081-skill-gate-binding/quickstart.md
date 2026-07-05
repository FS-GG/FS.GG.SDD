# Quickstart: validating the skill↔gate binding

**Feature**: 081-skill-gate-binding · **Date**: 2026-07-05

How to prove this feature works end-to-end. All steps are offline, no Governance runtime, no network (FR-013).

## Prerequisites

- .NET SDK (`net10.0`), the repo restored/built: `dotnet build FS.GG.SDD.sln`.

## 1. The corpus passes the real gates (US1)

The doctest runs the example corpus through the real `fsgg-sdd` gate commands.

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~SkillGateDoctest
```

**Expected**: green. Each stage skill's marked example is reported as exercised against its gate, and the corpus yields zero blocking diagnostics.

Manual cross-check — copy the corpus into a fresh workspace and drive it:

```sh
tmp=$(mktemp -d); cd "$tmp"
fsgg-sdd init
# copy docs/examples/lifecycle-artifacts/* into work/<id>/ per the corpus workId, then:
fsgg-sdd checklist --text     # coverage registers; no blocking findings
fsgg-sdd evidence  --text     # deferral accepted; no missingDeferralRationale
```

**Expected**: each gate reports ready/no-blocking; the deferral-bearing `evidence.yml` is accepted.

## 2. Drift is caught (US2) — the red-branch demonstration (SC-003)

Prove the doctest actually fails on drift:

```sh
# Revert the specify skill's FR example to the known-bad bold form, or edit the
# corpus spec.md FRs to `- **FR-001**: ...`, then:
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~SkillGateDoctest
```

**Expected**: **red**, with a message naming the offending skill/corpus file and the blocking diagnostic (coverage not registered). Revert → green.

## 3. Field lists match the typed contract (US3)

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~RequiredFieldContract
```

**Expected**: green — the evidence skill names all `requiredDeferralKeys` (`rationale`, `owner`, `scope`, `laterLifecycleVisibility`); the clarify skill names `sourceSpec`; the authoring-contracts §5 table matches the `RequiredKeys` registry. Add a required key to the registry without updating a skill → red naming the field + skill.

## 4. The diagnostic names its real cause (US4)

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Checklist
```

Author a checklist `CR-###` review line without a `[CHK:CHK-###]` back-reference and run the gate:

**Expected**: the blocking diagnostic id is `missingChecklistBackReference` (not `malformedChecklistFrontMatter`); a genuinely malformed front-matter checklist still reports `malformedChecklistFrontMatter`.

## 5. Guards stay green (mirror, seed, manifest, surface)

```sh
dotnet test tests/FS.GG.Contracts.Tests            --filter FullyQualifiedName~SkillMirror
dotnet test tests/FS.GG.SDD.Commands.Tests         --filter FullyQualifiedName~SeededSkills
dotnet test tests/FS.GG.SDD.Commands.Tests         --filter FullyQualifiedName~ProcessSkillManifest
dotnet test tests/FS.GG.SDD.Commands.Tests         --filter FullyQualifiedName~PublicSurface
```

**Expected**: all green — `.claude`≡`.codex`, seeded set matches, `skill-manifest.json` regenerated to the new body hashes, and `PublicSurface.baseline` (Commands + Cli) carries the new diagnostic symbol.

## Full suite

```sh
dotnet test FS.GG.SDD.sln
```

**Expected**: green. Definition of done for the feature = every step above passes and the four child issues' symptoms are covered by durable checks, not one-time edits.
