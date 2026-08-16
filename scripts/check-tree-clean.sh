#!/usr/bin/env bash
# check-tree-clean.sh — running this repository's own gate command must leave the working tree
# exactly as it found it. FS.GG.SDD#870.
#
# THE DEFECT IT PINS. `tests/FS.GG.SDD.Acceptance.Tests/PolyglotLifecycleAcceptanceTests.fs` drives
# the polyglot fixture through `npm ci`, `npm test`, a vite serve, `dotnet pack` and `dotnet test
# --results-directory results`. Every one of those lanes is deliberate. Their OUTPUT lands under
# `tests/fixtures/polyglot-lifecycle/`, which no `bin/`-shaped rule in `.gitignore` reached, so the
# `full` tier left 541 untracked files behind. Measured at 2342ca3: `git add -A && git commit` then
# staged all of them — `588 files changed, 684650 insertions(+)` — and nothing anywhere said so.
# `git commit` does not report size, the suite was green, and a reviewer of a PR that already touches
# 47 files has no reason to expect 543 more.
#
# WHY A CHECK AND NOT JUST THE RULE. The rule fixes the fixture that exists. This fixes the class:
# the next fixture that starts generating, or a lane that starts writing somewhere new, reds the PR
# that introduces it instead of quietly needing a `.gitignore` edit nobody knows to make. That is
# FS.GG.SDD#870 AC3 — "a check enforces it rather than leaving it to review".
#
# TWO LEGS, AND THE SECOND IS WHAT KEEPS THE FIRST HONEST.
#
#   [dirty]            `git status --porcelain` is non-empty. The contributor-visible condition, and
#                      the exact command AC2 names. FATAL.
#   [tracked-ignored]  a TRACKED file matches an ignore rule. FATAL, and it is here because leg one
#                      can be satisfied by hiding things: a blanket `dist/` or `tests/fixtures/**`
#                      rule makes `git status` green by making committed content invisible, and a
#                      later `git rm --cached` of it would then be a silent deletion with a clean
#                      status — the same shape `scripts/check-root-asymmetry.sh` exists to refuse.
#                      Without this leg, "found nothing" and "was made unable to look" share an exit
#                      code (epic FS-GG/.github#266).
#
# AND IT NEVER PASSES ON NOTHING. A tree that is not a git work tree, a `git` that fails, or a work
# tree with zero tracked files are all exit 2. A green `git status` obtained from somewhere other
# than the repository under test is not evidence about the repository under test.
#
# Usage:
#   check-tree-clean.sh [--tree <dir>] [--label <what just ran>]
#
# Exit: 0 = the tree is exactly as committed, and nothing tracked is ignored
#       1 = a VERDICT: [dirty] and/or [tracked-ignored] (each printed with its class)
#       2 = misconfiguration, or no verdict could be reached. Never a pass.

set -uo pipefail

TREE="."
LABEL=""

die() { echo "::error::check-tree-clean: $*" >&2; exit 2; }

usage() {
  cat <<'EOF'
check-tree-clean.sh — assert a step left the working tree exactly as it found it.

  check-tree-clean.sh [--tree <dir>] [--label <what just ran>]

  --tree <dir>     the repository work tree to inspect (default: .).
  --label <text>   named in the failure message, so the log says WHAT dirtied the tree.

Exit: 0 clean; 1 the tree is dirty or a tracked file is ignored; 2 misconfiguration / no verdict.
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --tree)
      [ $# -ge 2 ] || die "--tree requires a directory"
      TREE="$2"
      shift 2
      ;;
    --label)
      [ $# -ge 2 ] || die "--label requires a value"
      LABEL="$2"
      shift 2
      ;;
    -h | --help)
      usage
      exit 0
      ;;
    *)
      usage >&2
      die "unknown argument: $1"
      ;;
  esac
done

command -v git >/dev/null 2>&1 || die "git not found — this check IS git status."

[ -d "$TREE" ] || die "--tree is not a directory: $TREE"
cd "$TREE" || die "could not enter --tree: $TREE"

# Resolve, and SAY, which tree was measured. A clean status from the wrong repository is the one
# way a passing run here would mean nothing, so the toplevel goes in the log next to the verdict.
toplevel="$(git rev-parse --show-toplevel 2>&1)"
rc=$?
[ "$rc" -eq 0 ] || die "'$TREE' is not inside a git work tree, so there is no tree to call clean: $toplevel"

# `git rev-parse --show-toplevel` succeeds inside a bare repo's worktree-less .git as well; require
# an actual work tree before believing any status it prints.
inside_work_tree="$(git rev-parse --is-inside-work-tree 2>&1)"
[ "$inside_work_tree" = "true" ] || die "'$TREE' resolves to '$toplevel' but is not inside a work tree (--is-inside-work-tree: $inside_work_tree)."

# NEVER PASS ON NOTHING. An empty index means this is not the repository the caller believes it is
# (a fresh `git init` in a scratch directory, a wrong --tree, a checkout that never happened), and
# every subsequent question would answer "clean" for the wrong reason.
tracked_count="$(git ls-files | wc -l)"
[ "$tracked_count" -gt 0 ] || die "'$toplevel' is a work tree with ZERO tracked files. Refusing to report 'nothing changed' over nothing (epic FS-GG/.github#266)."

echo "check-tree-clean: tree='$toplevel' ($tracked_count tracked file(s))${LABEL:+  after: $LABEL}"

findings=0

# LEG 1 — the contributor-visible condition, spelled exactly as FS.GG.SDD#870 AC2 spells it.
#
# `-c status.showUntrackedFiles=normal` IS LOAD-BEARING, and this is the same remedy, for the same
# reason, that FS-GG/.github's `scripts/fsgg-coord-guards.sh:467,532` already applies to its own
# dirty-tree probe under FS-GG/.github#1043. `--porcelain` is a FORMATTING flag: it does not override
# `status.showUntrackedFiles`. With that set to `no` — from `.git/config` or from `~/.gitconfig` —
# a bare `git status --porcelain` answers EMPTY over a tree carrying the whole of #870, and this
# check then prints OK and exits 0. Measured on both vectors at b4cd931 against a fixture holding
# `tests/fixtures/demo/node_modules/left-pad/index.js`: bare status 0 lines, this script exit 0;
# with the flag, 1 line and exit 1.
#
# THE POPULATION THAT SETS IT IS THE POPULATION THIS GATE IS FOR. AC2 names the contributor as who
# relies on `git status` being usable, and `showUntrackedFiles=no` is what someone sets precisely
# BECAUSE 541 untracked files made it unusable. Left unguarded, the check is blind for exactly the
# people it exists to protect, and silent about it.
#
# NOT NEUTRALISED HERE, DELIBERATELY: `core.excludesFile`. A personal global ignore also hides paths
# from this probe, but it is a different act — the contributor has DECLARED those paths uninteresting,
# whereas `showUntrackedFiles=no` is a display preference that says nothing about which paths are
# generated. Overriding the former would red a developer's run for their own `.DS_Store` or editor
# swap files, which is a false red, not a caught defect; and no CI runner carries one, so it cannot
# fail open where the verdict is authoritative. Considered and declined, not overlooked.
#
# Line 121's summary re-read passes `--untracked-files=all` EXPLICITLY, and an explicit flag does
# override the config — verified on the same blind fixture, which reported 1 file there while the
# bare call reported 0 — so that call needs no `-c` and does not get a redundant one.
status_out="$(git -c status.showUntrackedFiles=normal status --porcelain 2>&1)"
rc=$?
[ "$rc" -eq 0 ] || die "git status --porcelain failed in '$toplevel' (exit $rc), so this check has no verdict:
$status_out"

if [ -n "$status_out" ]; then
  # The porcelain summary collapses an untracked directory to one `?? dir/` line, which is how 541
  # files hid behind 5. Report BOTH: the summary a contributor sees, and the count they would
  # actually stage. `-uall` is a second, cheap traversal and only runs on the failing path.
  all_out="$(git status --porcelain --untracked-files=all 2>/dev/null)"
  all_count="$(printf '%s\n' "$all_out" | grep -c . )"

  while IFS= read -r line; do
    [ -n "$line" ] || continue
    echo "::error::check-tree-clean: [dirty] $line" >&2
    findings=$((findings + 1))
  done <<<"$status_out"

  echo "::error::check-tree-clean: the line(s) above expand to $all_count file(s); 'git add -A' would stage every one of them." >&2
fi

# LEG 2 — leg one's own non-vacuity. A tracked file that matches an ignore rule means the ignore
# list has grown over committed content: `git status` is then green because it stopped looking.
#
# `--no-index` IS LOAD-BEARING AND ITS ABSENCE IS SILENT. Without it `git check-ignore` consults the
# index first and reports NOTHING for a path that is tracked — which is every path this leg is
# about, since the input is `git ls-files`. The check then answers "no tracked file is ignored" by
# construction, for every possible repository. Measured while writing leg 6 of
# scripts/tests/check-tree-clean.test.sh: a fixture with `dist/keep.txt` committed and `dist/`
# ignored returned exit 0 and printed the OK line until this flag was added. `--no-index` is the
# documented way to ask the question about the rules rather than about the index.
#
# `git check-ignore` exits 0 when it matched something, 1 when it matched nothing, and >1 on error —
# so a non-{0,1} exit is exit 2 here rather than an assumed clean answer.
ignored_tracked="$(git ls-files | git check-ignore --stdin --no-index 2>&1)"
rc=$?
if [ "$rc" -gt 1 ]; then
  die "git check-ignore --stdin failed in '$toplevel' (exit $rc), so the tracked-vs-ignored leg has no verdict:
$ignored_tracked"
fi

if [ -n "$ignored_tracked" ]; then
  while IFS= read -r line; do
    [ -n "$line" ] || continue
    echo "::error::check-tree-clean: [tracked-ignored] $line — this file is COMMITTED and an ignore rule now matches it." >&2
    findings=$((findings + 1))
  done <<<"$ignored_tracked"
fi

if [ "$findings" -gt 0 ]; then
  cat >&2 <<EOF
::error::check-tree-clean: $findings finding(s) in '$toplevel'. Running the suite must leave the tree exactly as it found it (FS.GG.SDD#870). A [dirty] path is generated output with no rule in .gitignore — add one scoped to the fixture that produces it. A [tracked-ignored] path is the opposite defect: an ignore rule has grown over committed content, and hiding it is not fixing it.
EOF
  exit 1
fi

echo "check-tree-clean: OK — '$toplevel' is exactly as committed, and no tracked file is ignored"
