#!/usr/bin/env bash
# lockfile-sync-gating.test.sh — the two facts that make `.github/workflows/lockfile-sync.yml`'s
# kit-branch exclusion correct rather than merely convenient (FS.GG.SDD#762).
#
# WHY THIS EXISTS
#   `FS.GG.Kit` 0.10.0 -> 0.15.0 (#730) was refused by `.github`'s `scripts/check-kit-bump-shape.py`
#   (`.github#1693`, enforcing `#1587` AC 2) — `contaminated`, exit 1, eight findings, every one a
#   `packages.lock.json`. Not one of those lockfiles carried a `FS.GG.Kit` change: they carried
#   `"FS.GG.Contracts": "[7.0.0, )"` -> `"[7.4.0, )"`, pre-existing drift between `main` and `main`'s
#   own `src/FS.GG.Contracts` version, which `lockfile-sync.yml` noticed on the bump branch and
#   repaired THERE. Correct repair, wrong PR. `lockfile-sync.yml` now skips `renovate/fs.gg.kit-*`.
#
#   That skip rests on a premise about THIS repo, and a premise nothing checks is a premise that rots.
#   Both legs below are that check.
#
# LEG 1 — the gating predicate actually excludes kit branches, and excludes NOTHING else.
#   Read from the workflow, not restated: the `if:` expression is extracted from the YAML and
#   evaluated against a table of head refs.
#
#   It is evaluated, not grepped, because a grep for a substring passes on an expression that has been
#   negated, parenthesised into irrelevance, or `&&`-ed with something false. It REFUSES rather than
#   guesses: the expression is tokenised against a tiny closed grammar (the two context reads this
#   workflow can legitimately branch on, `startsWith`, `!`, `&&`, `||`, parentheses), and anything
#   outside it exits non-zero saying so. So this cannot silently become a weaker test than it reads
#   as — the failure mode of a hand-rolled evaluator for a language it does not fully implement.
#
# LEG 2 — the structural premise: `FS.GG.Kit` is in no `packages.lock.json` in this repo.
#   `FS.GG.Kit` is referenced only by `.config/kit/FS.GG.Kit.receiver.proj`, which is deliberately not
#   in `FS.GG.SDD.sln` (the `restore-target`) and sets `RestorePackagesWithLockFile=false`. So bumping
#   the kit pin CANNOT change a committed lockfile, and any lockfile change `--force-evaluate` finds on
#   a kit branch is unrelated drift by construction. The day that stops being true — someone adds the
#   receiver project to the solution, or gives the kit a real `PackageReference` from a locked project —
#   the exclusion becomes unsafe, and this leg goes red on the PR that does it instead of a kit bump
#   silently going stale months later.
#
# Run:  bash scripts/tests/lockfile-sync-gating.test.sh    (no network, no dotnet)
set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
workflow="$repo_root/.github/workflows/lockfile-sync.yml"
fail=0

pass() { printf '  ok   %s\n' "$1"; }
bad()  { printf '  FAIL %s\n' "$1"; fail=1; }

# ── Leg 1: evaluate the `if:` on jobs.sync against a head-ref table ────────────────────────────────
#
# Cases. `run` = the caller does not gate this head ref out; `skip` = it does. Note that "run" here is
# about the CALLER's gate only — the reusable workflow applies its own renovate/build-config test on
# top, which is not this file's subject.
#
# Mutation record (each case fails against a specific wrong edit, which is why they are all here):
#   drop the exclusion entirely            -> the two `skip` cases fail
#   broaden it to 'renovate/'              -> the two other renovate `run` cases fail
#   broaden it to '' or invert the `!`     -> every `run` case fails
cases=(
  "renovate/fs.gg.kit-0.x|skip"
  "renovate/fs.gg.kit-1.x|skip"
  "renovate/fsharp.core-10.x|run"
  "renovate/nunit-4.x|run"
  "build-config/sync|run"
  "item/762-lockfile-repair|run"
)

if [ ! -f "$workflow" ]; then
  bad "workflow present ($workflow)"
else
  expr_text="$(
    python3 - "$workflow" <<'PY'
import re, sys

text = open(sys.argv[1], encoding="utf-8").read()

# jobs: -> sync: -> its `if:`. Comment lines are dropped first so prose quoting an expression
# (this file's own header does) can never be mistaken for the expression.
lines = [l for l in text.splitlines() if not l.lstrip().startswith("#")]
try:
    j = next(i for i, l in enumerate(lines) if re.match(r"^jobs:\s*$", l))
    s = next(i for i, l in enumerate(lines[j + 1:], j + 1) if re.match(r"^  sync:\s*$", l))
except StopIteration:
    print("REFUSED: no `jobs:` / `sync:` block in the workflow.", file=sys.stderr)
    sys.exit(3)

body, k = [], s + 1
while k < len(lines) and (lines[k].strip() == "" or lines[k].startswith("    ")):
    body.append(lines[k]); k += 1

m = re.search(r"^    if:\s*(.*)$", "\n".join(body), re.M)
if not m:
    print("REFUSED: jobs.sync has no `if:`.", file=sys.stderr)
    sys.exit(3)

head = m.group(1).strip()
if head in (">-", ">", "|-", "|"):          # block scalar: take its indented continuation
    start = "\n".join(body).index(m.group(0)) + len(m.group(0))
    cont = []
    for l in "\n".join(body)[start:].splitlines():
        if l.strip() == "":
            continue
        if not l.startswith("      "):
            break
        cont.append(l.strip())
    head = " ".join(cont)

head = head.strip()
# `if:` is legitimately written four ways — bare, `${{ }}`-wrapped, and either of those inside a
# YAML quoted scalar (which authors reach for because a bare leading `!` is a YAML tag). Unwrap all
# of them, so a correct workflow in a different style is not a false red.
if len(head) >= 2 and head[0] == head[-1] and head[0] in "'\"":
    head = head[1:-1].strip()
if head.startswith("${{") and head.endswith("}}"):
    head = head[3:-2].strip()
print(head)
PY
  )"
  rc=$?
  if [ "$rc" -ne 0 ] || [ -z "$expr_text" ]; then
    bad "extract jobs.sync.if from the workflow (rc=$rc)"
  else
    printf '  ..   jobs.sync.if = %s\n' "$expr_text"
    for c in "${cases[@]}"; do
      ref="${c%%|*}"; want="${c##*|}"
      got="$(
        HEADREF="$ref" python3 - "$expr_text" <<'PY'
import os, re, sys

expr = sys.argv[1]
ref = os.environ["HEADREF"]

# A CLOSED GRAMMAR. Every token this evaluator understands is replaced, in order; whatever is left
# over is something it does NOT understand, and it refuses rather than guessing a verdict. This is
# what keeps a hand-rolled evaluator from silently under-approximating GitHub's expression language.
py = expr
py = re.sub(
    r"startsWith\(\s*github\.event\.pull_request\.head\.ref\s*,\s*'([^']*)'\s*\)",
    lambda m: "True" if ref.startswith(m.group(1)) else "False",
    py,
)
py = py.replace(
    "github.event.pull_request.head.repo.full_name == github.repository", "True"
)
py = re.sub(r"&&", " and ", py)
py = re.sub(r"\|\|", " or ", py)
py = re.sub(r"!(?=\s*[\w(])", " not ", py)

residue = re.sub(r"\b(True|False|and|or|not)\b|[()\s]", "", py)
if residue:
    print(f"REFUSED: unsupported token(s) in jobs.sync.if: {residue!r}", file=sys.stderr)
    sys.exit(3)

print("run" if eval(py, {"__builtins__": {}}, {}) else "skip")
PY
      )"
      rc=$?
      if [ "$rc" -ne 0 ]; then
        bad "gating $ref — evaluator refused (rc=$rc)"
      elif [ "$got" = "$want" ]; then
        pass "gating $(printf '%-28s' "$ref") -> $got"
      else
        bad "gating $(printf '%-28s' "$ref") -> expected $want, got $got"
      fi
    done
  fi
fi

# ── Leg 2: FS.GG.Kit is in no committed lockfile ───────────────────────────────────────────────────
locks="$(cd "$repo_root" && git ls-files '*packages.lock.json')"
if [ -z "$locks" ]; then
  bad "premise: found no packages.lock.json to check — this repo is supposed to commit them"
else
  hits="$(cd "$repo_root" && printf '%s\n' "$locks" | xargs grep -l 'FS\.GG\.Kit' 2>/dev/null)"
  if [ -z "$hits" ]; then
    pass "premise: FS.GG.Kit absent from all $(printf '%s\n' "$locks" | wc -l | tr -d ' ') committed lockfiles"
  else
    bad "premise: FS.GG.Kit now appears in a committed lockfile, so a kit bump CAN move the lock graph
       and lockfile-sync.yml's renovate/fs.gg.kit-* exclusion is no longer safe. Files:
$(printf '%s\n' "$hits" | sed 's/^/         /')"
  fi
fi

if [ "$fail" -ne 0 ]; then
  echo "lockfile-sync-gating.test.sh: FAILURES" >&2
  exit 1
fi
echo "lockfile-sync-gating.test.sh: all passed"
