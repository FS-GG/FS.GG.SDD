# Critic tool-version reconciliation

The round-one successor review used the source-built `FS.GG.SDD.Cli`
`1.3.0-preview.2` executable to evaluate lifecycle views whose committed generator
identity is `FS.GG.SDD.Artifacts/1.1.0`. That cross-generator run proposed expected
regeneration and was incorrectly classified as evidence that the committed views
were stale.

The governing installed tool is `/home/developer/.dotnet/tools/fsgg-sdd` version
`1.1.0`. In both the implementation worktree and a clean detached worktree at
`739dfeb2fab5a685ee1f2a82600824961f64ed1d`, that tool reports:

- `fsgg-sdd analyze --work 912-typed-kernel-preview2 --dry-run`: `noChange`,
  coherent, zero diagnostics;
- `fsgg-sdd verify --work 912-typed-kernel-preview2 --dry-run`: `noChange`,
  coherent, zero diagnostics;
- `fsgg-sdd ship --work 912-typed-kernel-preview2 --dry-run`: `noChange`,
  coherent, zero diagnostics.

The committed work-model digest is
`6b5581cfdc23e2f378f3b6287eafc186e8dc7cf2450e0fab16e736c2b193cdab`.
The committed analysis digest is
`f962b5d0268cf4dd1f817b1ade6a25a6ccfef6064286a80ba3058aa4671336f4`,
and its source snapshot binds the exact work-model digest. The evidence source
snapshot binds that exact analysis digest. Verify and ship contain no diagnostics.

The successor critic confirmed that its worktree had no authored or generated
input differences and explicitly retracted the finding as tool-version skew. The
structured changes-required record remains immutable review history; this receipt
provides the evidence a fresh successor needs to supersede it on the next exact
head.
