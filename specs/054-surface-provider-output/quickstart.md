# Quickstart / Validation: Surface provider output on scaffold failure

**Feature**: `054-surface-provider-output` | Validates FR-001…FR-010, SC-001…SC-006.

Prereqs: .NET 10 SDK; `dotnet build FS.GG.SDD.sln`. Scaffold tests run real `dotnet new`
providers under `tests/fixtures/scaffold-provider/` (no mocks) and are serialized by
`[<Collection("Scaffold")>]`.

Run the feature's tests:

```bash
dotnet test tests/FS.GG.SDD.Commands.Tests --filter FullyQualifiedName~Scaffold
dotnet test tests/FS.GG.SDD.Cli.Tests      --filter FullyQualifiedName~ScaffoldParity
```

## Scenario A — Diagnose a provider failure from the report alone (US1, SC-002)

Fixture: `fails-midway` (a `postAction` runs `false` → non-zero exit).

```bash
fsgg-sdd scaffold --provider fixture   # in a temp dir with the fails-midway registry
```

Expect exit **2**, `scaffold.providerFailed`, and in the JSON report:
`scaffold.providerInvocation.commandLine` = the executed `dotnet new …` line,
`.exitCode` an integer, `.standardOutput` / `.standardError` present. Outcome still
`providerFailed`.

## Scenario B — The FS.GG.SDD#35 reproduction (US1-AC2, SC-001)

A fixture template that does **not** declare `productName`, invoked with `--productName`:
the `dotnet new` engine prints `'--productName' is not a valid option` to stderr and exits
non-zero.

```bash
fsgg-sdd scaffold --provider fixture --param productName=Acme
```

Assert the report's `standardError` **contains** `'--productName' is not a valid option`
(assert-contains, not byte-golden — engine wording is SDK data, R7). The cause is
diagnosable with **no** `PATH` shim and **no** re-run (SC-001).

## Scenario C — Provider fails to launch (US1-AC3, edge case)

Point the registry at an engine that cannot start (fixture with a bogus program on the
create edge).

Expect exit **2**, `scaffold.providerUnavailable`, `providerInvocation.processStarted =
false`, `exitCode = null` (**not** `0`, FR-003), `commandLine` = attempted command,
`standardError` = the launch error (R4).

## Scenario D — Three-projection parity (US2, SC-003)

```bash
fsgg-sdd scaffold --provider fixture --json  > out.json
fsgg-sdd scaffold --provider fixture --text  > out.txt
fsgg-sdd scaffold --provider fixture --rich  > out.rich   # non-interactive ⇒ zero ANSI
```

Assert all three carry the same four facts (command line, stdout, stderr, exit code); `out.rich`
== `out.txt` (rich degrades to plain text, zero ANSI) and the JSON bytes are unchanged by the
rich path. Covered by `ScaffoldParityTests.fs`.

## Scenario E — No noise on success or pre-invocation error (US3, SC-004)

- Success (`ok` fixture): JSON `scaffold.providerInvocation` is `null`; no stdout/stderr
  content; success contract byte-stable except that additive null; exit **0**.
- `providerMissing` (no `--provider`): exit **1**, input diagnostic only, no
  `providerInvocation` content (the provider was never run).

## Scenario F — Bounded output + truncation (edge case, SC-005)

Fixture provider prints > 64 KiB to a stream. Assert the captured stream is ≤
`providerOutputCapChars` (65 536) and its `…Truncated` flag is `true`.

## Scenario G — SDD-tree intrusion still surfaces output (edge case)

Fixture `writes-into-fsgg`: exit **2**, `scaffold.providerWroteSddTree` remains the primary
diagnostic, **and** `providerInvocation` is present for consistency.

## Scenario H — Provenance untouched (FR-010)

After any provider failure, assert `.fsgg/scaffold-provenance.json` still parses as schema
**v1** and contains **no** captured-output keys (guard test).

## Determinism (FR-009)

A controlled fixture emitting a fixed marker + fixed non-zero exit yields a byte-stable
JSON/text scaffold block (including truncation flags); rich is excluded from goldens.

See [contracts/scaffold-report-provider-output.md](./contracts/scaffold-report-provider-output.md)
and [contracts/provider-invocation-capture.md](./contracts/provider-invocation-capture.md) for
the field/behavior contracts; [data-model.md](./data-model.md) for the entities.
