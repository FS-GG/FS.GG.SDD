#!/usr/bin/env bash
# Unit test for scripts/check-root-asymmetry.sh (FS.GG.SDD#771 AC2).
#
# THE DEFECT IT PINS. `.agents/skills/skill-manifest.json` was this repo's producer-authoritative
# process manifest and existed in NO OTHER ROOT. ADR-0067 phase 4 makes `.agents/skills` a generated
# view of `.claude/skills`, which did not contain it, so the retirement would have deleted a
# load-bearing tracked file and `git status --porcelain` would have come back EMPTY. It was found by
# a human running `diff -r` once. AC2 requires that command to live somewhere that RUNS.
#
# WHY EVERY LEG IS PAIRED. A check that has never been observed to fail is a check nobody can
# distinguish from a `true`, and this one guards a class whose whole character is silence. So each
# assertion below has a fixture that differs in ONE fact and flips the verdict:
#
#   1 <-> 2   the same file, in the source root vs in the second root — pass vs [second-root-only].
#             Leg 2 IS the measured `387adc6` layout; leg 1 IS the layout this item ships.
#   3 <-> 4   the same path in both roots, identical bytes vs one byte apart.
#   5..8      the fail-CLOSED family: absent root, absent source, empty source, no non-source root.
#             Each is exit 2, and the pairing is against leg 1 — the same script, a usable subject,
#             exit 0. "I could not evaluate this" is never "I evaluated it and it passed" (#266).
#
# Run:  bash scripts/tests/check-root-asymmetry.test.sh
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
subject="$repo/scripts/check-root-asymmetry.sh"

[ -f "$subject" ] || { echo "check-root-asymmetry.test.sh: $subject is missing." >&2; exit 2; }

fail=0
ok()  { printf '  ok   %s\n' "$*"; }
bad() { printf '  FAIL %s\n' "$*"; fail=1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

SRC=".claude/skills"
DST=".agents/skills"

# seed_skill <tree> <root> <id> — a skill is a DIRECTORY holding SKILL.md; that is what makes the
# source declare anything at all, and what the empty-source guard counts.
seed_skill() {
  local t="$1" root="$2" id="$3"
  mkdir -p "$t/$root/$id/references"
  printf '# %s\n\nfixture body.\n' "$id" >"$t/$root/$id/SKILL.md"
  printf 'fixture reference for %s.\n' "$id" >"$t/$root/$id/references/note.md"
}

# make_tree <name> — a minimal receiver: two roots carrying the same one skill, byte-identically.
make_tree() {
  local t="$work/$1"
  mkdir -p "$t"
  seed_skill "$t" "$SRC" demo-skill
  seed_skill "$t" "$DST" demo-skill
  printf '%s' "$t"
}

run_check() {
  local t="$1"
  shift
  RUN_OUT="$(cd "$t" && bash "$subject" --source "$SRC" --tree . "$@" 2>&1)"
  RUN_RC=$?
}

expect_rc() {
  local what="$1" want="$2"
  if [ "$RUN_RC" -eq "$want" ]; then ok "$what (exit $RUN_RC)"; else
    bad "$what: expected exit $want, got $RUN_RC"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  fi
}

expect_out() {
  local what="$1" needle="$2"
  if printf '%s' "$RUN_OUT" | grep -qF -- "$needle"; then ok "$what"; else
    bad "$what: output does not contain '$needle'"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  fi
}

refute_out() {
  local what="$1" needle="$2"
  if printf '%s' "$RUN_OUT" | grep -qF -- "$needle"; then
    bad "$what: output unexpectedly contains '$needle'"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  else ok "$what"; fi
}

# ---------------------------------------------------------------------------------------------
# 1. THE SHIPPED LAYOUT — a producer-authoritative file in the SOURCE root is not a finding.
# ---------------------------------------------------------------------------------------------
printf '\n1. a file only in the SOURCE root is reported and is NOT a finding\n'
T1="$(make_tree t1)"
printf '{"schemaVersion":2,"skills":[]}\n' >"$T1/$SRC/skill-manifest.json"
run_check "$T1"
expect_rc "the post-#771 layout passes" 0
expect_out "the source-only file is still REPORTED, not hidden" "[source-only]"
refute_out "and it is not classed as a finding" "[second-root-only]"

# ---------------------------------------------------------------------------------------------
# 2. THE MUTATION, AND THE MEASURED DEFECT — the same file, in the SECOND root.
# ---------------------------------------------------------------------------------------------
# Byte-for-byte the `387adc6` layout FS.GG.SDD#771 measured: one tracked file, present in
# `.agents/skills` and in no other root, which phase 4 deletes with a clean `git status`.
printf '\n2. MUTATION — the same file in the SECOND root is a finding\n'
T2="$(make_tree t2)"
printf '{"schemaVersion":2,"skills":[]}\n' >"$T2/$DST/skill-manifest.json"
run_check "$T2"
expect_rc "the pre-#771 layout REDS" 1
expect_out "the finding names its class" "[second-root-only]"
expect_out "the finding names the file" "skill-manifest.json"

# 2b. Nested, not just top-level: the class must reach INSIDE a skill directory, where
# `diff -r` spells it `Only in <root>/<id>:` rather than `Only in <root>:`.
printf '\n2b. a nested second-root-only file is found too\n'
T2b="$(make_tree t2b)"
printf 'orphan.\n' >"$T2b/$DST/demo-skill/orphan.md"
run_check "$T2b"
expect_rc "a file only under <second-root>/<id>/ REDS" 1
expect_out "the nested finding names its class" "[second-root-only]"
expect_out "the nested finding names the file" "orphan.md"

# ---------------------------------------------------------------------------------------------
# 3./4. BYTE DIVERGENCE — present in both, and not the same file.
# ---------------------------------------------------------------------------------------------
printf '\n3. identical bytes in both roots pass\n'
T3="$(make_tree t3)"
run_check "$T3"
expect_rc "two byte-identical roots pass" 0
refute_out "nothing is called divergent" "[divergent]"

printf '\n4. MUTATION — one byte apart is a finding\n'
T4="$(make_tree t4)"
printf '# demo-skill\n\nfixture body CHANGED.\n' >"$T4/$DST/demo-skill/SKILL.md"
run_check "$T4"
expect_rc "a divergent copy REDS" 1
expect_out "the finding names its class" "[divergent]"

# ---------------------------------------------------------------------------------------------
# 5..8. FAIL CLOSED — every "no subject" is exit 2, never a pass.
# ---------------------------------------------------------------------------------------------
printf '\n5. an ABSENT non-source root is exit 2, never a quiet pass\n'
T5="$(make_tree t5)"
rm -rf "${T5:?}/$DST"
run_check "$T5"
expect_rc "an absent root yields no verdict" 2
expect_out "the refusal names the class" "[absent-root]"
refute_out "and it does not report success" "OK — 1 non-source root"

printf '\n6. an ABSENT source is exit 2\n'
T6="$(make_tree t6)"
rm -rf "${T6:?}/$SRC"
run_check "$T6"
expect_rc "an absent source yields no verdict" 2
expect_out "the refusal says an absent source is not an empty one" "An absent source is not an empty one"

printf '\n7. a source declaring NO skills is exit 2 (the vacuous-pass guard)\n'
T7="$(make_tree t7)"
rm -rf "${T7:?}/$SRC/demo-skill"
run_check "$T7"
expect_rc "an empty source yields no verdict" 2
expect_out "the refusal cites the vacuous-pass epic" "refusing to report 'nothing is asymmetric' over nothing"

printf '\n8. a root set holding ONLY the source is exit 2 — nothing compared is nothing passed\n'
T8="$(make_tree t8)"
run_check "$T8" --roots "$SRC"
expect_rc "a source-only root set yields no verdict" 2
expect_out "the refusal says nothing was compared" "Nothing was compared, so nothing passed"

# ---------------------------------------------------------------------------------------------
# 9. THE WIRING — the real command, over this repository's real tree.
# ---------------------------------------------------------------------------------------------
# Legs 1-8 prove what the script does with a fixture; this one proves the invocation
# `.github/workflows/skill-view-check.yml` runs actually has a subject in THIS repo, and that the
# subject is the second root and not an empty root set that would pass over anything.
printf '\n9. this repository, through the resolved (not overridden) root set\n'
RUN_OUT="$(cd "$repo" && bash "$subject" --source .claude/skills --tree . 2>&1)"
RUN_RC=$?
expect_rc "this repo's tracked roots are asymmetry-free" 0
expect_out "the audited root set includes the second root" ".agents/skills"
expect_out "at least one non-source root was actually compared" "non-source root(s) are subsets of"

if [ "$fail" -ne 0 ]; then
  echo "check-root-asymmetry.test.sh: FAILURES" >&2
  exit 1
fi
echo "check-root-asymmetry.test.sh: all passed"
