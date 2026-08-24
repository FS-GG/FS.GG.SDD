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

The independent review also found that preview.3's first portable implementation returned no
diagnostics for invalid evidence receipts. The clean package consumer now executes the same invalid
receipt set through .NET and Fable and requires the exact four diagnostic codes from both runtimes.

- Green repair: both runtimes report zero satisfied obligations and
  `SPEC-EVIDENCE-MISSING,SPEC-EVIDENCE-DUPLICATE,SPEC-EVIDENCE-KIND,SPEC-EVIDENCE-REF-REQUIRED`.
- Mutation: rename the portable `SPEC-EVIDENCE-KIND` diagnostic while leaving .NET unchanged.
- Observed result: `tests/fixtures/typed-specifications/run-clean-consumer.sh` exits 1 and prints the
  exact .NET/Fable diagnostic diff. The gate therefore detects semantic drift rather than compilation
  alone.
