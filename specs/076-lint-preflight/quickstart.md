# Quickstart: validate the pre-flight authoring lint

End-to-end checks that prove the feature against its success criteria. Run from repo root.

## Prerequisites

- .NET SDK (`net10.0`), repo restored/built: `dotnet build FS.GG.SDD.sln`.
- The CLI runs via `dotnet run --project src/FS.GG.SDD.Cli -- <args>`.

## SC-001 — the four defect classes are caught in one run

```sh
# Combined broken fixture: one artifact exercising all four classes.
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/combined-checklist.md --json
```

Expect: exit **1**; `lint.defects` contains one of each class
`CoverageLine`, `MissingDecisionTag` (via its clarifications fixture), `FrontMatter`, `DuplicateId`
across the fixture set; every defect has a non-empty `correction` and a `grammarPointer`.

## SC-002 / FR-013 — canonical examples lint clean (no false positives)

```sh
for f in docs/examples/lifecycle-artifacts/checklist.md \
         docs/examples/lifecycle-artifacts/clarifications.md \
         docs/examples/lifecycle-artifacts/evidence.yml \
         docs/examples/lifecycle-artifacts/tasks.yml; do
  dotnet run --project src/FS.GG.SDD.Cli -- lint "$f" --json
done
```

Expect each: exit **0**, `lint.outcome = "Clean"`, `lint.defects = []`.

## SC-006 — exit-code taxonomy (CI gate)

```sh
dotnet run --project src/FS.GG.SDD.Cli -- lint docs/examples/lifecycle-artifacts/checklist.md ; echo "clean=$?"      # 0
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md   ; echo "defects=$?"    # 1
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/does-not-exist.md         ; echo "missing=$?"    # 2
dotnet run --project src/FS.GG.SDD.Cli -- lint README.md                                     ; echo "unknown=$?"    # 2
```

## SC-003 — every defect carries a fix hint + resolvable grammar pointer

```sh
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md --json \
  | grep -E '"correction"|"anchor"'
```

Expect: each defect shows a `correction` and an `anchor` that exists as a heading in
`docs/reference/authoring-contracts.md` (enforced by `LintGrammarPointerTests`).

## SC-005 — determinism

```sh
a=$(dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md --json)
b=$(dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md --json)
[ "$a" = "$b" ] && echo "deterministic" || echo "NON-DETERMINISTIC"
```

## FR-016 — `<stage> --explain` (non-blocking, no mutation)

```sh
# Run against a work item whose clarifications.md has a missing decision tag.
dotnet run --project src/FS.GG.SDD.Cli -- clarify --explain --work <id> --json
git status --porcelain    # expect: NO changes written (no state advanced, nothing mutated)
```

Expect: the same defect list as `lint` on that artifact; `nextAction` is `None`; exit follows the
`0/1/2` mapping; the working tree is unchanged.

## FR-015 — unparseable artifact ⇒ single parse defect

```sh
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/unparseable/garbage.md --json
```

Expect: exit **2** (unusable) with a single `Parse`/`Unresolvable` defect — not a cascade.

## Projections (FR-010)

```sh
dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md --text
NO_COLOR=1 dotnet run --project src/FS.GG.SDD.Cli -- lint tests/fixtures/lint/broken-all/checklist.md --rich
```

Expect: `--text` lists the same defects; `--rich` renders panels/tables but degrades to zero-ANSI
under `NO_COLOR`, and its underlying JSON is byte-identical to the `--json` run.

## Full suite

```sh
dotnet test FS.GG.SDD.sln
```

Expect green, including `LintTests`, `LintExitCodeTests`, `LintGrammarPointerTests`, the extended
`ExampleArtifactsContractTests`, and the CLI golden json/text tests.
