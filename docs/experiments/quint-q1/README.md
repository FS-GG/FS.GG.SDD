# Quint-first Q1 cross-domain qualification

This directory is the review surface for FS-GG/FS.GG.SDD#922. It qualifies a closed,
non-production Quint authoring candidate across requirements/evidence, an S.I.R. damage rule, and a
concurrent coordination process. It changes no production backend or public authority.

- `profile.md` defines the proposed closed FS-GG profile.
- `slices/` contains the canonical literate sources. Generated `.qnt` files are disposable.
- `compiled-contract.schema.json` and `compiled-contract.example.json` demonstrate the smallest
  language-neutral contract admitted by Q1.
- `quint-llm-kit-evaluation.md` records the pinned guidance review and attribution.
- `workflow-comparison.md` records the same-corpus pinned-kit/minimal execution comparison.
- `candidate-manifest.json` is the machine-readable identity receipt.
- `qualification-report.md` is the human review report and verdict.
- `../../../tests/quint-q1/qualify.sh` is the pinned positive and mutation harness.

The producer candidate is locally qualified, but cross-repository acceptance remains pending until
EHotwagner/S.I.R.#353 replays the exact witness against the real interpreter and two independent
reviewers accept the complete evidence set.
