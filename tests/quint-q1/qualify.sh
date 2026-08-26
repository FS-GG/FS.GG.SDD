#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
slice_root="$repo_root/docs/experiments/quint-q1/slices"
expected_quint_sha="939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f"
expected_lmt_sha="37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
expected_rust_sha="b2efdeac5713d153e41bf2143b94ed75d888fdd5637f4a5d61a04c695313510a"
expected_java_sha="e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786"
expected_apalache_jar_sha="4753c0ebb2cbb266e2c6ac19ab5ca3827d726cc80fd1fc5d7c1eeb64736cd60b"

quint_bin="${QUINT_BIN:?set QUINT_BIN to the pinned Quint v0.32.0 binary}"
lmt_bin="${LMT_BIN:?set LMT_BIN to the pinned lmt binary}"
rust_bin="${QUINT_RUST_EVALUATOR_BIN:-$HOME/.quint/rust-evaluator-v0.6.0/quint_evaluator}"
java_bin="${JAVA_BIN:?set JAVA_BIN to the pinned Temurin 21.0.9+10 java binary}"
apalache_jar="${APALACHE_JAR:?set APALACHE_JAR to the pinned Apalache 0.56.1 jar}"

fail() {
  printf 'Q1-REFUSAL: %s\n' "$*" >&2
  return 1
}

check_sha() {
  local expected="$1"
  local path="$2"
  local label="$3"
  [[ -f "$path" ]] || fail "$label is missing: $path"
  local observed
  observed="$(sha256sum "$path" | cut -d' ' -f1)"
  [[ "$observed" == "$expected" ]] || fail "$label digest mismatch: expected $expected, observed $observed"
}

check_tools() {
  check_sha "$expected_quint_sha" "$quint_bin" "Quint"
  check_sha "$expected_lmt_sha" "$lmt_bin" "lmt"
  check_sha "$expected_rust_sha" "$rust_bin" "Quint Rust evaluator"
  check_sha "$expected_java_sha" "$java_bin" "Java runtime"
  check_sha "$expected_apalache_jar_sha" "$apalache_jar" "Apalache"
  [[ "$($quint_bin --version)" == "0.32.0" ]] || fail "Quint version is not 0.32.0"
  "$java_bin" -version 2>&1 | grep -F '21.0.9' >/dev/null || fail "Java version is not 21.0.9"
}

check_sources() {
  local root="$1"
  (cd "$root" && sha256sum -c "$repo_root/tests/quint-q1/sources.sha256") >/dev/null \
    || fail "canonical source digest mismatch"
}

check_fences() {
  local root="$1"
  local observed
  observed="$(
    for path in "$root"/*.md; do
      local_name="$(basename "$path")"
      sed -n 's/^```quint \([^[:space:]]\+\.qnt +=\)$/\1/p' "$path" \
        | while IFS= read -r header; do printf '%s|%s\n' "$local_name" "$header"; done
    done
  )"
  local expected
  expected="$(cat "$repo_root/tests/quint-q1/fence-manifest.txt")"
  [[ "$observed" == "$expected" ]] || fail "fence inventory/order mismatch"
}

run_quint_clean() {
  local output="$1"
  shift
  if ! "$quint_bin" "$@" >"$output" 2>&1; then
    cat "$output" >&2
    return 1
  fi
  if grep -Eq 'Error \[|(^|[[:space:]])error:' "$output"; then
    cat "$output" >&2
    fail "Quint reported an error with a zero process exit"
  fi
}

extract() {
  local destination="$1"
  mkdir -p "$destination"
  cp "$slice_root"/*.md "$destination"/
  check_fences "$destination"
  local output="$destination/lmt.log"
  (cd "$destination" && "$lmt_bin" \
    requirements-and-evidence.md sir-damage-rule.md coordination-process.md) >"$output" 2>&1
  [[ ! -s "$output" ]] || { cat "$output" >&2; fail "lmt emitted warnings or diagnostics"; }
}

expect_refusal() {
  local label="$1"
  shift
  if "$@" >/dev/null 2>&1; then
    fail "$label mutation unexpectedly passed"
  fi
  printf 'Q1-MUTATION-PASS: %s\n' "$label"
}

check_contract() {
  local path="$1"
  jq -e '
    .schema == "fsgg.quint.compiled-contract/q1" and
    .profile == "fsgg-quint-profile/1" and
    ((keys | sort) == ["actions","bounds","digests","evidence","invariants","profile","relations","requirements","schema","sources","specification","verificationProfiles"]) and
    ([paths | .[-1] | strings | ascii_downcase | select(. == "expression" or . == "ast" or . == "compilernodeid")] | length == 0) and
    (.sources | length > 0) and (.verificationProfiles | length > 0)
  ' "$path" >/dev/null || fail "compiled contract is incomplete or carries forbidden semantic fields"
}

run_root="$(mktemp -d /tmp/fsgg-quint-q1.XXXXXX)"
trap 'rm -rf -- "$run_root"' EXIT

check_tools
check_sources "$repo_root"
check_fences "$slice_root"
jq empty "$repo_root/docs/experiments/quint-q1/compiled-contract.schema.json"
check_contract "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json"

extract "$run_root/a"
extract "$run_root/b"
for module in requirements.qnt sir-damage.qnt coordination.qnt; do
  cmp "$run_root/a/$module" "$run_root/b/$module" >/dev/null \
    || fail "non-deterministic extraction: $module"
  run_quint_clean "$run_root/$module.typecheck.log" typecheck "$run_root/a/$module"
done

run_quint_clean "$run_root/requirements.test.log" test "$run_root/a/requirements.qnt" --main RequirementsSliceTests
run_quint_clean "$run_root/sir.test.log" test "$run_root/a/sir-damage.qnt" --main SirDamageSliceTests
run_quint_clean "$run_root/coordination.test.log" test "$run_root/a/coordination.qnt" --main CoordinationSliceTests
run_quint_clean "$run_root/sir.witness.log" test "$run_root/a/sir-damage.qnt" \
  --main SirDamageSliceTests --match reviewedWitness --seed 92220 --backend rust \
  --out-itf "$run_root/sir_{test}_{seq}.itf.json"
jq 'del(."#meta".description, ."#meta".timestamp) | ."#meta".source = (."#meta".source | split("/") | last)' \
  "$run_root/sir_reviewedWitness_0.itf.json" >"$run_root/sir.normalized.itf.json"
cmp "$repo_root/tests/quint-q1/fixtures/sir-reviewed-witness.itf.json" \
  "$run_root/sir.normalized.itf.json" >/dev/null || fail "normalized reviewed S.I.R. witness drift"
jq empty "$repo_root/tests/quint-q1/fixtures/sir-reviewed-witness.json"

run_quint_clean "$run_root/requirements.run.log" run "$run_root/a/requirements.qnt" --main RequirementsSlice \
  --max-samples 200 --max-steps 8 --seed 92201 --invariants acceptedOnlyWithEvidence --backend rust --verbosity 1
run_quint_clean "$run_root/sir.run.log" run "$run_root/a/sir-damage.qnt" --main SirDamageSlice \
  --max-samples 200 --max-steps 8 --seed 92202 --invariants nonNegativeHitPoints knownLastAction --backend rust --verbosity 1
run_quint_clean "$run_root/coordination.run.log" run "$run_root/a/coordination.qnt" --main CoordinationSlice \
  --max-samples 1000 --max-steps 20 --seed 92203 \
  --invariants atMostOneApply receiptMatchesApply completeHasReceipt staleNeverApplies knownPhase \
  --backend rust --verbosity 1

# Apalache writes a diagnostic tree in its process working directory. Keep that disposable output inside
# the harness root so a qualification run never dirties the repository.
cd "$run_root"
export PATH="$(dirname "$java_bin"):$PATH"
run_quint_clean "$run_root/requirements.verify.log" verify "$run_root/a/requirements.qnt" \
  --main RequirementsSlice --invariants acceptedOnlyWithEvidence --max-steps 8 \
  --apalache-version 0.56.1 --verbosity 1
run_quint_clean "$run_root/sir.verify.log" verify "$run_root/a/sir-damage.qnt" \
  --main SirDamageSlice --invariants nonNegativeHitPoints knownLastAction --max-steps 8 \
  --apalache-version 0.56.1 --verbosity 1
run_quint_clean "$run_root/coordination.verify.log" verify "$run_root/a/coordination.qnt" \
  --main CoordinationSlice \
  --invariants atMostOneApply receiptMatchesApply completeHasReceipt staleNeverApplies knownPhase \
  --max-steps 10 --apalache-version 0.56.1 --verbosity 1
run_quint_clean "$run_root/coordination.temporal.log" verify "$run_root/a/coordination.qnt" \
  --main CoordinationSlice --temporal eventualCompletion --backend tlc --verbosity 1

mkdir -p "$run_root/missing" "$run_root/reordered" "$run_root/duplicate" "$run_root/stale"
cp "$slice_root"/*.md "$run_root/missing"/
sed -i '0,/^```quint requirements.qnt +=$/d' "$run_root/missing/requirements-and-evidence.md"
expect_refusal missing-fence check_fences "$run_root/missing"

cp "$slice_root"/*.md "$run_root/reordered"/
sed -i '0,/^```quint coordination.qnt +=$/s//```quint coordination-other.qnt +=/' "$run_root/reordered/coordination-process.md"
expect_refusal reordered-or-unexpected-target check_fences "$run_root/reordered"

cp "$slice_root"/*.md "$run_root/duplicate"/
printf '\n```quint coordination.qnt +=\nmodule Duplicate {}\n```\n' >>"$run_root/duplicate/coordination-process.md"
expect_refusal duplicate-fence check_fences "$run_root/duplicate"

mkdir -p "$run_root/stale/docs/experiments/quint-q1/slices"
cp "$slice_root"/*.md "$run_root/stale/docs/experiments/quint-q1/slices"/
sed -i 's/REQ-AUDIT-001/REQ-AUDIT-EDITED/' "$run_root/stale/docs/experiments/quint-q1/slices/requirements-and-evidence.md"
expect_refusal stale-source check_sources "$run_root/stale"

cp "$run_root/a/coordination.qnt" "$run_root/edited-output.qnt"
printf '\n// independent edit\n' >>"$run_root/edited-output.qnt"
expect_refusal hand-edited-generated-output cmp "$run_root/a/coordination.qnt" "$run_root/edited-output.qnt"

cp "$run_root/a/requirements.qnt" "$run_root/requirements-mutant.qnt"
sed -i '/observedEvidence.contains(auditRequirement.evidenceId),/d' "$run_root/requirements-mutant.qnt"
expect_refusal missing-evidence-guard "$quint_bin" run "$run_root/requirements-mutant.qnt" \
  --main RequirementsSlice --max-samples 500 --max-steps 5 --seed 92211 \
  --invariants acceptedOnlyWithEvidence --backend rust --verbosity 1

cp "$run_root/a/sir-damage.qnt" "$run_root/sir-mutant.qnt"
sed -i 's/clampAtZero(hitPoints - amount)/hitPoints - amount/' "$run_root/sir-mutant.qnt"
expect_refusal combat-boundary-defect "$quint_bin" run "$run_root/sir-mutant.qnt" \
  --main SirDamageSlice --max-samples 500 --max-steps 5 --seed 92212 \
  --invariants nonNegativeHitPoints --backend rust --verbosity 1

cp "$run_root/a/coordination.qnt" "$run_root/coordination-mutant.qnt"
sed -i '/action retry = all {/,/^  }/ s/applyCount'"'"' = applyCount,/applyCount'"'"' = applyCount + 1,/' \
  "$run_root/coordination-mutant.qnt"
expect_refusal double-apply-on-retry "$quint_bin" run "$run_root/coordination-mutant.qnt" \
  --main CoordinationSlice --max-samples 2000 --max-steps 20 --seed 92213 \
  --invariants atMostOneApply receiptMatchesApply --backend rust --verbosity 1

cp "$run_root/a/coordination.qnt" "$run_root/liveness-mutant.qnt"
sed -i '/lossCount == 0,/d; s/lossCount'"'"' = lossCount + 1,/lossCount'"'"' = lossCount,/; /action retry = all {/,/^  }/ s/retryCount'"'"' = retryCount + 1,/retryCount'"'"' = retryCount,/' \
  "$run_root/liveness-mutant.qnt"
expect_refusal unbounded-lost-response-liveness "$quint_bin" verify "$run_root/liveness-mutant.qnt" \
  --main CoordinationSlice --temporal eventualCompletion --backend tlc --verbosity 1

jq '.expression = {"op":"hidden-authority"}' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-mutant.json"
expect_refusal arbitrary-expression-contract check_contract "$run_root/contract-mutant.json"

if [[ -n "${Q1_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q1_JUNIT_OUT")"
  junit_tmp="$Q1_JUNIT_OUT.tmp"
  {
    printf '%s\n' '<?xml version="1.0" encoding="UTF-8"?>'
    printf '%s\n' '<testsuite name="quint-q1-qualification" tests="24" failures="0" errors="0" skipped="0">'
    for case_name in \
      pinned-tools canonical-sources fence-inventory contract-shape deterministic-extraction \
      typecheck named-tests normalized-sir-witness requirements-simulation sir-simulation \
      coordination-simulation apalache-safety tlc-liveness complete-positive-bundle \
      mutation-missing-fence mutation-reordered-target mutation-duplicate-fence mutation-stale-source \
      mutation-edited-output mutation-missing-evidence mutation-combat-boundary mutation-double-apply \
      mutation-unbounded-liveness mutation-arbitrary-expression
    do
      printf '  <testcase classname="FS.GG.SDD.QuintQ1" name="%s"/>\n' "$case_name"
    done
    printf '%s\n' '</testsuite>'
  } >"$junit_tmp"
  mv "$junit_tmp" "$Q1_JUNIT_OUT"
fi

printf 'Q1-QUALIFIED: 3 slices, 14 positive commands, 10 independent mutations\n'
