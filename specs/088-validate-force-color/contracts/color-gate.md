# Contract: Color/TTY Gate with Force-Color Override

**Applies to**: every `--rich`-capable command (command reports via `resolve`, the validation-report via `resolveValidation`), through the shared `Rendering.detectCapabilities`.

## Inputs

- `outputRedirected: bool` — redirection state of the sink the report routes to (stdout for normal reports, stderr for the Blocked path).
- `forceColor: bool` — `forceColorRequested args = forceColorEnv() || (args contains "--force-color")`.
- Environment: `NO_COLOR` (presence, any value), `TERM` (`"dumb"`), `FORCE_COLOR` (boolean-ish).

## `FORCE_COLOR` interpretation (boolean-ish)

| `FORCE_COLOR` value | forces color? |
|---------------------|---------------|
| unset (`null`)      | no |
| `""` (empty)        | no |
| `"0"`               | no |
| any other value (`"1"`, `"true"`, `"always"`, …) | yes |

`NO_COLOR` keeps standard semantics: present with **any** value (including empty) disables color.

## Effective capabilities (computed by `detectCapabilities forceColor outputRedirected`)

```
IsInteractive = (not outputRedirected) || forceColor
ColorEnabled  = (not NO_COLOR-present) && ((TERM <> "dumb") || forceColor)
Width         = if outputRedirected then None else Some Console.WindowWidth   // raw redirect
```

## Gate (unchanged predicate)

Rich ANSI is emitted **iff** `IsInteractive && ColorEnabled`; otherwise output degrades to the zero-ANSI `renderText` projection.

## Precedence (normative)

`NO_COLOR` > force-color > capability sensing (redirected / `TERM=dumb`).

| redirected | TERM=dumb | NO_COLOR | force | rich emitted? |
|:---:|:---:|:---:|:---:|:---:|
| no  | no  | no  | –   | yes (baseline) |
| yes | no  | no  | no  | no (baseline degrade) |
| yes | no  | no  | yes | **yes** |
| yes | yes | no  | yes | **yes** (force overrides dumb) |
| yes | no  | yes | yes | **no** (NO_COLOR wins) |
| no  | yes | no  | no  | no (baseline) |

## Invariants

- Force-color affects **only** whether the rich projection emits ANSI. JSON/text/Markdown bytes, exit code, and stdout/stderr routing are identical with or without a force-color signal.
- The predicate `IsInteractive && ColorEnabled` in `resolve`/`resolveValidation` is byte-identical before/after this feature (all new logic is inside `detectCapabilities`).
- Non-rich projections never contain ANSI regardless of force-color.
