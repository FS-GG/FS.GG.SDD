---
name: fs-gg-sdd-typed-correspond
description: Trace accepted Typed SDD obligations to fingerprinted implementation, test, and evidence observations.
---

# Typed SDD correspond

Run `fsgg-sdd typed-sdd correspond --accepted <workspace.json> --contract <compiled-contract.json>
--observations <observations.json>`. Add `--changed <ID,ID>` for impact-derived selection and exactly one
of `--json`, `--plain`, or `--rich`; JSON is the default. Treat satisfied, missing, stale, contradicted,
ambiguous, unsupported, and unobserved as distinct outcomes. Correct forged fingerprints, incomplete
catalogues, duplicate observations, and malformed bindings before interpreting any entry: global
integrity findings always block, including selective runs. The report is derived evidence, never a
second editable coverage authority.
