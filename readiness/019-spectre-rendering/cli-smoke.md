# CLI smoke — feature 019 (Rich Spectre.Console rendering)

Disposable project; `fsgg-sdd` Release build. Quickstart scenarios 2–6.

## Scenario 4 — default JSON == explicit --json (SC-002)
```
diff a.json b.json -> IDENTICAL (exit 0)
```

## Scenario 2 — --rich redirected == --text, zero ANSI (SC-003)
```
diff rich.txt text.txt -> IDENTICAL
ESC sequences in rich.txt: 0
```

## Scenario 3 — NO_COLOR --rich, zero ANSI (SC-003)
```
ESC sequences in nocolor.txt: 0
```

## Scenario 6 — Blocked outcome routes to stderr, exit unchanged (SC-005)
```
exit code: 1   stdout bytes: 0   stderr bytes: 126
stderr first line: command: verify
```

_Interactive (TTY) rich rendering is captured separately in `fsi-rich-surface.txt`
and asserted by 32 `FS.GG.SDD.Cli.Tests` against a real Spectre console._
