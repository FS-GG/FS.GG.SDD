# Feature Specification: Surface provider output on scaffold failure

**Feature Branch**: `054-surface-provider-output`

**Created**: 2026-07-01

**Status**: Draft

**Input**: Coordination board item — FS.GG.SDD#35 (P2 SDD, In progress): "[cross-repo]
`fsgg-sdd scaffold --provider rendering` exits 127 in CI (provider command not found);
opaque — no provider stderr surfaced". The underlying `exit 127` root cause (the
`--productName` parameter surface) is already fixed upstream in FS.GG.Rendering (Feature
217). The remaining, SDD-owned ask is the diagnostics observability gap: on a provider
failure, `fsgg-sdd scaffold` reports only the bare exit code and never the provider's
invoked command line or captured stdout/stderr, so a downstream consumer cannot diagnose
*why* the provider failed without inserting a logging shim on `PATH`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Diagnose a provider failure from the report alone (Priority: P1)

A product author (or a CI job) runs `fsgg-sdd scaffold --provider <name> …` and the
external template provider fails. Today the scaffold report says only
`"Provider '<name>' exited 127."` with `producedPathCount: 0`. The author has no way to
see *which* command the provider ran or what error the provider's engine printed. They
must reproduce locally, put a logging shim ahead of `dotnet` on `PATH`, and re-run — exactly
what the board item's reporter had to do to discover that the real error was
`'--productName' is not a valid option`.

With this feature, the scaffold report itself carries the provider's fully-resolved invoked
command line, its captured standard output, its captured standard error, and its exit code.
The author reads the failure cause directly from the report — no shim, no re-run.

**Why this priority**: This is the entire point of the board item and the MVP. Without it,
provider failures remain undiagnosable from a downstream repo, which blocks composition CI
(FS.GG.Templates#30). Everything else is refinement of this one capability.

**Independent Test**: Run scaffold against a fixture provider that fails after printing a
known error to stderr; assert the report contains that provider's command line, the known
stderr text, and the exit code. Deliverable value: the failure is self-explanatory from the
report with no external tooling.

**Acceptance Scenarios**:

1. **Given** a provider that exits non-zero after writing a diagnostic to stderr, **When**
   the author runs scaffold, **Then** the report includes the provider's invoked command
   line, its captured stdout, its captured stderr (containing the diagnostic), and its exit
   code — and still classifies the outcome as `providerFailed` at exit 2.
2. **Given** the exact reproduction from FS.GG.SDD#35 (provider execs
   `dotnet new fs-gg-ui … --productName Acme` against a template that rejects `--productName`),
   **When** scaffold runs, **Then** the report surfaces the engine's own
   `'--productName' is not a valid option` text, so the cause is identifiable without a
   `PATH` shim.
3. **Given** a provider that fails to launch at all (its engine binary is absent), **When**
   scaffold runs, **Then** the report surfaces the attempted command line and the launch
   error and classifies the outcome as `providerUnavailable` at exit 2.

---

### User Story 2 - Consistent visibility across automation and human surfaces (Priority: P2)

The failure detail must be usable both by a machine (CI parsing the JSON automation
contract) and by a human (reading the text or rich rendering). The provider command,
stdout, stderr, and exit code are facts of the single `CommandReport`, projected the same
three ways as every other scaffold fact: JSON is the contract, text is the portable
summary, rich is the presentation. A CI job greps the JSON; a developer scans the rich
panel; both see the same provider-failure facts.

**Why this priority**: The board item is filed by a CI consumer, so the JSON path matters
most; but a human debugging locally reads text/rich. Parity across projections is required
by the product's projection rule (rich/text add and drop no facts vs JSON).

**Independent Test**: Run the same failing scaffold three times with `--json`, `--text`,
`--rich`; assert all three carry the identical provider-output facts and that `--rich`
degrades to zero-ANSI when non-interactive.

**Acceptance Scenarios**:

1. **Given** a provider failure, **When** the report is projected as JSON, text, and rich,
   **Then** every projection carries the provider command line, stdout, stderr, and exit
   code, and no projection drops or invents a fact relative to the JSON contract.
2. **Given** a provider failure with `--rich` in a non-interactive/redirected context,
   **When** scaffold runs, **Then** the rich projection renders the same facts as plain
   text with zero ANSI and identical exit code.

---

### User Story 3 - No noise on success or on pre-invocation errors (Priority: P3)

Provider stdout/stderr are surfaced only when the provider was actually invoked and the
outcome is a provider-defect failure. A successful scaffold and a user-input error that
occurs *before* the provider is invoked (missing/unknown provider, unsupported contract
version, missing required parameter — the exit-1 class) do not dump provider output into
the report, keeping the success path's deterministic contract clean and pre-invocation
errors focused on the author's input mistake.

**Why this priority**: Protects the existing deterministic success contract and avoids
drowning routine runs in provider chatter. Valuable but subordinate to actually surfacing
the failure detail.

**Independent Test**: Run a successful scaffold and a `providerMissing` scaffold; assert
neither report contains provider stdout/stderr content and both exit codes are unchanged
(0 and 1 respectively).

**Acceptance Scenarios**:

1. **Given** a successful scaffold, **When** the report is produced, **Then** it carries no
   provider stdout/stderr content and the JSON success contract is byte-stable relative to
   today except for any additive, empty provider-output fields.
2. **Given** a scaffold invoked with no `--provider` (or an unknown/unsupported/parameter-
   missing provider), **When** it blocks at exit 1, **Then** the report surfaces the input
   diagnostic only and no provider stdout/stderr (the provider was never run).

---

### Edge Cases

- **Very large provider output**: A provider that prints megabytes to stdout/stderr must not
  produce an unbounded report. Captured output is bounded to a documented maximum per stream;
  when truncated, the report indicates truncation.
- **Empty stderr but non-zero exit**: The report still shows the command line and exit code
  with an empty (present) stderr field, rather than omitting the failure detail.
- **Failed to launch (no exit code)**: When the provider process never starts
  (`providerUnavailable`), there is no exit code; the report surfaces the attempted command
  line and the launch error and represents the exit code as "not launched" rather than a
  misleading `0`.
- **Non-UTF-8 / binary bytes on a stream**: Captured output is decoded defensively so a
  binary blob on stderr cannot crash the report or corrupt the JSON.
- **SDD-tree intrusion then exit 0** (`providerWroteSddTree`): The primary diagnostic remains
  the intrusion, but provider output is surfaced for consistency so the author sees what the
  provider did.
- **Secrets in the command line**: The surfaced command line is the provider's
  forwarded parameters and template arguments (e.g. `productName`), which SDD already plans
  and could print; the feature surfaces no credential or feed token that SDD does not already
  handle.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: On every provider-defect failure — `providerFailed` (including the SDD-tree
  intrusion case `providerWroteSddTree`) and `providerUnavailable` — the scaffold
  `CommandReport` MUST surface the provider's fully-resolved invoked command line (program +
  arguments as executed).
- **FR-002**: On the same failures, the report MUST surface the provider's captured standard
  output and standard error as two separate, labeled facts (never merged into one stream).
- **FR-003**: The provider's exit code MUST be surfaced as a structured report fact, not only
  interpolated into the human-readable diagnostic message. When the provider failed to launch
  (no process started), the report MUST represent the absence of an exit code distinctly from
  a real `0`.
- **FR-004**: The provider-output facts MUST appear in all three report projections — the
  JSON automation contract, the `--text` summary, and the `--rich` rendering — with JSON as
  the single source of truth and text/rich adding and dropping no facts. Rich remains
  presentation-only and continues to degrade to zero-ANSI when non-interactive or color is
  disabled.
- **FR-005**: Captured provider output MUST be bounded to a documented maximum size per
  stream; when either stream is truncated, the report MUST indicate that truncation occurred
  so a runaway provider cannot produce an unbounded report.
- **FR-006**: Provider-output content MUST be surfaced only when the provider was invoked and
  the outcome is a provider-defect failure. A successful scaffold and any user-input error
  that occurs before the provider is invoked (`providerMissing`, `providerUnknown`,
  `providerVersionUnsupported`, `providerParamMissing`) MUST NOT carry provider stdout/stderr
  content.
- **FR-007**: The feature MUST NOT change outcome classification or exit codes: provider-
  defect failures still exit 2, user-input errors still exit 1, success still exits 0. It adds
  diagnostic visibility only; it never reclassifies an outcome and never reports an incomplete
  scaffold as complete.
- **FR-008**: The `scaffold.providerFailed` (and sibling) diagnostic's remediation guidance
  MUST point the consumer to the newly surfaced provider output, so the report is self-
  describing about where to read the failure cause.
- **FR-009**: The report's structure MUST be deterministic for a given provider execution:
  the presence, ordering, and shape of the provider-output facts are fixed; only their
  textual content is execution data. A fixture provider producing fixed output MUST yield a
  byte-stable JSON/text projection suitable for golden tests, in the same way real-provider
  content is data today (e.g. produced paths). Rich output remains excluded from
  deterministic/golden contracts.
- **FR-010**: The persisted `.fsgg/scaffold-provenance.json` schema MUST remain v1. Transient
  provider stdout/stderr is diagnostic runtime data belonging to the report, not a durable
  provenance fact, so it MUST NOT be added to the persisted provenance record.

### Key Entities *(include if data involved)*

- **Provider invocation result**: The outcome of running the external template provider —
  the resolved command line, whether the process started, the exit code (absent when not
  started), the captured stdout, the captured stderr, and per-stream truncation flags. This
  is the new information the failure report must convey; today only "started" and "exit code"
  exist.
- **Scaffold report block**: The `scaffold` section of the `CommandReport` that today carries
  provider name, outcome, produced paths, effective parameters, etc. It gains the provider-
  invocation-result facts, projected identically across JSON/text/rich.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The FS.GG.SDD#35 reproduction (the `--productName` rejection) is diagnosable
  entirely from the scaffold report — the failing command line and the engine's rejection
  text are both visible — with zero additional tooling (no `PATH` shim, no re-run).
- **SC-002**: 100% of provider-defect failures (`providerFailed`, `providerUnavailable`,
  `providerWroteSddTree`) surface the provider command line, stdout, stderr, and exit-code
  fact in the report.
- **SC-003**: All three projections of a provider-failure report carry identical provider-
  output facts — the JSON contract, the text summary, and the rich rendering agree; zero JSON
  facts are dropped or added by text/rich.
- **SC-004**: A successful scaffold and every exit-1 user-input failure produce no provider
  stdout/stderr content in the report; the success-path JSON contract remains byte-stable
  except for additive, empty provider-output fields.
- **SC-005**: No captured stream in any report exceeds the documented size bound, and every
  truncation is explicitly indicated.
- **SC-006**: Exit codes and outcome strings for success, provider-defect, and user-input
  paths are unchanged from today's behavior (0 / 2 / 1 respectively).

## Assumptions

- **Failure-only surfacing**: Provider output is surfaced on provider-defect failures, not on
  successful runs. A future `--verbose`-style flag to show provider output on success is out
  of scope for this feature; the board item asks specifically for failure visibility.
- **Capture at the process edge**: Both provider streams are captured where the process is
  run (the MVU `RunProcess` edge), where they are currently drained and discarded; the drain
  is retained (to avoid deadlock) but the content is now carried forward instead of dropped.
- **Size bound**: A concrete per-stream byte/line cap and a truncation marker are chosen in
  the plan; the spec requires only that a documented bound and a truncation indicator exist.
- **Report-only, provenance untouched**: Provider stdout/stderr live only in the transient
  report, never in the persisted `.fsgg/scaffold-provenance.json` (which stays schema v1).
- **Determinism model**: Structure is deterministic; content is data. Golden/fixture tests
  assert on controlled fixture-provider output; real-provider content is treated as data the
  same way produced paths already are.
- **No new outcomes or exit codes**: This feature is additive diagnostics; the existing
  outcome taxonomy and exit-code mapping are unchanged.
- **Reuses existing fixture providers**: The failing-provider fixtures already used by the
  scaffold tests (`fails-midway`, `writes-into-fsgg`) are the basis for verifying surfaced
  output; a fixture that prints a known stderr line is added if none exists.
