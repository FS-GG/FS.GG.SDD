# 869 — verification evidence

Durable record of the two things a reader of this package would otherwise have to reconstruct from
PR prose: the gate-inversion sweep, and the replay fixed point.

## Environment

Local Release build, .NET 10, worktree `869-crake-3ad0`. CI builds and tests the same sources at
`-c Debug` via `.github/workflows/gate.yml` job `gate`, step "Test (offline suite)"
(`bash scripts/test.sh --no-build -c Debug`). That trigger is `pull_request: branches: [main]` with
**no `paths:` filter**, so nothing in this diff can be silently skipped.

Harness for every row:
`dotnet test tests/FS.GG.SDD.Commands.Tests -c Release --no-build --filter FullyQualifiedName~RefreshEvidenceDeadlock`
(14 tests). Each mutation reverted with `git checkout -- <file>` and `git status` confirmed clean
between rows. **Baseline: 14/14 pass.**

## Gate-inversion sweep

Each row breaks the gate's SUBJECT — the behaviour it exists to protect — not the gate's predicate.

| # | Subject mutation | Observed | Verdict |
|---|---|---|---|
| A | `WorkModel.fs` — `requiredEvidence` edge restored to blocking `unknown …` | **6 red / 8 green** | JUSTIFIED |
| B | `HandlersEvidence.fs` — `withSeededObligations` sees no undeclared obligations | **5 red / 9 green**; both work-model tests stay **green** | JUSTIFIED |
| C | `HandlersRefresh.fs` — attribution hard-codes the spec path again | **3 red / 11 green** | JUSTIFIED |
| D | `EvidenceDomain.fs` — obligations no longer minted from `task.RequiredEvidence` | **5 red / 9 green**, including the FR-003 gate | JUSTIFIED |
| E | `HandlersEvidence.fs` — the seeder also rewrites authored declarations | **1 red / 13 green** — only the byte-identity gate | JUSTIFIED |
| F | the FR-003 gate's own CONTROL leg regressed to the un-analyzed fixture | **1 red / 13 green** — the gate refuses to certify itself | JUSTIFIED |

**A and B together are the measurement of the two-lock claim.** B reds only the end-to-end tests and
leaves both work-model tests green — "lock 1 is open and it is still not enough". A reds the
work-model tests as well. Neither fix alone converges, so `#869`'s either/or is refuted by
measurement rather than by argument.

**D and F are why the FR-003 gate is not vacuous.** The obvious version of that gate asserted only
`verify.Outcome = Blocked` on a fixture that never runs `analyze`, so it blocked at
`evidence.missingAnalysisPrerequisite` identically with and without the enforcement — it would have
passed against a build with the enforcement deleted. D deletes the enforcement for real and the
rewritten gate reds; F regresses the control leg and the gate reds on its own control assertion.

## Replay fixed point

Second pass over this package after `ship`, every command reporting `noChange`:

```
analyze noChange | evidence noChange | verify noChange
ship    noChange | refresh  noChange | agents  noChange
```

## Dogfooding

This package entered `#869`'s own deadlocked state during authoring: FR-009 was added to `spec.md`
after `evidence.yml` already existed, which minted an obligation `evidence.yml` did not declare. It
was recovered with the documented sequence — `checklist`, `plan --accept-upstream`, `tasks`,
`refresh`, `analyze`, `evidence` — with no hand-edit of `evidence.yml` and no deletion of an
authored artifact.
