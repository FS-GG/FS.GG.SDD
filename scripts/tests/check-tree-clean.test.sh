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
#  11 <-> 12  leg 2's fixture again, unchanged, under `status.showUntrackedFiles=no` — set in
#             `.git/config` and then in `~/.gitconfig`. Both must STILL red. See the warning below:
#             these two exist because the hermeticism directly above them hid this defect once.
#
# HERMETIC — AND THAT IS ALSO A HAZARD THIS FILE HAS ALREADY BEEN BITTEN BY.
#
# Every fixture is a fresh repository under `mktemp -d`, outside this checkout, with the global and
# system git config forced empty. That is right for legs 1-10: a developer's `core.excludesFile`
# must not decide whether they pass. No network, no build, no dotnet, no npm — which is why this can
# run as its own cheap gate step ahead of the suite.
#
# But forcing the config empty ALSO means no leg here exercises a configured git, so a check that is
# blind under a common contributor config looks fully covered. That is not hypothetical: at b4cd931
# the subject ran a bare `git status --porcelain`, which `--porcelain` does NOT protect from
# `status.showUntrackedFiles=no`, so it printed OK and exited 0 on a tree carrying the whole of
# FS.GG.SDD#870 — and all 24 assertions here passed, precisely BECAUSE `GIT_CONFIG_GLOBAL=/dev/null`
# meant none of them could see it. The hermeticism did not cause the defect; it made it invisible
# while appearing to have handled config-dependence.
#
# So legs 11 and 12 deliberately UN-force the config, one vector each. When adding a leg here, ask
# which git configuration it silently assumes — the answer for legs 1-10 is "none, by construction",
# and that is a coverage claim, not a safety one.
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

# --- 11/12. status.showUntrackedFiles=no must NOT be able to silence the gate ------------------
#
# `--porcelain` is a FORMATTING flag and does not override `status.showUntrackedFiles`. Both legs
# reuse leg 2's fixture UNCHANGED — the same untracked `node_modules/left-pad/index.js`, the #870
# condition in miniature — and assert the same exit 1. The only thing that varies is where git reads
# the setting from, because the two vectors are configured in different places and a fix that
# addressed only one would still leave the other blind.
#
# Both legs are red without `-c status.showUntrackedFiles=normal` in the subject: they return exit 0
# with the OK line, over a tree that leg 2 proves is dirty. That is the pairing.

# 11. the repository's own .git/config.
t11="$(make_repo blind-repo-local)"
mkdir -p "$t11/tests/fixtures/demo/node_modules/left-pad"
printf 'vendored\n' >"$t11/tests/fixtures/demo/node_modules/left-pad/index.js"
git -C "$t11" config status.showUntrackedFiles no
# Precondition: a bare porcelain status really is blind here, or the leg proves nothing.
if [ -n "$(git -C "$t11" status --porcelain)" ]; then
  bad "11. fixture precondition: expected a bare 'git status --porcelain' to be BLIND under this config"
fi
run_check "$t11"
expect_rc "11. dirty tree, status.showUntrackedFiles=no in .git/config" 1
expect_out "11. still classifies it [dirty]" "check-tree-clean: [dirty]"

# 12. the user's ~/.gitconfig. `GIT_CONFIG_GLOBAL` is what this file forces to /dev/null for every
# other leg, so here it is pointed at a real config instead — the one vector the hermeticism hides.
t12="$(make_repo blind-global)"
mkdir -p "$t12/tests/fixtures/demo/node_modules/left-pad"
printf 'vendored\n' >"$t12/tests/fixtures/demo/node_modules/left-pad/index.js"
printf '[status]\n\tshowUntrackedFiles = no\n' >"$work/global-gitconfig"
GIT_CONFIG_GLOBAL="$work/global-gitconfig" run_check "$t12"
expect_rc "12. dirty tree, status.showUntrackedFiles=no in ~/.gitconfig" 1
expect_out "12. still classifies it [dirty]" "check-tree-clean: [dirty]"
# Prove leg 12's vector was actually in force, rather than the leg passing for leg 2's reason.
if [ -n "$(GIT_CONFIG_GLOBAL="$work/global-gitconfig" git -C "$t12" status --porcelain)" ]; then
  bad "12. fixture precondition: GIT_CONFIG_GLOBAL was not in force — a bare porcelain status still saw the file, so this leg did not test its vector"
else
  ok "12. the global vector was genuinely in force (a bare porcelain status was blind)"
fi

echo
if [ "$fail" -ne 0 ]; then
  echo "check-tree-clean.test.sh: FAILED" >&2
  exit 1
fi
echo "check-tree-clean.test.sh: all legs passed"
