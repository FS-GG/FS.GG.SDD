# Contract: Parameter-forwarding invariant

Pins how SDD forwards `--param` to the provider invocation for the `lifecycle=sdd` shape.
This documents the behavior at `HandlersScaffold.fs:84-96, 175-178`. The verification
surfaced a defect in the original wire form (`-p:k=v`, which `dotnet new` SDK 10 mis-parses);
the corrective change forwards each pair as `--<key> <value>` instead (research Decision 8).
The forwarding *invariant* below is unchanged — only the per-pair wire form is now `--k v`.

## The forwarded set

Let:
- `D` = provider-declared default parameters (from `.fsgg/providers.yml`).
- `A` = author `--param key=value` pairs.
- `effective = D ⊕ A` — author values overlay defaults (a `Map<string,string>`).

The provider create invocation MUST receive exactly:

```
dotnet new <templateId> -o . <--k v for each (k,v) in effective, k sorted> [--force]
```

## Invariants (asserted)

| ID | Invariant | How asserted |
|---|---|---|
| F1 (FR-002, US1.1) | `lifecycle=sdd` arrives at the child **verbatim** | recording fixture's `scaffold-manifest.txt` contains `lifecycle=sdd` after a real run |
| F2 (FR-003, SC-001, US1.3) | forwarded `--k v` set **equals** `effective` — no added, dropped, renamed, or reinterpreted key/value | dry-run inspection of the planned `RunProcess` create-arg vector |
| F3 (FR-008, order) | forwarded set is **independent of author `--param` order** | supply `--param` in two orders → identical create-arg vector |
| F4 (FR-007, US3.2) | forwarding is **value-agnostic** — an arbitrary `lifecycle` value behaves identically to `sdd` | run with `lifecycle=<nonce>`; outcome/vector/provenance shape identical modulo the echoed value |

## Out of scope (unchanged)

- Provider-side interpretation of `lifecycle` values (`spec-kit|sdd|none`) — owned entirely
  by the provider/template; SDD never models it.
- Install/update steps and `--force`/`--no-update` semantics — unchanged from 030.
