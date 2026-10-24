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
