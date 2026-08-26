# Quint Q2 qualification fixtures

This directory carries the smallest coherent golden corpus from the accepted
Q1 experiment into Q2. The five `q1-*` files are byte-for-byte copies from
merge commit `60351fd0614a5c8e4bdf286c21f185196116fd69`:

- `docs/experiments/quint-q1/profile.md`
- `docs/experiments/quint-q1/compiled-contract.schema.json`
- `docs/experiments/quint-q1/compiled-contract.example.json`
- `tests/quint-q1/fixtures/sir-reviewed-witness.json`
- `tests/quint-q1/fixtures/sir-reviewed-witness.itf.json`

`q1-identity-manifest.json` is a concise derived lock receipt. Its facts and
digests come from the sealed Q1 candidate manifest and S.I.R. consumer response
receipt at the same merge commit. It does not claim additional licenses where
Q1 recorded none.

`q1-typecheck-corpus.receipt.json` binds each of the three exact Q1 literate
slices to its generated module and real Quint 0.32.0 `typecheck --out` capture.
The raw compiler JSON is deliberately not a committed authority; the executable
adapter harness consumes locally reproduced captures and checks the recorded
digests plus fail-closed mutations.

These fixtures are immutable compatibility inputs. Q2 implementations should
produce separate actual outputs and compare them to this corpus rather than
rewriting the files in place.
