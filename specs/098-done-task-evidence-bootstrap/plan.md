# Plan: Done-task evidence bootstrap

The change stays inside the existing pure command planner. `HandlersEvidence` filters exactly the
bootstrap diagnostic at the authoring boundary, then reuses the existing obligation derivation,
hybrid no-clobber merge, write effects, evidence disposition gate, and observed-run machinery.

No public F# API or `.fsi` changes. No schema or artifact-layout change. `evidence.yml` remains the
authored machine contract, schema v1. The MVU effect boundary is unchanged: the pure planner still
requests the same `WriteFile` effects and the interpreter remains untouched.

Verification:

1. Add one command-level lifecycle test that reproduces analyze → implement/done → first evidence.
2. Prove task bytes are preserved and the initial scaffold does not pass verify.
3. Author real verification declarations, materialize their cited test, register a passing TRX, and
   prove verify advances only then.
4. Re-run the receipt path and assert byte idempotence.
5. Run the focused Commands test project, seeded-skill parity/drift tests, and the solution gate.

Agent guidance remains one contract: edit the authored Claude skill bodies and their byte-identical
Codex copies together; the CLI embeds the Claude bytes and drift tests pin parity.
