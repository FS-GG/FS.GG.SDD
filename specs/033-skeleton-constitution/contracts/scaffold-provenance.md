# Contract: scaffold delivers the constitution outside app-only provenance

**Scope**: behavior contract over the existing `scaffold` command — no provider-contract,
invocation-protocol, or provenance-schema change.

## Guarantees

1. **Delivered via reused, unchanged `init` effects (FR-004)**. `scaffold` lays the skeleton by
   replaying `initEffects`; because the constitution is now part of that list, a successful
   `fsgg-sdd scaffold --provider <name>` produces `.fsgg/constitution.md` in the product with
   **no** scaffold-specific constitution logic and **no** change to the provider contract or
   invocation protocol.

2. **Excluded from `generatedProduct` (FR-005)**. `.fsgg/constitution.md` MUST NOT appear in the
   `generatedProduct` paths of `.fsgg/scaffold-provenance.json`. This holds structurally:
   `produced = after − before − skeletonFiles − provenance` (`HandlersScaffold.fs:308-310`), and
   `skeletonFiles` is derived from `initEffects` `WriteFile` paths (`HandlersScaffold.fs:77-82`),
   so the new path is subtracted in the same change that adds it.

3. **Provenance schema & bytes unaffected**. `scaffold-provenance.json` stays schema v1; only the
   *set* of `generatedProduct` paths is governed, and the constitution is absent from it. The
   app-only produced set (e.g. `App.fsproj`, `Program.fs`) is unchanged.

4. **Skeleton byte-identical to `init` (FR-006)**. The constitution emitted on the scaffold path
   is byte-identical to the one emitted by `init`, like every other skeleton file.

## Verification (real-filesystem, public surface)

- **US2-AC1**: scaffold with a test provider + `lifecycle=sdd` into a temp dir ⇒
  `.fsgg/constitution.md` is present; the scaffold report attributes it to the SDD skeleton, not
  the provider.
- **US2-AC2**: read `generatedProduct` from the resulting `scaffold-provenance.json` ⇒
  `.fsgg/constitution.md` is **absent**.
- **Regression (free)**: the existing `ScaffoldCommandTests.fs:442-469`/`:474-492`/`:498-509`
  (dynamic skeleton enumeration, byte-identity, determinism) keep passing unchanged — their
  hardcoded app-only produced set does not include the constitution, which is the FR-005 proof.

## Non-guarantees

- The provider's own `lifecycle`-gated `.specify/` emission (Rendering-owned) is out of scope and
  unchanged. The SDD constitution is emitted by the always-run `init` effects regardless of the
  provider's `lifecycle` parameter.
