# Phase 0 Research: Declared-or-Default Acceptance Build/Run Probes

All Technical Context unknowns were resolvable from the existing harness and the
spec; there are no open `NEEDS CLARIFICATION` items. The decisions below pin the
design choices the plan depends on.

## D1 — Declared-command read shape (forward-compatible with H2)

- **Decision**: Model a declared command as a record
  `DeclaredCommand = { Executable: string; Arguments: string list }` consumed as
  `DeclaredCommand option`. The probe always runs with the **product root** as the
  working directory (FR-003); the working directory is not part of the declared
  shape in this feature.
- **Rationale**: The existing edge (`startProcess` / `runToCompletion` and
  `CommandEffects.RunProcess`) already takes `(fileName, args: string list,
  workingDir)`. An `(executable, args)` record is the structured pre-tokenized
  form `dotnet new`/`Process.Start` consume, so resolving to it avoids shell
  tokenization ambiguity entirely. The H2 `ProviderDescriptor`
  (FS-GG/FS.GG.SDD#8) will add build/run command fields; mapping any reasonable
  `{ command/executable, args }` descriptor field into this record is a direct
  read with **no probe rewrite** (SC-004). The probe parameter is `option`, so
  the only state reachable today (`None`) is the default.
- **Alternatives considered**:
  - *Single command string, tokenized by the harness* — rejected: re-introduces
    shell-quoting rules and a parser the edge does not need, and is a worse match
    for a structured descriptor field.
  - *Adding a working-directory field now* — rejected: FR-003 fixes the working
    directory at the product root; speculative fields violate idiomatic
    simplicity (constitution IV). Add it only if/when H2 declares it.

## D2 — Pure resolver + existing edge (no new MVU loop)

- **Decision**: Add pure functions `resolveBuildCommand` and `resolveRunCommand`
  that return a `ProbeCommand = { Executable: string; Arguments: string list;
  WorkingDirectory: string }`, and keep `startProcess` / `runToCompletion` / the
  run-probe grace loop as the I/O edge. `buildProbe` / `runProbe` gain an optional
  declared-command parameter, resolve it, then hand the `ProbeCommand` to the
  unchanged edge.
- **Rationale**: Separating the *decision* (declared-or-default, discovery,
  empty-normalization) from the *I/O* makes the decision unit-testable **without
  spawning any process** — the core of SC-002 ("a test that never starts a
  `dotnet` process for the declared case"). It honors constitution V (I/O at the
  edge) while avoiding MVU ceremony the test-edge probes don't need (principle V
  exempts simple selectors/validators).
- **Alternatives considered**:
  - *Route probes through the lifecycle MVU `RunProcess` effect* — rejected: the
    acceptance probes are deliberately at the test edge, not in the scaffold MVU
    loop; threading them through `CommandEffects` would couple harness timing
    (grace window, kill-on-survive) into the lifecycle interpreter for no benefit.
  - *Inline the branch inside `buildProbe`/`runProbe`* — rejected: forces process
    spawning to test the decision, weakening the SC-002 guarantee.

## D3 — Empty/whitespace declared command → default (FR-010)

- **Decision**: The resolver treats `None` **and** a `Some` whose `Executable` is
  null/empty/whitespace as "no declared command" and returns the default. (Args
  are irrelevant when there is no executable.)
- **Rationale**: A blank executable can never launch a real process; treating it
  as default (not as an attempt to start `""`) prevents a spurious cannot-start
  failure and matches FR-010 and the registry-gating precedent
  (`registryPath` already trims+filters empty env values).
- **Alternatives considered**: *Reject blank as a config error* — rejected: the
  spec explicitly requires fall-through to default, and "absent" and "blank" are
  indistinguishable provider intent.

## D4 — Deterministic runnable-project discovery for the default run probe (FR-008)

- **Decision**: The default run command becomes
  `dotnet run --project <discovered>` where `<discovered>` is selected by
  enumerating `*.fsproj` and `*.csproj` under the product root, taking the
  forward-slash relative paths, **sorting with ordinal string order**, and
  choosing the first. No runnable project found ⇒ the run probe returns a
  diagnosed, non-zero, **not-started** `ProbeResult` (never a silent pass, never a
  hang).
- **Rationale**: Ordinal-sorted-first is fully deterministic ("same product →
  same target", FR-008) and needs no project-file XML parsing. The reference
  provider produces a single runnable product (spec Assumptions), so first-sorted
  is that product; for the multi-project edge case the choice is reproducible. The
  shift from `dotnet run --no-build` at the root to `dotnet run --project <p>` is
  treated as observably equivalent for a single-runnable product (spec
  Assumptions; FR-005 verified by SC-001).
- **Alternatives considered**:
  - *Parse `OutputType`/`IsPackable` to pick the "real" exe* — rejected for this
    feature: adds an XML parser and project-semantics knowledge for no observed
    benefit on the single-runnable reference product; revisit only if a provider
    ships multiple exe projects.
  - *Keep `dotnet run --no-build` at the root* — rejected: the spec normalizes the
    default to the `--project` form so the declared and default paths share one
    invocation shape, and `--no-build` coupling to the prior build step is dropped
    in favor of an explicit target.

## D5 — Bounded execution parity for declared commands (FR-006 / FR-007 / SC-005)

- **Decision**: Declared commands flow through the **same** edge as the defaults:
  the build probe's 300 s completion timeout (`runToCompletion`) and the run
  probe's 10 s grace + 60 s overall cap with kill-on-survive. Cannot-start,
  non-zero exit, and timeout each yield a distinct diagnosed non-zero
  `ProbeResult` (the edge already encodes these three shapes).
- **Rationale**: Because resolution produces a `ProbeCommand` the edge already
  knows how to run, declared and default commands are bound-for-bound identical by
  construction — no second timeout path to keep in sync. Satisfies FR-006/FR-007
  and guarantees no hang (SC-005).
- **Alternatives considered**: *Separate timeout budget for declared commands* —
  rejected: divergent bounds invite drift and contradict "same bounded execution
  semantics as the defaults" (FR-006).

## D6 — Offline test strategy for the declared path (SC-002), provider-agnostic (FR-009)

- **Decision**: Cover the declared path in the **default offline inner loop** two
  ways: (a) pure-resolver assertions over `resolveBuildCommand` /
  `resolveRunCommand` / `discoverRunnableProject` (declared-beats-default,
  empty→default, deterministic discovery, no-runnable→diagnosed) that spawn
  nothing; and (b) one execution test that runs a **synthetic** declared command
  which exits deterministically through the real edge, asserting the resulting
  `ProbeResult`. The synthetic command uses only generic, platform-standard
  tooling — never a provider/template/package/docs identifier (FR-009) — and is
  disclosed as synthetic in the test name (constitution VI).
- **Rationale**: The pure-resolver tests are the literal SC-002 evidence (no
  `dotnet` process for the declared case) and run anywhere with no network. The
  single execution test proves the edge honors the resolved declared command
  end-to-end (FR-003) under the real timeout machinery. Keeping both offline means
  the network-gated `FSGG_SDD_ACCEPTANCE_REGISTRY` path is untouched by this
  feature's new coverage.
- **Alternatives considered**:
  - *Only spawn-based tests* — rejected: weaker SC-002 evidence and platform-shell
    fragile; the resolver assertion is the strongest, cheapest proof.
  - *Drive the declared path through the real provider* — rejected: no provider
    declares a command yet (H2 is out of scope), and it would couple the test to
    network gating.
