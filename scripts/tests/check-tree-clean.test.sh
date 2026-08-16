#!/usr/bin/env bash
# Unit test for scripts/check-tree-clean.sh (FS.GG.SDD#870 AC3).
#
# THE DEFECT IT PINS. The `full` tier ran the polyglot lifecycle acceptance fixture, which left 541
# untracked files under `tests/fixtures/polyglot-lifecycle/` with no rule in `.gitignore`. The suite
# was green, `git commit` said nothing about size, and `git add -A` staged 684,650 insertions of
# vendored JavaScript and test output. Nothing in the repository asked whether the tree was still
# clean, so the only thing standing between that and `main` was a human reading a `--stat`.
#
# WHY EVERY LEG IS PAIRED. A check that has never been observed to fail is indistinguishable from a
# `true`, and this one guards a class whose entire character is silence. So each assertion below has
# a fixture that differs in ONE fact and flips the verdict:
#
#   1 <-> 2   the same repository, with and without one untracked file — clean vs [dirty].
#             Leg 2 IS the measured FS.GG.SDD#870 condition, in miniature.
#   1 <-> 3   an unmodified vs a modified tracked file. `git status` is not only about untracked.
#   1 <-> 4   nothing staged vs a staged-but-uncommitted addition.
#   5 <-> 6   THE SAME PATH, `dist/keep.txt`, twice: once as generated output an ignore rule
#             legitimately covers (clean), once as COMMITTED content the same rule now hides
#             ([tracked-ignored]). This pair is why leg 1 cannot be satisfied by widening
#             `.gitignore` until the tree looks quiet — "found nothing" and "was made unable to
#             look" must not share an exit code (epic FS-GG/.github#266).
#   7..10     the fail-CLOSED family: a non-git directory, a work tree with zero tracked files, an
#             absent tree, an unknown flag. Each is exit 2, paired against leg 1 — the same script,
#             a usable subject, exit 0. "I could not evaluate this" is never "it passed".
#
# HERMETIC. Every fixture is a fresh repository under `mktemp -d`, outside this checkout, with the
# global and system git config forced empty so a developer's `core.excludesFile` cannot make a leg
# pass or fail. No network, no build, no dotnet, no npm — this is why it can run as its own cheap
# gate step ahead of the suite.
#
# Run:  bash scripts/tests/check-tree-clean.test.sh
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
subject="$repo/scripts/check-tree-clean.sh"

[ -f "$subject" ] || {
  echo "check-tree-clean.test.sh: $subject is missing." >&2
  exit 2
}

# Determinism: no user, global or system git config may reach any fixture. `git init` and every
# `git` the SUBJECT runs inherits these, which is the point.
export GIT_CONFIG_GLOBAL=/dev/null
export GIT_CONFIG_SYSTEM=/dev/null
export GIT_AUTHOR_NAME="tree-clean fixture"
export GIT_AUTHOR_EMAIL="fixture@example.invalid"
export GIT_COMMITTER_NAME="$GIT_AUTHOR_NAME"
export GIT_COMMITTER_EMAIL="$GIT_AUTHOR_EMAIL"

fail=0
ok() { printf '  ok   %s\n' "$*"; }
bad() {
  printf '  FAIL %s\n' "$*"
  fail=1
}

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# make_repo <name> — a minimal committed repository: one tracked file, clean status.
make_repo() {
  local t="$work/$1"
  mkdir -p "$t"
  git -C "$t" init -q -b main
  printf 'tracked source.\n' >"$t/keep.txt"
  mkdir -p "$t/src"
  printf 'module Fixture\n' >"$t/src/Fixture.fs"
  git -C "$t" add -A
  git -C "$t" commit -qm "fixture base"
  printf '%s' "$t"
}

run_check() {
  local t="$1"
  shift
  RUN_OUT="$(bash "$subject" --tree "$t" "$@" 2>&1)"
  RUN_RC=$?
}

expect_rc() {
  local what="$1" want="$2"
  if [ "$RUN_RC" -eq "$want" ]; then
    ok "$what (exit $RUN_RC)"
  else
    bad "$what: expected exit $want, got $RUN_RC"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  fi
}

expect_out() {
  local what="$1" needle="$2"
  case "$RUN_OUT" in
    *"$needle"*) ok "$what (said '$needle')" ;;
    *)
      bad "$what: output did not contain '$needle'"
      printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
      ;;
  esac
}

expect_not_out() {
  local what="$1" needle="$2"
  case "$RUN_OUT" in
    *"$needle"*)
      bad "$what: output unexpectedly contained '$needle'"
      printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
      ;;
    *) ok "$what (did not say '$needle')" ;;
  esac
}

echo "check-tree-clean.test.sh: subject=$subject"

# --- 1. a committed, untouched tree is clean -------------------------------------------------
t1="$(make_repo clean)"
run_check "$t1"
expect_rc "1. untouched committed tree" 0
expect_out "1. names the tree it measured" "2 tracked file(s)"

# --- 2. one untracked file — the FS.GG.SDD#870 condition in miniature ------------------------
t2="$(make_repo dirty-untracked)"
mkdir -p "$t2/tests/fixtures/demo/node_modules/left-pad"
printf 'vendored\n' >"$t2/tests/fixtures/demo/node_modules/left-pad/index.js"
run_check "$t2" --label "the polyglot fixture"
expect_rc "2. untracked generated output" 1
expect_out "2. classifies it [dirty]" "check-tree-clean: [dirty]"
expect_out "2. names what ran" "after: the polyglot fixture"
expect_out "2. expands the collapsed directory line to a file count" "would stage every one of them"

# --- 3. a MODIFIED tracked file (status is not only about untracked) -------------------------
t3="$(make_repo dirty-modified)"
printf 'tracked source, edited by the suite.\n' >"$t3/keep.txt"
run_check "$t3"
expect_rc "3. modified tracked file" 1
expect_out "3. classifies it [dirty]" "check-tree-clean: [dirty]"

# --- 4. a STAGED but uncommitted addition ----------------------------------------------------
t4="$(make_repo dirty-staged)"
printf 'staged\n' >"$t4/staged.txt"
git -C "$t4" add staged.txt
run_check "$t4"
expect_rc "4. staged uncommitted addition" 1
expect_out "4. classifies it [dirty]" "check-tree-clean: [dirty]"

# --- 5. generated output an ignore rule legitimately covers is CLEAN --------------------------
#     This is the shape the fix ships: the fixture generates into dist/, a committed rule covers it.
t5="$(make_repo ignored-generated)"
printf 'dist/\n' >"$t5/.gitignore"
git -C "$t5" add .gitignore
git -C "$t5" commit -qm "ignore generated dist/"
mkdir -p "$t5/dist"
printf 'generated by the suite\n' >"$t5/dist/keep.txt"
run_check "$t5"
expect_rc "5. ignored generated output" 0
expect_not_out "5. no dirty finding" "check-tree-clean: [dirty]"

# --- 6. THE SAME PATH, committed, with the SAME rule — leg 5's non-vacuity --------------------
#     `git status` is empty here too. If that were the only question asked, this tree would pass
#     while a tracked file sits invisible behind an ignore rule.
t6="$(make_repo tracked-then-ignored)"
mkdir -p "$t6/dist"
printf 'committed on purpose\n' >"$t6/dist/keep.txt"
git -C "$t6" add -f dist/keep.txt
printf 'dist/\n' >"$t6/.gitignore"
git -C "$t6" add .gitignore
git -C "$t6" commit -qm "commit dist/keep.txt, then ignore dist/"
# Precondition of the pairing: leg one has nothing to say about this tree.
if [ -n "$(git -C "$t6" status --porcelain)" ]; then
  bad "6. fixture precondition: expected an EMPTY git status, so that only the tracked-ignored leg can fire"
  git -C "$t6" status --porcelain | sed 's/^/       | /'
fi
run_check "$t6"
expect_rc "6. tracked file matched by an ignore rule" 1
expect_out "6. classifies it [tracked-ignored]" "check-tree-clean: [tracked-ignored]"
expect_out "6. names the offending path" "dist/keep.txt"
expect_not_out "6. and leg one stayed silent" "check-tree-clean: [dirty]"

# --- 7. a directory that is not a git work tree — exit 2, never a pass ------------------------
t7="$work/not-a-repo"
mkdir -p "$t7"
printf 'loose file\n' >"$t7/loose.txt"
run_check "$t7"
expect_rc "7. not a git work tree" 2
expect_out "7. says there is no tree to call clean" "no tree to call clean"

# --- 8. a work tree with ZERO tracked files — exit 2, never a pass ----------------------------
t8="$work/empty-repo"
mkdir -p "$t8"
git -C "$t8" init -q -b main
run_check "$t8"
expect_rc "8. work tree with no tracked files" 2
expect_out "8. refuses to pass on nothing" "ZERO tracked files"

# --- 9. an absent tree — exit 2 --------------------------------------------------------------
run_check "$work/does-not-exist"
expect_rc "9. absent tree" 2
expect_out "9. says why" "not a directory"

# --- 10. an unknown flag — exit 2, never a silent default ------------------------------------
run_check "$t1" --no-such-flag
expect_rc "10. unknown argument" 2
expect_out "10. names the argument" "unknown argument: --no-such-flag"

echo
if [ "$fail" -ne 0 ]; then
  echo "check-tree-clean.test.sh: FAILED" >&2
  exit 1
fi
echo "check-tree-clean.test.sh: all legs passed"
