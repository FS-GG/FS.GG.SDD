# Plan — Reliable scheduled real-provider acceptance

Use the existing repository secret as the scheduled lane's read-only registry source and remove the
workflow-only neutral-skip branch. The existing resolver already fails closed when all sources are
absent, so both materialization and the test step can run unconditionally for every supported event.
Manual input and repository-dispatch content keep their existing precedence over the secret.

Add an offline workflow-contract test beside the acceptance resolver tests. The test reads the exact
workflow shipped by the repository and proves:

- the nightly schedule and secret binding remain present;
- no preflight output can bypass materialization or the real-provider tests;
- the local `RequiresRegistryFact` still advertises an explicit skip when the registry capability is
  absent;
- the default output directory has a valid identifier shape, avoiding stochastic failures when the
  provider derives a product name from that directory.

The repository secret is operational configuration rather than committed source. Provision it from
the current external registry, then verify the merged workflow through a manual dispatch with no
input so the secret fallback and all five real-provider facts run together.

Run the default provider build probe with single-worker, no-build-server flags. This preserves
provider-declared commands verbatim while preventing persistent MSBuild/compiler descendants from
holding redirected output pipes after the top-level build process exits.

No `.fsi`, public-surface baseline, generated artifact, migration note, package version, or package
release changes.
