#!/usr/bin/env bash
# Unit test for the WRITE SET of scripts/materialize-skill-roots.fsx (FS.GG.SDD#767).
#
# THE DEFECT IT PINS. FS.GG.Kit 0.15.0 carried ADR-0067 §5's retirement of `.codex/skills` into this
# receiver and its materializer swept the four kit-owned skills (23 files) out of that root. The
# driver still took `Fsgg.Schemas.agentSkillRoots` verbatim, so it planned those 23 files as writes:
# `--check` exited 1 naming them, and the WRITE mode — the command the driver's own header and
# `.github/workflows/skill-union.yml` both print as the repair — would have put every one of them
# back, into the root the transport contract had just removed. A receiver would have undone a
# retirement by running its own documented command, with every gate green.
#
# WHY IT IS ASSERTED BY MUTATION AND NOT BY RE-RUNNING `--check` (criterion 2). On 2026-07-28 `--check`
# exited 1 on this repo's own tree, so "run it and see 0" would pass the moment ANYTHING made the tree
# coherent — including re-creating the 23 mirrors, the exact outcome this guards against. It is also a
# statement about one tree on one day: the 23 paths disappear from `main` when ADR-0067 phase 4
# (FS-GG/.github#1676) retires the root here, and the assertion would then be vacuous forever after.
# So each fixture below declares a root set, and each assertion has a PAIRED fixture that differs only
# in that declaration and flips the outcome. Every leg here is red against the pre-#767 driver, which
# had no such input at all.
#
# NOTHING IS HARD-CODED ABOUT WHICH ROOT IS RETIRED. The fixtures derive their roots from
# `Fsgg.Schemas.agentSkillRoots` in the SAME assembly the driver loads, so this test keeps meaning
# what it says when FS.GG.SDD#757 narrows that constant to two — the victim root is simply whichever
# root the constant declares second. MEASURED at authoring time: with the constant at three the victim
# is `.codex`; the `.agents`-as-victim shape that #757 will select is covered by leg B, which retires
# the provider-source root and is green today.
#
# Run:  dotnet build src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release
#       bash scripts/tests/materialize-skill-roots.test.sh
set -uo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"
release="$repo/src/FS.GG.Contracts/bin/Release/net10.0"

if [ ! -f "$release/FS.GG.Contracts.dll" ]; then
  echo "materialize-skill-roots.test.sh: $release/FS.GG.Contracts.dll is not built." >&2
  echo "  Build it first: dotnet build src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release" >&2
  exit 2
fi

fail=0
ok()  { printf '  ok   %s\n' "$*"; }
bad() { printf '  FAIL %s\n' "$*"; fail=1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# ---------------------------------------------------------------------------------------------
# The declared root set, read from the assembly under test — never re-spelled here.
# ---------------------------------------------------------------------------------------------
cat >"$work/declared.fsx" <<EOF
#r "$release/FS.GG.Contracts.dll"
printfn "%s" (String.concat " " Fsgg.Schemas.agentSkillRoots)
printfn "%s" Fsgg.SkillMirror.providerSourceRoot
EOF

if ! declared_out="$(dotnet fsi "$work/declared.fsx" 2>&1)"; then
  echo "materialize-skill-roots.test.sh: could not read Schemas.agentSkillRoots:" >&2
  echo "$declared_out" >&2
  exit 2
fi

read -r declared_line <<<"$declared_out"
provider_root="$(printf '%s\n' "$declared_out" | sed -n '2p')"
read -r -a declared <<<"$declared_line"

if [ "${#declared[@]}" -lt 2 ]; then
  echo "materialize-skill-roots.test.sh: agentSkillRoots declares ${#declared[@]} root(s); these fixtures need at least 2." >&2
  exit 2
fi

printf '  declared roots: %s   (provider-source root: %s)\n' "$declared_line" "$provider_root"

# ---------------------------------------------------------------------------------------------
# Fixture construction.
# ---------------------------------------------------------------------------------------------

# kit_decl <tree> <FsggKitSkillRoots> <FsggKitRetiredSkillRoots> <FsggKitViewSkillRoots>
#
# A STANDALONE receiver project that declares the three kit properties directly, rather than one
# referencing FS.GG.Kit. The driver reads these through MSBuild's evaluator either way, and the
# subject here is what the driver DOES with a declaration — a fixture that had to restore the real
# package could only ever assert today's kit's opinion, which is leg F's job, not this one's.
kit_decl() {
  local t="$1"
  mkdir -p "$t/.config/kit"
  cat >"$t/.config/kit/FS.GG.Kit.receiver.proj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultItems>false</EnableDefaultItems>
    <FsggKitSkillRoots>$2</FsggKitSkillRoots>
    <FsggKitRetiredSkillRoots>$3</FsggKitRetiredSkillRoots>
    <FsggKitViewSkillRoots>$4</FsggKitViewSkillRoots>
  </PropertyGroup>
</Project>
EOF
}

# seed_skill <tree> <root> <id> — the whole file set, not just SKILL.md, because that is what the
# driver mirrors. Byte-identical in every root it is seeded into.
seed_skill() {
  local t="$1" root="$2" id="$3"
  mkdir -p "$t/$root/skills/$id/references"
  printf '# %s\n\nfixture body.\n' "$id" >"$t/$root/skills/$id/SKILL.md"
  printf 'fixture reference for %s.\n' "$id" >"$t/$root/skills/$id/references/note.md"
}

# The producer manifest's committed home, read from the SAME declaration the driver derives it from
# (FS.GG.SDD#771): `Schemas.agentSkillRoots`' FIRST root — the tracked source root — and NOT
# `providerSourceRoot`, which ADR-0067 §6 turns into an untracked generated view. Spelled once, here,
# so leg E below can move it without every other fixture having to know.
source_root="${declared[0]}"
manifest_rel="$source_root/skills/skill-manifest.json"

# make_tree <name> <skill-roots> <retired-roots> <view-roots> — a minimal receiver the driver
# accepts: the sln marker it locates the repo root by, ITS OWN copy of the script under test, the
# built library it `#r`s, a producer manifest declaring nothing (so the verdict rests on presence +
# cross-root identity and the subject stays the ROOT SET), and the kit declaration.
make_tree() {
  local name="$1" t="$work/$1"
  mkdir -p "$t/scripts" "$t/src/FS.GG.Contracts/bin/Release" "$t/$provider_root/skills" "$t/$source_root/skills"
  : >"$t/FS.GG.SDD.sln"
  cp "$repo/scripts/materialize-skill-roots.fsx" "$t/scripts/materialize-skill-roots.fsx"
  ln -s "$release" "$t/src/FS.GG.Contracts/bin/Release/net10.0"
  printf '{"schemaVersion":2,"skills":[]}\n' >"$t/$manifest_rel"
  kit_decl "$t" "$2" "$3" "$4"
  printf '%s' "$t"
}

# run_driver <tree> [--check] — captures stdout+stderr and the exit code into RUN_OUT / RUN_RC.
run_driver() {
  local t="$1"
  shift
  RUN_OUT="$(cd "$t" && dotnet fsi scripts/materialize-skill-roots.fsx "$@" 2>&1)"
  RUN_RC=$?
}

# `.claude` -> `.claude/skills`, the spelling the kit properties use.
kitspell() { printf '%s/skills' "$1"; }

join_kit() {
  local out="" r
  for r in "$@"; do
    out="${out:+$out;}$(kitspell "$r")"
  done
  printf '%s' "$out"
}

expect_rc() {
  local what="$1" want="$2"
  if [ "$RUN_RC" -eq "$want" ]; then ok "$what (exit $RUN_RC)"; else
    bad "$what: expected exit $want, got $RUN_RC"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  fi
}

# The `roots` line, compared as a WHOLE VALUE and never as a substring. `expect_out`'s grep -F would
# pass `.claude .codex` against a driver printing `.claude .codex .agents` — a superset satisfying an
# assertion that the superset is exactly what it must not be. Measured: leg B passed against the
# pre-#767 driver for precisely that reason before this helper existed.
roots_line_is() {
  local what="$1" want="$2" got
  got="$(printf '%s\n' "$RUN_OUT" | sed -n 's/^  roots  *: //p' | head -1)"
  if [ "$got" = "$want" ]; then ok "$what"; else
    bad "$what: the write set is '$got', expected exactly '$want'"
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
# A. A RETIRED ROOT IS NOT WRITTEN — the subject.
# ---------------------------------------------------------------------------------------------
# The victim is `agentSkillRoots`' SECOND root: never the first, which is the producer-authoritative
# root every canonical body is read from (`canonicalRootOf` picks "the first root that has it"), and
# so the one root a retirement fixture must not remove.
victim="${declared[1]}"
survivors=()
for r in "${declared[@]}"; do [ "$r" = "$victim" ] || survivors+=("$r"); done

printf '\nA. retired root "%s" is neither written nor swept (survivors: %s)\n' "$victim" "${survivors[*]}"

A="$(make_tree A "$(join_kit "${survivors[@]}")" "$(kitspell "$victim")" "")"
for r in "${survivors[@]}"; do seed_skill "$A" "$r" demo-skill; done
# What the kit's sweep leaves behind: a skill the RECEIVER owns, in the retired root. ADR-0065
# §Retiring a root forbids the receiver hand-deleting a mirror, so it must survive untouched.
seed_skill "$A" "$victim" leftover-repo-owned
leftover="$A/$victim/skills/leftover-repo-owned/SKILL.md"
leftover_before="$(cat "$leftover")"

run_driver "$A" --check
expect_rc "swept tree is CLEAN under --check" 0
roots_line_is "the write set excludes the retired root" "${survivors[*]}"
expect_out "the retirement is reported, not silently skipped" "retired      : $victim"
refute_out "no drift is claimed against the retired root" "DRIFT $victim/"

run_driver "$A"
expect_rc "write mode succeeds" 0
if [ -e "$A/$victim/skills/demo-skill" ]; then
  bad "write mode RE-CREATED the mirror under the retired root $victim"
else
  ok "write mode created nothing under the retired root $victim"
fi
if [ "$(cat "$leftover")" = "$leftover_before" ]; then
  ok "the receiver's own skill under the retired root is untouched"
else
  bad "the receiver's own skill under the retired root was modified"
fi

# ---------------------------------------------------------------------------------------------
# A'. THE MUTATION: lift the retirement, and the same tree gets the mirror back.
# ---------------------------------------------------------------------------------------------
# Identical fixture, one difference: the declaration retires nothing and lists every declared root as
# a materialize target. If this leg did NOT re-create the mirror, leg A would be asserting something
# other than the retirement — it would pass for a driver that simply never writes that root, or for a
# fixture whose seeding made the write a no-op. This is also, exactly, what the pre-#767 driver did on
# leg A's tree.
printf '\nA'"'"'. MUTATION — retirement lifted, so the same tree DOES get the mirror\n'

Am="$(make_tree Am "$(join_kit "${declared[@]}")" "" "")"
for r in "${survivors[@]}"; do seed_skill "$Am" "$r" demo-skill; done
seed_skill "$Am" "$victim" leftover-repo-owned

run_driver "$Am"
expect_rc "write mode succeeds with nothing retired" 0
roots_line_is "the write set now includes every declared root" "${declared[*]}"
if [ -f "$Am/$victim/skills/demo-skill/SKILL.md" ] &&
  cmp -s "$Am/$victim/skills/demo-skill/SKILL.md" "$Am/${declared[0]}/skills/demo-skill/SKILL.md"; then
  ok "the mirror IS re-created under $victim when nothing retires it"
else
  bad "the mutation did not re-create the mirror — leg A cannot fail, so it proves nothing"
fi
# The coherence guard's negative case, on a fixture whose declarations agree.
refute_out "agreeing declarations are not reported as a disagreement" "disagree about the runtime surface"

# ---------------------------------------------------------------------------------------------
# B. RETIRING THE PROVIDER-SOURCE ROOT — the shape FS.GG.SDD#757 will select.
# ---------------------------------------------------------------------------------------------
# When the constant narrows to two, leg A's victim becomes the provider-source root (`.agents`).
# Covering it now means #757 lands against a test that already knows the answer, instead of one that
# starts exercising an unmeasured path on the day the constant changes.
#
# THIS LEG'S SUBJECT CHANGED WITH FS.GG.SDD#771, AND THE OLD ONE IS NOW LEG E'S. It used to read
# "the manifest is still read from a RETIRED provider-source root" — true when the manifest lived at
# `.agents/skills/skill-manifest.json`, and a fact that stopped existing when #771 moved it to the
# tracked source root. What remains here is the write-set claim: retiring the provider-source root
# removes it from the writes and nothing else breaks. That the manifest is read from the SOURCE root,
# and that its absence is loud, is asserted by leg E, on its own fixtures.
if [ "$provider_root" != "${declared[0]}" ] && [ "$provider_root" != "$victim" ]; then
  printf '\nB. retiring the provider-source root "%s"\n' "$provider_root"

  b_survivors=()
  for r in "${declared[@]}"; do [ "$r" = "$provider_root" ] || b_survivors+=("$r"); done

  B="$(make_tree B "$(join_kit "${b_survivors[@]}")" "$(kitspell "$provider_root")" "")"
  for r in "${b_survivors[@]}"; do seed_skill "$B" "$r" demo-skill; done

  run_driver "$B" --check
  expect_rc "retiring the provider-source root leaves the tree clean" 0
  roots_line_is "the write set excludes the provider-source root" "${b_survivors[*]}"
else
  printf '\nB. skipped — the provider-source root is already leg A'"'"'s subject\n'
fi

# ---------------------------------------------------------------------------------------------
# C. THE TWO DECLARATIONS MUST AGREE — a stale one is refused, not laundered.
# ---------------------------------------------------------------------------------------------
# The kit's own props say the runtime surface is `FsggKitSkillRoots` + `FsggKitViewSkillRoots` and
# that THAT union must equal `agentSkillRoots`. If they disagree, one of them is stale and the driver
# must not pick a winner — picking one is how a stale declaration reaches a green run. Its paired
# negative is leg A' above, which asserts an agreeing pair is NOT reported as a disagreement.
printf '\nC. disagreeing declarations are refused\n'

C="$(make_tree C "$(join_kit "${declared[@]}" ".fictional")" "" "")"
for r in "${declared[@]}"; do seed_skill "$C" "$r" demo-skill; done

run_driver "$C" --check
if [ "$RUN_RC" -eq 0 ]; then
  bad "a declaration naming a root the contract does not declare was ACCEPTED"
  printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
else
  ok "a declaration the contract does not corroborate is refused (exit $RUN_RC)"
fi
expect_out "the refusal names both sets" "disagree about the runtime surface"

# ---------------------------------------------------------------------------------------------
# D. FAIL CLOSED — an UNREADABLE declaration is not an empty one.
# ---------------------------------------------------------------------------------------------
# An unrestored receiver project evaluates every kit property to "" and exits 0. Read as a
# declaration that says "nothing is retired", it restores the full write set and re-creates exactly
# the mirrors this item exists to keep swept — a fail-open reached by doing nothing wrong. Both legs'
# paired negative is leg A, whose identical tree with a readable declaration exits 0.
printf '\nD. an unevaluated declaration is refused, never read as "nothing retired"\n'

# SEEDED IN EVERY DECLARED ROOT, deliberately: a driver that ignores the declaration entirely finds
# this tree COHERENT and exits 0, so a non-zero exit here can only come from the declaration guard.
# Measured: with only the survivors seeded, both D legs "passed" against the pre-#767 driver — which
# exited 1 for ordinary drift under the un-retired root, an unrelated cause wearing the right code.
D1="$(make_tree D1 "$(join_kit "${survivors[@]}")" "$(kitspell "$victim")" "")"
for r in "${declared[@]}"; do seed_skill "$D1" "$r" demo-skill; done
rm -f "$D1/.config/kit/FS.GG.Kit.receiver.proj"

run_driver "$D1" --check
if [ "$RUN_RC" -eq 0 ]; then
  bad "a MISSING receiver project was accepted"
  printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
else
  ok "a missing receiver project is refused (exit $RUN_RC)"
fi
expect_out "the refusal names the declaration it could not read" ".config/kit/FS.GG.Kit.receiver.proj"

D2="$(make_tree D2 "$(join_kit "${survivors[@]}")" "$(kitspell "$victim")" "")"
for r in "${declared[@]}"; do seed_skill "$D2" "$r" demo-skill; done
# The unrestored shape: a receiver project that imports no kit props, so every property evaluates to
# the empty string and MSBuild exits 0.
cat >"$D2/.config/kit/FS.GG.Kit.receiver.proj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultItems>false</EnableDefaultItems>
  </PropertyGroup>
</Project>
EOF

run_driver "$D2" --check
if [ "$RUN_RC" -eq 0 ]; then
  bad "an EMPTY evaluation was read as 'no roots are retired'"
  printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
else
  ok "an empty evaluation is refused rather than read as a declaration (exit $RUN_RC)"
fi
expect_out "the refusal says the evaluation, not the retirement, was empty" "evaluated EMPTY"

# ---------------------------------------------------------------------------------------------
# E. THE PRODUCER MANIFEST'S HOME, AND ITS ABSENCE — FS.GG.SDD#771 AC3, in BOTH directions.
# ---------------------------------------------------------------------------------------------
# #771 moved `skill-manifest.json` out of the provider-source root (`.agents/skills`, which ADR-0067
# §6 makes an untracked generated VIEW) and into the tracked SOURCE root. The criterion attached to
# that move is not "the new path works": it is that the OLD failure mode survives the move. With the
# manifest absent the driver must still DIE LOUDLY — `producer manifest missing: <path>` — and never
# fall back to an empty declaration, because an empty declaration content-addresses nothing and every
# subsequent `[drifted]` assertion in this driver would then pass over an unverified tree. A
# relocation that quietly turns a hard failure into an empty set is worse than no relocation, and
# invisible.
#
# ALL THREE LEGS ARE PAIRED, and E3 is the one that could not pass before #771:
#   E1 present at the SOURCE root  -> clean, exit 0.
#   E2 absent everywhere           -> non-zero, and the message NAMES the source-root path.
#   E3 present ONLY at the OLD path (the provider-source root) -> still non-zero. Against the
#      pre-#771 driver E3 exits 0, because that IS where it used to look. It is the leg that makes
#      E1 a statement about WHICH path rather than about any path at all.
printf '\nE. the producer manifest is read from the source root "%s", and its absence is LOUD\n' "$source_root"

E1="$(make_tree E1 "$(join_kit "${declared[@]}")" "" "")"
for r in "${declared[@]}"; do seed_skill "$E1" "$r" demo-skill; done

run_driver "$E1" --check
expect_rc "the manifest at $manifest_rel is READ (tree clean)" 0
refute_out "no missing-manifest failure on the shipped layout" "producer manifest missing"

E2="$(make_tree E2 "$(join_kit "${declared[@]}")" "" "")"
for r in "${declared[@]}"; do seed_skill "$E2" "$r" demo-skill; done
rm -f "$E2/$manifest_rel"

run_driver "$E2" --check
if [ "$RUN_RC" -eq 0 ]; then
  bad "a MISSING producer manifest was accepted — the driver defaulted to an empty declaration"
  printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
else
  ok "a missing producer manifest is refused (exit $RUN_RC)"
fi
expect_out "the refusal is the LOUD one, by its own words" "producer manifest missing"
expect_out "and it names the SOURCE-root path it looked for" "$manifest_rel"

# The same mutation in write mode: the loud failure must not be a `--check`-only courtesy. Write mode
# is the command this driver's own header documents FIRST, so a silent empty declaration there would
# fan out an unverified union into every root.
run_driver "$E2"
if [ "$RUN_RC" -eq 0 ]; then
  bad "WRITE mode accepted a missing producer manifest"
  printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
else
  ok "write mode refuses a missing producer manifest too (exit $RUN_RC)"
fi
expect_out "write mode's refusal is the same loud one" "producer manifest missing"

if [ "$provider_root" != "$source_root" ]; then
  E3="$(make_tree E3 "$(join_kit "${declared[@]}")" "" "")"
  for r in "${declared[@]}"; do seed_skill "$E3" "$r" demo-skill; done
  mv "$E3/$manifest_rel" "$E3/$provider_root/skills/skill-manifest.json"

  run_driver "$E3" --check
  if [ "$RUN_RC" -eq 0 ]; then
    bad "the manifest at the PRE-#771 path ($provider_root/skills/) satisfied the driver — the relocation is not real"
    printf '%s\n' "$RUN_OUT" | sed 's/^/       | /'
  else
    ok "the manifest at the pre-#771 path does NOT satisfy the driver (exit $RUN_RC)"
  fi
  expect_out "and the driver says which path it wanted" "$manifest_rel"
else
  printf '  (E3 skipped — the source root and the provider-source root are the same root)\n'
fi

# ---------------------------------------------------------------------------------------------
# F. THE WIRING — this repository's real, pinned declaration actually retires `.codex/skills`.
# ---------------------------------------------------------------------------------------------
# Legs A-D prove what the driver does with a declaration; this one proves the declaration it reads in
# THIS repo says what #767 measured. It is the only leg that touches the real FS.GG.Kit pin, and it
# goes red if a future kit changes the disposition of `.codex/skills` — which is the correct signal,
# because the driver's premise would have changed.
#
# AN UNEVALUATED DECLARATION FAILS THIS LEG; IT DOES NOT SATISFY IT. Measured in CI on the first run
# of this test: the evaluation came back with every property empty, and the SECOND assertion below
# passed on that emptiness — `.codex/skills` really is absent from `""`. That is the same fail-open
# the driver refuses two files over, reproduced inside its own test: a check whose subject failed to
# load, reporting the answer it wanted. So the evaluation is now validated FIRST, loudly, with the
# MSBuild output attached, and both assertions are skipped rather than answered when it is unusable.
printf '\nF. the pinned FS.GG.Kit declaration in this repo\n'

eval_kit() { (cd "$repo" && dotnet build .config/kit/FS.GG.Kit.receiver.proj --no-restore \
  -getProperty:FsggKitSkillRoots -getProperty:FsggKitRetiredSkillRoots -getProperty:FsggKitViewSkillRoots 2>&1); }

props="$(eval_kit)"
if ! printf '%s' "$props" | grep -q '"FsggKitSkillRoots": ".'; then
  restore_out="$(cd "$repo" && dotnet restore .config/kit/FS.GG.Kit.receiver.proj 2>&1)"
  restore_rc=$?
  props="$(eval_kit)"
fi

kit_retired="$(printf '%s' "$props" | sed -n 's/.*"FsggKitRetiredSkillRoots": "\(.*\)".*/\1/p')"
kit_runtime="$(printf '%s' "$props" | sed -n 's/.*"FsggKitSkillRoots": "\(.*\)".*/\1/p')"
kit_views="$(printf '%s' "$props" | sed -n 's/.*"FsggKitViewSkillRoots": "\(.*\)".*/\1/p')"

if [ -z "$kit_runtime" ]; then
  bad "the pinned kit declaration did not EVALUATE (FsggKitSkillRoots is empty) — this leg has no subject, it is not passing"
  printf '     restore (rc=%s):\n' "${restore_rc:-not-attempted}"
  printf '%s\n' "${restore_out:-<not attempted: the first evaluation returned a non-empty document>}" | sed 's/^/       | /'
  printf '     evaluation:\n'
  printf '%s\n' "$props" | sed 's/^/       | /'
  printf '     Repair: dotnet restore .config/kit/FS.GG.Kit.receiver.proj\n'
else
  if printf '%s' "$kit_retired" | tr ';' '\n' | grep -qx '\.codex/skills'; then
    ok ".codex/skills is declared RETIRED by the pinned kit (FsggKitRetiredSkillRoots=$kit_retired)"
  else
    bad ".codex/skills is not in FsggKitRetiredSkillRoots ('$kit_retired') — #767's premise no longer holds"
  fi

  if printf '%s;%s' "$kit_runtime" "$kit_views" | tr ';' '\n' | grep -qx '\.codex/skills'; then
    bad ".codex/skills is ALSO declared as a runtime root ('$kit_runtime' / '$kit_views')"
  else
    ok ".codex/skills is in no runtime root set (materialize='$kit_runtime' view='$kit_views')"
  fi
fi

if [ "$fail" -ne 0 ]; then
  echo "materialize-skill-roots.test.sh: FAILURES" >&2
  exit 1
fi
echo "materialize-skill-roots.test.sh: all passed"
