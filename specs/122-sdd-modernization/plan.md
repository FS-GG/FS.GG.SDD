# Implementation Plan: Risk-Scaled SDD

**Branch**: `item/937-sdd-modernization` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md)

## Summary

Replace artifact-count confidence with a three-profile lifecycle. Candidate-bound observed execution and independent critic review become the ordinary confidence path; provenance remains visible but does not independently determine readiness. Preserve all legacy schemas while changing new guidance and CI defaults. Add a deterministic, conservative path classifier so required GitHub contexts always report but expensive work runs only for relevant profiles.

## Technical Context

**Language/Version**: F# on .NET 10; POSIX Bash for pre-build CI classification

**Primary Dependencies**: Existing FS.GG.SDD Artifacts/Commands libraries, xUnit, GitHub Actions

**Storage**: Existing Markdown/YAML/JSON lifecycle artifacts; no new database

**Testing**: xUnit semantic tests, shell mutation tests, CLI smoke tests, exact-candidate GitHub checks

**Target Platform**: Linux/macOS/Windows CLI consumers; Ubuntu GitHub Actions runners

**Project Type**: Multi-package library and CLI product

**Performance Goals**: Small pull requests do not enter the approximately ten-minute full gate; normal changes use the existing fast tier; high-risk changes retain the full suite and exact Quint acceptance

**Constraints**: Stable required context names; backward-readable schemas; conservative fallback; no weakening of release, migration, authority, destructive, security, or public-contract controls

**Scale/Scope**: Ten lifecycle stages, fourteen files in a representative current package, five PR workflows, roughly 1,787 full-suite tests, and 117 files mentioning synthetic provenance

## Constitution Check

- Principle I: satisfied through specification, public-contract review, semantic tests, then implementation. No new F# public surface is required for the first slice.
- Principle II: intentionally amended. The current compulsory artifact list conflicts with FR-003; the constitution will distinguish decision-bearing requirements by risk instead of requiring every artifact for every item.
- Principle VI: intentionally amended. Tests remain mandatory, but fixture provenance becomes metadata and the control focuses on candidate-bound observed execution.
- Principle VII: satisfied by updating both Claude and Codex guidance from one authored contract.
- Principle VIII: satisfied; missing comparison input and unrecognized paths select high risk.
- Tier: Tier 1 because readiness semantics, CLI behavior, generated guidance, and CI policy change.
- Temporary exception: this modernization is delivered through the old full route because that route is still authoritative until the amendment merges. The exception does not bypass tests or review.

Post-design check: the design preserves compatibility, observability, candidate binding, and protected controls. The two deliberate constitutional changes are the feature outcome, not unresolved violations.

## Project Structure

### Documentation

```text
specs/122-sdd-modernization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── evidence-confidence.md
│   └── risk-profile.md
└── tasks.md

docs/
└── sdd-modernization.md
```

### Source and verification

```text
.specify/memory/constitution.md
.specify/presets/fsharp-opinionated/
.github/workflows/gate.yml
scripts/ci-risk
scripts/tests/ci-risk.test.sh
src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Evidence.fs
src/FS.GG.SDD.Commands/CommandWorkflow/HandlersVerify.fs
tests/FS.GG.SDD.Commands.Tests/
.claude/skills/fs-gg-sdd-*/SKILL.md
.codex/skills/fs-gg-sdd-*/SKILL.md
```

**Structure Decision**: Keep compatibility in existing artifact and command modules, add one dependency-free classifier at the pre-build boundary, and update authored guidance at its existing producer roots. The workflow consumes the classifier rather than reimplementing path policy in YAML.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Amend compulsory artifact doctrine | The doctrine is the bureaucracy being modernized | A feature-local exception would leave every later item paying the old process |
| Bash pre-build classifier beside F# product | CI must decide before restore/build | A compiled classifier requires paying the expensive setup it is meant to skip |

## Delivery Slices

1. Modernize doctrine and generated guidance: risk profiles, concise decision package, evidence confidence rules, compatibility.
2. Change readiness semantics so synthetic metadata does not override a valid observed pass; retain observed-receipt and stale-artifact refusals.
3. Add and mutation-test conservative CI risk classification; preserve required contexts and select fast/full work by profile.
4. Exercise all profiles, run exact-candidate tests, obtain independent critic acceptance, and document measured before/after behavior.
