#!/usr/bin/env bash
# Unit test for FS.GG.SDD#801: the workflow-level `skill-view generate` invocations must resolve
# the declaration, not a hand-copied `--source`/`--roots` pair.
#
# THE DEFECT IT PINS. `scripts/skill-view generate --receiver-proj` was released by
# FS-GG/.github#1893 so a receiver's generate step reads the same `<FsggKitSkillRoots>` /
# `<FsggKitViewSkillRoots>` declaration `check` already grades. `.github/workflows/gate.yml` and
# `.github/workflows/skill-view-check.yml` still hand-copied `--source .claude/skills --roots
# ".agents/skills"` instead — recreating the exact split declaration mode removes. A legal
# ADR-0065 root-disposition change (moving which root is tracked and which is generated, or
# renaming either) then edits `.config/kit/FS.GG.Kit.receiver.proj` alone, and these two
# hand-copied pairs silently keep generating the OLD roots: `skill-view check --receiver-proj`
# grades the union correctly downstream, but by then the generate step has already written (or
# left absent) the wrong tree. This test pins two things: that both workflow files now spell the
# declared invocation, literally, and that the invocation this replaces is genuinely blind to a
# declaration it does not read — so the fix is not cosmetic.
#
# LEGS 1-2 ARE DIRECTLY REVERT-SENSITIVE: they read the two committed workflow files verbatim, so
# reverting FS.GG.SDD#801's edit to either file fails the matching leg immediately — this is not a
# second hand-maintained list pinned against a first; the workflow file IS the ground truth graded.
#
# LEGS 3-4 ARE PAIRED, in the house style of `materialize-skill-roots.test.sh` and
# `check-root-asymmetry.test.sh`: leg 3 shows `--receiver-proj` honours a declaration that differs
# from the ADR-0011 default pair (so it is not itself secretly hard-coded to `.claude/skills` /
# `.agents/skills`); leg 4 shows the OLD hand-copied invocation, run against that SAME disposition
# change, cannot find its source at all. That is the concrete shape of "target the wrong root
# before check detects it" the issue names — under leg 4's declaration a hand-copied caller has no
# way to notice its pair went stale, because it never reads the declaration in the first place.
#
# Run:  bash scripts/tests/skill-view-generate-invocation.test.sh
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
tool="$repo/scripts/skill-view"
gate_yml="$repo/.github/workflows/gate.yml"
check_yml="$repo/.github/workflows/skill-view-check.yml"
receiver_proj_rel=".config/kit/FS.GG.Kit.receiver.proj"

for f in "$tool" "$gate_yml" "$check_yml"; do
  [ -f "$f" ] || { echo "skill-view-generate-invocation.test.sh: $f is missing." >&2; exit 2; }
done

fail=0
ok()  { printf '  ok   %s\n' "$*"; }
bad() { printf '  FAIL %s\n' "$*"; fail=1; }

expected="bash scripts/skill-view generate --receiver-proj $receiver_proj_rel"

# assert_declared_invocation <label> <file> — the file's `skill-view generate` run: line must be
# the declared form, EXACTLY (whitespace-trimmed), and must name neither --source nor --roots.
assert_declared_invocation() {
  local label="$1" file="$2" line
  line="$(grep -m1 -E '^[[:space:]]*run:[[:space:]]*bash scripts/skill-view generate' "$file" \
          | sed 's/^[[:space:]]*run:[[:space:]]*//; s/[[:space:]]*$//')"
  if [ -z "$line" ]; then
    bad "$label: no 'skill-view generate' invocation found in $file"
    return
  fi
  if [ "$line" = "$expected" ]; then
    ok "$label: generate step reads '$expected'"
  else
    bad "$label: generate step is '$line', expected '$expected' (FS.GG.SDD#801 — reverted to a hand-copied pair?)"
  fi
  case "$line" in
    *--source*|*--roots*)
      bad "$label: generate step still names --source/--roots directly: '$line'" ;;
  esac
}

assert_declared_invocation "gate.yml" "$gate_yml"
assert_declared_invocation "skill-view-check.yml" "$check_yml"

# ---------------------------------------------------------------------------------------------
# Legs 3-4: the declaration this replaces is genuinely blind to a disposition change; the
# declaration-driven form is not.
# ---------------------------------------------------------------------------------------------

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# seed_swapped_tree <dir> — a legal ADR-0065 disposition SWAP of this repo's own pair:
# `.agents/skills` is the tracked LIVE source and `.claude/skills` is the generated VIEW — the
# reverse of what gate.yml and skill-view-check.yml assumed before FS.GG.SDD#801. The union is
# still the ADR-0011 default pair, so this is exactly the class of change `skill-view check
# --receiver-proj` stays green over; only a generate step that reads the declaration notices which
# root is which. Each leg gets its OWN fresh copy so leg 4 sees the swap BEFORE any correct
# generate has ever run over it — not leg 3's leftover view.
seed_swapped_tree() {
  local t="$1"
  mkdir -p "$t/.agents/skills/demo" "$t/.config/kit"
  printf '# demo\n\nfixture body.\n' >"$t/.agents/skills/demo/SKILL.md"
  cat >"$t/$receiver_proj_rel" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultItems>false</EnableDefaultItems>
    <FsggKitSkillRoots>.agents/skills</FsggKitSkillRoots>
    <FsggKitViewSkillRoots>.claude/skills</FsggKitViewSkillRoots>
  </PropertyGroup>
</Project>
EOF
}

# leg 3: --receiver-proj follows the DECLARED disposition — source '.agents/skills', view
# '.claude/skills' — not the pair gate.yml/skill-view-check.yml hard-coded before FS.GG.SDD#801.
tree3="$work/receiver-leg3"
seed_swapped_tree "$tree3"
if out3="$(bash "$tool" generate --receiver-proj "$tree3/$receiver_proj_rel" --tree "$tree3" 2>&1)"; then
  if [ -f "$tree3/.claude/skills/demo/SKILL.md" ]; then
    ok "leg 3: --receiver-proj generated '.claude/skills' as a view of '.agents/skills', per the swapped declaration"
  else
    bad "leg 3: --receiver-proj exited 0 but '.claude/skills/demo/SKILL.md' is missing:"$'\n'"$out3"
  fi
else
  bad "leg 3: --receiver-proj against a swapped declaration should succeed; it did not:"$'\n'"$out3"
fi

# leg 4: the OLD hand-copied pair this issue replaces, run against a FRESH copy of the SAME
# swapped declaration — before anything has generated a view over it. It still assumes
# '.claude/skills' is the LIVE source, which the swap just made a VIEW root that does not exist
# yet, so it has no source to read and must refuse rather than silently generating over the wrong
# root or reporting nothing changed. This is the concrete shape of "a legal root-disposition change
# can make generation target the wrong root before check detects it."
tree4="$work/receiver-leg4"
seed_swapped_tree "$tree4"
if out4="$(bash "$tool" generate --source "$tree4/.claude/skills" --roots ".agents/skills" --tree "$tree4" 2>&1)"; then
  bad "leg 4: the hand-copied --source/--roots pair should refuse a source the disposition swap moved; it exited 0 instead:"$'\n'"$out4"
else
  case "$out4" in
    *"--tree is not a directory"*)
      bad "leg 4: refused for the wrong reason (bad --tree, not a missing source):"$'\n'"$out4" ;;
    *".claude/skills"*)
      ok "leg 4: the hand-copied pair is blind to the swapped declaration — '.claude/skills' does not exist yet — and correctly refuses (the class FS.GG.SDD#801 removes): $out4" ;;
    *)
      bad "leg 4: refused, but not for the expected reason (source '.claude/skills' unreadable after the swap):"$'\n'"$out4" ;;
  esac
fi

if [ "$fail" -eq 0 ]; then
  echo "skill-view-generate-invocation.test.sh: OK"
else
  echo "skill-view-generate-invocation.test.sh: FAILED" >&2
fi
exit "$fail"
