#!/usr/bin/env bash
# check-root-asymmetry.sh — nothing may live under a NON-SOURCE skill root that the source root
# does not also carry. The check that would have found FS.GG.SDD#771 before it was a decision.
#
# ADR-0067 §6/§8/§9; FS.GG.SDD#771 AC2; the retirement order lives in FS-GG/.github
# `docs/coordination/skill-apparatus-retirement-order.md` §4/§7 and is tracked as FS-GG/.github#1676.
#
# WHAT IT IS FOR. ADR-0067 phase 4 stops tracking every root but one and regenerates the rest as
# VIEWS of it. A view carries exactly what the source carries, so anything that exists ONLY under a
# non-source root is deleted by that retirement — and deleted SILENTLY, because the file was tracked
# under a path the same commit stops tracking. Measured on this repo at `387adc6` (FS.GG.SDD#771):
#
#   $ diff -r .claude/skills .agents/skills
#   Only in .agents/skills: skill-manifest.json
#   $ git rm -r --cached .agents/skills && … && git status --porcelain
#               (empty)
#
# One tracked file, 6,891 bytes, the repo's producer-authoritative process manifest, gone with a
# CLEAN `git status`. It was found by a human running `diff -r` once. This script is that command,
# kept where something runs it — which is the whole of AC2: "that check goes into whatever runs
# before the retirement, not into a reviewer's memory."
#
# THE DIRECTION IS THE SUBJECT, AND IT IS NOT SYMMETRIC.
#
#   [second-root-only]  a path under a non-source root with no counterpart in the source.  FATAL.
#                       This is the class above: content the retirement would delete.
#   [source-only]       a path under the source with no counterpart in a non-source root.
#                       REPORTED, NEVER FATAL. It is the normal and correct state for a
#                       producer-authoritative file that lives in the source root by decision —
#                       `skill-manifest.json` is exactly this after FS.GG.SDD#771 — and it is also
#                       what a `--mode copy` view legitimately looks like, since `skill-view
#                       generate` copies `<id>/` skill directories and no top-level files. Making
#                       it fatal would forbid the very layout this repo just chose.
#   [divergent]         a path present in BOTH with different bytes. FATAL. Not AC2's subject, but
#                       two committed copies of one skill set disagreeing is a defect under every
#                       reading, and `skill-union / skill-union` — which used to assert exactly
#                       this — was retired on 2026-07-28 (FS-GG/.github#1715). Its replacement
#                       `skill-view check` says outright that it does not compare the two roots'
#                       bytes against each other. This closes that one direction while a second
#                       committed root still exists; when the root becomes a view it cannot fail,
#                       because a view cannot diverge from what it is a view of.
#
# IT USES `diff -r` LITERALLY, on purpose. The measurement that found the defect was `diff -r`, the
# acceptance criterion names `diff -r`, and re-implementing the comparison with `find`+`cmp` here
# would be a second traversal answering a question one already answers — the shape this repo's own
# `materialize-skill-roots.fsx` header calls "NOT A `cp -R`" for the same reason. The one addition
# is `-q`, and it does not touch AC2's direction: see the note at the invocation.
#
# AND IT NEVER PASSES ON NOTHING (epic FS-GG/.github#266). An absent source, a source that declares
# no skills, an absent non-source root, or a resolved root set with no non-source root in it are all
# exit 2. "I could not evaluate this" is never "I evaluated it and it passed". In particular an
# ABSENT non-source root is NOT a quiet pass here: under phase 4 the view must be generated before
# this runs, exactly as `.github/workflows/skill-view-check.yml` documents for its own step.
#
# THE ROOT SET IS NOT THIS SCRIPT'S TO CHOOSE. It comes from `lib/roots.sh` — the same precedence
# parser `skill-view`, `skill-union-assert.sh` and `coordination-sync` resolve with
# (FS-GG/.github#525) — so this cannot become a fifth opinion about which roots exist.
#
# Usage:
#   check-root-asymmetry.sh --source <dir> [--tree <dir>] [--roots "<r1> <r2> ..."]
#
# Exit: 0 = every non-source root is a subset of the source, byte for byte
#       1 = a VERDICT: [second-root-only] or [divergent] paths exist (each printed with its class)
#       2 = misconfiguration, or no verdict could be reached. Never a pass.

set -euo pipefail

TREE="."
ROOTS=""
ROOTS_ARG=""
ROOTS_SRC=""
SOURCE=""

# The same default pair `scripts/skill-view` takes: ADR-0065's two as amended by ADR-0067 §5 and
# landed by FS-GG/.github#1636. `lib/roots.sh` takes the default from its CALLER precisely so a
# migration changes it in one reviewed place; this caller must agree with its sibling, not innovate.
DEFAULT_ROOTS=".claude/skills .agents/skills"
DEFAULT_LABEL="default (ADR-0065's two)"

die() { echo "::error::check-root-asymmetry: $*" >&2; exit 2; }

# shellcheck source=lib/args.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib/args.sh"
# shellcheck source=lib/roots.sh
. "$(dirname "${BASH_SOURCE[0]}")/lib/roots.sh"

usage() {
  cat <<'EOF'
check-root-asymmetry.sh — assert every non-source skill root is a subset of the source root.

  check-root-asymmetry.sh --source <dir> [--tree <dir>] [--roots "<r1> <r2> ..."]

  --source <dir>   the ONE tracked source of truth, relative to --tree (e.g. .claude/skills).
  --tree <dir>     the repository root the roots are relative to (default: .).
  --roots "<...>"  override the root set. Precedence: --roots > $AGENT_SKILL_ROOTS >
                   <tree>/.agent-skill-roots > the ADR-0065 default.

Exit: 0 ok; 1 a non-source root carries something the source does not (or diverges); 2
misconfiguration / no verdict.
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --tree)    need_val "$@"; TREE="$2"; shift 2 ;;
    --roots)   need_val "$@"; ROOTS_ARG="$2"; shift 2 ;;
    --source)  need_val "$@"; SOURCE="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) usage >&2; die "unknown argument: $1" ;;
  esac
done

command -v diff >/dev/null 2>&1 || die "diff not found — this check IS diff -r."

[ -n "$SOURCE" ] || die "--source <dir> is required: the root every other root must be a subset OF."
[ -d "$TREE" ] || die "--tree is not a directory: $TREE"
TREE_ABS="$(cd "$TREE" && pwd)"

src_rel="${SOURCE#./}"
src_abs="$TREE_ABS/$src_rel"
[ -d "$src_abs" ] || die "--source '$SOURCE' is not a directory under '$TREE_ABS'. An absent source is not an empty one."

# A source that declares no skills would make every root trivially a superset of nothing, and this
# script would report success over a tree that had lost everything. Same refusal, same reason, as
# `skill-view`'s empty-expected-set guard.
declared=0
for d in "$src_abs"/*/; do
  [ -d "$d" ] || continue
  [ -f "$d/SKILL.md" ] || continue
  declared=$((declared + 1))
done
[ "$declared" -gt 0 ] || die "--source '$SOURCE' holds no <id>/SKILL.md — refusing to report 'nothing is asymmetric' over nothing (epic FS-GG/.github#266)."

resolve_roots "$TREE_ABS" "$DEFAULT_ROOTS" "$DEFAULT_LABEL" "$ROOTS_ARG"
[ -n "$ROOTS" ] || die "no roots resolved — refusing to examine nothing."

echo "check-root-asymmetry: tree='$TREE_ABS' source='$src_rel' ($declared skill(s)) roots='$ROOTS' (from $ROOTS_SRC)"

audited=0
findings=0

for root in $ROOTS; do
  root="${root#./}"
  if [ "$root" = "$src_rel" ]; then
    echo "  $root: identity (this root IS the source) — nothing to compare"
    continue
  fi

  dst="$TREE_ABS/$root"
  # Deliberately NOT a quiet skip. Under ADR-0067 phase 4 this root is a generated view and must be
  # generated before this runs; "it wasn't there so there was nothing to find" is the fail-open this
  # whole family of checks exists to remove.
  [ -e "$dst" ] || die "[absent-root] '$root' does not exist under '$TREE_ABS'. This check has no subject there — that is exit 2, not a pass. If '$root' is a generated view, run `scripts/skill-view generate --source $src_rel --roots \"$root\"` first."
  [ -d "$dst" ] || die "[not-a-directory-root] '$root' exists and is not a directory. A regular file whose body names a path is a COMMITTED SYMLINK checked out with core.symlinks=false (ADR-0067 §6)."

  audited=$((audited + 1))

  # THE MEASUREMENT. `-q` is added to the `diff -r` the acceptance criterion names, and adds nothing
  # to its subject: the `Only in <dir>: <name>` lines — the entire AC2 direction — are spelled
  # IDENTICALLY with and without it. What it changes is the OTHER class: without `-q` a file present
  # in both roots with different bytes is reported as its whole textual diff, whose first line is
  # `diff -r <a> <b>` and whose body is arbitrary file content, so classifying it means parsing a
  # diff. With `-q` it is one line, `Files <a> and <b> differ`. Measured while writing leg 4 of
  # scripts/tests/check-root-asymmetry.test.sh, which reported exit 0 against a genuinely divergent
  # fixture until this flag was added.
  #
  # NOT `--no-dereference`, deliberately: under ADR-0067 §6 the second root is a symlink to the
  # source, and a view must compare EQUAL to what it is a view of. Following the link is the point.
  #
  # `diff` exits 0 (same), 1 (differences) or 2 (trouble). `set +e` keeps `set -e` from turning
  # "differences" into an unclassified abort, and the trouble case is caught below by its own exit
  # code rather than being read as a clean tree.
  set +e
  out="$(diff -qr "$src_abs" "$dst" 2>&1)"
  rc=$?
  set -e
  [ "$rc" -le 1 ] || die "diff -qr '$src_rel' '$root' could not complete (exit $rc):
$out"

  # CLASSIFIED BY LITERAL PREFIX, NOT BY REGEX. The discriminator is an absolute PATH, and a path is
  # not a pattern: `/home/runner/work/FS.GG.SDD/FS.GG.SDD` fed to `grep -E` has three `.`s that each
  # match ANY character, and a tree checked out under a directory containing `+`, `(` or `[` is worse
  # than loose — it is a broken or an accidentally-matching expression, in a check whose whole job is
  # to be trusted. `case` patterns treat a QUOTED expansion as literal text, so the interpolation
  # cannot become syntax. Same reason the report below strips the prefix with `${line//"$TREE_ABS"…}`
  # (quoted, therefore literal) rather than an unquoted glob.
  #
  # AND EVERY LINE MUST LAND IN A BUCKET. `diff -qr` has more to say than three classes — `File X is
  # a regular file while file Y is a directory`, `Symbolic links … differ`, and whatever a future
  # `diff` adds. A line this script does not recognise is a fact about the tree it cannot interpret,
  # and dropping it on the floor would be a pass earned by not understanding the evidence. It is
  # exit 2: no verdict, never a clean tree (epic FS-GG/.github#266).
  second_only=""
  source_only=""
  divergent=""
  unclassified=""

  while IFS= read -r line; do
    [ -n "$line" ] || continue
    case "$line" in
      "Only in $dst:"* | "Only in $dst/"*) second_only+="$line"$'\n' ;;
      "Only in $src_abs:"* | "Only in $src_abs/"*) source_only+="$line"$'\n' ;;
      "Files "*" differ" | "Binary files "*" differ") divergent+="$line"$'\n' ;;
      *) unclassified+="$line"$'\n' ;;
    esac
  done <<<"$out"

  [ -z "$unclassified" ] || die "diff -qr '$src_rel' '$root' said something this check does not classify, so it has no verdict here:
$(printf '%s' "$unclassified" | sed 's/^/  /')"

  if [ -n "$second_only" ]; then
    while IFS= read -r line; do
      [ -n "$line" ] || continue
      echo "::error::check-root-asymmetry: [second-root-only] ${line//"$TREE_ABS"\//}" >&2
      findings=$((findings + 1))
    done <<<"$second_only"
  fi

  if [ -n "$divergent" ]; then
    while IFS= read -r line; do
      [ -n "$line" ] || continue
      echo "::error::check-root-asymmetry: [divergent] ${line//"$TREE_ABS"\//}" >&2
      findings=$((findings + 1))
    done <<<"$divergent"
  fi

  if [ -n "$source_only" ]; then
    while IFS= read -r line; do
      [ -n "$line" ] || continue
      echo "  [source-only] ${line//"$TREE_ABS"\//}  (not a finding: the source may legitimately carry more)"
    done <<<"$source_only"
  fi

  if [ -z "$second_only" ] && [ -z "$divergent" ]; then
    echo "  $root: OK — a subset of $src_rel, byte for byte"
  else
    echo "  $root: FINDINGS"
  fi
done

[ "$audited" -gt 0 ] || die "the resolved root set ('$ROOTS', from $ROOTS_SRC) contains no root other than the source. Nothing was compared, so nothing passed."

if [ "$findings" -gt 0 ]; then
  echo "::error::check-root-asymmetry: $findings finding(s) across $audited non-source root(s). Every [second-root-only] path is content ADR-0067 phase 4 would delete with a clean 'git status'; move it into '$src_rel' (FS.GG.SDD#771)." >&2
  exit 1
fi

echo "check-root-asymmetry: OK — $audited non-source root(s) are subsets of $src_rel"
