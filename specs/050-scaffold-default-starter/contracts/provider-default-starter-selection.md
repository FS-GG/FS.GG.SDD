# Contract: Provider default-starter selection (value-agnostic, FR-005 / SC-005)

This is the documented, author-facing contract a provider author follows to declare and change a
**default starter** with **no** generic-SDD code change. A "starter"/"profile" is just a
provider-declared scaffold parameter; the "default starter" is that parameter's `default`.

## Declaring a default starter (`.fsgg/providers.yml`, owned by the provider/author)

```yaml
schemaVersion: 1
providers:
  - name: <provider>
    contractVersion: "1.0.0"
    templateId: <template-id>
    source: <template-source>
    parameters:
      - key: <starterKey>     # e.g. the parameter your template uses to select a starter
        required: false       # a default only takes effect for non-required (or omitted) params
        default: <value>      # the DEFAULT STARTER — forwarded when the author omits --param <starterKey>
```

## Selection precedence (what `fsgg-sdd scaffold` does)

1. **Author omits the parameter** → the declared `default` is forwarded to the provider as
   `--<starterKey> <value>`. The author lands on the provider's intended default starter with no
   extra flags. (FR-001)
2. **Author passes `--param <starterKey>=<other>`** → `<other>` is forwarded; the declared default
   is **not** applied. The explicit choice always wins. (FR-002)
3. The **effective value** (declared default or override) is recorded in
   `.fsgg/scaffold-provenance.json` and the scaffold report, so the chosen starter is auditable and
   the product is reproducible. (FR-003)

## Changing the default starter

Edit the `default:` value in the registry. The next unchanged `fsgg-sdd scaffold` run forwards the
new default — **zero lines of generic SDD code change** (SC-001). The previous starter remains
explicitly selectable via `--param <starterKey>=<previous>` (SC-002).

## Boundaries (FR-004 / SC-003)

- SDD forwards the value **verbatim** and never interprets, validates, or enumerates allowed
  starters — the provider owns which starters exist and which is default.
- A `default` does **not** make a `required` parameter optional; an omitted required value still
  surfaces `scaffold.providerParamMissing`.
- A blank/whitespace `default` is surfaced as a blank declaration, never a silently invented value.
- Generic SDD source and generic-contract tests/fixtures carry **no** provider-specific starter
  value, package id, template id, path, or docs URL. The canonical rendering registry (with its
  real default starter) is owned by **FS.GG.Templates**, consumed only through the versioned
  provider contract and the network-gated composition-acceptance.

> Authoritative encoding mirrored from
> `specs/038-retype-provider-contracts/contracts/provider-registry-encoding.md`. Published mirror
> lives in `docs/release/schema-reference.md` and `docs/reference/authoring-contracts.md`.
