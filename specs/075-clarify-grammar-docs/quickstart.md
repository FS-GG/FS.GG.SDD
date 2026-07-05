# Quickstart / Validation Guide

How to prove this feature works end-to-end. All commands run from the repo root
(`/home/developer/projects/FS.GG.SDD`).

## Prerequisites

- .NET 10 SDK (the repo's pinned toolchain).
- A clean build: `dotnet build`.

## 1. Drift & manifest guards stay green after the edits

The edits touch two skill bodies (×2 roots) and the manifest. These tests fail if any root
diverges or the manifest is stale:

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests \
  --filter "FullyQualifiedName~SeededSkills|FullyQualifiedName~ProcessSkillManifest"
```

Expected: green. If red on `embedded == claude` or `claude == codex`, a root was not mirrored;
if red on the manifest sha256/staleness test, run:

```sh
dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --write
dotnet run --project src/FS.GG.SDD.Cli -- registry skill-manifest --check   # must exit 0
```

## 2. The documented grammar matches the live parser

The new reference-doc example blocks are executed through the real parsers:

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter "FullyQualifiedName~AuthoringDocsContract"
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter "FullyQualifiedName~ExampleArtifactsContract"
```

Expected: green — every labelled example (`clarify-decision:*`, `front-matter:*`, `clarify-dup:*`)
produces the outcome asserted in `contracts/documented-grammars.md`, and the worked example
artifact still parses clean.

## 3. SC-001 — author `clarify` from the skills alone

The acceptance criterion (epic #122). Simulate a first-time author who reads **only** the two
skill bodies (not the shipped example, not the source):

1. Read `.claude/skills/fs-gg-sdd-clarify/SKILL.md` and
   `.claude/skills/fs-gg-sdd-authoring-contracts/SKILL.md`.
2. From that alone, hand-write a `clarifications.md` for a work item with an open `AMB-001`:
   correct front matter (gating fields), a `## Decisions` line carrying `[AMB:AMB-001]`, and a
   `## Remaining Ambiguity` disclaimer.
3. Run the real stage against it (in a scratch work item, e.g. under a temp `work/<id>/`):

   ```sh
   dotnet run --project src/FS.GG.SDD.Cli -- clarify --work <id> --text
   ```

   Expected: no `malformedClarificationFrontMatter`, no `missingClarificationAnswer`, no
   `unresolvedBlockingAmbiguity`, no `duplicateClarificationId` — i.e. the stage advances on the
   first attempt (contrast: the TD1 run blocked 4×). The `--text` counters
   (`blockingAmbiguities`, etc.) read zero.

## 4. Full suite

```sh
dotnet test
```

Expected: green across all projects (documentation + one test-file extension; no product
behavior change).

## What "done" looks like

- All of §1–§4 green.
- A reader can satisfy `clarify` using only the skill bodies (SC-001).
- The newly documented grammars are parser-validated in `docs/reference/authoring-contracts.md`
  (SC-003) and byte-identical across `.claude`/`.codex` with a regenerated manifest (SC-004).
