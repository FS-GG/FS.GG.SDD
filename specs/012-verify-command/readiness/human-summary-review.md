# Human Summary Review — Verify Command Text Projection

The `--text` projection is rendered from `CommandReport` only (no new facts).
Captured CLI text output (`cli-text-smoke.txt`) includes:

```
command: verify
verificationReadiness: verificationReady
nextAction: verify.next.ship
```

The renderer (`CommandRendering.renderText`) emits, for the verification block:
work id, verify path, readiness, ready/advisory/warning/blocking finding counts,
evidence disposition counts (supported/deferred/missing/stale/synthetic/invalid),
test disposition counts (satisfied/deferred/missing/stale/invalid), and skill
visible/missing counts. Every rendered fact has a corresponding field in the JSON
verification summary (`CommandSerialization.writeVerification`), so the text view
introduces no facts absent from the JSON report.

Evidence: `verify text projection uses report facts` and
`verify CLI text smoke renders human projection` (both passing in
`command-verify-tests.txt`), plus `cli-text-smoke.txt` / `cli-json-smoke.txt`.
