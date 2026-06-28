# Feature 042 — `fsgg-sdd registry validate` smoke evidence

CLI: `dotnet run --project src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -- registry validate <path>`

| Scenario | Spec ref | Result |
|---|---|---|
| 1. Canonical fixture → valid, 0 diagnostics | SC-001/FR-008 | exit 0 ✓ (scenario1-canonical.json) |
| 3. Broken copy (bad version + ghost consumer + bad dep) | SC-002/FR-006 | exit 1, 3 diagnostics in document order ✓ (scenario3-broken.json) |
| 4. Missing / non-YAML file → single load diagnostic, no crash | FR-001/US1-S3/Const.VIII | exit 1 ✓ (scenario4-loadfail.json) |
| 5. Two --json runs byte-identical | SC-004/FR-007 | DETERMINISTIC ✓ |
| --text / --rich projections (rich degrades to plain text non-interactive) | CLI output contract | ✓ |

The canonical file validating clean is the combined demonstration of US1 (path-in/verdict-out),
US2 (repo-id edges / owner `github` / coherence not flagged), and US3 (bare-integer `1`/`2`,
prerelease `0.1.52-preview.1`, `1.x` range accepted) on the real registry — closing FS.GG.SDD#12.
