# Quickstart: Verify scaffold repo-init & script-executable steps

A run/validation guide for the post-instantiation behavior. Implementation detail lives in
`tasks.md` and the source; this guide proves the feature end-to-end.

## Prerequisites

- .NET 10 SDK (`dotnet --version`).
- `git` on PATH (for the happy-path repo-init scenarios; one scenario deliberately removes it).
- The in-repo scaffold fixtures under `tests/fixtures/scaffold-provider/`.

## Run the suite

```bash
dotnet test FS.GG.SDD.sln
# or just the scaffold-bearing projects:
dotnet test tests/FS.GG.SDD.Commands.Tests
dotnet test tests/FS.GG.SDD.Cli.Tests
```

## Scenario US1 — scaffolded product lands in an initialized repo

1. Scaffold a fixture provider into a fresh temp dir **outside** any git work tree.
2. **Expect**: a `.git` directory at the product root; the work tree contains the SDD
   skeleton, the provider product files, and `.fsgg/scaffold-provenance.json`; the report's
   `repoInitOutcome = initialized`.

## Scenario US2 — produced shell scripts are executable

1. Scaffold the **`with-script`** fixture (emits a neutral `run.sh`).
2. **Expect**: `run.sh` carries an executable bit; report `executableScriptCount = 1`,
   `executableScriptsSkipped = 0`.
3. Scaffold the **`empty`** (or no-script) fixture ⇒ `executableScriptCount = 0` (reported
   no-op, US2-AC2).

## Scenario US3 — safeguards (existing repo / git absent)

- **Existing repo**: `git init` the temp dir first, then scaffold into it (with `--force`).
  **Expect**: no nested repository; `repoInitOutcome = skippedExistingRepository`; advisory
  `scaffold.repoInitSkippedExistingRepository`; scaffold still succeeds.
- **git absent**: run with `git` removed from PATH (test controls the environment).
  **Expect**: `repoInitOutcome = skippedGitUnavailable`; advisory
  `scaffold.repoInitSkippedGitUnavailable`; normal success outcome and exit code (SC-004).

## Scenario US4 — generic, no provider leak

1. Scaffold a **non-rendering** fixture. **Expect**: identical repo-init + exec behavior.
2. Run `ScaffoldGuardTests` ⇒ no `fs-gg-ui` / `FS.GG.Rendering` / provider-specific
   package/template/path/script-name/docs-URL in the scaffold source union (SC-005).

## Edge / determinism checks

- **Provider failure** (`fails-midway` / `writes-into-fsgg`): post-instantiation steps do
  **not** run; existing failure diagnostic + exit code preserved; `repoInitOutcome =
  notApplicable` (FR-009).
- **Empty success** (`empty`): repo **is** initialized over skeleton + provenance;
  `executableScriptCount = 0` (FR-004 edge).
- **Dry run** (`--dry-run`): no `.git`, no chmod; the hint describes the planned steps
  (FR-008).
- **Re-run / `--force`**: existing-repo case (no nesting); executable bits idempotent (FR-013).
- **Determinism** (FR-012): two identical runs into clean temp dirs in the same environment
  yield byte-identical `scaffold-provenance.json` and report JSON.

## Three-projection parity (FR-011 / SC-006)

For one happy-path run, capture `--json`, `--text`, and `--rich`. **Expect** the same
repo-init outcome and executable-script counts in all three, with `--json` byte-identical to
the default and `--rich` adding/dropping no fact. See
[contracts/report-projection.md](./contracts/report-projection.md).

## References

- Behavior: [contracts/repo-init-step.md](./contracts/repo-init-step.md),
  [contracts/make-executable-step.md](./contracts/make-executable-step.md)
- Staging: [contracts/post-instantiation-staging.md](./contracts/post-instantiation-staging.md)
- Types/diagnostics: [data-model.md](./data-model.md)
</content>
