# US1 vertical-slice evidence — feature 078 (#125)
# Real CLI run: fsgg-sdd specify (no --input) -> covered diagnostic missingSpecificationIntent
# Captured 2026-07-05T10:58:34Z via src/FS.GG.SDD.Cli (Debug).

## --json correction (the automation contract; carries the remediation pointer):
"correction": "Provide --input with labeled facts, one per line: \u0022value: \u003Cuser value\u003E\u0022, \u0022scope: \u003Cscope\u003E\u0022, \u0022requirement: \u003Cmeasurable requirement\u003E\u0022. See the shipped example docs/examples/lifecycle-artifacts/spec.md and the grammar at docs/reference/authoring-contracts.md#specify---input-intent-facts."

## non-covered control — outsideProject correction stays byte-identical (no pointer):
"correction": "Run fsgg-sdd init or pass --root for an initialized SDD project."
