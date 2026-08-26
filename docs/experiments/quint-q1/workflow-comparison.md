# Same-corpus workflow comparison

## Method

The candidate does not execute prompt text as authority. Instead, Q1 turns the adopted portions of
`quint-lang`, `quint-modeling`, and `quint-execute-spec` at commit
`cc75369f741af7d490936f82002c2d28e3b3d78d` into an explicit pinned-kit command sequence: isolated
extraction, typecheck, named examples, and seeded simulation. The FS-GG-minimal sequence runs those same
steps without the upstream orchestration vocabulary. The harness validates the guidance checkout,
tracked-tree receipt, and three skill-document digests before either sequence; declaring `latest` is an
independent required refusal.

Both sequences consume separately extracted modules. The harness compares all three generated modules
byte-for-byte before execution and uses the same Quint binary, Rust evaluator, seeds, bounds, examples,
and invariants. Safety and temporal model checking then run once over those agreed bytes because repeating
the same model checker over identical input adds runtime, not independent evidence.

## Observations

| Measure | Pinned-kit sequence | FS-GG-minimal sequence | Result |
|---|---|---|---|
| Canonical corpus | Three Markdown slices | Same three slices | Exact source-digest agreement |
| Generated modules | 3 separately extracted files | 3 separately extracted files | Byte-identical |
| Fast tier | 3 typechecks, 4 explicit named examples, 1 seeded coordination run | Same | All pass |
| Stable labels | Upstream guidance adapted to catalogue IDs | Catalogue IDs directly | Same executable identities |
| Semantic authority | None; guidance only | Literate source/profile | Minimal sequence is authoritative candidate |
| Additional dependency | 37.7 MB guidance checkout | None after profile authoring | Reject runtime dependency on the kit |

A complete repaired run, including both fast sequences, three seeded domain runs, three Apalache safety
checks, one TLC temporal check, schema/semantic validation, and 19 mutations, took 25.9 seconds on the
recorded Linux x86-64 host. This is one observation, not a budget or service promise.

## Diagnostics and semantic diffs

Missing, reordered, duplicate, and stale extraction mutations now report the Markdown filename and line
binding (or the exact stale source path) before Quint runs. Named-example failures retain the example name;
Quint's sampled invariant diagnostic is generic, so the harness preserves it and appends the exact
catalogue binding and mutant source location. Schema errors report JSON instance paths and keywords;
semantic contract errors name the binding class.

The literate diff is the useful review surface: prose, catalogue rows, executable transition, property,
and example stay adjacent. The generated-module diff is useful as a deterministic corroboration and for
locating extractor drift, but is not independently editable authority. Q1 found this distinction useful:
an independent critic traced missing `phase`/`responseLost` catalogue reads directly from the literate
diff, while the generated diff confirmed the repair changed only the intended catalogue/model bytes.
Raw compiler IDs and a serialized expression tree added no review value and remain rejected.

## Readability result

The first domain/readability review, performed without author explanation at producer commit `7829475`,
correctly found omitted catalogue reads/writes, a missing stable-action name, absent promised mutations,
and insufficient diagnostic evidence. The first architecture/tooling review independently found the
unbound model-checker cache, shallow schema validation, missing guidance substitution refusal, and an
incomplete cross-repo seal. Those are concrete readability/traceability measurements, not acceptance.
This repaired generation requires fresh successor reviews; the verdict remains pending until they accept
the new exact bytes.
