# Contract: Recording fixture provider (rendering-agnostic)

A **test-owned** `dotnet new` template that stands in for the real Rendering provider to
exercise the `lifecycle=sdd` composition shape deterministically. This is not a new product
interface; it is the conformance fixture published with the verification (FR-001).

## Identity & neutrality

- Lives under `tests/fixtures/scaffold-provider/lifecycle/`.
- Uses only neutral identifiers: template short name `fsgg-fixture-lifecycle`, registry
  provider name `fixture`, source via the `__FIXTURE__` absolute-path token.
- **MUST NOT** contain any real FS.GG.Rendering package id, template id, provider name, or
  docs URL (FR-001, SC-005). The leak-invariant scan enforces this.

## Declared symbols (`.template.config/template.json`)

| Symbol | datatype | `replaces` | default | required (registry) |
|---|---|---|---|---|
| `productName` | string | `PRODUCT_NAME` | `Product` | yes |
| `lifecycle` | string | `LIFECYCLE_VALUE` | (none) | no — `yes` in the `*-required` registry |

## Produced files (app-only tree)

| Path | Role |
|---|---|
| `App.fsproj` | app project stub; substitutes `PRODUCT_NAME` |
| `Program.fs` | app entry stub |
| `scaffold-manifest.txt` | **recording channel** — contains `lifecycle=LIFECYCLE_VALUE` (and `productName=PRODUCT_NAME`) so the forwarded value can be read back |

- All produced paths are outside any SDD-owned tree (`.fsgg/`, `work/`, `readiness/`, agent
  files) → recorded as `generatedProduct`.

## Behavior the fixture guarantees (so the verification can rely on it)

1. Given `-p:lifecycle=<v>`, `scaffold-manifest.txt` contains exactly `lifecycle=<v>` — the
   value passes through `dotnet new` substitution unchanged. (Backs the verbatim-arrival
   assertion; any SDD-side mangling shows up as a mismatch.)
2. Produces no file under any SDD-owned tree (so a clean run's provenance is purely app-only).
3. Deterministic: identical inputs → identical produced files (no clock, no randomness).

## Variant fixtures (edge cases, FR-008)

| Fixture dir | Behavior | Registry |
|---|---|---|
| `lifecycle/` | normal app-only tree | `lifecycle.providers.yml`, `lifecycle-required.providers.yml` |
| `lifecycle-empty/` | declares `lifecycle`, produces no files | `lifecycle-empty.providers.yml` |
| `lifecycle-intrusion/` | declares `lifecycle`, writes into `.fsgg/`/`work/`/`readiness/` | `lifecycle-intrusion.providers.yml` |

> Implementation note (resolved in tasks): if `dotnet new` tolerates an undeclared `-p:`
> param for the empty/intrusion templates, those variants reduce to new registries pointing
> at the existing `empty/` and `writes-into-fsgg/` fixtures; otherwise each declares the
> `lifecycle` symbol. Either way no Rendering identifier is introduced.
