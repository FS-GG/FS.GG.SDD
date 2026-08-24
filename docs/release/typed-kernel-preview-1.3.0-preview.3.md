# Typed specification kernel preview.3

`FS.GG.SDD.Artifacts 1.3.0-preview.3` fixes the real-consumer Fable boundary found by
S.I.R. P3. The package now carries `fable/FS.GG.SDD.Artifacts.fsproj` and the
package-owned portable typed-kernel source used by Fable compilation.

The portable projection covers specification identity, provenance, evidence
obligations, validation, normalization, SHA-256 fingerprinting, compilation, and
semantic diff. JSON codec, generated-view IO, and requirements Markdown migration
remain the net10.0 surface; their existing behavior and public API are unchanged.

Compatibility receipts require:

- exact normalized-byte and fingerprint equality between .NET and Fable;
- a clean packed-package Fable consumer, with no producer checkout reference;
- `FS.GG.Contracts 7.5.2` as the exact package dependency;
- no S.I.R., gameplay, or consumer source in the packed Fable tree;
- fresh-cache locked restore from the public package feed.
