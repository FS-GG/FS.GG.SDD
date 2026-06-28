# Phase 0 Research: Composition-Acceptance Consumes the Dispatched Registry

All Technical Context items resolved; no open NEEDS CLARIFICATION. Decisions below.

## D1 — Trigger surface: `repository_dispatch` added alongside existing triggers

**Decision**: Add `on: repository_dispatch: types: [composition-registry-updated]` to
`composition-acceptance.yml`, keeping the existing `schedule` and `workflow_dispatch` triggers
untouched.

**Rationale**: `repository_dispatch` is the GitHub-native target of the org reusable dispatch
*sender* (Templates#15 / `.github` dispatch-sender #22), which sends a typed event with a
`client_payload`. Constraining to `types: [composition-registry-updated]` means any other event type
does not trigger the acceptance (Edge Cases: "Wrong or malformed event MUST NOT trigger"). The
existing sources are additive (FR-001, US3) — no trigger is removed.

**Alternatives considered**: A separate new workflow file for the dispatch path — rejected: it would
duplicate the materialize+run steps and risk behavioral drift between sources (FR-006 demands one
behavior). A `workflow_dispatch`-only manual paste of content — rejected: that is the drift the
feature exists to remove.

## D2 — Source selection: deterministic precedence in one extracted resolver

**Decision**: Resolve exactly one registry source by precedence **explicit manual `registry_path`
input > dispatched `client_payload.registry_content` > scheduled secret**, in a single POSIX-shell
script `scripts/workflows/resolve-acceptance-registry.sh` that the workflow calls.

**Rationale**: The three sources are largely partitioned by event (`workflow_dispatch` carries the
input, `repository_dispatch` carries the payload, `schedule` carries neither so it falls to the
secret), so a linear precedence is unambiguous and matches FR-004 / the Edge Case "An explicit manual
`registry_path` input, when supplied, overrides." Putting the chain in one script (rather than inline
YAML `if`/`elif`) makes it unit-testable against a real shell and real temp files — the only way to
honor constitution VI here without "testing YAML." The workflow becomes a thin caller that exports the
script's printed path to `GITHUB_ENV`.

**Alternatives considered**: Keep the `if/elif/else` inline in the step's `run:` block — rejected: not
unit-testable, and the new fail-closed branch + drift surfacing make it more logic than belongs
untested in YAML. A composite action — rejected: heavier than one repo-local script for a single repo.

## D3 — Fail-closed semantics (FR-005) vs. the opt-in offline case (FR-007)

**Decision**: A `repository_dispatch`-triggered run with missing/empty `registry_content` exits
non-zero with a clear `::error::` diagnostic — never pass, never skip. The resolver distinguishes
"triggered by dispatch" (an explicit wiring intent) from "no source at all" by being told the event
name; on the dispatch event, empty content is a defect, not the offline opt-out.

**Rationale**: Edge Cases require fail-closed on an explicit dispatch because a missing registry then
is a wiring defect, not the offline "unset" case. The existing workflow already exits 1 when no source
is present, so the schedule-with-no-secret path is unchanged; the new branch makes the *dispatch*-with-
empty-content case fail loudly too (constitution VIII: distinguish malformed input from the optional
no-op). The offline inner loop (no env at all, local/PR) is untouched — its facts discovery-skip
(`RequiresRegistryFact`) and the workflow never runs there.

**Alternatives considered**: Treat empty dispatch as a skip — rejected by SC-005 (never a silent skip).

## D4 — Verbatim materialization of multi-line / special-character YAML (FR-002, Edge Cases)

**Decision**: Write the chosen content byte-for-byte with `printf '%s'` into a `RUNNER_TEMP` file,
exactly as today's secret path does. Read the dispatched content from the event payload via the
GitHub-provided `${{ github.event.client_payload.registry_content }}` passed through an **environment
variable** (not interpolated into the script body), so YAML newlines, quotes, and `$`/backtick
characters survive unmangled.

**Rationale**: The provider must resolve identically to the canonical file (drift = 0, SC-001/FR-002).
Passing untrusted/multi-line content via `env:` rather than string-interpolating it into the shell is
both correct (preserves bytes) and the standard injection-safe pattern. `printf '%s'` avoids the
trailing-newline and escape mangling that `echo` introduces.

**Alternatives considered**: `echo "$CONTENT" >` — rejected: `echo` mangles backslashes/leading `-`.
Base64 transport — rejected: the sender publishes raw `registry_content`; SDD must consume the
published contract as-is (FR-009), not invent an encoding.

## D5 — Drift signal surfaced at the run layer, NOT in the result document (FR-008)

**Decision**: Surface the 12-char sha256 (`client_payload.registry_sha256_12` / `version`) to the
GitHub **Step Summary** and step log, and recompute the sha256 of the materialized bytes as an
integrity cross-check. The `composition-acceptance-result` v1 document — body and `sensed` block — is
**not** changed.

**Rationale**: The spec is explicit that the result document and its `sensed` block are unchanged
(Key Entities; Assumptions "does not change the `composition-acceptance-result` v1 contract"), yet
FR-008/SC-006 require the registry content identity to be surfaced and the run traceable. The only
consistent reading is: surface the drift signal at the **run** (Step Summary / log / annotation),
which is where the dispatch payload is available and which needs no schema change. Recomputing the
hash from the materialized file and comparing to the advertised value catches transport corruption
(defense for FR-002's byte-for-byte guarantee).

**Alternatives considered**: Add a `registrySha256_12` field to the result's `sensed` block — rejected:
it changes the v1 contract the spec freezes. Encode it in the run **name** only — kept as a nice-to-
have but the Step Summary is the durable, queryable record, so that is the primary sink.

## D6 — Testing a shell resolver inside the F# inner loop

**Decision**: Add `RegistryResolverTests.fs` to `FS.GG.SDD.Acceptance.Tests` as plain `[<Fact>]`
(offline, **not** `RequiresRegistryFact`), invoking `resolve-acceptance-registry.sh` through the
existing `AcceptanceSupport.runToCompletion` process edge with controlled env + temp files, asserting
the materialized file bytes, the printed path, and the exit code per case. Gate the facts to a shell-
available OS (skip on Windows) so the cross-platform inner loop stays green.

**Rationale**: This is the real-fixture discipline the constitution prefers (real `bash`, real temp
files, no mocks), and it reuses an edge the project already owns. The project already hosts offline,
non-gated facts (`ProbeResolutionTests.fs`), so no new project is warranted. The script must be copied
to the test output dir (an `fsproj` `Content`/`None CopyToOutputDirectory` item, or resolved via
`AcceptanceSupport.repoRoot`) so the test can locate it deterministically — prefer resolving it from
`repoRoot/scripts/workflows/...` to avoid a second copy.

**Alternatives considered**: `bats` shell tests — rejected: adds a non-.NET test toolchain to CI for
one script. Parsing the YAML and asserting trigger keys — kept as an optional lightweight check but it
does not exercise behavior; the process-edge test is the substantive coverage.

## D7 — Cross-repo contract ownership (FR-009)

**Decision**: Treat `composition-registry-updated` (event type + `client_payload` fields
`registry_content`, `registry_path`, `registry_sha256_12`, `version`, plus sender-added
`source_repo`/`source_sha`/`source_ref`) as a **versioned cross-repo contract owned jointly with
FS.GG.Templates**. Author the SDD consumer-side view as `contracts/registry-dispatch.md`; record the
coordination obligation (the canonical registry + sender live in FS.GG.Templates / FS-GG.github) and
that any change to the contract is a coordinated two-sided change. Use the **cross-repo-coordination**
skill for any actual contract change or compatibility-registry update.

**Rationale**: SDD must consume the published contract without inventing rendering identity (FR-003/
FR-009). Documenting the consumed shape locally makes SDD's dependency explicit and versioned
(constitution II / Engineering Constraints: cross-repo integration via explicit, versioned contracts)
without copying any rendering identity token.

**Alternatives considered**: Rely solely on the Templates-side doc — rejected: SDD's consumer
expectations (which fields it reads, fail-closed rules) deserve a local, testable contract.

## D8 — Green outcome precondition (FR-010 / SC-002)

**Decision**: No SDD code change is needed for the green outcome; it is unblocked by Rendering#9 (root
`.slnx` + `Directory.Build.props` + `global.json` + build wrapper, merged 2026-06-28), which makes the
composed product buildable/runnable so the existing `appBuilds`/`appRuns` probes pass.

**Rationale**: The build/run probes (035) already resolve `dotnet build` / `dotnet run --project
<discovered>` over the produced product; they failed only because the produced product lacked a root
build entry point. With that merged, a live registry resolving the published template yields a passing
verdict. This feature's job is to deliver the live registry (Story 1); green (Story 2) then follows.

**Alternatives considered**: None — the precondition is an external merge, already satisfied per the
spec Assumptions.
