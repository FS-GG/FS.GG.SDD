# Quickstart: Lifecycle/CLI Semantics Papercuts

Runnable validation scenarios proving the five papercuts are fixed. Each maps to a
user story and its acceptance scenarios. Commands assume a built `fsgg-sdd` and a
work item `<id>` driven to the relevant stage; see the per-stage fixtures under
`tests/fixtures/**` and the command test suites for the canonical, automated form.

## Prerequisites

```bash
dotnet build FS.GG.SDD.sln
# fsgg-sdd is the CLI entry point (src/FS.GG.SDD.Cli/Program.fs)
```

Run the focused suites that gate this feature:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests   # checklist, specify, clarify, verify, ship, help
dotnet test tests/FS.GG.SDD.Artifacts.Tests  # ambiguity parse, work-model currency
dotnet test tests/FS.GG.SDD.Cli.Tests        # help projections + degradation
dotnet test tests/FS.GG.SDD.Validation.Tests # determinism matrix
```

## US1 — Fix-and-re-run reflects the fix truthfully

### §3.1 checklist purge-and-re-derive (FR-001, SC-001)

1. Drive `<id>` to a `checklist` with at least one `fail` row.
2. Fix the offending source (add the missing acceptance coverage) and re-run:
   ```bash
   fsgg-sdd checklist --work <id>
   ```
3. **Expect**: the report and `work/<id>/checklist.md` contain **no** row derived
   from the superseded snapshot; the corrected requirement is `pass`; any
   still-failing requirement remains `fail`; no manual deletion of `checklist.md`.
4. Re-run again with no change → `outcome: noChange`, byte-identical output.

### §3.2 specify edit reported truthfully (FR-002, SC-002)

1. With `<id>` at `status: specified`, edit `work/<id>/spec.md` content.
2. Re-run:
   ```bash
   fsgg-sdd specify --work <id> --json
   ```
3. **Expect**: the report either reflects the edited content (requirement/ambiguity
   facts re-parsed) **or** states unambiguously that `specify` promotes only the
   first draft and that downstream stages read the live `spec.md` — never a bare,
   ambiguous result that leaves you unsure whether the edit is consumed.

## US2 — A correct, complete run ends clean (§3.4, FR-005–007, SC-004)

1. Drive `<id>` to a verification-ready state with a current work model.
2. Run:
   ```bash
   fsgg-sdd verify --work <id> --json
   fsgg-sdd ship   --work <id> --json
   ```
3. **Expect**: neither report carries a `staleGeneratedView` advisory whose sole
   cause is the stage writing its own readiness file; `verify` ends clean and
   `ship` reports `shipReady` (not `advisory`). No trailing `refresh` needed.
4. **Genuine staleness still flags**: edit an upstream authored source
   (e.g. `spec.md`) after the work model was generated, then re-run `verify` →
   `staleGeneratedView` **is** reported (FR-007).

## US3 — "No open questions" without blocking (§3.3, FR-003/004, SC-003)

1. Author a spec whose `## Ambiguities` section reads, as a bullet:
   ```markdown
   ## Ambiguities

   - None outstanding
   ```
2. Run:
   ```bash
   fsgg-sdd clarify --work <id>
   ```
3. **Expect**: no blocking ambiguity is raised; `clarify` proceeds.
4. **Genuine ambiguity still blocks**: replace with `- AMB-001 open: …` → `clarify`
   blocks as before. A disclaimer **plus** a real `- AMB-001 …` bullet blocks on
   AMB-001 only.

## US4 — `--help` discoverability (§3.5, FR-008–011, SC-005/006)

```bash
fsgg-sdd --help            # top-level usage: commands + global flags, exit 0
fsgg-sdd -h                # same, exit 0
fsgg-sdd verify --help     # verify's flag listing, exit 0
fsgg-sdd --help --json     # top-level help as canonical JSON
fsgg-sdd --help --rich     # Spectre rendering; degrades to plain text when piped
fsgg-sdd frobnicate        # unknownCommand, exit 1
fsgg-sdd frobnicate --help # unknownCommand, exit 1 (unknown wins)
```

**Expect**: every help invocation returns usage/flag content and exits 0 with zero
`unknownCommand` responses; the command list includes every lifecycle command plus
`version`/`validate`/`registry`; genuinely unknown commands still return
`unknownCommand`. Verify determinism:

```bash
diff <(fsgg-sdd --help --json) <(fsgg-sdd --help --json)   # empty
NO_COLOR=1 fsgg-sdd --help --rich | cat                    # zero ANSI escapes
```

## Determinism & contract gates

- `CommandReportJsonTests` / `DeterminismMatrixTests` — default/`--json` and
  `--text` are byte-stable; regenerate the `help` golden and the rewritten
  checklist/ship goldens through them.
- Schema-reference conformance — `docs/release/schema-reference.md` and
  `release-readiness.json` must list the additive `help` jsonField and agree with
  produced output.
