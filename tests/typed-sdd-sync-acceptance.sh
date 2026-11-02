#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
: "${QUINT_BIN:?exact Quint binary is required}"
: "${LMT_BIN:?exact lmt binary is required}"
: "${SYNC_JAVA_HOME:?exact Temurin JRE root is required}"
: "${SYNC_APALACHE_DIST:?exact extracted Apalache parent is required}"
: "${SYNC_CLI_DLL:=$repo_root/src/FS.GG.SDD.Cli/bin/Debug/net10.0/FS.GG.SDD.Cli.dll}"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/typed-sdd-sync-acceptance.XXXXXX")"
mkdir -p "$scratch/tmp"
export TMPDIR="$scratch/tmp"
fail() { printf 'typed-sdd-sync acceptance: %s; retained attempt: %s\n' "$*" "$scratch" >&2; exit 1; }
cli() { dotnet "$SYNC_CLI_DLL" "$@"; }
assert_json() {
  python3 - "$1" "$2" "$3" <<'PY'
import json, sys
with open(sys.argv[1], encoding="utf-8-sig") as stream:
    report = json.load(stream)
try:
    expected = json.loads(sys.argv[3])
except json.JSONDecodeError:
    expected = sys.argv[3]
if report.get(sys.argv[2]) != expected:
    raise SystemExit(f"{sys.argv[1]}: {sys.argv[2]}={report.get(sys.argv[2])!r}, expected {expected!r}")
PY
}

printf '%s  %s\n' '939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f' "$QUINT_BIN" | sha256sum -c --strict >/dev/null || fail 'Quint object mismatched'
printf '%s  %s\n' '37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10' "$LMT_BIN" | sha256sum -c --strict >/dev/null || fail 'lmt object mismatched'
mkdir -p "$scratch/work"
cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$repo_root/tests/fixtures/typed-sdd-sync/bindings.json" "$scratch/work/"
cli typed-sdd provision --cache "$scratch/cache" --quint "$QUINT_BIN" --lmt "$LMT_BIN" >"$scratch/provision.json" || fail 'provision failed'
assert_json "$scratch/provision.json" outcome succeeded

author=(typed-sdd author --root "$scratch/work" --work sync --title 'Sync acceptance'
  --agent acceptance --backend quint-specification-v1 --profile fsgg-quint-profile/2
  --cache "$scratch/cache" --source specification.md --bindings bindings.json
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST")
cli "${author[@]}" --session first >"$scratch/author.json" || fail 'accepted author did not synchronize'
assert_json "$scratch/author.json" outcome succeeded
receipt="$scratch/work/readiness/sync/quint/sync-report.json"
link="$scratch/work/readiness/sync/quint/sync-authority.json"
manifest="$scratch/work/readiness/sync/typed-authority.json"
[[ -f "$receipt" ]] || fail 'accepted author omitted the fresh sync receipt'
assert_json "$receipt" outcome satisfied
assert_json "$link" revision 0
assert_json "$receipt" acceptedRevision 0

# A damaged accepted revision link cannot be used as the next transaction base.
cp "$link" "$scratch/accepted-link.json"
python3 - "$link" <<'PY'
from pathlib import Path
import json, sys
path = Path(sys.argv[1])
value = json.loads(path.read_text())
value['workspaceFingerprint'] = '0' * 64
path.write_text(json.dumps(value))
PY
if cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/damaged-link.json"; then
  fail 'damaged accepted revision link passed'
fi
assert_json "$scratch/damaged-link.json" outcome blocked
cp "$scratch/accepted-link.json" "$link"
cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/repaired-link.json" || fail 'revision link repair did not restore fresh success'
assert_json "$scratch/repaired-link.json" acceptedRevision 0

# Move accepted bytes while a bounded model run is in flight. The temp model
# directory appears only after initial authority closure validation, so this
# exercises the final exact-input recheck rather than an initial stale read.
cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/interrupted-sync.json" &
sync_pid=$!
attempt_seen=0
for _ in $(seq 1 200); do
  if find "$scratch/tmp" -maxdepth 1 -type d -name 'fsgg-typed-sync-*' -print -quit | grep -q .; then
    attempt_seen=1
    break
  fi
  sleep 0.05
done
[[ "$attempt_seen" == 1 ]] || fail 'sync did not reach a bounded model attempt'
printf '\n<!-- changed while checking -->\n' >>"$scratch/work/specification.md"
if wait "$sync_pid"; then fail 'in-flight accepted source change passed'; fi
assert_json "$scratch/interrupted-sync.json" outcome blocked
grep -F 'changed during sync' "$scratch/interrupted-sync.json" >/dev/null || fail 'in-flight refusal lost exact-input finding'
cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"
cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/after-interruption.json" || fail 'sync did not recover after stale input was restored'
assert_json "$scratch/after-interruption.json" outcome satisfied

if cli verify --root "$scratch/work" --work sync >"$scratch/verify-without-tools.json" 2>"$scratch/verify-without-tools.err"; then
  fail 'verify passed without a fresh execution closure'
fi
grep -F 'typedSdd.syncBlocked' "$scratch/verify-without-tools.err" >/dev/null || fail 'verify did not name the sync refusal'

# A caller can relabel an old JSON file, but verify reruns the accepted closure.
printf '{"outcome":"satisfied"}\n' >"$receipt"
printf '\n<!-- stale accepted source -->\n' >>"$scratch/work/specification.md"
if cli verify --root "$scratch/work" --work sync --sync-cache "$scratch/cache" \
  --sync-java-home "$SYNC_JAVA_HOME" --sync-apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/forged-verify.json" 2>"$scratch/forged-verify.err"; then
  fail 'forged receipt and stale accepted source passed verify'
fi
grep -F 'typedSdd.syncBlocked' "$scratch/forged-verify.err" >/dev/null || fail 'forged receipt did not reach fresh sync gate'
assert_json "$receipt" outcome blocked
cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"

if cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$scratch/unavailable" >"$scratch/no-verifier.json"; then
  fail 'missing verifier passed'
fi
assert_json "$scratch/no-verifier.json" outcome blocked
cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/after-verifier-repair.json" || fail 'verifier repair did not restore fresh success'
assert_json "$receipt" outcome satisfied

# Keep the source valid Quint but make one declared invariant false. Authoring
# checks the proposed model before commit and must preserve accepted authority.
before_invalid="$(sha256sum "$manifest" | cut -d' ' -f1)"
sed -i 's/val acceptedAuthorityIsCoherent = acceptedCoherent/val acceptedAuthorityIsCoherent = false/' "$scratch/work/specification.md"
if cli "${author[@]}" --accept --session broken-invariant >"$scratch/broken-invariant.json"; then
  fail 'type-valid broken invariant passed the accepted author path'
fi
assert_json "$scratch/broken-invariant.json" outcome blocked
assert_json "$receipt" outcome satisfied
[[ "$(sha256sum "$manifest" | cut -d' ' -f1)" == "$before_invalid" ]] || fail 'invalid proposed invariant replaced accepted authority'
assert_json "$link" revision 0

cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"
cli "${author[@]}" --accept --session repaired >"$scratch/repaired.json" || fail 'repair did not synchronize'
assert_json "$scratch/repaired.json" outcome succeeded
assert_json "$receipt" outcome satisfied
assert_json "$link" revision 1
assert_json "$receipt" acceptedRevision 1

# A declared new domain property that the unchanged reducer already satisfies
# must pass; the accepted source and binding range both advance together.
python3 - "$scratch/work/specification.md" "$scratch/work/bindings.json" <<'PY'
from pathlib import Path
import json, sys
source_path, binding_path = map(Path, sys.argv[1:])
source = source_path.read_text()
anchor = '    { id: "SYNC-MAX-SAMPLES", kind: "bound"'
row = '    { id: "INVAR-nonnegativeAcceptedRevision", kind: "verification", references: [], assumptions: [], evidenceObligationIds: [], fromId: "", toId: "", verificationKind: "invariant", subjectIds: Set("PROD-001"), boundIds: Set("SYNC-MAX-STEPS"), minimum: 0, maximum: 0 },\n'
if source.count(anchor) != 1:
    raise SystemExit('bound row anchor is not unique')
source = source.replace(anchor, row + anchor)
anchor = '  val acceptedRevisionIsMonotonic = acceptedRevision >= 0\n'
if source.count(anchor) != 1:
    raise SystemExit('invariant declaration anchor is not unique')
source = source.replace(anchor, anchor + '  val nonnegativeAcceptedRevision = acceptedRevision >= 0\n')
source_path.write_text(source)
bindings = json.loads(binding_path.read_text())
bindings['exports'][0]['source']['end']['line'] += 1
binding_path.write_text(json.dumps(bindings, separators=(',', ':')))
PY
cli "${author[@]}" --accept --session already-satisfied >"$scratch/already-satisfied.json" || fail 'new already-satisfied property was refused'
assert_json "$scratch/already-satisfied.json" outcome succeeded
assert_json "$receipt" outcome satisfied
assert_json "$link" revision 2

cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"
cp "$repo_root/tests/fixtures/typed-sdd-sync/bindings.json" "$scratch/work/bindings.json"
cli "${author[@]}" --accept --session restored >"$scratch/restored.json" || fail 'baseline restoration did not synchronize'
assert_json "$link" revision 3

# A syntactically valid scenario with a disabled acceptance transition is
# vacuous/unreachable; a clean Quint typecheck cannot make the author pass.
python3 - "$scratch/work/specification.md" <<'PY'
from pathlib import Path
import sys
path = Path(sys.argv[1])
source = path.read_text()
before = '.then(classifyAs("CoherentDelta"))'
if source.count(before) != 1:
    raise SystemExit('coherent scenario anchor is not unique')
path.write_text(source.replace(before, '.then(classifyAs("Ambiguous"))'))
PY
if cli "${author[@]}" --accept --session unreachable >"$scratch/unreachable.json"; then
  fail 'unreachable declared scenario passed'
fi
assert_json "$scratch/unreachable.json" outcome blocked
assert_json "$link" revision 3

cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"
cli "${author[@]}" --accept --session restored-after-unreachable >"$scratch/restored-again.json" || fail 'unreachable-scenario repair did not synchronize'
assert_json "$link" revision 4

# Explicitly conflicting accepted modules must fail the authority join, even
# though the model syntax and types remain valid.
python3 - "$scratch/work/specification.md" <<'PY'
from pathlib import Path
import sys
path = Path(sys.argv[1])
source = path.read_text()
before = 'id: "EVID-001", kind: "evidence-requirement", references: [], assumptions: ["mode=safe"]'
after = before.replace('mode=safe', 'mode=unsafe')
if source.count(before) != 1:
    raise SystemExit('expected evidence module declaration was not unique')
path.write_text(source.replace(before, after))
PY
if cli "${author[@]}" --accept --session conflicting >"$scratch/conflicting.json"; then
  fail 'conflicting cross-module assumptions passed'
fi
assert_json "$scratch/conflicting.json" outcome blocked
assert_json "$link" revision 4

# Removing the link after four accepted edits must not reset the next revision.
cp "$repo_root/tests/fixtures/typed-sdd-sync/specification.md" "$scratch/work/specification.md"
cp "$link" "$scratch/revision-four-link.json"
rm "$link"
if cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/missing-link.json"; then
  fail 'missing accepted revision link reset the accepted revision'
fi
assert_json "$scratch/missing-link.json" outcome blocked
grep -F 'no sync revision link' "$scratch/missing-link.json" >/dev/null || fail 'missing-link refusal had another cause'
cp "$scratch/revision-four-link.json" "$link"
cli typed-sdd sync --root "$scratch/work" --work sync --cache "$scratch/cache" \
  --java-home "$SYNC_JAVA_HOME" --apalache-dist "$SYNC_APALACHE_DIST" \
  >"$scratch/recovered-revision-four.json" || fail 'revision-four link repair did not recover'
assert_json "$scratch/recovered-revision-four.json" acceptedRevision 4

printf 'typed-sdd-sync acceptance: positive, invariant break, unreachable scenario, repair, already-satisfied property, stale in-flight source, forged receipt, missing verifier and conflicting assumptions passed\n'
rm -rf -- "$scratch"
