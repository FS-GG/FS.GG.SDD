# Quickstart: Validate Risk-Scaled SDD

From the repository root:

```bash
bash scripts/tests/ci-risk.test.sh
bash scripts/test.sh fast
```

The classifier test must prove small, normal, high, rename, deletion, empty-input, and unknown-path behavior. The fast suite must prove that observed synthetic-marked passes satisfy while unobserved, stale, failed, and malformed receipts remain blocked.

For this feature itself, run the high-risk path because it changes CI policy and lifecycle semantics:

```bash
bash scripts/test.sh
bash scripts/ci-risk --paths .github/workflows/gate.yml .specify/memory/constitution.md
```

Expected classifier result: `profile=high`, `test_tier=full`, `protected_controls=true`.

Before merge, bind the exact candidate commit to successful checks and an independent critic verdict. A new commit requires fresh acceptance.
