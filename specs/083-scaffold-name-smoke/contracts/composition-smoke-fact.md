# Contract — Composition-Acceptance Smoke Fact (hyphenated scaffold name)

This feature adds a **gated test fact** to the composition-acceptance lane. It defines **no new
machine contract** — it consumes the existing scaffold command surface and the existing
`composition-acceptance-result` v1 document unchanged. This file fixes the fact's observable
contract so `/speckit-tasks` and review have a precise target.

## The gated fact

- **Selection**: carries `[<Trait("kind","composition-acceptance")>]` — selected by the
  existing workflow's `dotnet test --filter "kind=composition-acceptance"`. No new job.
- **Gating**: carries `[<RequiresRegistryFact>]` — statically skipped at discovery when
  `FSGG_SDD_ACCEPTANCE_REGISTRY` is unset/empty (offline inner loop stays green, no network).

### Inputs
- The external registry (verbatim `.fsgg/providers.yml`) named by
  `FSGG_SDD_ACCEPTANCE_REGISTRY` — the only channel carrying real provider identity.
- Fixed provider name token `rendering` (generic, author-supplied — not an identifier).
- Fixed hyphenated product-name value `Roquelike-DungeonCrawler`, forwarded on the key
  `resolveNameParameter descriptor` resolved from the registry descriptor.
- Generic lifecycle marker `lifecycle=sdd` (as the existing fixed request).

### Behavior contract
| Condition | Result |
|---|---|
| Registry env unset/empty | Fact **Skipped** at discovery; no result document written. |
| Provider available, scaffold succeeds, `dotnet build` exit 0 **and** `dotnet test` exit 0 | Fact **passes**; verdict `pass`. |
| Provider available, `dotnet build` exit ≠ 0 | Fact **fails** naming the build diagnostic (this is the `FS0010` hyphen-in-namespace regression class). |
| Provider available, build exit 0 but `dotnet test` exit ≠ 0 | Fact **fails** naming the test diagnostic. |
| Provider unreachable / cannot start | Verdict `skip-unavailable`; fact does **not** fail SDD. |

- Each probe is bounded (300 s via `runToCompletion`); a hung probe is killed and reported as a
  non-zero, timed-out failure (never a hang).
- An empty-but-green `dotnet test` (exit 0, zero tests) satisfies the pass condition.

### Non-goals / invariants
- **No provider identity in generic SDD**: the name-param key is resolved from the descriptor;
  no `productName`/rendering package/template/path/docs token appears in the acceptance
  project or its non-guard sources (extends the existing no-identity scan).
- **No schema change**: the `composition-acceptance-result` v1 document, golden, and
  determinism matrix are byte-unchanged.
- **No new CI job**: reuses `composition-acceptance.yml` unchanged (verify the `--filter`
  selects the new fact).

## The offline companion fact (always-on)

- **Purpose**: make the neutrality + request-shape invariant PR-visible (mirrors the existing
  `the fixed composition request passes no explicit starter parameter` companion).
- **Asserts**: over a synthetic registry declaring a `nameParameter`, the request builder
  forwards `(resolveNameParameter descriptor, "Roquelike-DungeonCrawler")` alongside
  `lifecycle=sdd`, and the acceptance sources name no rendering identity token.
- **Offline**: no network, no real provider; runs on every `dotnet test`.
