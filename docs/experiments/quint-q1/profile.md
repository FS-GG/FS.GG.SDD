# Proposed FS-GG Quint profile 1 (Q1 candidate)

Status: non-production experiment. This profile has no authority until Q1 and the post-Q1 ADR-0077
amendment are independently accepted, Q2 implements it, and Q3 publishes it.

## Exact candidate identities

| Component | Identity |
|---|---|
| Literate extractor | `driusan/lmt@62fe18f2f6a6e11c158ff2b2209e1082a4fcd59c` |
| Extractor build compiler | `go1.24.1 linux/amd64`, archive SHA-256 `cb2396bae64183cdccf81a9a6df0aea3bce9511fc21469fb89a0c00470088073` |
| Extractor candidate binary | Linux amd64 SHA-256 `37e0b0365c2641edce40b48605471f61fa12e97c3e2376152f0e849abdc31f10` |
| Quint | `v0.32.0`, Linux amd64 SHA-256 `939b64095b706017f2f202c6f99c860c40be7c31bddc2b98557316e50f42cd7f` |
| Rust evaluator | `v0.6.0`, archive SHA-256 `61755a09d5052d93a4e75e840059edfd0d3674aeda164b9d2464be3d6e21b1c2`, binary SHA-256 `b2efdeac5713d153e41bf2143b94ed75d888fdd5637f4a5d61a04c695313510a` |
| JRE | Eclipse Temurin `21.0.9+10`, Linux amd64 JRE archive SHA-256 `aeab55d064a1a27a3744b0880b9b414077b4ed2b1790817eea3df60aec946431`, `java` binary SHA-256 `e865867065e48928c58293f30e7ae26a79c842f8607fa51d7e2e9fb90b602786` |
| Apalache | `v0.56.1`, archive SHA-256 `a61c07569d7195ddc589f01037fa10fafef4fb0796af2f1c9cb45226375dfbfc`, extracted-tree receipt `3466d07f06d7ac80ee0f171a96383183cee9d91bf1b5995d897d4f15c004569f`, jar SHA-256 `4753c0ebb2cbb266e2c6ac19ab5ca3827d726cc80fd1fc5d7c1eeb64736cd60b` |
| Guidance corpus | `quint-co/quint-llm-kit@cc75369f741af7d490936f82002c2d28e3b3d78d`, tracked-tree receipt SHA-256 `68a11d403846de3af26759eef97f4a35eff5e71d561d41ea17d96e535c171556` |

`lmt` has no tagged release and no Go module declaration at the selected commit. Q1 can reproduce its
single-file build but does not claim a hermetic dependency contract. Q2 must either package this exact
reviewed source plus Go closure or refuse it. A moving `go install ...@latest` is never admitted.

## Canonical literate source

- One UTF-8 Markdown document is canonical for each slice.
- An ordered code fence begins exactly with `````quint <relative-target>.qnt +=``. The target is a plain
  filename with no directory separator, `.` segment, absolute form, or duplicate module declaration.
- The committed fence manifest fixes document order, fence order, target, and source SHA-256.
- `lmt` runs in a fresh isolated directory containing only declared documents. Generated modules are
  compared with a second clean extraction, consumed by Quint, and discarded.
- Missing, reordered, duplicate, unexpected-target, stale-source, and hand-edited-output cases fail before
  a result is accepted. `lmt` warnings are errors.
- The Markdown line is the source location during Q1. Q2 must qualify a deterministic source-map codec;
  raw compiler source IDs are never stable FS-GG identities.

## Closed demonstrated language subset

Allowed because all three Q1 slices exercise them: modules/imports, aliases/records/enums, `int`/`bool`/
`str`, finite `Set` and `List`, pure values/functions, state variables, guarded actions, `all`/`any`,
bounded nondeterministic choice, action composition in `run`, state invariants, and one temporal property.
Finite simulation bounds and seeds are verification inputs, not production-domain constants.

Everything not listed is unsupported by profile 1. In particular, macros/code generation beyond `lmt`
tangling, dynamic imports/targets, compiler-node-derived IDs, foreign execution, raw IR exposure,
unbounded external data, Choreo, and arbitrary expression serialization fail closed. Adding one is profile
growth requiring new fixtures and compatibility judgement.

## Catalogue and authority rules

- Stable IDs are explicit typed string fields in catalogue records. They are unique within `(kind, id)`
  and must match `^[A-Z][A-Za-z0-9]*(?:[-.][A-Za-z0-9]+)*$`.
- Every prose-named requirement, action, invariant, evidence obligation, implementation binding, external
  subject, and verification profile has exactly one catalogue row.
- Ordered collections use `List`; semantic sets use `Set` and compile in lexicographic canonical order.
- Prose may explain or navigate catalogue entries but may not add behavioral meaning.
- The compiled contract records stable relationships and digests only. Quint remains behavioral authority.

## Verification tiers

1. `typecheck` and profile/contract validation for every semantic change.
2. Named `test` and seeded Rust `run` for calculations/actions.
3. Bounded Apalache/TLC verification for affected invariant, temporal, dispatcher, bound, compiler, or
   profile changes. Q1 records the exact separate toolchain requirement; Q2 owns its hermetic cache.
4. Consumer-owned ITF/runtime replay when the impact graph reaches a bound implementation.

An unavailable model checker is distinct from a pass. It cannot be replaced by more simulation.
