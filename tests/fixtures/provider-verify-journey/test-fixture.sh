#!/bin/sh
# Test-handoff fixture for the CLI-to-Verify journey (scripts/verify-capture-journey.py).
# `--version` answers the independently selected policy probe. `run` checks its literal argv,
# then acts by FSGG_FIXTURE_MODE:
#   write    write the declared handoff citing the current bytes of its input, exit 0
#   none     write nothing, exit 0
#   mismatch write a handoff citing wrong bytes, exit 0
#   exit2    write the handoff, exit 2
#   escape   write the handoff, first leaving a detached (own session) descendant that ignores TERM
#   hang     leave a detached descendant, then block far past any declared timeout
# The build-test-package provider profile also declares `build` and `package` commands:
#   build    writes out/build/product.out (ASCII uppercase of product.txt) and a handoff citing it
#   package  packs out/build/product.out into out/package/product.pack (a deterministic tar whose only member is
#            product.out) and a handoff citing the pack
# `package` needs out/build/product.out, so it relies on Governance running gates in id order (build, package, test as in 1.15)
# and exits 6 if the build output is absent.
# FSGG_FIXTURE_BUILD_MODE=wrong-output builds something other than the uppercase of product.txt, and
# FSGG_FIXTURE_PACKAGE_MODE=wrong-content packs different bytes under the member name product.out. Both write a
# handoff that is internally CONSISTENT with what they wrote, so Governance accepts them; only a harness that
# checks the actual build output and packed-artifact identity can tell.
# FSGG_FIXTURE_WITNESS, when set, receives '<outer-pid> <start-time>' for this process and each
# descendant; the inherited /proc shows outer pids, so the maintained runner can check them after
# the worker ended. The shell builtin `read` opens /proc/self/stat in this very process.
witness() {
  [ -n "${FSGG_FIXTURE_WITNESS:-}" ] || return 0
  read -r pid rest < /proc/self/stat
  rest=${rest##*) }
  set -- $rest
  shift 19
  printf '%s %s\n' "$pid" "$1" >> "$FSGG_FIXTURE_WITNESS"
}
if [ "$#" -eq 1 ] && [ "$1" = "--version" ]; then printf 'verify-journey-fixture 1.0.0\n'; exit 0; fi
handoff() { # node-id output source-path...
  node="$1"; out="$2"; shift 2
  mkdir -p "$(dirname "$out")"
  sources=""
  for src in "$@"; do
    d=$(sha256sum "$src" | cut -d' ' -f1)
    sources="${sources:+$sources,}{\"path\":\"$src\",\"digest\":\"sha256:$d\"}"
  done
  printf '{"contractVersion":"2.0.0","schemaVersion":1,"evidence":{"nodes":[{"id":"%s","state":"real"}],"dependencies":[]},"governedReferences":[],"sources":[%s]}' "$node" "$sources" > "$out"
}
if [ "$1" = build ] || [ "$1" = package ]; then
  [ "$#" -eq 10 ] && [ "$2" = --input ] && [ "$4" = --output ] || exit 4
  [ "$7" != "" ] && [ "$8" = "" ] && [ "$9" = "with spaces" ] && [ "${10}" = '$(literal);&' ] || exit 5
  witness
  if [ "$1" = build ]; then
    [ "$6" = --build-output ] || exit 4
    mkdir -p "$(dirname "$7")"
    if [ "${FSGG_FIXTURE_BUILD_MODE:-}" = wrong-output ]; then printf 'not the uppercase product\n' > "$7"
    else tr 'a-z' 'A-Z' < product.txt > "$7"; fi
    handoff build:build "$5" "$3" product.txt "$7"
  else
    [ "$6" = --package ] || exit 4
    [ -f "$3" ] || exit 6
    mkdir -p "$(dirname "$7")" out/package/stage
    if [ "${FSGG_FIXTURE_PACKAGE_MODE:-}" = wrong-content ]; then printf 'tampered package content\n' > out/package/stage/product.out
    else cp "$3" out/package/stage/product.out; fi
    tar --sort=name --mtime=@0 --owner=0 --group=0 --numeric-owner -cf "$7" -C out/package/stage product.out
    handoff package:package "$5" "$3" "$7"
  fi
  exit 0
fi
[ "$1" = run ] || exit 3
[ "$#" -eq 8 ] && [ "$2" = --input ] && [ "$4" = --output ] || exit 4
[ "$6" = "" ] && [ "$7" = "with spaces" ] && [ "$8" = '$(literal);&' ] || exit 5
mode="${FSGG_FIXTURE_MODE:-write}"
witness
case "$mode" in
  escape|hang)
    setsid sh -c 'trap "" TERM HUP; read -r pid rest < /proc/self/stat; rest=${rest##*) }; set -- $rest; shift 19; [ -z "${FSGG_FIXTURE_WITNESS:-}" ] || printf "%s %s\n" "$pid" "$1" >> "$FSGG_FIXTURE_WITNESS"; exec sleep 300' > /dev/null 2>&1 < /dev/null &
    # Give the descendant time to record itself before this process continues or ends.
    sleep 1
    ;;
esac
[ "$mode" = hang ] && exec sleep 300
[ "$mode" = none ] && exit 0
digest=$(sha256sum "$3" | cut -d' ' -f1)
[ "$mode" = mismatch ] && digest=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
mkdir -p "$(dirname "$5")"
printf '{"contractVersion":"2.0.0","schemaVersion":1,"evidence":{"nodes":[{"id":"test:test","state":"real"}],"dependencies":[]},"governedReferences":[],"sources":[{"path":"%s","digest":"sha256:%s"}]}' "$3" "$digest" > "$5"
[ "$mode" = exit2 ] && exit 2
exit 0
