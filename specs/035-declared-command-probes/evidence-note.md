# Evidence & Principle V Note — Declared-or-Default Acceptance Build/Run Probes

**Principle IV (idiomatic simplicity).** Plain F#: one record per entity
(`DeclaredCommand`, `ProbeCommand`), one pure resolver per probe
(`resolveBuildCommand` / `resolveRunCommand`), and one deterministic discovery
helper (`discoverRunnableProject`). No custom operators, SRTP, reflection, or
computation expressions.

**Principle V (MVU boundary).** The probes live at the **test edge**, not in a
lifecycle MVU loop (matching today's harness). The change keeps I/O at the
existing edge — `startProcess` / `runToCompletion` for the build path and the
`runWithGrace` grace/overall loop for the run path — and extracts the decision
into a **pure** resolver (`declared-or-default → ProbeCommand`), which is the
tested transition. Per Principle V, simple selectors/validators do not need MVU
ceremony; no new MVU scaffolding is warranted.

**Principle II / III (visibility).** The acceptance project declares **no
`.fsi`** (established harness pattern; Principle III's signature-file obligation
applies to public `FS.GG.SDD.*` package modules, and this feature adds none).
`data-model.md`'s `val` signatures are the advisory contract.

**Test evidence (Principle VI).** New offline facts in `ProbeResolutionTests.fs`
fail-before / pass-after:

- Pure resolver, no process spawned (SC-002): default branch is `dotnet build` /
  `dotnet run --project <discovered>`; declared branch invokes the declared
  command, never `dotnet`; blank executable → default (FR-010).
- Deterministic ordinal-first discovery; empty product → `None` → diagnosed
  not-started run (FR-008).
- Real-edge execution of **synthetic** commands (generic platform tooling only —
  `true`/`false`/`sleep`, no provider/template/package/path/docs token): clean
  exit, non-zero exit, missing executable (could-not-start), and timeout-kill at
  a short bound (FR-003/FR-006/FR-007/SC-005).
- Defaults carry only the generic `dotnet`/`build`/`run`/`--project` token set
  (FR-009/SC-003); the standing "acceptance project carries no Governance
  reference" invariant still passes.

**Synthetic-evidence disclosure (Principle V).** The end-to-end edge facts use
synthetic declared commands built from generic, platform-standard POSIX tooling
(`true`, `false`, `sleep`) standing in for a provider-declared build/run command;
no real provider is involved. Disclosed in-test by the `Synthetic` token in the
fact names. The real-provider composition path remains network-gated on
`FSGG_SDD_ACCEPTANCE_REGISTRY` and is unchanged by this feature.
