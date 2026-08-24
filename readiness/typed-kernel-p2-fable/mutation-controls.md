# Gate inversion receipt

The failing control is the already-published preview.2 package, which is identical in
typed-kernel behavior but lacks the two `fable/` entries added by this change.

- Preview.2 clean S.I.R. Fable compile: red.
- Stable findings: missing `System.Text.Json` reference, missing
  `SpecificationModel`/`SpecificationDiagnostic`/`SpecificationProvenance` constructors,
  and incomplete `ExtensionContract` construction.
- Preview.3 candidate with package-owned Fable project/source: green.
- Packed-path regression test explicitly requires both entries and rejects consumer/gameplay
  namespace markers. Removing either entry makes
  `artifacts package carries a producer-owned portable Fable kernel` fail.

This is the production escape inverted directly: a package without the Fable projection
cannot compile the real consumer, while the repaired package compiles and matches .NET bytes.
