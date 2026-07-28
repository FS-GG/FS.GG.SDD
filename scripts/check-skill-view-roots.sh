#!/usr/bin/env bash
# The RUNTIME SKILL-ROOT SET this repo declares — asserted on every `skill-view-check` run, which is a
# REQUIRED status check on `main` under enforce_admins.
#
# WHY THIS EXISTS (FS-GG/.github#1760, ADR-0067 §9 phase 4, the LAST of seven receivers). This repo
# used to commit its skills TWICE: `.claude/skills` and `.agents/skills` each held the same 32 skills
# in 51 byte-identical tracked files. Phase 4 retired the second copy: `.agents/skills` is now a VIEW
# root (ADR-0065 §A root's three dispositions) whose content `scripts/skill-view generate` resolves
# from `.claude/skills` at checkout. The union of `<FsggKitSkillRoots>` and `<FsggKitViewSkillRoots>`
# is the runtime root set, and it did not change.
#
# WHAT THE RETIREMENT GAVE UP, WHICH IS THE ONLY REASON THIS FILE IS HERE. Before it, a change that
# dropped `.agents/skills` from this repo's runtime contract would have been caught by
# `coordination-coherence`: the root was materialized into, so removing it produced missing files
# against the pin. Now it is not materialized into, and every gate that could notice goes QUIET
# instead of red. MEASURED ON THIS REPO'S OWN TREE, 2026-07-28, with the root emptied out of
# `<FsggKitViewSkillRoots>` and the directory deleted:
#
#   * `dotnet build .config/kit/FS.GG.Kit.receiver.proj -t:FsggKitMaterialize`
#       -> "FS.GG.Kit: no view skill roots declared (FsggKitViewSkillRoots is empty) — nothing to
#          assert."  Build succeeded, 0 errors.
#   * `coordination-sync --check --against-pin --repo FS-GG/FS.GG.SDD --include-build-config .`
#       -> "OK — all 28 materialized file(s) match the FS.GG.Kit 0.15.0 this tree pins."
#          That is the REQUIRED context `kit / coordination-kit`, green on exactly the tree this
#          alarm exists to fail.
#
# Both green, and `.agents/skills` simply gone from the runtime contract. The only observable
# consequence would be that Codex resolves zero skills here and exits 0 saying nothing (ADR-0067 §8's
# measured silent class). That is exactly the trade ADR-0067 §8 forbids — "a rewrite that removes the
# loud failure and adds the quiet one is worse than no rewrite" — so the retirement ships the
# replacement alarm in the same change. This is it.
#
# WHERE IT RIDES, AND WHY THAT HOST. `skill-view-check` is REQUIRED on `main`, is authored in this
# repo, needs no `dotnet` and no restore, and already runs `scripts/skill-view generate` as its first
# step. That is FS.GG.Audio's shape (a repo-owned script on an already-required context) as ported to
# FS.GG.Game and FS.GG.Governance, not a fifth one; FS.GG.Net's shape — a NEW gate job that is not
# required — is the one to avoid, and FS-GG/.github#1727 is open about it. FS-GG/.github#1710 owns
# collapsing the per-receiver copies; this is the fifth payment of that cost and is recorded as such
# rather than quietly repeated.
#
# THE REQUIRED SET IS THE ONE FROM THE API, NOT THE ONE FROM ANY WORKFLOW COMMENT. Read
# 2026-07-28, because FS.GG.Audio#212's worker nearly shipped a wrong change on the authority of a
# `gate.yml` comment that branch protection contradicted:
#
#   $ gh api repos/FS-GG/FS.GG.SDD/branches/main/protection
#   contexts: ["Deterministic gate (locked restore + build + test)",
#              "Shared-build-config drift check",
#              "API compatibility gate (breaking-change → SemVer major)",
#              "kit / coordination-kit",
#              "skill-view-check"]              enforce_admins: true
#
# `skill-union / skill-union` is required NOWHERE — this repo's caller was retired on 2026-07-28
# (`83b1f75`, FS-GG/.github#1715) and `skill-view-check` took its place in the required set first.
#
# IT GRADES THE DECLARATION, NOT MSBUILD'S EVALUATION, and that is deliberate rather than lazy. The
# faithful alternative is `dotnet msbuild -getProperty:` on the receiver project, which needs a
# RESTORE of the pinned FS.GG.Kit — a network round-trip and a .NET SDK added to a REQUIRED check
# that currently needs neither, to grade a two-line fact this repo authors in its own tree. It would
# also introduce a second source of truth for the package's defaults: a property this repo does NOT
# declare evaluates to the package default, so a text reader would have to restate
# `.claude/skills;.agents/skills` to interpret an absence, and a restated default is the
# invented-location bug one file over. Requiring BOTH properties to be declared EXPLICITLY removes
# the question: an absence is a RED, not a guess.
#
# ABSENCE IS RED HERE, AND THAT IS A PROPERTY OF THE HOST RATHER THAN OF THE CHECK. FS.GG.Audio's
# copy treats an absent view root as EXPECTED, because its host job runs on a bare checkout that
# never materializes and never generates. THIS host is different: `skill-view-check.yml` runs
# `scripts/skill-view generate` immediately before this script, and `FsggSddGenerateSkillView` in
# `.config/kit/FS.GG.Kit.receiver.proj` covers every tree the materialize runs in. By the time this
# runs the view MUST exist, so an absent root means the generate was removed or did not run — which
# is exactly the regression worth reporting. Do not import Audio's carve-out; it would make this
# lane unfalsifiable here.
#
# AND EVERY LANE DEMONSTRATES IT CAN FIRE, INCLUDING THE DANGLING CASE. FS.GG.Audio#212 is the
# reason this is spelled out: its `[[ ! -e "$view" ]]` absence test FOLLOWS SYMLINKS, so a DANGLING
# view root — ADR-0067 §8's headline class — answered `! -e` exactly as a missing path does, took
# the green branch, and left the branch whose message says "DANGLING" unreachable for the case it
# names. It survived a day because that lane had no can-fire demo while the declaration lane had
# six fixtures. So: `! -e && ! -L` for absence, a DEDICATED `-L && ! -e` branch for dangling, `! -d`
# for ADR-0067 §6's text-file class, and a demo that drives the ASSERTION (not merely the predicate)
# for every one of them — a demo that exercises only the predicate survives a mutation of the `bad`
# arm. FS.GG.Templates' stage-1 alarm has no view-resolution lane at all; do not copy from it.
#
# Fails CLOSED throughout: an unreadable project, a missing property, a multi-line declaration this
# reader cannot parse, a union that is not ADR-0011's two, a live root holding zero skills, and a
# declared root that is not actually resolvable on disk are each a failure. "I could not look" is
# never "looked, and fine" (FS-GG/.github#266).

set -euo pipefail

REPO_ROOT="${REPO_ROOT:-$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)}"

# ADR-0011 Decision 1 as amended by ADR-0067 §5 and executed by FS-GG/.github#1636: `.codex/skills` is
# retired, and the runtime root set is these two. SORTED, so the comparison is set equality and not an
# accident of which property each root is declared in — moving a root between the two properties is a
# legal disposition change (ADR-0065) and must NOT red this.
FSGG_RUNTIME_ROOTS_EXPECTED='.agents/skills .claude/skills'

# The root the others are views OF. Named once: the resolve lane counts against it.
FSGG_LIVE_ROOT='.claude/skills'

# The receiver project is where both properties live.
FSGG_RECEIVER_PROJ="${FSGG_RECEIVER_PROJ:-$REPO_ROOT/.config/kit/FS.GG.Kit.receiver.proj}"

PASS=0
FAIL=0
ok()  { PASS=$((PASS + 1)); printf '  \xe2\x9c\x93 %s\n' "$1"; }
bad() { FAIL=$((FAIL + 1)); printf '  \xe2\x9c\x97 %s\n' "$1"; }

# msbuild_property <file> <name>
# Echo the text of a single-line `<name>value</name>` element; echo nothing and return 1 when the
# element is absent, empty, or not on one line. Deliberately NOT an XML parser: the one thing this
# needs to distinguish is "declared with a value" from "anything else", and every "anything else"
# lands on the same red. A declaration this cannot read is a declaration a reviewer should reformat.
msbuild_property() {
  local file="$1" name="$2" value
  [[ -r "$file" ]] || return 1
  value="$(sed -n "s|^[[:space:]]*<${name}>\(.*\)</${name}>[[:space:]]*$|\1|p" "$file" | head -1)"
  [[ -n "$value" ]] || return 1
  printf '%s' "$value"
}

# runtime_root_union <file>
# Echo the sorted, space-separated union of <FsggKitSkillRoots> and <FsggKitViewSkillRoots>. Returns 1
# with nothing on stdout when either property is not declared — an undeclared property is the failure
# this alarm exists for, so it must not be silently treated as an empty contribution.
runtime_root_union() {
  local file="$1" live view
  live="$(msbuild_property "$file" FsggKitSkillRoots)"     || return 1
  view="$(msbuild_property "$file" FsggKitViewSkillRoots)" || return 1
  printf '%s;%s' "$live" "$view" | tr ';' '\n' \
    | sed 's|[[:space:]]||g; s|/*$||' | grep -v '^$' | sort -u | paste -sd' ' -
}

# ---------------------------------------------------------------------------------------------
# LANE 1 — the DECLARATION. Is the runtime root set still ADR-0011's two?
# ---------------------------------------------------------------------------------------------

# assert_runtime_roots <lane>
assert_runtime_roots() {
  local lane="$1" union
  if ! union="$(runtime_root_union "$FSGG_RECEIVER_PROJ")"; then
    bad "$lane: cannot read the runtime root set from $FSGG_RECEIVER_PROJ — both <FsggKitSkillRoots> and <FsggKitViewSkillRoots> must be declared, each on ONE line. ADR-0067 §9 phase 4 made this repo's second runtime root a generated VIEW, and no other gate can see it leave the contract (see this file's header)."
    return
  fi
  if [[ "$union" == "$FSGG_RUNTIME_ROOTS_EXPECTED" ]]; then
    ok "$lane: runtime skill roots are ADR-0011's two ($union) — the union of <FsggKitSkillRoots> and <FsggKitViewSkillRoots>"
  else
    bad "$lane: this repo's runtime skill roots are '$union', not '$FSGG_RUNTIME_ROOTS_EXPECTED'. A root that leaves this union leaves the runtime contract, and every other gate stays green while it does: coordination-coherence looks only at <FsggKitSkillRoots>, and FsggKitCheckSkillView reports 'nothing to assert' for an empty <FsggKitViewSkillRoots>. Codex would then resolve zero skills here and exit 0 saying nothing (ADR-0067 §8). If the root set is genuinely meant to change, that is an ADR-0065 §Retiring a root contract migration — amend the record and this constant in the same change."
  fi
}

# assert_runtime_roots_can_fire <lane>
# "Demonstrated, not asserted" (FS-GG/.github#1611 category D: a gate that never fires and a gate that
# always passes are indistinguishable from outside). Entirely offline, entirely local: five fixture
# projects in a temp dir plus one path that does not exist, driving the ASSERTION rather than only the
# predicate, with the counters snapshotted and restored.
assert_runtime_roots_can_fire() {
  local lane="$1" tmp saved_pass saved_fail proj
  tmp="$(mktemp -d)"
  saved_pass="$PASS" saved_fail="$FAIL"

  local ok_cases=0 fired=0

  # (1) the shape this repo ships: both declared, union is the two roots -> PASS
  proj="$tmp/good.proj"
  printf '<Project>\n  <FsggKitSkillRoots>.claude/skills</FsggKitSkillRoots>\n  <FsggKitViewSkillRoots>.agents/skills</FsggKitViewSkillRoots>\n</Project>\n' > "$proj"
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 0 && "$PASS" -eq 1 ]] && ok_cases=$((ok_cases + 1))

  # (2) the disposition swap: same union, roots declared the other way round -> PASS. This is a legal
  #     ADR-0065 move and reddening it would make the alarm an obstacle to the contract it protects.
  proj="$tmp/swapped.proj"
  printf '<Project>\n  <FsggKitSkillRoots>.agents/skills</FsggKitSkillRoots>\n  <FsggKitViewSkillRoots>.claude/skills</FsggKitViewSkillRoots>\n</Project>\n' > "$proj"
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 0 && "$PASS" -eq 1 ]] && ok_cases=$((ok_cases + 1))

  # (3) THE REGRESSION THIS FILE EXISTS FOR: the view root emptied. Every other gate is green on that
  #     tree — measured, see the header — and this must not be.
  proj="$tmp/emptied.proj"
  printf '<Project>\n  <FsggKitSkillRoots>.claude/skills</FsggKitSkillRoots>\n  <FsggKitViewSkillRoots></FsggKitViewSkillRoots>\n</Project>\n' > "$proj"
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 1 ]] && fired=$((fired + 1))

  # (4) the property deleted outright -> RED. An absent property must never read as an empty
  #     contribution to the union, which would make the deletion the very thing it silently allows.
  proj="$tmp/deleted.proj"
  printf '<Project>\n  <FsggKitSkillRoots>.claude/skills</FsggKitSkillRoots>\n</Project>\n' > "$proj"
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 1 ]] && fired=$((fired + 1))

  # (5) a THIRD root added without a contract migration -> RED. The alarm is set equality, not a
  #     minimum: ADR-0065 governs adding a root exactly as it governs removing one. `.codex/skills` is
  #     the realistic mistake here — it is retired (ADR-0067 §5) and this repo still holds 28 of its
  #     OWN skills there, which is not the same thing as it being a runtime root.
  proj="$tmp/extra.proj"
  printf '<Project>\n  <FsggKitSkillRoots>.claude/skills;.codex/skills</FsggKitSkillRoots>\n  <FsggKitViewSkillRoots>.agents/skills</FsggKitViewSkillRoots>\n</Project>\n' > "$proj"
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 1 ]] && fired=$((fired + 1))

  # (6) an unreadable project -> RED. "I could not look" is never "looked, and fine".
  PASS=0 FAIL=0; FSGG_RECEIVER_PROJ="$tmp/does-not-exist.proj" assert_runtime_roots "$lane" >/dev/null
  [[ "$FAIL" -eq 1 ]] && fired=$((fired + 1))

  PASS="$saved_pass" FAIL="$saved_fail"
  rm -rf "$tmp"

  if [[ "$ok_cases" -eq 2 && "$fired" -eq 4 ]]; then
    ok "$lane: the runtime-root alarm can fire — 4 of 4 regressions RED (emptied view root, deleted property, extra root, unreadable project) and 2 of 2 legal shapes GREEN"
  else
    bad "$lane: the runtime-root alarm is NOT demonstrably live — $ok_cases/2 legal shapes passed and $fired/4 regressions fired. A gate that cannot fire is not a gate (FS-GG/.github#1611 category D)."
  fi
}

# ---------------------------------------------------------------------------------------------
# LANE 2 — RESOLUTION. Is every DECLARED root actually there, and does it expose the whole set?
# ---------------------------------------------------------------------------------------------
# The declaration lane cannot see a checkout whose view root was never generated, nor one whose link
# dangles. This one reads the filesystem, and it resolves THROUGH the link deliberately (`find -L`):
# a dangling view root, or one that degraded to a plain text file under `git -c core.symlinks=false`
# (ADR-0067 §6), resolves to zero skills while both runtimes exit 0 saying nothing.
#
# Every declared runtime root is graded, not only the view: `.claude/skills` disappearing is the same
# silent class, and the union is what the contract names.

# resolve_one <lane> <tree> <root> <live_n>
resolve_one() {
  local lane="$1" tree="$2" root="$3" live_n="$4" path root_n
  path="$tree/$root"

  # ABSENT: nothing at the path, and it is not a link either. `! -e` ALONE IS NOT THIS TEST — `-e`
  # follows symlinks, so a dangling link answers it identically and would be misreported here while
  # the branch that names it below went unreachable (FS.GG.Audio#212, measured).
  if [[ ! -e "$path" && ! -L "$path" ]]; then
    bad "$lane: declared runtime root '$root' does not exist. This job runs 'skill-view generate' immediately before this script and FsggSddGenerateSkillView covers every tree the materialize runs in, so an absent root here means the generate was removed or did not run — that runtime resolves ZERO skills and exits 0 while doing it (ADR-0067 §8). Regenerate: bash scripts/skill-view generate --source $FSGG_LIVE_ROOT --roots \"$root\""
    return
  fi

  # DANGLING: a link that resolves to nothing. ADR-0067 §8's headline class, and its own branch.
  if [[ -L "$path" && ! -e "$path" ]]; then
    bad "$lane: declared runtime root '$root' is a DANGLING symlink — it resolves to zero skills and BOTH runtimes exit 0 saying nothing (ADR-0067 §8). Regenerate it: bash scripts/skill-view generate --source $FSGG_LIVE_ROOT --roots \"$root\""
    return
  fi

  # NOT A DIRECTORY: ADR-0067 §6's text-file class.
  if [[ ! -d "$path" ]]; then
    bad "$lane: declared runtime root '$root' exists but is not a directory. A COMMITTED symlink checks out as a plain text file under 'git -c core.symlinks=false' (ADR-0067 §6) and both runtimes then load zero skills silently. The view root must be generated, never committed."
    return
  fi

  root_n="$(find -L "$path" -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l)" || root_n=0
  if [[ "$root_n" -eq "$live_n" ]]; then
    ok "$lane: declared runtime root '$root' exposes all $root_n skill(s) the live root holds"
  else
    bad "$lane: declared runtime root '$root' exposes $root_n skill(s) but the live root $FSGG_LIVE_ROOT holds $live_n. A partly-visible root is the same silent failure as an empty one (ADR-0067 §8)."
  fi
}

# assert_roots_resolve <lane> [tree] [root ...]
# With no tree/roots, grades THIS repo through the declaration read above — the wiring invocation.
assert_roots_resolve() {
  local lane="$1" tree="${2:-$REPO_ROOT}" union live live_n root
  shift 2 2>/dev/null || shift
  if [[ "$#" -gt 0 ]]; then
    union="$*"
  elif ! union="$(runtime_root_union "$FSGG_RECEIVER_PROJ")"; then
    bad "$lane: not graded — the runtime root set could not be read (see above). Nothing was verified."
    return
  fi
  live="$tree/$FSGG_LIVE_ROOT"
  live_n="$(find "$live" -mindepth 1 -maxdepth 1 -type d 2>/dev/null | wc -l)" || live_n=0
  if [[ "$live_n" -eq 0 ]]; then
    bad "$lane: the live root $FSGG_LIVE_ROOT holds ZERO skills — refusing to report 'everything is visible' over nothing (FS-GG/.github#266)."
    return
  fi
  for root in $union; do
    resolve_one "$lane" "$tree" "$root" "$live_n"
  done
}

# assert_roots_resolve_can_fire <lane>
# The same can-fire discipline for the SECOND lane, and the reason it is not optional.
# `assert_runtime_roots` shipped with a six-fixture demo on FS.GG.Audio and was correct;
# `assert_view_resolves` shipped there with NO demo and was wrong on its own headline case for a whole
# day (FS.GG.Audio#212). The lane with the proof held and the lane without it did not, in the same
# file, on the same day.
#
# THE DEMO'S OWN MUTATION WAS TESTED. Each fixture below was run against a deliberately broken
# `resolve_one` before this landed — absence collapsed back to a bare `! -e`, and the dangling
# fixture then took the absent branch — and the counters below still separate the two, because each
# case asserts the SPECIFIC message its branch emits rather than only that something fired.
assert_roots_resolve_can_fire() {
  local lane="$1" tmp saved_pass saved_fail t out
  tmp="$(mktemp -d)"
  saved_pass="$PASS" saved_fail="$FAIL"

  local ok_cases=0 fired=0

  mk() {  # mk <name> -> echoes a tree root holding two live skills
    local t="$tmp/$1"
    mkdir -p "$t/.claude/skills/alpha" "$t/.claude/skills/beta" "$t/.agents"
    printf '%s' "$t"
  }

  # run_case <tree> — drives the ASSERTION over one fixture tree and leaves its verdict in PASS/FAIL
  # and its diagnostic in `out`. The output is captured through a FILE, not `$( )`: a command
  # substitution runs in a SUBSHELL, so the counters the assertion increments would be discarded and
  # every case below would read `FAIL=0` — a demo that always reports "nothing fired" while looking
  # exactly like one that works.
  run_case() {
    PASS=0 FAIL=0
    assert_roots_resolve "$lane" "$1" ".agents/skills" >"$tmp/out.txt" 2>&1
    out="$(cat "$tmp/out.txt")"
  }

  # (1) a resolving view over the same population -> PASS. The shape a generated tree has.
  t="$(mk resolving)"; ln -s ../.claude/skills "$t/.agents/skills"
  run_case "$t"
  [[ "$FAIL" -eq 0 && "$PASS" -eq 1 ]] && ok_cases=$((ok_cases + 1))

  # (2) nothing at the path at all -> RED here, unlike FS.GG.Audio's copy. This host generates the
  #     view immediately before this script runs, so an absent root is a broken pipeline, not a bare
  #     clone. See the header for why that carve-out does not transfer.
  t="$(mk absent)"
  run_case "$t"
  [[ "$FAIL" -eq 1 ]] && printf '%s' "$out" | grep -q 'does not exist' && fired=$((fired + 1))

  # (3) a DANGLING link -> RED, through its OWN branch. ADR-0067 §8's headline class, and the case
  #     FS.GG.Audio's `[[ ! -e ]]` reported GREEN at `52a358f`. The message assertion is what makes
  #     this distinguishable from case (2) collapsing onto it.
  t="$(mk dangling)"; ln -s ../.claude/skills-that-do-not-exist "$t/.agents/skills"
  run_case "$t"
  [[ "$FAIL" -eq 1 ]] && printf '%s' "$out" | grep -q 'DANGLING symlink' && fired=$((fired + 1))

  # (4) a plain FILE where the root belongs -> RED. What a COMMITTED symlink degrades to under
  #     `git -c core.symlinks=false`, measured in ADR-0067 §6: exit 0, zero skills, no diagnostic.
  t="$(mk textfile)"; printf '../.claude/skills' > "$t/.agents/skills"
  run_case "$t"
  [[ "$FAIL" -eq 1 ]] && printf '%s' "$out" | grep -q 'is not a directory' && fired=$((fired + 1))

  # (5) a PARTIAL view -> RED. A real directory holding fewer skills than the live root.
  t="$(mk partial)"; mkdir -p "$t/.agents/skills/alpha"
  run_case "$t"
  [[ "$FAIL" -eq 1 ]] && printf '%s' "$out" | grep -q 'partly-visible' && fired=$((fired + 1))

  # (6) an EMPTY live root -> RED, and the whole lane refuses rather than reporting per-root success.
  #     "I could not evaluate this" is never "I evaluated it and it passed" (FS-GG/.github#266).
  t="$tmp/nolive"; mkdir -p "$t/.claude/skills" "$t/.agents"; ln -s ../.claude/skills "$t/.agents/skills"
  run_case "$t"
  [[ "$FAIL" -eq 1 ]] && printf '%s' "$out" | grep -q 'ZERO skills' && fired=$((fired + 1))

  PASS="$saved_pass" FAIL="$saved_fail"
  rm -rf "$tmp"

  if [[ "$ok_cases" -eq 1 && "$fired" -eq 5 ]]; then
    ok "$lane: the view-resolution alarm can fire — 5 of 5 regressions RED, each through the branch that names it (absent root, dangling link, text file, partial view, empty live root) and 1 of 1 legal shape GREEN"
  else
    bad "$lane: the view-resolution alarm is NOT demonstrably live — $ok_cases/1 legal shapes passed and $fired/5 regressions fired through their own branch. A gate that cannot fire is not a gate (FS-GG/.github#1611 category D)."
  fi
}

printf 'skill-view-roots: the runtime skill-root contract (ADR-0011 / ADR-0065 / ADR-0067 §8)\n'
assert_runtime_roots           "roots"
assert_runtime_roots_can_fire  "can-fire(roots)"
assert_roots_resolve           "resolve"
assert_roots_resolve_can_fire  "can-fire(resolve)"

printf 'skill-view-roots: %d passed, %d failed\n' "$PASS" "$FAIL"
[[ "$FAIL" -eq 0 ]] || exit 1
