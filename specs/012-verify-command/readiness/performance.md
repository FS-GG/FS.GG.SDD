# Verify Command Performance Evidence

Budget: each scenario completes under 2 seconds through the command test harness
on the local development machine.

| Scenario | Harness assertion | Result |
|---|---|---|
| `verify-create` | `verify create and rerun complete under local harness budget` asserts create < 2.0s | PASS |
| `verify-rerun-current` | same test asserts rerun < 2.0s | PASS |
| `verify-refreshes-work-model` | covered by the create path (work-model refresh planned with verify view) < 2.0s | PASS |

Evidence: `command-verify-tests.txt` (17 passed) and `full-suite.txt`
(235 passed). The performance assertions live in
`tests/FS.GG.SDD.Commands.Tests/VerifyCommandTests.fs`
(`verify create and rerun complete under local harness budget`).
