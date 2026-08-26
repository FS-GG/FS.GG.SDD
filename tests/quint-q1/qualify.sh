#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
slice_root="$repo_root/docs/experiments/quint-q1/slices"
expected_quint_sha="939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f"
expected_lmt_sha="37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10"
expected_rust_sha="b2efdeac5713d153e41bf2143b94ed75d888fdd5637f4a5d61a04c695313510a"
expected_java_sha="e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786"
expected_apalache_jar_sha="4753c0ebb2cbb266e2c6ac19ab5ca3827d726cc80fd1fc5d7c1eeb64736cd60b"
expected_apalache_tree_sha="3466d07f06d7ac80ee0f171a96383183cee9d91bf1b5995d897d4f15c004569f"
expected_node_sha="d51d79e0e04abfe366345496a8e1379d56493271af4e0d6f27dd6ba76be628ea"
expected_ajv_tree_sha="e14d4bfc96cce335d1d370f844294c8c6eeced38c61da0f5ae224e26f74d5007"
expected_guidance_tree_sha="68a11d403846de3af26759eef97f4a35eff5e71d561d41ea17d96e535c171556"
expected_guidance_revision="cc75369f741af7d490936f82002c2d28e3b3d78d"

quint_bin="${QUINT_BIN:?set QUINT_BIN to the pinned Quint v0.32.0 binary}"
lmt_bin="${LMT_BIN:?set LMT_BIN to the pinned lmt binary}"
rust_bin="${QUINT_RUST_EVALUATOR_BIN:-$HOME/.quint/rust-evaluator-v0.6.0/quint_evaluator}"
java_bin="${JAVA_BIN:?set JAVA_BIN to the pinned Temurin 21.0.9+10 java binary}"
apalache_dist="${APALACHE_DIST:?set APALACHE_DIST to the pinned Apalache 0.56.1 distribution}"
node_bin="${NODE_BIN:?set NODE_BIN to the pinned Node.js v26.7.0 binary}"
ajv_root="${AJV_ROOT:?set AJV_ROOT to the pinned Ajv 8.17.1 closure}"
guidance_root="${GUIDANCE_ROOT:?set GUIDANCE_ROOT to quint-llm-kit at the pinned commit}"

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

check_file_tree() {
  local expected="$1"
  local root="$2"
  local label="$3"
  local observed
  observed="$(cd "$root" && find . -type f -print0 | sort -z | xargs -0 sha256sum | sha256sum | cut -d' ' -f1)"
  [[ "$observed" == "$expected" ]] || fail "binding=$label tree digest mismatch: expected $expected, observed $observed"
}

check_guidance() {
  local root="$1"
  local revision="$2"
  [[ "$revision" == "$expected_guidance_revision" ]] \
    || { fail "binding=guidance-revision moving/latest substitution refused: $revision"; return 1; }
  [[ "$(git -C "$root" rev-parse HEAD)" == "$expected_guidance_revision" ]] \
    || { fail "binding=guidance-revision checkout does not match $expected_guidance_revision"; return 1; }
  local observed
  observed="$(cd "$root" && git ls-files -z | sort -z | xargs -0 sha256sum | sha256sum | cut -d' ' -f1)"
  [[ "$observed" == "$expected_guidance_tree_sha" ]] \
    || { fail "binding=guidance-tree expected $expected_guidance_tree_sha, observed $observed"; return 1; }
  check_sha ba2312c2da15be623f8fcce0d256ae4427905ff3592e6d74bac0caa3aff68532 \
    "$root/quint-llm-kit-plugin/skills/quint-lang/SKILL.md" "quint-lang guidance" || return 1
  check_sha fe595baac9353a4c8ca572ee91b0d516000a17a83697db6244597597ae69c591 \
    "$root/quint-llm-kit-plugin/skills/quint-modeling/SKILL.md" "quint-modeling guidance" || return 1
  check_sha 7bd1dbfddcded796c41bd0019b02f4b47a42e13ffd2d87010448ed86e0f38ab5 \
    "$root/quint-llm-kit-plugin/skills/quint-execute-spec/SKILL.md" "quint-execute-spec guidance" || return 1
}

check_tools() {
  check_sha "$expected_quint_sha" "$quint_bin" "Quint"
  check_sha "$expected_lmt_sha" "$lmt_bin" "lmt"
  check_sha "$expected_rust_sha" "$rust_bin" "Quint Rust evaluator"
  check_sha "$expected_java_sha" "$java_bin" "Java runtime"
  check_file_tree "$expected_apalache_tree_sha" "$apalache_dist" "apalache-distribution"
  check_sha "$expected_apalache_jar_sha" "$apalache_dist/apalache/lib/apalache.jar" "Apalache jar"
  check_sha "$expected_node_sha" "$node_bin" "Node.js"
  check_file_tree "$expected_ajv_tree_sha" "$ajv_root" "ajv-closure"
  [[ "$($quint_bin --version)" == "0.32.0" ]] || fail "Quint version is not 0.32.0"
  "$java_bin" -version 2>&1 | grep -F '21.0.9' >/dev/null || fail "Java version is not 21.0.9"
  [[ "$($node_bin --version)" == "v26.7.0" ]] || fail "Node.js version is not v26.7.0"
  check_guidance "$guidance_root" "$expected_guidance_revision"
}

check_sources() {
  local root="$1"
  (cd "$root" && sha256sum -c "$repo_root/tests/quint-q1/sources.sha256") \
    || fail "binding=canonical-source digest mismatch under $root"
}

check_fences() {
  local root="$1"
  local observed
  observed="$(
    for path in "$root"/*.md; do
      local_name="$(basename "$path")"
      awk -v name="$local_name" '/^```quint [^[:space:]]+\.qnt \+=$/ { header=$0; sub(/^```quint /, "", header); printf "%s:%d|%s\n", name, FNR, header }' "$path"
    done
  )"
  local expected
  expected="$(cat "$repo_root/tests/quint-q1/fence-manifest.txt")"
  if [[ "$observed" != "$expected" ]]; then
    diff -u --label expected:fence-manifest --label observed:source-locations \
      <(printf '%s\n' "$expected") <(printf '%s\n' "$observed") >&2 || true
    fail "binding=fence-inventory source-located fence inventory/order mismatch"
  fi
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
  local expected_diagnostic="$2"
  shift 2
  local diagnostic="$run_root/refusal-$label.log"
  if "$@" >"$diagnostic" 2>&1; then
    fail "$label mutation unexpectedly passed"
  fi
  grep -F "$expected_diagnostic" "$diagnostic" >/dev/null \
    || { cat "$diagnostic" >&2; fail "$label refused for the wrong reason; expected diagnostic '$expected_diagnostic'"; }
  printf 'Q1-MUTATION-PASS: %s\n' "$label"
}

quint_refusal_probe() {
  local binding="$1"
  local source="$2"
  local raw_diagnostic="$3"
  shift 3
  local probe_log="$run_root/probe-${binding//[^A-Za-z0-9]/-}.log"
  if "$@" >"$probe_log" 2>&1; then
    return 0
  fi
  cat "$probe_log" >&2
  grep -F "$raw_diagnostic" "$probe_log" >/dev/null || return 2
  printf 'binding=%s source=%s rawDiagnostic=%s\n' "$binding" "$source" "$raw_diagnostic" >&2
  return 1
}

check_contract() {
  local path="$1"
  local generated_module="$2"
  AJV_ROOT="$ajv_root" "$node_bin" "$repo_root/tests/quint-q1/validate-contract.mjs" \
    "$repo_root/docs/experiments/quint-q1/compiled-contract.schema.json" "$path" \
    || { fail "binding=compiled-contract draft-2020-12 validation failed: $path"; return 1; }
  jq -e '
    .schema == "fsgg.quint.compiled-contract/q1" and
    .profile == "fsgg-quint-profile/1" and
    ((keys | sort) == ["actions","bounds","digests","evidence","invariants","profile","relations","requirements","schema","sources","specification","verificationProfiles"]) and
    ([paths | .[-1] | strings | ascii_downcase | select(. == "expression" or . == "ast" or . == "compilernodeid")] | length == 0) and
    (.sources | length > 0) and (.verificationProfiles | length > 0) and
    ([.requirements, .invariants, .evidence, .verificationProfiles, [.actions[].id]] | all(. == (sort | unique))) and
    ([.sources[].id] == ([.sources[].id] | sort | unique)) and
    ([.relations[] | [.from, .to][]] - ([.requirements[], .invariants[], .evidence[], .actions[].id, .verificationProfiles[], .sources[].id] | unique) | length == 0) and
    ([.sources[] | select(.startLine > .endLine)] | length == 0) and
    ([.sources[].path | select(startswith("docs/") | not) // select(contains("..")) // select(contains("//")) // select(contains("\\"))] | length == 0)
  ' "$path" >/dev/null || { fail "binding=compiled-contract semantic uniqueness/order/path/reference validation failed: $path"; return 1; }
  local declared_source_digest declared_module_digest
  declared_source_digest="$(jq -r '.digests.canonicalSource' "$path")"
  declared_module_digest="$(jq -r '.digests.generatedModule' "$path")"
  while IFS=$'\t' read -r source_path end_line; do
    [[ -f "$repo_root/$source_path" ]] || { fail "binding=compiled-contract source path is missing: $source_path"; return 1; }
    [[ "$(wc -l <"$repo_root/$source_path")" -eq "$end_line" ]] \
      || { fail "binding=compiled-contract line range does not bind $source_path:$end_line"; return 1; }
    [[ "sha256:$(sha256sum "$repo_root/$source_path" | cut -d' ' -f1)" == "$declared_source_digest" ]] \
      || { fail "binding=compiled-contract canonicalSource digest mismatch for $source_path"; return 1; }
    [[ "sha256:$(sha256sum "$generated_module" | cut -d' ' -f1)" == "$declared_module_digest" ]] \
      || { fail "binding=compiled-contract generatedModule digest mismatch for $generated_module"; return 1; }
  done < <(jq -r '.sources[] | [.path, (.endLine|tostring)] | @tsv' "$path")
}

check_candidate_manifest() {
  local generated_root="$1"
  local manifest="$repo_root/docs/experiments/quint-q1/candidate-manifest.json"
  jq -e '
    .schema == "fsgg.quint.qualification-manifest/q1" and
    .status == "producer-candidate-qualified-cross-repo-acceptance-pending" and
    .qualification.positiveCommands == 23 and
    .qualification.independentMutations == 19 and
    .gitBinding.method == "successor-commit-receipt" and
    (.pending | index("EHotwagner/S.I.R.#353 exact interpreter replay") != null)
  ' "$manifest" >/dev/null || fail "binding=candidate-manifest status/count/git binding mismatch"

  while IFS=$'\t' read -r field path; do
    local expected observed
    expected="$(jq -r --arg field "$field" '.documents[$field]' "$manifest")"
    observed="$(sha256sum "$repo_root/$path" | cut -d' ' -f1)"
    [[ "$observed" == "$expected" ]] \
      || { fail "binding=candidate-manifest document $field mismatch at $path"; return 1; }
  done <<'EOF'
profileSha256	docs/experiments/quint-q1/profile.md
contractSchemaSha256	docs/experiments/quint-q1/compiled-contract.schema.json
contractExampleSha256	docs/experiments/quint-q1/compiled-contract.example.json
sirReplayEnvelopeSha256	tests/quint-q1/fixtures/sir-reviewed-witness.json
normalizedSirItfSha256	tests/quint-q1/fixtures/sir-reviewed-witness.itf.json
qualificationHarnessSha256	tests/quint-q1/qualify.sh
contractValidatorSha256	tests/quint-q1/validate-contract.mjs
qualificationReportSha256	docs/experiments/quint-q1/qualification-report.md
workflowComparisonSha256	docs/experiments/quint-q1/workflow-comparison.md
guidanceEvaluationSha256	docs/experiments/quint-q1/quint-llm-kit-evaluation.md
fenceManifestSha256	tests/quint-q1/fence-manifest.txt
sourceManifestSha256	tests/quint-q1/sources.sha256
EOF

  while IFS=$'\t' read -r id source_path module_path; do
    local expected_source expected_module
    expected_source="$(jq -r --arg id "$id" '.slices[] | select(.id == $id) | .sourceSha256' "$manifest")"
    expected_module="$(jq -r --arg id "$id" '.slices[] | select(.id == $id) | .moduleSha256' "$manifest")"
    [[ "$(sha256sum "$repo_root/$source_path" | cut -d' ' -f1)" == "$expected_source" ]] \
      || { fail "binding=candidate-manifest source slice mismatch: $id"; return 1; }
    [[ "$(sha256sum "$generated_root/$module_path" | cut -d' ' -f1)" == "$expected_module" ]] \
      || { fail "binding=candidate-manifest generated slice mismatch: $id"; return 1; }
  done <<'EOF'
requirements-and-evidence	docs/experiments/quint-q1/slices/requirements-and-evidence.md	requirements.qnt
sir-damage-rule	docs/experiments/quint-q1/slices/sir-damage-rule.md	sir-damage.qnt
coordination-process	docs/experiments/quint-q1/slices/coordination-process.md	coordination.qnt
EOF
}

run_root="$(mktemp -d /tmp/fsgg-quint-q1.XXXXXX)"
trap 'rm -rf -- "$run_root"' EXIT

check_tools
if (exec 3<>/dev/tcp/127.0.0.1/19222) 2>/dev/null; then
  exec 3>&- 3<&-
  fail "binding=apalache-endpoint pre-existing server on 127.0.0.1:19222"
fi
export QUINT_HOME="$run_root/quint-home"
export HOME="$run_root/home"
mkdir -p "$QUINT_HOME" "$HOME"
ln -s "$apalache_dist" "$QUINT_HOME/apalache-dist-0.56.1"
ln -s "$(dirname "$rust_bin")" "$QUINT_HOME/rust-evaluator-v0.6.0"
check_sources "$repo_root"
check_fences "$slice_root"
jq empty "$repo_root/docs/experiments/quint-q1/compiled-contract.schema.json"

extract "$run_root/a"
extract "$run_root/b"
extract "$run_root/kit-workflow"
for module in requirements.qnt sir-damage.qnt coordination.qnt; do
  cmp "$run_root/a/$module" "$run_root/b/$module" >/dev/null \
    || fail "non-deterministic extraction: $module"
  cmp "$run_root/a/$module" "$run_root/kit-workflow/$module" >/dev/null \
    || fail "binding=workflow-comparison semantic output differs for $module"
  run_quint_clean "$run_root/$module.typecheck.log" typecheck "$run_root/a/$module"
  run_quint_clean "$run_root/$module.kit.typecheck.log" typecheck "$run_root/kit-workflow/$module"
done
check_contract "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" "$run_root/a/requirements.qnt"
check_candidate_manifest "$run_root/a"

run_quint_clean "$run_root/requirements.test.log" test "$run_root/a/requirements.qnt" --main RequirementsSliceTests --match evidenceBeforeAcceptance
run_quint_clean "$run_root/sir.test.log" test "$run_root/a/sir-damage.qnt" --main SirDamageSliceTests --match reviewedWitness
run_quint_clean "$run_root/coordination.stale.test.log" test "$run_root/a/coordination.qnt" --main CoordinationSliceTests --match staleObservationIsRefused
run_quint_clean "$run_root/coordination.retry.test.log" test "$run_root/a/coordination.qnt" --main CoordinationSliceTests --match lostResponseRetryIsIdempotent
run_quint_clean "$run_root/requirements.kit.test.log" test "$run_root/kit-workflow/requirements.qnt" --main RequirementsSliceTests --match evidenceBeforeAcceptance
run_quint_clean "$run_root/sir.kit.test.log" test "$run_root/kit-workflow/sir-damage.qnt" --main SirDamageSliceTests --match reviewedWitness
run_quint_clean "$run_root/coordination.kit.stale.test.log" test "$run_root/kit-workflow/coordination.qnt" --main CoordinationSliceTests --match staleObservationIsRefused
run_quint_clean "$run_root/coordination.kit.retry.test.log" test "$run_root/kit-workflow/coordination.qnt" --main CoordinationSliceTests --match lostResponseRetryIsIdempotent
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
  --invariants atMostOneApply receiptMatchesApply completeHasReceipt staleNeverApplies staleRefusalNeverApplies knownPhase \
  --backend rust --verbosity 1

# The pinned-kit workflow and the FS-GG-minimal workflow consume identical extracted bytes. The second
# seeded run makes the comparison executable rather than an author-scored prose claim.
run_quint_clean "$run_root/coordination.kit.run.log" run "$run_root/kit-workflow/coordination.qnt" --main CoordinationSlice \
  --max-samples 1000 --max-steps 20 --seed 92203 \
  --invariants atMostOneApply receiptMatchesApply completeHasReceipt staleNeverApplies staleRefusalNeverApplies knownPhase \
  --backend rust --verbosity 1

# Apalache writes a diagnostic tree in its process working directory. Keep that disposable output inside
# the harness root so a qualification run never dirties the repository.
cd "$run_root"
export PATH="$(dirname "$java_bin"):$PATH"
run_quint_clean "$run_root/requirements.verify.log" verify "$run_root/a/requirements.qnt" \
  --main RequirementsSlice --invariants acceptedOnlyWithEvidence --max-steps 8 \
  --apalache-version 0.56.1 --server-endpoint 127.0.0.1:19222 --verbosity 1
run_quint_clean "$run_root/sir.verify.log" verify "$run_root/a/sir-damage.qnt" \
  --main SirDamageSlice --invariants nonNegativeHitPoints knownLastAction --max-steps 8 \
  --apalache-version 0.56.1 --server-endpoint 127.0.0.1:19222 --verbosity 1
run_quint_clean "$run_root/coordination.verify.log" verify "$run_root/a/coordination.qnt" \
  --main CoordinationSlice \
  --invariants atMostOneApply receiptMatchesApply completeHasReceipt staleNeverApplies staleRefusalNeverApplies knownPhase \
  --max-steps 10 --apalache-version 0.56.1 --server-endpoint 127.0.0.1:19222 --verbosity 1
run_quint_clean "$run_root/coordination.temporal.log" verify "$run_root/a/coordination.qnt" \
  --main CoordinationSlice --temporal eventualCompletion --backend tlc \
  --server-endpoint 127.0.0.1:19222 --verbosity 1
check_file_tree "$expected_apalache_tree_sha" "$apalache_dist" "apalache-distribution-after-execution"

mkdir -p "$run_root/missing" "$run_root/reordered" "$run_root/duplicate" "$run_root/stale"
cp "$slice_root"/*.md "$run_root/missing"/
sed -i '0,/^```quint requirements.qnt +=$/d' "$run_root/missing/requirements-and-evidence.md"
expect_refusal missing-fence 'binding=fence-inventory' check_fences "$run_root/missing"

cp "$slice_root"/*.md "$run_root/reordered"/
sed -i '0,/^```quint coordination.qnt +=$/s//```quint coordination-other.qnt +=/' "$run_root/reordered/coordination-process.md"
expect_refusal reordered-or-unexpected-target 'binding=fence-inventory' check_fences "$run_root/reordered"

cp "$slice_root"/*.md "$run_root/duplicate"/
printf '\n```quint coordination.qnt +=\nmodule Duplicate {}\n```\n' >>"$run_root/duplicate/coordination-process.md"
expect_refusal duplicate-fence 'binding=fence-inventory' check_fences "$run_root/duplicate"

mkdir -p "$run_root/stale/docs/experiments/quint-q1/slices"
cp "$slice_root"/*.md "$run_root/stale/docs/experiments/quint-q1/slices"/
sed -i 's/REQ-AUDIT-001/REQ-AUDIT-EDITED/' "$run_root/stale/docs/experiments/quint-q1/slices/requirements-and-evidence.md"
expect_refusal stale-source 'binding=canonical-source' check_sources "$run_root/stale"

cp "$run_root/a/coordination.qnt" "$run_root/edited-output.qnt"
printf '\n// independent edit\n' >>"$run_root/edited-output.qnt"
expect_refusal hand-edited-generated-output 'EOF on' cmp "$run_root/a/coordination.qnt" "$run_root/edited-output.qnt"

cp "$run_root/a/requirements.qnt" "$run_root/requirements-mutant.qnt"
sed -i '/observedEvidence.contains(auditRequirement.evidenceId),/d' "$run_root/requirements-mutant.qnt"
expect_refusal missing-evidence-guard 'binding=acceptedOnlyWithEvidence' quint_refusal_probe \
  acceptedOnlyWithEvidence 'requirements-mutant.qnt:acceptedOnlyWithEvidence' 'Invariant violated' \
  "$quint_bin" run "$run_root/requirements-mutant.qnt" \
  --main RequirementsSlice --max-samples 500 --max-steps 5 --seed 92211 \
  --invariants acceptedOnlyWithEvidence --backend rust --verbosity 1

cp "$run_root/a/sir-damage.qnt" "$run_root/sir-mutant.qnt"
sed -i 's/clampAtZero(hitPoints - amount)/hitPoints - amount/' "$run_root/sir-mutant.qnt"
expect_refusal combat-boundary-defect 'binding=nonNegativeHitPoints' quint_refusal_probe \
  nonNegativeHitPoints 'sir-mutant.qnt:nonNegativeHitPoints' 'Invariant violated' \
  "$quint_bin" run "$run_root/sir-mutant.qnt" \
  --main SirDamageSlice --max-samples 500 --max-steps 5 --seed 92212 \
  --invariants nonNegativeHitPoints --backend rust --verbosity 1

cp "$run_root/a/coordination.qnt" "$run_root/coordination-mutant.qnt"
sed -i '/action retry = all {/,/^  }/ s/applyCount'"'"' = applyCount,/applyCount'"'"' = applyCount + 1,/' \
  "$run_root/coordination-mutant.qnt"
expect_refusal double-apply-on-retry 'binding=atMostOneApply' quint_refusal_probe \
  atMostOneApply 'coordination-mutant.qnt:atMostOneApply' 'Invariant violated' \
  "$quint_bin" run "$run_root/coordination-mutant.qnt" \
  --main CoordinationSlice --max-samples 2000 --max-steps 20 --seed 92213 \
  --invariants atMostOneApply receiptMatchesApply --backend rust --verbosity 1

cp "$run_root/a/coordination.qnt" "$run_root/lost-update-mutant.qnt"
sed -i '/observedRevision == revision,/d; s/\.then(refuseStale)/.then(apply)/' "$run_root/lost-update-mutant.qnt"
expect_refusal lost-update-revision-guard 'binding=staleObservationIsRefused' quint_refusal_probe \
  staleObservationIsRefused 'lost-update-mutant.qnt:staleObservationIsRefused' 'staleObservationIsRefused' "$quint_bin" test \
  "$run_root/lost-update-mutant.qnt" --main CoordinationSliceTests --match staleObservationIsRefused

cp "$run_root/a/coordination.qnt" "$run_root/stale-apply-mutant.qnt"
sed -i '/action refuseStale = all {/,/^  }/ s/applyCount'"'"' = applyCount,/applyCount'"'"' = applyCount + 1,/' \
  "$run_root/stale-apply-mutant.qnt"
expect_refusal stale-refusal-applies 'binding=staleObservationIsRefused' quint_refusal_probe \
  staleObservationIsRefused 'stale-apply-mutant.qnt:staleObservationIsRefused' 'staleObservationIsRefused' "$quint_bin" test \
  "$run_root/stale-apply-mutant.qnt" --main CoordinationSliceTests --match staleObservationIsRefused

cp "$run_root/a/coordination.qnt" "$run_root/completion-order-mutant.qnt"
sed -i '/action complete = all {/,/^  }/ { /^    receipt,$/d; s/phase == Applied,/phase == Prepared,/; }' \
  "$run_root/completion-order-mutant.qnt"
printf '%s\n' \
  'module CompletionOrderMutationTest {' \
  '  import CoordinationSlice.*' \
  '  run rejectsCompletionBeforeApply = init.then(prepare).then(complete).expect(completeHasReceipt)' \
  '}' >>"$run_root/completion-order-mutant.qnt"
expect_refusal completion-without-receipt-ordering 'binding=rejectsCompletionBeforeApply' quint_refusal_probe \
  rejectsCompletionBeforeApply 'completion-order-mutant.qnt:rejectsCompletionBeforeApply' 'rejectsCompletionBeforeApply' "$quint_bin" test \
  "$run_root/completion-order-mutant.qnt" --main CompletionOrderMutationTest --match rejectsCompletionBeforeApply

cp "$run_root/a/coordination.qnt" "$run_root/liveness-mutant.qnt"
sed -i '/lossCount == 0,/d; s/lossCount'"'"' = lossCount + 1,/lossCount'"'"' = lossCount,/; /action retry = all {/,/^  }/ s/retryCount'"'"' = retryCount + 1,/retryCount'"'"' = retryCount,/' \
  "$run_root/liveness-mutant.qnt"
expect_refusal unbounded-lost-response-liveness 'binding=eventualCompletion' quint_refusal_probe \
  eventualCompletion 'liveness-mutant.qnt:eventualCompletion' 'found a counterexample' \
  "$quint_bin" verify "$run_root/liveness-mutant.qnt" \
  --main CoordinationSlice --temporal eventualCompletion --backend tlc \
  --server-endpoint 127.0.0.1:19222 --verbosity 1

jq '.expression = {"op":"hidden-authority"}' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-mutant.json"
expect_refusal arbitrary-expression-contract 'binding=compiled-contract' check_contract "$run_root/contract-mutant.json" "$run_root/a/requirements.qnt"

jq '.requirements[0] = "not a stable id"' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-id-mutant.json"
expect_refusal invalid-contract-id 'binding=compiled-contract' check_contract \
  "$run_root/contract-id-mutant.json" "$run_root/a/requirements.qnt"

jq '.sources[0].path = "../escape.md"' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-path-mutant.json"
expect_refusal escaping-contract-path 'binding=compiled-contract' check_contract \
  "$run_root/contract-path-mutant.json" "$run_root/a/requirements.qnt"

jq '.sources[0].startLine = 91 | .sources[0].endLine = 90' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-range-mutant.json"
expect_refusal reversed-contract-line-range 'binding=compiled-contract' check_contract \
  "$run_root/contract-range-mutant.json" "$run_root/a/requirements.qnt"

jq '.digests.canonicalSource = "sha256:bad"' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-digest-mutant.json"
expect_refusal malformed-contract-digest 'binding=compiled-contract' check_contract \
  "$run_root/contract-digest-mutant.json" "$run_root/a/requirements.qnt"

jq '.actions += [.actions[0]]' \
  "$repo_root/docs/experiments/quint-q1/compiled-contract.example.json" >"$run_root/contract-duplicate-mutant.json"
expect_refusal duplicate-contract-action 'binding=compiled-contract' check_contract \
  "$run_root/contract-duplicate-mutant.json" "$run_root/a/requirements.qnt"

expect_refusal moving-latest-guidance 'binding=guidance-revision' \
  check_guidance "$guidance_root" latest

if [[ -n "${Q1_JUNIT_OUT:-}" ]]; then
  mkdir -p "$(dirname "$Q1_JUNIT_OUT")"
  junit_tmp="$Q1_JUNIT_OUT.tmp"
  {
    printf '%s\n' '<?xml version="1.0" encoding="UTF-8"?>'
    printf '%s\n' '<testsuite name="quint-q1-qualification" tests="42" failures="0" errors="0" skipped="0">'
    for case_name in \
      pinned-tools canonical-sources fence-inventory contract-shape deterministic-extraction \
      typecheck named-tests normalized-sir-witness requirements-simulation sir-simulation \
      coordination-simulation apalache-safety tlc-liveness complete-positive-bundle \
      coordination-stale-example coordination-retry-example \
      kit-requirements-typecheck kit-sir-typecheck kit-coordination-typecheck \
      kit-requirements-tests kit-sir-tests kit-coordination-tests kit-coordination-simulation \
      mutation-missing-fence mutation-reordered-target mutation-duplicate-fence mutation-stale-source \
      mutation-edited-output mutation-missing-evidence mutation-combat-boundary mutation-double-apply \
      mutation-unbounded-liveness mutation-arbitrary-expression mutation-lost-update \
      mutation-stale-refusal-apply mutation-completion-order mutation-invalid-contract-id \
      mutation-escaping-contract-path mutation-reversed-line-range mutation-malformed-digest \
      mutation-duplicate-action mutation-moving-guidance
    do
      printf '  <testcase classname="FS.GG.SDD.QuintQ1" name="%s"/>\n' "$case_name"
    done
    printf '%s\n' '</testsuite>'
  } >"$junit_tmp"
  mv "$junit_tmp" "$Q1_JUNIT_OUT"
fi

printf 'Q1-QUALIFIED: 3 slices, 23 positive commands, 19 independent mutations\n'
