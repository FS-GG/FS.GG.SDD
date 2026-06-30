# Contract: Authoring Reference Document & Drift Guard

## New file: `docs/reference/authoring-contracts.md`

Required sections (each backed by R1/R2/R3 in research.md):

1. **Acceptance coverage line**
   - The accepted form: a list item `- FR-###: <text>` with `AC-###` (and optional `US-###`) on
     the **same line**.
   - At least one copyable accepted example.
   - An explicit "does not establish coverage" list: bold `**FR-###**`, bracketed-only AC tags,
     colon-less lines — with the one-sentence reason (loose id-scan vs. strict reference-scan).

2. **`evidence.yml` declarations**
   - The `kind` vocabulary: `implementation · verification · review · generated-view · synthetic ·
     deferral · note · missing`, and the note that **unknown kinds silently become `verification`**.
   - The `result` vocabulary: `pass · fail · deferred · missing · stale · advisory · blocked`.
   - The **satisfaction rule**: an obligation is satisfied only by a matching **non-synthetic**
     declaration whose `result` is `pass`; a synthetic `pass` and a `deferral` do not satisfy it.
   - A copyable satisfying example declaration.
   - **Disambiguation (FR-011)**: a short note that this `evidence.yml` is the SDD **lifecycle**
     evidence contract, and that a scaffolded product may ship a *separate, unrelated* "evidence"
     document that does not describe this contract. No external provider package/path/URL named.

3. **`specify --input` intent facts**
   - The three facts (user value, scope, measurable requirement) and the **labeled-line** form the
     parser accepts (`value:` / `scope:` / `requirement:`), noting an unlabeled line is read as
     user value.

## Machine-checkable example tags (drift guard input)

Canonical examples are placed in fenced blocks with an info-string label so a test can extract
them deterministically:

- ` ```text coverage:accepted ` … one coverage line per non-blank row → each MUST yield ≥1
  acceptance reference in `.RequirementReferences` (via `parseSpecificationFacts`).
- ` ```text coverage:rejected ` … each MUST yield **0** references.
- ` ```yaml evidence:satisfied ` … a declaration (parsed by `parseEvidence`) that MUST satisfy its
  obligation under the non-synthetic-`pass` rule.
- ` ```yaml evidence:unsatisfied ` … a declaration that MUST NOT satisfy (e.g. synthetic pass,
  `fail`, `deferred`).

The labels are a documentation-internal convention (not a public schema); they exist so the doc
and the tool are co-verified.

## New test: `tests/FS.GG.SDD.Commands.Tests/AuthoringDocsContractTests.fs` (SC-005)

- Reads `docs/reference/authoring-contracts.md` via a stable repo-relative path (mirror existing
  fixture-reading tests).
- **Drives the public parse surface, not the raw internals.** `requirementReferences`
  (`Specification.fs`) and `parseEvidenceKind` (`Evidence.fs`) are restricted by their `.fsi`
  files and there is no `InternalsVisibleTo`, so they are not callable from the test assembly;
  calling them would require widening the public surface (a Tier-1 change this feature does not
  take, keeping Principle III PASS). The guard instead uses:
  - **Coverage** — `Specification.parseSpecificationFacts` on a minimal spec `FileSnapshot`
    wrapping each example line, then asserts on the resulting `.RequirementReferences`
    (populated by the real `requirementReferences`). `coverage:accepted` ⇒ ≥1 reference carrying
    an acceptance-scenario id; `coverage:rejected` ⇒ 0.
  - **Evidence** — `Evidence.parseEvidence` on an evidence `FileSnapshot` wrapping each block,
    yielding `EvidenceDeclaration` records with the real `kind`/`result`/`synthetic` vocabulary
    (`kind` already mapped via the real `parseEvidenceKind`). Satisfaction is then the
    non-synthetic-`pass` rule `normalizedEvidenceResult result = "pass" && not synthetic` — the
    same rule the verify/evidence disposition ladders apply, re-expressed in one line in the test
    because it is not exposed as a public predicate (T002 keeps the test rule in sync with the
    handler ladder).
- Fails if any documented accepted form is rejected by the parser, or any rejected form is
  accepted — i.e. the docs can never silently drift from behavior.

## Touch points (mirrors)

- `docs/quickstart.md`: add a coverage-line example at the `checklist` stage and a satisfying
  `evidence.yml` snippet at the `evidence` stage (FR-005); link the reference.
- `docs/index.md`: link `reference/authoring-contracts.md`.
- `.claude/skills/fs-gg-sdd-project/SKILL.md` and `.codex/skills/fs-gg-sdd-project/SKILL.md`:
  add an equivalent short "authoring contracts" subsection (FR-013) — the same facts, kept in
  sync between the two surfaces.
