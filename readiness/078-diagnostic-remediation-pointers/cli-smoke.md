# US1 vertical-slice evidence — feature 078 (#125)
# Real CLI run: fsgg-sdd specify (no --input) -> covered diagnostic missingSpecificationIntent
# Captured 2026-07-05T10:58:34Z via src/FS.GG.SDD.Cli (Debug).
# Recaptured 2026-07-18 for FS.GG.SDD#539: the pointer now targets the vendored fs-gg-sdd-* skills
# (present in every scaffold) instead of tool-repo-only docs/ paths.

## --json correction (the automation contract; carries the remediation pointer):
"correction": "Provide --input with labeled facts, one per line: \\u0022value: \\u003Cuser value\\u003E\\u0022, \\u0022scope: \\u003Cscope\\u003E\\u0022, \\u0022requirement: \\u003Cmeasurable requirement\\u003E\\u0022. See the fs-gg-sdd-specify skill and the grammar under the fs-gg-sdd-authoring-contracts skill (#3-specify---input-intent-facts-used-by-specify)."

## non-covered control — outsideProject correction stays byte-identical (no pointer):
"correction": "Run fsgg-sdd init or pass --root for an initialized SDD project."
